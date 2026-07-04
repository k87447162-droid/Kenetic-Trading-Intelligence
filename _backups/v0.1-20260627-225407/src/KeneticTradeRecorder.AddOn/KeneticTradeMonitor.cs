// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.AddOn - KeneticTradeMonitor.cs   (PHASE 1)
//
//  PURPOSE:
//      The Trade Monitor AddOn. Discovers accounts, subscribes to their execution
//      stream, normalizes each execution, runs it through the (verified) trade
//      assembler, and forwards the resulting legs to a recording sink. This is the
//      ONLY component that touches the NinjaTrader account/execution API.
//
//  OBSERVATION-ONLY GUARANTEE:
//      This class subscribes to events and reads state. It NEVER calls any order
//      submission, modification, or cancellation API. There is intentionally no
//      reference anywhere in this file to Submit*, Change*, Cancel*, CreateOrder,
//      Account.Submit, etc. The recorder cannot alter trading behavior.
//
//  RESPONSIBILITIES:
//      - Account discovery + subscription at startup.
//      - Apply the configured account filter (the v0.1 "account selector").
//      - Map NinjaTrader Execution -> ExecutionRecord (raw provenance preserved).
//      - Serialize all assembler calls; de-duplicate repeated execution events.
//      - Forward legs to IRecordingSink. No persistence logic lives here.
//
//  ACCOUNT DISCOVERY (v0.1 scope):
//      NinjaTrader's Account.All is a plain Collection<Account> (not observable),
//      so there is no CollectionChanged event to hook. We enumerate the accounts
//      present when the AddOn initializes and subscribe to those. Accounts that
//      first appear AFTER the AddOn starts (e.g. a broker connected later in the
//      same session) are picked up by disabling/re-enabling the AddOn or
//      restarting NinjaTrader. Dynamic discovery via connection events is a
//      planned Phase 1.x enhancement once it can be validated against live NT8.
//
//  THREAD SAFETY:
//      OnExecutionUpdate may fire on a NinjaTrader connection/background thread.
//      The assembler and the de-dup set are mutated only under _assemblerLock.
//      Subscription bookkeeping is guarded by _subLock. The sink is responsible
//      for being non-blocking (it enqueues). No NinjaTrader data thread is blocked
//      on I/O.
//
//  DEPENDENCY INJECTION:
//      NinjaTrader instantiates AddOns via a parameterless constructor, so classic
//      constructor injection is not available. OnStateChange(Configure) therefore
//      acts as the composition root, wiring concrete dependencies behind interfaces
//      (ITradeAssembler, IRecordingSink) so they remain swappable and testable.
//
//  VERIFY-ON-YOUR-BUILD MARKERS:
//      Lines tagged [NT-VERIFY] use NinjaTrader API surface that is stable across
//      NT8 but worth a one-time confirmation against your exact build/version.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using KeneticTradeRecorder.AddOn;
using KeneticTradeRecorder.Core;
using KeneticTradeRecorder.Shared;

namespace NinjaTrader.NinjaScript.AddOns
{
    /// <summary>
    /// Phase 1 trade observation AddOn. Records every execution and the assembled
    /// trade lifecycle (entry / scale-in / scale-out / exit / reversal) for the
    /// selected accounts, without modifying any trading behavior.
    /// </summary>
    public class KeneticTradeMonitor : AddOnBase
    {
        private readonly object _assemblerLock = new object();
        private readonly object _subLock = new object();

        private readonly ITradeAssembler _assembler = new TradeAssembler();
        private readonly HashSet<string> _seenExecutionIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<Account> _subscribed = new HashSet<Account>();
        private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;

        // PHASE 2 read seam. The indicator publishes market state to this same
        // process-wide registry; the recorder reads the Current snapshot per
        // instrument at execution time (Previous/History are available for future
        // transition-aware recording). Reading is non-blocking and never affects trading.
        private readonly ISnapshotRegistry _snapshots = SnapshotHub.Instance;

        // Observability for the read seam (Phase 2). Not persisted; lets us confirm
        // live that context is being resolved once the indicator is publishing.
        internal MarketSnapshot? LastResolvedContext { get; private set; }
        internal long ContextResolvedCount { get; private set; }
        internal long ContextMissingCount { get; private set; }

        private RecorderConfig _config = new RecorderConfig();
        private string _dataDir = ".";
        private IRecordingSink? _sink;
        private TradeIntelligenceJournal? _tirJournal;
        private RecorderStatusWriter? _status;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "Kenetic Trade Monitor";
            }
            else if (State == State.Configure)
            {
                InitializeComposition();
                HookAccounts();
                InitializeStatus();
            }
            else if (State == State.Terminated)
            {
                UnhookAccounts();
                _sink?.Dispose();
                _sink = null;
                _tirJournal?.Dispose();
                _tirJournal = null;
                _status?.Dispose();
                _status = null;
            }
        }

        // ------------------------------------------------------------------
        // Composition root
        // ------------------------------------------------------------------

        private void InitializeComposition()
        {
            string recorderDir = ResolveRecorderDirectory();
            string configPath = System.IO.Path.Combine(recorderDir, "config.json");

            if (!System.IO.File.Exists(configPath))
            {
                try { RecorderConfig.WriteDefault(configPath); } catch { /* non-fatal */ }
            }

            _config = RecorderConfig.LoadOrDefault(configPath);

            string dataDir = !string.IsNullOrEmpty(_config.DataDirectory)
                ? _config.DataDirectory
                : System.IO.Path.Combine(recorderDir, "data");
            _dataDir = dataDir;

            Action<string>? echo = _config.EchoToOutputWindow ? WriteOutput : null;
            _sink = new BackgroundRecordingSink(dataDir, echo);
            _tirJournal = new TradeIntelligenceJournal(dataDir, echo);

            WriteOutput($"[Kenetic] Trade Monitor active. Data: {dataDir} | Filter: {_config.FilterMode} | ClassifierV{OriginClassifier.Version}");
        }

        /// <summary>
        /// Starts the status writer used by the external validation script. Best-effort:
        /// a status-write failure must never affect recording.
        /// </summary>
        private void InitializeStatus()
        {
            try
            {
                _status = new RecorderStatusWriter(_dataDir, BuildStatus);
            }
            catch { /* status is best-effort; never block recording */ }
        }

        /// <summary>Builds the current status snapshot (called by the status writer's timer).</summary>
        private RecorderStatus BuildStatus()
        {
            DateTimeOffset? lastSnap = null;
            string lastInst = string.Empty;
            foreach (string i in _snapshots.Instruments)
            {
                MarketSnapshot? c = _snapshots.Current(i);
                if (c != null && (lastSnap == null || c.BarTimeUtc > lastSnap.Value))
                {
                    lastSnap = c.BarTimeUtc;
                    lastInst = i;
                }
            }

            return new RecorderStatus
            {
                RecorderVersion = "0.1",
                ClassifierVersion = OriginClassifier.Version,
                StartedAtUtc = _startedUtc,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                DataDirectory = _dataDir,
                SubscribedAccounts = CurrentSubscribedAccounts(),
                SnapshotInstrumentCount = _snapshots.Count,
                SnapshotInstruments = _snapshots.Instruments,
                ContextResolvedCount = ContextResolvedCount,
                ContextMissingCount = ContextMissingCount,
                TradeIntelligenceWritten = _tirJournal != null ? _tirJournal.Written : 0,
                LastSnapshotUtc = lastSnap,
                LastSnapshotInstrument = lastInst,
            };
        }

        /// <summary>Snapshot of currently subscribed account names (thread-safe).</summary>
        private IReadOnlyList<string> CurrentSubscribedAccounts()
        {
            lock (_subLock)
            {
                var names = new List<string>(_subscribed.Count);
                foreach (Account a in _subscribed) names.Add(a.Name);
                return names;
            }
        }

        /// <summary>Resolves the recorder's working directory under the NinjaTrader user folder.</summary>
        private static string ResolveRecorderDirectory()
        {
            // [NT-VERIFY] NinjaTrader.Core.Globals.UserDataDir -> "...\Documents\NinjaTrader 8\".
            string baseDir;
            try { baseDir = NinjaTrader.Core.Globals.UserDataDir; }
            catch { baseDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); }
            string dir = System.IO.Path.Combine(baseDir, "KeneticTradeRecorder");
            System.IO.Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Writes a line to the NinjaTrader Output window (safe from background threads).</summary>
        private static void WriteOutput(string line)
        {
            // [NT-VERIFY] NinjaTrader.Code.Output.Process is the documented way to write to
            // the Output window from any thread / any NinjaScript type (including AddOns).
            try { NinjaTrader.Code.Output.Process(line, PrintTo.OutputTab1); }
            catch { /* never let logging break recording */ }
        }

        // ------------------------------------------------------------------
        // Account discovery + subscription
        // ------------------------------------------------------------------

        private void HookAccounts()
        {
            // [NT-VERIFY] Account.All is NinjaTrader's collection of accounts. It is a plain
            // Collection<Account> in NT8 (no change notification), so we enumerate once here.
            lock (Account.All)
            {
                foreach (Account a in Account.All)
                    TrySubscribe(a);
            }
        }

        private void UnhookAccounts()
        {
            lock (_subLock)
            {
                foreach (Account a in new List<Account>(_subscribed))
                    a.ExecutionUpdate -= OnExecutionUpdate;
                _subscribed.Clear();
            }
        }

        private void TrySubscribe(Account a)
        {
            if (a == null) return;
            if (!_config.ShouldRecord(a.Name)) return;

            lock (_subLock)
            {
                if (_subscribed.Add(a))
                {
                    a.ExecutionUpdate += OnExecutionUpdate;
                    WriteOutput($"[Kenetic] Subscribed account: {a.Name}");
                }
            }
        }

        // ------------------------------------------------------------------
        // Execution handling (observation only)
        // ------------------------------------------------------------------

        private void OnExecutionUpdate(object? sender, ExecutionEventArgs e)
        {
            // Runs on a NinjaTrader connection/background thread. Cheap + thread-safe.
            Execution exec = e.Execution;
            if (exec == null || exec.Order == null || exec.Instrument == null || exec.Account == null)
                return;

            int signed = SignedQuantity(exec.Order.OrderAction, exec.Quantity);
            if (signed == 0)
                return;

            var key = new AccountInstrumentKey(exec.Account.Name, exec.Instrument.FullName);
            TradeOrigin origin = OriginClassifier.Classify(
                exec.Order.FromEntrySignal, exec.Order.Name, exec.Name);

            var record = new ExecutionRecord(
                new ExecutionId(exec.ExecutionId ?? string.Empty),
                new OrderId(exec.Order.OrderId ?? string.Empty),
                key,
                origin,
                signed,
                exec.Price,
                ToUtc(exec.Time),
                exec.Name ?? string.Empty,                  // RawSignalName  (Execution.Name)
                exec.Order.Name ?? string.Empty,            // RawOrderName   (Order.Name)
                exec.Order.FromEntrySignal ?? string.Empty, // RawFromEntrySignal
                exec.Order.OrderAction.ToString(),          // RawOrderAction
                exec.Commission);              // [NT-VERIFY] Execution.Commission exists in NT8.

            IReadOnlyList<AssembledLeg> legs;
            lock (_assemblerLock)
            {
                string exId = record.ExecutionId.Value;
                // De-duplicate: NinjaTrader can raise the same execution more than once.
                if (!string.IsNullOrEmpty(exId) && !_seenExecutionIds.Add(exId))
                    return;

                legs = _assembler.Apply(record);
            }

            IRecordingSink? sink = _sink;
            if (sink == null) return;

            // PHASE 3 (v0.1): resolve market state from the persistent registry and
            // persist one Trade Intelligence Record per leg. Reading + writing are
            // non-blocking; a missing snapshot NEVER prevents recording (hasContext=false),
            // which keeps "capture every discretionary trade" the top guarantee.
            string instrument = exec.Instrument.FullName;
            MarketSnapshot? context = _snapshots.Current(instrument);
            MarketSnapshot? previous = _snapshots.Previous(instrument);
            LastResolvedContext = context;
            if (context != null) ContextResolvedCount++; else ContextMissingCount++;

            TradeIntelligenceJournal? tir = _tirJournal;

            for (int i = 0; i < legs.Count; i++)
            {
                AssembledLeg leg = legs[i];

                // Raw leg capture (Phase 1 path, unchanged / live-verified).
                sink.Record(leg, record);

                // Trade Intelligence Record (v0.1): trade state + market context.
                if (tir != null)
                {
                    var tradeState = new TradeSnapshot
                    {
                        Key = leg.Key,
                        TradeId = leg.TradeId,
                        Side = leg.TradeSide,
                        SignedPosition = leg.PositionAfter,
                        AveragePrice = leg.Price,          // v0.1: this leg's fill price
                        Origin = leg.Origin,
                        AsOfUtc = leg.TimeUtc,
                    };

                    var intelligence = new TradeIntelligenceRecord
                    {
                        TradeId = leg.TradeId,
                        Key = leg.Key,
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        Trade = tradeState,
                        MarketContext = context,           // null -> record still written, hasContext:false
                        // Trader / OrderFlow / Structure / Event / Outcome / Replay: deferred.
                    };

                    tir.Write(intelligence, leg, record, previous);
                }
            }
        }

        /// <summary>Maps a NinjaTrader OrderAction + quantity to a signed quantity (+ buy, - sell).</summary>
        private static int SignedQuantity(OrderAction action, int quantity)
        {
            switch (action)
            {
                case OrderAction.Buy:
                case OrderAction.BuyToCover:
                    return +quantity;
                case OrderAction.Sell:
                case OrderAction.SellShort:
                    return -quantity;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Converts a NinjaTrader execution time to UTC.
        /// [NT-VERIFY / IMPORTANT] NinjaTrader DateTimes are commonly Kind=Unspecified in the
        /// user's configured time zone. We treat Unspecified as Local for conversion. If your
        /// NinjaTrader instance runs in a fixed exchange time zone that differs from the machine
        /// local zone, adjust this single method. The raw execution time is also reflected in the
        /// emitted record, so timestamps remain auditable and the rule can be corrected centrally.
        /// </summary>
        private static DateTimeOffset ToUtc(DateTime t)
        {
            switch (t.Kind)
            {
                case DateTimeKind.Utc:
                    return new DateTimeOffset(t, TimeSpan.Zero);
                case DateTimeKind.Local:
                    return new DateTimeOffset(t.ToUniversalTime(), TimeSpan.Zero);
                default:
                    return new DateTimeOffset(
                        DateTime.SpecifyKind(t, DateTimeKind.Local).ToUniversalTime(), TimeSpan.Zero);
            }
        }
    }
}
