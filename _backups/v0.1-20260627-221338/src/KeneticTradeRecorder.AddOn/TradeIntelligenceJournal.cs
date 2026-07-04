// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.AddOn - TradeIntelligenceJournal.cs   (PHASE 3 - v0.1)
//
//  PURPOSE:
//      Persist ONE Trade Intelligence Record per fill as a line of an append-only
//      JSON Lines file, so that at end of day the database can be exported and an
//      LLM can reconstruct exactly what the market looked like at each trade.
//
//      v0.1 captures, per fill:
//        * trade state + full raw execution provenance (from the assembled leg)
//        * structured market fields for quick querying (regime, open type, phase,
//          tape speed, session, price, versions)
//        * the EXACT current AND previous indicator diagnostic lines, which the
//          verified parser can expand back into the full 74-field MarketSnapshot
//          (full-fidelity market state with zero new serialization risk)
//        * a trader slot (null in v0.1; human decision capture comes later)
//
//  WHY A SEPARATE WRITER (not the leg sink):
//      The Phase 1 leg sink is live-verified. This journal is isolated so adding
//      trade-intelligence capture cannot regress proven leg recording. It mirrors
//      the same mandated threading model: producers ENQUEUE only; a single worker
//      owns the file; nothing blocks a NinjaTrader data/connection thread.
//
//  DEPENDENCIES:  System + Core formatter only. No NuGet, no NinjaTrader. JSON is
//                 hand-written (same approach as the leg sink) for clean, stable,
//                 LLM-friendly output and zero serialization dependency.
//
//  THREAD SAFETY: Write() is safe to call concurrently and never blocks on I/O.
//                 Dispose() drains and stops.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using KeneticTradeRecorder.Core;
using KeneticTradeRecorder.Shared;

namespace KeneticTradeRecorder.AddOn
{
    /// <summary>Append-only JSONL journal of Trade Intelligence Records (v0.1). See header.</summary>
    public sealed class TradeIntelligenceJournal : IDisposable
    {
        /// <summary>Schema tag written on every record.</summary>
        public const string Schema = "tir/0.1";

        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private readonly ManualResetEventSlim _signal = new ManualResetEventSlim(false);
        private readonly Thread _worker;
        private readonly Action<string>? _echo;
        private readonly string _directory;
        private readonly object _fileLock = new object();
        private volatile bool _stopRequested;

        /// <summary>Number of records enqueued (for live observability).</summary>
        public long Written { get; private set; }

        /// <param name="dataDirectory">Recorder data dir; an "intelligence" subfolder is created under it.</param>
        /// <param name="echo">Optional callback to mirror a short confirmation line (e.g. NinjaTrader Print).</param>
        public TradeIntelligenceJournal(string dataDirectory, Action<string>? echo)
        {
            _echo = echo;
            _directory = Path.Combine(dataDirectory ?? ".", "intelligence");
            Directory.CreateDirectory(_directory);

            _worker = new Thread(DrainLoop)
            {
                IsBackground = true,
                Name = "Kenetic-TIRJournal"
            };
            _worker.Start();
        }

        /// <summary>
        /// HOT PATH (data thread): build the v0.1 record line and enqueue. No I/O, no blocking.
        /// <paramref name="record"/> supplies CreatedAtUtc and the current MarketSnapshot
        /// (record.MarketContext); <paramref name="leg"/>/<paramref name="source"/> supply trade
        /// provenance; <paramref name="previous"/> supplies the prior snapshot for transition context.
        /// </summary>
        public void Write(
            TradeIntelligenceRecord record,
            in AssembledLeg leg,
            in ExecutionRecord source,
            MarketSnapshot? previous)
        {
            string line = ToJsonLine(record, in leg, in source, previous);
            _queue.Enqueue(line);
            Written++;
            _signal.Set();
        }

        private void DrainLoop()
        {
            while (!_stopRequested)
            {
                _signal.Wait();
                _signal.Reset();
                Flush();
            }
            Flush(); // final drain on shutdown
        }

        private void Flush()
        {
            if (_queue.IsEmpty) return;

            string path = Path.Combine(_directory,
                "tir-" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".jsonl");

            lock (_fileLock)
            {
                using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    while (_queue.TryDequeue(out string? line))
                    {
                        if (line == null) continue;
                        sw.WriteLine(line);
                    }
                }
            }
            try { _echo?.Invoke($"[Kenetic] Trade intelligence records written: {Written}"); }
            catch { /* never let echo break recording */ }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _stopRequested = true;
            _signal.Set();
            try { _worker.Join(TimeSpan.FromSeconds(5)); } catch { /* ignore */ }
            _signal.Dispose();
        }

        // ---------------------------------------------------------------------
        // v0.1 record serialization (dependency-free, LLM-friendly)
        // ---------------------------------------------------------------------

        private static string? LineFor(MarketSnapshot? s)
        {
            if (s == null) return null;
            return !string.IsNullOrEmpty(s.RawDiagnostic)
                ? s.RawDiagnostic
                : MarketSnapshotDiagnosticFormatter.Format(s);
        }

        private static string Iso(DateTimeOffset t) =>
            t.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);

        private static string ToJsonLine(
            TradeIntelligenceRecord record,
            in AssembledLeg leg,
            in ExecutionRecord src,
            MarketSnapshot? previous)
        {
            MarketSnapshot? cur = record.MarketContext;
            string? curLine  = LineFor(cur);
            string? prevLine = LineFor(previous);

            var sb = new StringBuilder(1024);
            sb.Append('{');
            WriteStr(sb, "schema", Schema); Comma(sb);
            WriteStr(sb, "recordId", leg.ExecutionId.Value + "#" + leg.LegIndex.ToString(CultureInfo.InvariantCulture)); Comma(sb);
            WriteStr(sb, "recordVersion", record.RecordVersion.ToString(CultureInfo.InvariantCulture)); Comma(sb);
            WriteStr(sb, "createdAtUtc", Iso(record.CreatedAtUtc)); Comma(sb);
            WriteStr(sb, "account", leg.Key.Account); Comma(sb);
            WriteStr(sb, "instrument", leg.Key.Instrument); Comma(sb);

            // ----- trade -----
            sb.Append("\"trade\":{");
            WriteStr(sb, "tradeId", leg.TradeId.Value); Comma(sb);
            WriteStr(sb, "execId", leg.ExecutionId.Value); Comma(sb);
            WriteStr(sb, "orderId", src.OrderId.Value); Comma(sb);
            WriteNum(sb, "legIndex", leg.LegIndex); Comma(sb);
            WriteStr(sb, "role", leg.Role.ToString()); Comma(sb);
            WriteStr(sb, "side", leg.TradeSide.ToString()); Comma(sb);
            WriteNum(sb, "signedQty", leg.SignedQuantity); Comma(sb);
            WriteNum(sb, "posBefore", leg.PositionBefore); Comma(sb);
            WriteNum(sb, "posAfter", leg.PositionAfter); Comma(sb);
            WriteReal(sb, "price", leg.Price); Comma(sb);
            WriteBool(sb, "opensTrade", leg.OpensTrade); Comma(sb);
            WriteBool(sb, "closesTrade", leg.ClosesTrade); Comma(sb);
            WriteBool(sb, "isReversalLeg", leg.IsReversalLeg); Comma(sb);
            WriteStr(sb, "origin", leg.Origin.ToString()); Comma(sb);
            WriteStr(sb, "executionTimeUtc", Iso(leg.TimeUtc)); Comma(sb);
            WriteReal(sb, "commission", src.Commission); Comma(sb);
            WriteStr(sb, "rawSignalName", src.RawSignalName); Comma(sb);
            WriteStr(sb, "rawOrderName", src.RawOrderName); Comma(sb);
            WriteStr(sb, "rawFromEntrySignal", src.RawFromEntrySignal); Comma(sb);
            WriteStr(sb, "rawOrderAction", src.RawOrderAction);
            sb.Append("},");

            // ----- market (structured fields for querying + full-fidelity diagnostic lines) -----
            sb.Append("\"market\":{");
            WriteBool(sb, "hasContext", cur != null); Comma(sb);
            WriteStr(sb, "symbol", cur?.Symbol ?? leg.Key.Instrument); Comma(sb);
            if (cur != null) { WriteStr(sb, "barTimeUtc", Iso(cur.BarTimeUtc)); } else { WriteNull(sb, "barTimeUtc"); } Comma(sb);
            WriteNum(sb, "calculationVersion", cur?.CalculationVersion ?? 0); Comma(sb);
            WriteNum(sb, "snapshotVersion", cur?.SnapshotVersion ?? 0); Comma(sb);
            WriteStr(sb, "regime", (cur?.Regime ?? MarketRegime.Unknown).ToString()); Comma(sb);
            WriteStr(sb, "openType", (cur?.Gap.OpenType ?? OpenType.Unknown).ToString()); Comma(sb);
            WriteStr(sb, "rthPhase", (cur?.RthPhase ?? RthPhase.Unknown).ToString()); Comma(sb);
            WriteStr(sb, "ethPhase", (cur?.EthPhase ?? EthPhase.Unknown).ToString()); Comma(sb);
            WriteStr(sb, "session", (cur?.Session ?? MarketSession.Unknown).ToString()); Comma(sb);
            WriteStr(sb, "tapeSpeed", (cur?.Tape.Speed ?? TapeSpeed.Unknown).ToString()); Comma(sb);
            WriteReal(sb, "price", cur?.Price ?? 0d); Comma(sb);
            if (curLine != null)  { WriteStr(sb, "currentDiagnostic", curLine); }   else { WriteNull(sb, "currentDiagnostic"); } Comma(sb);
            if (prevLine != null) { WriteStr(sb, "previousDiagnostic", prevLine); } else { WriteNull(sb, "previousDiagnostic"); }
            sb.Append('}');

            // ----- trader (v0.1 placeholder) -----
            sb.Append(',');
            WriteNull(sb, "trader");

            sb.Append('}');
            return sb.ToString();
        }

        // -- dependency-free JSON helpers (same approach as the leg sink) --
        private static void Comma(StringBuilder sb) => sb.Append(',');

        private static void WriteStr(StringBuilder sb, string key, string? value)
        {
            sb.Append('"').Append(key).Append("\":\"");
            Escape(sb, value ?? string.Empty);
            sb.Append('"');
        }

        private static void WriteNull(StringBuilder sb, string key)
            => sb.Append('"').Append(key).Append("\":null");

        private static void WriteNum(StringBuilder sb, string key, long value)
            => sb.Append('"').Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));

        private static void WriteReal(StringBuilder sb, string key, double value)
            => sb.Append('"').Append(key).Append("\":").Append(value.ToString("R", CultureInfo.InvariantCulture));

        private static void WriteBool(StringBuilder sb, string key, bool value)
            => sb.Append('"').Append(key).Append("\":").Append(value ? "true" : "false");

        private static void Escape(StringBuilder sb, string s)
        {
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
        }
    }
}
