using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

// -----------------------------------------------------------------------------
//  KeneticExport - JSONL (Trade Intelligence Records v0.1) -> SQLite (trades.db)
//
//  Usage:
//    kenetic-export <inputPathOrDir> [outputDb]
//
//    <inputPathOrDir>  a tir-*.jsonl file, OR a folder containing them
//                      (e.g. ...\KeneticTradeRecorder\data\intelligence)
//    [outputDb]        output SQLite path (default: trades.db next to the input)
//
//  The schema + reconstruction were verified against the recorder's real output.
//  Re-running is idempotent (INSERT OR REPLACE keyed by recordId).
// -----------------------------------------------------------------------------

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: kenetic-export <inputPathOrDir> [outputDb]");
    return 2;
}

string input = args[0];
List<string> files = new();
string baseDir;

if (Directory.Exists(input))
{
    baseDir = input;
    files.AddRange(Directory.GetFiles(input, "tir-*.jsonl"));
}
else if (File.Exists(input))
{
    baseDir = Path.GetDirectoryName(Path.GetFullPath(input)) ?? ".";
    files.Add(input);
}
else
{
    Console.Error.WriteLine($"input not found: {input}");
    return 2;
}

if (files.Count == 0)
{
    Console.Error.WriteLine($"no tir-*.jsonl files found in: {input}");
    return 2;
}
files.Sort();

string outDb = args.Length >= 2 ? args[1] : Path.Combine(baseDir, "trades.db");

using var con = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = outDb }.ToString());
con.Open();

using (var pragma = con.CreateCommand())
{
    pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
    pragma.ExecuteNonQuery();
}

using (var ddl = con.CreateCommand())
{
    ddl.CommandText = @"
CREATE TABLE IF NOT EXISTS trades(
  record_id TEXT PRIMARY KEY,
  created_at_utc TEXT,
  account TEXT,
  instrument TEXT,
  trade_id TEXT,
  exec_id TEXT,
  role TEXT,
  side TEXT,
  signed_qty INTEGER,
  pos_before INTEGER,
  pos_after INTEGER,
  price REAL,
  opens_trade INTEGER,
  closes_trade INTEGER,
  origin TEXT,
  execution_time_utc TEXT,
  commission REAL,
  has_context INTEGER,
  bar_time_utc TEXT,
  regime TEXT,
  open_type TEXT,
  rth_phase TEXT,
  session TEXT,
  tape_speed TEXT,
  market_price REAL,
  current_diagnostic TEXT,
  previous_diagnostic TEXT,
  record_json TEXT
);
CREATE INDEX IF NOT EXISTS ix_trades_instrument ON trades(instrument);
CREATE INDEX IF NOT EXISTS ix_trades_time ON trades(execution_time_utc);
CREATE INDEX IF NOT EXISTS ix_trades_regime ON trades(regime);";
    ddl.ExecuteNonQuery();
}

const string insertSql = @"
INSERT OR REPLACE INTO trades VALUES(
 $record_id,$created_at_utc,$account,$instrument,$trade_id,$exec_id,$role,$side,$signed_qty,
 $pos_before,$pos_after,$price,$opens_trade,$closes_trade,$origin,$execution_time_utc,$commission,
 $has_context,$bar_time_utc,$regime,$open_type,$rth_phase,$session,$tape_speed,$market_price,
 $current_diagnostic,$previous_diagnostic,$record_json);";

int rows = 0, bad = 0;
using (var tx = con.BeginTransaction())
using (var cmd = con.CreateCommand())
{
    cmd.Transaction = tx;
    cmd.CommandText = insertSql;
    var p = new Dictionary<string, SqliteParameter>();
    foreach (var name in new[] {
        "record_id","created_at_utc","account","instrument","trade_id","exec_id","role","side","signed_qty",
        "pos_before","pos_after","price","opens_trade","closes_trade","origin","execution_time_utc","commission",
        "has_context","bar_time_utc","regime","open_type","rth_phase","session","tape_speed","market_price",
        "current_diagnostic","previous_diagnostic","record_json" })
        p[name] = cmd.Parameters.Add("$" + name, SqliteType.Text);

    foreach (var file in files)
    {
        foreach (var line in File.ReadLines(file))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            JsonDocument doc;
            try { doc = JsonDocument.Parse(line); }
            catch { bad++; continue; }

            using (doc)
            {
                var r = doc.RootElement;
                JsonElement t = r.GetProperty("trade");
                JsonElement m = r.GetProperty("market");

                p["record_id"].Value          = Str(r, "recordId");
                p["created_at_utc"].Value      = Str(r, "createdAtUtc");
                p["account"].Value             = Str(r, "account");
                p["instrument"].Value          = Str(r, "instrument");
                p["trade_id"].Value            = Str(t, "tradeId");
                p["exec_id"].Value             = Str(t, "execId");
                p["role"].Value                = Str(t, "role");
                p["side"].Value                = Str(t, "side");
                p["signed_qty"].Value          = Num(t, "signedQty");
                p["pos_before"].Value          = Num(t, "posBefore");
                p["pos_after"].Value           = Num(t, "posAfter");
                p["price"].Value               = Real(t, "price");
                p["opens_trade"].Value         = Bool(t, "opensTrade");
                p["closes_trade"].Value        = Bool(t, "closesTrade");
                p["origin"].Value              = Str(t, "origin");
                p["execution_time_utc"].Value  = Str(t, "executionTimeUtc");
                p["commission"].Value          = Real(t, "commission");
                p["has_context"].Value         = Bool(m, "hasContext");
                p["bar_time_utc"].Value        = Str(m, "barTimeUtc");
                p["regime"].Value              = Str(m, "regime");
                p["open_type"].Value           = Str(m, "openType");
                p["rth_phase"].Value           = Str(m, "rthPhase");
                p["session"].Value             = Str(m, "session");
                p["tape_speed"].Value          = Str(m, "tapeSpeed");
                p["market_price"].Value        = Real(m, "price");
                p["current_diagnostic"].Value  = Str(m, "currentDiagnostic");
                p["previous_diagnostic"].Value = Str(m, "previousDiagnostic");
                p["record_json"].Value         = line;

                cmd.ExecuteNonQuery();
                rows++;
            }
        }
    }
    tx.Commit();
}

Console.WriteLine($"Exported {rows} record(s) from {files.Count} file(s) -> {outDb}" + (bad > 0 ? $"  ({bad} unparseable line(s) skipped)" : ""));
return 0;

// ---- helpers: tolerate missing/null fields ----
static object Str(JsonElement e, string name)
    => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : DBNull.Value;
static object Num(JsonElement e, string name)
    => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : DBNull.Value;
static object Real(JsonElement e, string name)
    => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : DBNull.Value;
static object Bool(JsonElement e, string name)
    => e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
        ? (v.GetBoolean() ? 1 : 0) : DBNull.Value;
