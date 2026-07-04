// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.AddOn - BackgroundRecordingSink.cs
//
//  PURPOSE:
//      Phase 1 persistence/observability sink. Writes every detected leg (with
//      full raw provenance) as one line of an append-only JSON Lines file, and
//      optionally echoes it to the NinjaTrader Output window. Establishes the
//      mandated threading model on day one: the NinjaTrader data/connection thread
//      only ENQUEUES; all file I/O happens on a dedicated background worker.
//
//  SCOPE NOTE:
//      This is the Phase 1 sink. The durable, schema'd journal + SQLite background
//      writer is Phase 3/4 and will implement IRecordingSink the same way, so the
//      detection layer never changes when persistence is upgraded.
//
//  THREAD SAFETY:
//      Record() is safe to call concurrently from data threads and never blocks on
//      I/O. A single worker thread owns the file. Dispose() drains and stops.
//
//  PERFORMANCE:
//      Hot path = build one string + enqueue + set an event. No locks held across
//      I/O on the producer side. File is opened in append mode per flush batch.
//
//  DEPENDENCIES:   System only (no NinjaTrader, no NuGet). JSON is hand-written to
//                  avoid taking any serialization dependency into NinjaTrader.
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
    /// <summary>Append-only JSONL sink with a background writer. See file header.</summary>
    public sealed class BackgroundRecordingSink : IRecordingSink
    {
        /// <summary>Schema version of the JSONL observation records this sink emits.</summary>
        public const int SchemaVersion = 2;

        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private readonly ManualResetEventSlim _signal = new ManualResetEventSlim(false);
        private readonly Thread _worker;
        private readonly Action<string>? _echo;
        private readonly string _directory;
        private readonly object _fileLock = new object();
        private volatile bool _stopRequested;

        /// <summary>
        /// Creates the sink and starts its background writer.
        /// </summary>
        /// <param name="dataDirectory">Recorder data dir; an "observations" subfolder is created under it.</param>
        /// <param name="echo">Optional callback to mirror each line (e.g. NinjaTrader Print). Invoked on the worker thread.</param>
        public BackgroundRecordingSink(string dataDirectory, Action<string>? echo)
        {
            _echo = echo;
            _directory = Path.Combine(dataDirectory ?? ".", "observations");
            Directory.CreateDirectory(_directory);

            _worker = new Thread(DrainLoop)
            {
                IsBackground = true,
                Name = "Kenetic-RecordingSink"
            };
            _worker.Start();
        }

        /// <inheritdoc/>
        public void Record(in AssembledLeg leg, in ExecutionRecord source)
        {
            // HOT PATH (data thread): serialize to a line and enqueue. No I/O, no blocking.
            string line = ToJsonLine(in leg, in source);
            _queue.Enqueue(line);
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

            // One file handle per batch; daily file name keeps files manageable.
            string path = Path.Combine(_directory,
                "phase1-" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".jsonl");

            lock (_fileLock)
            {
                using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    while (_queue.TryDequeue(out string? line))
                    {
                        if (line == null) continue;
                        sw.WriteLine(line);
                        try { _echo?.Invoke(line); } catch { /* never let echo break recording */ }
                    }
                }
            }
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
        // Minimal, dependency-free JSON line writer
        // ---------------------------------------------------------------------

        private static string ToJsonLine(in AssembledLeg leg, in ExecutionRecord src)
        {
            var sb = new StringBuilder(384);
            sb.Append('{');
            WriteNum(sb, "schema", SchemaVersion); Comma(sb);
            WriteStr(sb, "tsUtc", leg.TimeUtc.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)); Comma(sb);
            WriteStr(sb, "account", leg.Key.Account); Comma(sb);
            WriteStr(sb, "instrument", leg.Key.Instrument); Comma(sb);
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
            WriteStr(sb, "origin", leg.Origin.ToString()); Comma(sb);
            WriteNum(sb, "originClassifierVersion", OriginClassifier.Version); Comma(sb);
            WriteBool(sb, "opensTrade", leg.OpensTrade); Comma(sb);
            WriteBool(sb, "closesTrade", leg.ClosesTrade); Comma(sb);
            WriteBool(sb, "isReversalLeg", leg.IsReversalLeg); Comma(sb);
            WriteStr(sb, "reversalGroup", leg.ReversalGroupId == Guid.Empty ? "" : leg.ReversalGroupId.ToString("N")); Comma(sb);
            // ----- raw provenance (verbatim, for later re-classification) -----
            WriteStr(sb, "rawSignalName", src.RawSignalName); Comma(sb);
            WriteStr(sb, "rawOrderName", src.RawOrderName); Comma(sb);
            WriteStr(sb, "rawFromEntrySignal", src.RawFromEntrySignal); Comma(sb);
            WriteStr(sb, "rawOrderAction", src.RawOrderAction); Comma(sb);
            WriteReal(sb, "commission", src.Commission);
            sb.Append('}');
            return sb.ToString();
        }

        private static void Comma(StringBuilder sb) => sb.Append(',');

        private static void WriteStr(StringBuilder sb, string key, string? value)
        {
            sb.Append('"').Append(key).Append("\":\"");
            Escape(sb, value ?? string.Empty);
            sb.Append('"');
        }

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
