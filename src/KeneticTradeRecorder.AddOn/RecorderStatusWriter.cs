// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.AddOn - RecorderStatusWriter.cs   (PHASE 3 - v0.1)
//
//  PURPOSE:
//      Emit a small machine-readable status file the external validation script can
//      read to confirm, WITHOUT guessing, that the recorder is live and healthy:
//        * recorder loaded            -> file exists + recent updatedAtUtc
//        * account subscription works -> subscribedAccountCount > 0
//        * snapshot registry init     -> snapshotInstrumentCount field present
//        * SessionLevels publishing   -> snapshotInstrumentCount > 0
//        * journal writing            -> tradeIntelligenceWritten (and the files)
//
//      Written on load and refreshed on a short timer, so validation works both
//      BEFORE a trade (readiness) and AFTER (evidence). Pure System; no NuGet, no
//      NinjaTrader. Never throws into the recorder; failure to write status must
//      never affect recording.
//
//  THREAD SAFETY: the timer callback owns the file; the supplied provider reads
//      immutable counts / concurrent collections. Dispose stops the timer.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace KeneticTradeRecorder.AddOn
{
    /// <summary>Immutable snapshot of recorder status at one instant.</summary>
    public sealed class RecorderStatus
    {
        public string RecorderVersion = "0.1";
        public int ClassifierVersion;
        public DateTimeOffset StartedAtUtc;
        public DateTimeOffset UpdatedAtUtc;
        public string DataDirectory = string.Empty;
        public IReadOnlyList<string> SubscribedAccounts = Array.Empty<string>();
        public int SnapshotInstrumentCount;
        public IReadOnlyCollection<string> SnapshotInstruments = Array.Empty<string>();
        public long ContextResolvedCount;
        public long ContextMissingCount;
        public long TradeIntelligenceWritten;
        public DateTimeOffset? LastSnapshotUtc;
        public string LastSnapshotInstrument = string.Empty;
    }

    /// <summary>Periodically serializes a <see cref="RecorderStatus"/> to data/status/recorder-status.json.</summary>
    public sealed class RecorderStatusWriter : IDisposable
    {
        public const string Schema = "kenetic-status/0.1";

        private readonly string _path;
        private readonly string _tmp;
        private readonly Func<RecorderStatus> _provider;
        private readonly Timer _timer;

        public RecorderStatusWriter(string dataDirectory, Func<RecorderStatus> provider)
        {
            _provider = provider;
            string dir = Path.Combine(dataDirectory ?? ".", "status");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "recorder-status.json");
            _tmp  = _path + ".tmp";

            WriteOnce(); // immediate readiness file
            _timer = new Timer(_ => WriteOnce(), null,
                               TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        /// <summary>Force an immediate status write (used on shutdown).</summary>
        public void WriteOnce()
        {
            try
            {
                RecorderStatus s = _provider();
                string json = Serialize(s);
                File.WriteAllText(_tmp, json, new UTF8Encoding(false));
                // Overwrite atomically-ish: copy temp over target, then drop temp.
                File.Copy(_tmp, _path, overwrite: true);
                try { File.Delete(_tmp); } catch { /* ignore */ }
            }
            catch
            {
                // Status is best-effort observability; never disturb recording.
            }
        }

        public void Dispose()
        {
            try { _timer.Dispose(); } catch { /* ignore */ }
            WriteOnce(); // final snapshot
        }

        private static string Serialize(RecorderStatus s)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            Str(sb, "schema", Schema); sb.Append(',');
            Str(sb, "recorderVersion", s.RecorderVersion); sb.Append(',');
            Num(sb, "classifierVersion", s.ClassifierVersion); sb.Append(',');
            Str(sb, "startedAtUtc", Iso(s.StartedAtUtc)); sb.Append(',');
            Str(sb, "updatedAtUtc", Iso(s.UpdatedAtUtc)); sb.Append(',');
            Str(sb, "dataDirectory", s.DataDirectory); sb.Append(',');
            Num(sb, "subscribedAccountCount", s.SubscribedAccounts != null ? s.SubscribedAccounts.Count : 0); sb.Append(',');
            StrArray(sb, "subscribedAccounts", s.SubscribedAccounts); sb.Append(',');
            Num(sb, "snapshotInstrumentCount", s.SnapshotInstrumentCount); sb.Append(',');
            StrArray(sb, "snapshotInstruments", s.SnapshotInstruments); sb.Append(',');
            Num(sb, "contextResolvedCount", s.ContextResolvedCount); sb.Append(',');
            Num(sb, "contextMissingCount", s.ContextMissingCount); sb.Append(',');
            Num(sb, "tradeIntelligenceWritten", s.TradeIntelligenceWritten); sb.Append(',');
            if (s.LastSnapshotUtc.HasValue) { Str(sb, "lastSnapshotUtc", Iso(s.LastSnapshotUtc.Value)); }
            else { sb.Append("\"lastSnapshotUtc\":null"); }
            sb.Append(',');
            Str(sb, "lastSnapshotInstrument", s.LastSnapshotInstrument);
            sb.Append('}');
            return sb.ToString();
        }

        private static string Iso(DateTimeOffset t) =>
            t.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);

        private static void Str(StringBuilder sb, string k, string? v)
        { sb.Append('"').Append(k).Append("\":\""); Escape(sb, v ?? string.Empty); sb.Append('"'); }

        private static void Num(StringBuilder sb, string k, long v)
        { sb.Append('"').Append(k).Append("\":").Append(v.ToString(CultureInfo.InvariantCulture)); }

        private static void StrArray(StringBuilder sb, string k, IEnumerable<string>? items)
        {
            sb.Append('"').Append(k).Append("\":[");
            bool first = true;
            if (items != null)
                foreach (var it in items)
                {
                    if (!first) sb.Append(',');
                    sb.Append('"'); Escape(sb, it ?? string.Empty); sb.Append('"');
                    first = false;
                }
            sb.Append(']');
        }

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
