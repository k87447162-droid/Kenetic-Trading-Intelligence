# SessionLevels — Feature Additions Implementation Spec
> Destination: `docs/07_Development/Architecture/11_SessionLevels_Feature_Additions.md`
> Adds: VAH/VAL, session VWAP (fixes M-5), volume percentile, BOS (fixes DI-3 — currently has NO writer), CHOCH, one-time framing, Initial Balance, Opening Range, value-migration age, and an instance-ID tag (DI-1 diagnostic).
> Style: matches existing conventions (private fields, session reset, per-bar update, append-only dump fields). Dump fields are APPENDED so existing parsers stay compatible.
> Version: bump indicator version string; dump schema is additive (v2 → v2.1).

---

## 1. New private fields (place near the existing `lastBosBar` block, ~line 1616)

```csharp
// -- Value Area ----------------------------------------------------------
private double todayVAH, todayVAL;

// -- Session VWAP (M-5 fix: cumulative, session-anchored) -----------------
private double vwapCumPV, vwapCumVol, sessionVWAP;

// -- Volume percentile ----------------------------------------------------
private const int VolPctlLookback = 100;

// -- Structure signals (DI-3 fix: BOS finally gets a writer) --------------
private int    lastBrokenSwingHighBar = -1, lastBrokenSwingLowBar = -1;
private int    lastChochBar = -1;
private string lastChochDir = "none";
private string prevBosDir   = "none";   // for CHOCH detection

// -- One-time framing -----------------------------------------------------
private int    otfCount = 0;
private string otfDir   = "none";

// -- Initial Balance / Opening Range --------------------------------------
private bool   wasRTH = false;
private double ibHigh, ibLow, orHigh, orLow;
private bool   ibFinal = false, orFinal = false;
private DateTime rthOpenTime = DateTime.MinValue;
public int IBMinutes { get; set; }   // default 60 in SetDefaults
public int ORMinutes { get; set; }   // default 15 in SetDefaults

// -- Value migration age (feature: timeSinceValueMigrated) -----------------
private double   lastPocForMigration = 0;
private DateTime lastPocMigrationTime = DateTime.MinValue;

// -- Instance identity (DI-1 diagnostic) -----------------------------------
private string instanceId;
```

In `SetDefaults`: `IBMinutes = 60; ORMinutes = 15;`
In `State.DataLoaded` (with the other initializations, ~line 343):
```csharp
instanceId = string.Format("{0}-{1}m-{2}", Instrument.MasterInstrument.Name,
    BarsPeriod.Value, Guid.NewGuid().ToString("N").Substring(0, 6));
```

## 2. Session reset additions (inside the existing new-session block that clears `volumeProfile`, ~line 961)

```csharp
todayVAH = 0; todayVAL = 0;
vwapCumPV = 0; vwapCumVol = 0; sessionVWAP = 0;
ibHigh = 0; ibLow = 0; orHigh = 0; orLow = 0; ibFinal = false; orFinal = false;
rthOpenTime = DateTime.MinValue; otfCount = 0; otfDir = "none";
lastBrokenSwingHighBar = -1; lastBrokenSwingLowBar = -1;
lastChochBar = -1; lastChochDir = "none"; prevBosDir = "none";
lastPocForMigration = 0; lastPocMigrationTime = Time[0];
```

## 3. VAH/VAL — extend the existing POC calculation

Replace the body of `CalculatePOC()` (line ~1121) with:

```csharp
private void CalculatePOC()
{
    if (volumeProfile.Count == 0) return;
    todayPOC = volumeProfile.OrderByDescending(x => x.Value).First().Key;

    // --- 70% Value Area expansion (standard two-sided walk from POC) ---
    double totalVol  = volumeProfile.Values.Sum();
    double targetVol = totalVol * 0.70;
    double accVol    = volumeProfile[todayPOC];
    double up = todayPOC, dn = todayPOC;

    while (accVol < targetVol)
    {
        double nextUp = up + TickSize, nextDn = dn - TickSize;
        double volUp = volumeProfile.ContainsKey(Math.Round(nextUp / TickSize) * TickSize)
                     ? volumeProfile[Math.Round(nextUp / TickSize) * TickSize] : 0;
        double volDn = volumeProfile.ContainsKey(Math.Round(nextDn / TickSize) * TickSize)
                     ? volumeProfile[Math.Round(nextDn / TickSize) * TickSize] : 0;
        if (volUp == 0 && volDn == 0)
        {
            if (nextUp > todayHigh && nextDn < todayLow) break;  // exhausted range
            up = Math.Min(nextUp, todayHigh); dn = Math.Max(nextDn, todayLow);
            continue;
        }
        if (volUp >= volDn) { up = nextUp; accVol += volUp; }
        else                { dn = nextDn; accVol += volDn; }
    }
    todayVAH = up; todayVAL = dn;

    // --- Value migration age (POC move > 4 ticks = migration event) ---
    if (lastPocForMigration == 0) lastPocForMigration = todayPOC;
    if (Math.Abs(todayPOC - lastPocForMigration) > 4 * TickSize)
    {
        lastPocForMigration  = todayPOC;
        lastPocMigrationTime = Time[0];
    }
}
```

## 4. Session VWAP (fixes M-5) — add to the same per-bar path that calls `UpdateVolumeProfile()`

```csharp
double typical = (High[0] + Low[0] + Close[0]) / 3.0;
vwapCumPV  += typical * Volume[0];
vwapCumVol += Volume[0];
sessionVWAP = vwapCumVol > 0 ? vwapCumPV / vwapCumVol : 0;
```
Then wherever the dump currently emits `sessVwap`, use `sessionVWAP` instead of the dead variable (or delete the dead one — one concept, one writer).

## 5. Volume percentile — helper method

```csharp
private double GetVolumePercentile()
{
    int n = Math.Min(VolPctlLookback, CurrentBar);
    if (n < 20) return -1;                       // insufficient -> parser treats -1 as n/a
    int below = 0;
    for (int i = 1; i <= n; i++) if (Volume[i] < Volume[0]) below++;
    return 100.0 * below / n;
}
```

## 6. IB / OR / OTF — one per-bar method, call from `OnBarUpdate` right after `isRTH` is set (~line 1513)

```csharp
private void UpdateSessionWindows()
{
    // RTH open transition
    if (isRTH && !wasRTH)
    {
        rthOpenTime = Time[0];
        ibHigh = High[0]; ibLow = Low[0]; ibFinal = false;
        orHigh = High[0]; orLow = Low[0]; orFinal = false;
    }
    wasRTH = isRTH;
    if (rthOpenTime == DateTime.MinValue) return;   // started mid-session: fields stay n/a today

    double minsSinceRth = (Time[0] - rthOpenTime).TotalMinutes;
    if (!orFinal) { if (minsSinceRth <= ORMinutes) { orHigh = Math.Max(orHigh, High[0]); orLow = Math.Min(orLow, Low[0]); } else orFinal = true; }
    if (!ibFinal) { if (minsSinceRth <= IBMinutes) { ibHigh = Math.Max(ibHigh, High[0]); ibLow = Math.Min(ibLow, Low[0]); } else ibFinal = true; }

    // One-time framing on the chart series (timeframe = chart period; documented)
    if (CurrentBar > 0)
    {
        bool upFrame = Low[0]  >= Low[1];
        bool dnFrame = High[0] <= High[1];
        if (upFrame && !dnFrame)      { otfDir = otfDir == "up"   ? otfDir : "up";   otfCount = otfDir == "up"   ? otfCount + 1 : 1; otfDir = "up"; }
        else if (dnFrame && !upFrame) { otfCount = otfDir == "down" ? otfCount + 1 : 1; otfDir = "down"; }
        else                          { otfCount = 0; otfDir = "none"; }   // inside/outside bar breaks framing
    }
}

private string GetWindowRelation(double hi, double lo, bool final)
{
    if (hi == 0 || rthOpenTime == DateTime.MinValue) return "n/a";
    string state = final ? "" : "forming_";
    if (Close[0] > hi) return string.Format("{0}ABOVE({1:+0.00})", state, Close[0] - hi);
    if (Close[0] < lo) return string.Format("{0}BELOW({1:+0.00;-0.00})", state, Close[0] - lo);
    return string.Format("{0}INSIDE({1:F0}%)", state, hi > lo ? (Close[0] - lo) / (hi - lo) * 100 : 50);
}
```

## 7. BOS + CHOCH (fixes DI-3) — new method, call from `OnBarUpdate` after `DetectSwingPoints()` (~line 452)

Rule: BOS_UP = close crosses above the most recent CONFIRMED swing high (one latch per swing); BOS_DOWN symmetric. CHOCH = a BOS whose direction opposes the previous BOS — the change-of-character event.

```csharp
private void UpdateStructureSignals()
{
    if (swingHighs.Count == 0 && swingLows.Count == 0) return;

    if (swingHighs.Count > 0)
    {
        var sh = swingHighs[swingHighs.Count - 1];
        if (sh.BarIndex != lastBrokenSwingHighBar && Close[0] > sh.Price)
        {
            lastBrokenSwingHighBar = sh.BarIndex;
            if (prevBosDir == "BOS_DOWN") { lastChochDir = "CHOCH_UP"; lastChochBar = CurrentBar; }
            lastBosBar = CurrentBar; lastBosDir = "BOS_UP"; prevBosDir = "BOS_UP";
        }
    }
    if (swingLows.Count > 0)
    {
        var sl = swingLows[swingLows.Count - 1];
        if (sl.BarIndex != lastBrokenSwingLowBar && Close[0] < sl.Price)
        {
            lastBrokenSwingLowBar = sl.BarIndex;
            if (prevBosDir == "BOS_UP") { lastChochDir = "CHOCH_DOWN"; lastChochBar = CurrentBar; }
            lastBosBar = CurrentBar; lastBosDir = "BOS_DOWN"; prevBosDir = "BOS_DOWN";
        }
    }
}

private string GetCHOCH()
{
    if (lastChochBar < 0) return "none";
    int age = CurrentBar - lastChochBar;
    if (age > 30) return "none";
    return string.Format("{0}(bar-{1})", lastChochDir, age);
}
```
Note: swings confirm `SwingBars` bars late by design — BOS timing inherits that lag; acceptable and honest. Remove nothing from `GetBOS()`; it finally has a writer.

## 8. State dump — APPEND a segment (do not renumber the existing Format placeholders)

Wherever the final dump string is assembled, after the existing `string.Format(...)`:

```csharp
string extra = string.Format(System.Globalization.CultureInfo.InvariantCulture,
    " VAH={0:F2}({1:+0.00;-0.00}) VAL={2:F2}({3:+0.00;-0.00}) " +
    "sessVWAP={4:F2}({5:+0.00;-0.00}) volPctl={6:F0} " +
    "IB={7} OR={8} otf={9}x{10} choch={11} " +
    "valueMigAgeMin={12:F0} inst={13}",
    todayVAH, Close[0] - todayVAH,
    todayVAL, Close[0] - todayVAL,
    sessionVWAP, sessionVWAP > 0 ? Close[0] - sessionVWAP : 0,
    GetVolumePercentile(),
    GetWindowRelation(ibHigh, ibLow, ibFinal),
    GetWindowRelation(orHigh, orLow, orFinal),
    otfDir, otfCount,
    GetCHOCH(),
    (Time[0] - lastPocMigrationTime).TotalMinutes,
    instanceId);
dumpLine = dumpLine + extra;   // adapt variable name to yours
```

The `inst=` tag is the DI-1 diagnostic: every dump line now names which instance emitted it, so the dual-emitter conflict becomes visible and attributable instead of interleaved anonymously.

## 9. Data Dictionary entries (add to `docs/06_Data/Data_Dictionary.md` — required before analysis uses them)

| Field | Definition | Units | n/a convention |
|---|---|---|---|
| VAH / VAL | 70% value area edges, two-sided volume walk from session POC over tick-resolution profile | price, + dist pts | 0 before first bars |
| sessVWAP | Cumulative typical-price VWAP anchored at session start (replaces dead field, M-5) | price, + dist pts | 0 |
| volPctl | Percentile rank of current bar volume vs prior min(100, CurrentBar) bars | 0–100 | -1 if <20 bars |
| IB / OR | First 60 / 15 RTH minutes high-low; relation = ABOVE(dist)/BELOW(dist)/INSIDE(pos%), `forming_` prefix until window closes | — | n/a if indicator started mid-RTH |
| otf | One-time-framing direction × consecutive-bar count on the CHART timeframe (5m here — not 30m TPO framing) | count | none/0 |
| choch | Change of character: BOS opposing the prior BOS; age-capped 30 bars | — | none |
| valueMigAgeMin | Minutes since POC last moved > 4 ticks | min | resets at session |
| inst | Emitter identity: symbol-period-guid6 | — | — |

## 10. Known-broken adjacent fields (flagged, not fixed here — separate small commits)
- **M-8 gapDir:** read UP on a -277 open; verify it references prior session close, not prior bar. One-line fix once the reference is confirmed.
- **M-9 minsIn:** reads -425 at the open; `sessionStartTime` anchor is wrong or ETH-anchored. Define or replace with RTH-derived minutes.
- **DI-1 root:** these additions diagnose (via `inst=`) but do not resolve the two-instance conflict — resolution is a workspace decision (one dumping instance per symbol) once `inst=` reveals which is which.

## 11. Test plan (before trusting a single new field in research)
1. Compile; run on replay for 2026-07-07 (the double-distribution day is a perfect fixture).
2. Assert: VAL ≤ POC ≤ VAH every bar; VA width sane vs day range.
3. Assert: BOS fires during the 10:15–11:55 breakdown (the DI-3 fixture where it read `none`); CHOCH fires at the ~09:30 repair turn.
4. Assert: IB freezes at 08:30 MT; OR at 07:45; relations flip correctly during the flush.
5. Assert: sessVWAP ≠ 0 from bar 1; volPctl hits >95 on the 07:35 volRatio-6.67 bar.
6. Two-instance check: run your current workspace as-is; confirm dump lines now carry two different `inst=` tags — that's DI-1 caught red-handed.

---

## 12. Historical Backfill Mode (toggleable; off by default)

**Purpose:** replay historical bars and write state files for past sessions, joinable with old grid exports to backfill the Feature Library. Verified safe: the delta engine is a bar-structure approximation (`UpdateCumulativeDelta`, body/range × volume), so historical delta = live fidelity. All LOCATION/VALUE/STRUCTURE/TIME features are bar-derived and replay exactly.

### Properties (SetDefaults)
```csharp
EnableHistoricalDump = false;   // THE toggle — set true for one reload, then back to false
HistoricalDumpFolder = System.IO.Path.Combine(
    NinjaTrader.Core.Globals.UserDataDir, @"KeneticTradeRecorder\data\backfill");
```

### Fields
```csharp
private System.IO.StreamWriter histWriter;
private DateTime histWriterDate = DateTime.MinValue;
```

### Gate change in `MaybeEmitStateDump()` (replaces the two guard lines)
```csharp
bool live = (State == State.Realtime);
bool hist = (State == State.Historical && EnableHistoricalDump);
if (live && !EnableStateDump) return;
if (!live && !hist) return;
```

### Emission (replaces the Print/Publish pair)
```csharp
line = line + extra + (live ? " mode=LIVE" : " mode=HIST");
if (live)
{
    Print(line);
    KeneticTradeRecorder.Core.KeneticPublish.PublishDiagnostic(line);
}
else
    WriteHistoricalLine(line);
```

### Writer (one file per session date; overwrite-on-rerun = deterministic regeneration)
```csharp
private void WriteHistoricalLine(string line)
{
    try
    {
        DateTime d = currentSessionDate == DateTime.MinValue ? Time[0].Date : currentSessionDate;
        if (histWriter == null || d != histWriterDate)
        {
            if (histWriter != null) { histWriter.Flush(); histWriter.Close(); }
            System.IO.Directory.CreateDirectory(HistoricalDumpFolder);
            string f = System.IO.Path.Combine(HistoricalDumpFolder,
                string.Format("state-{0}-{1:yyyyMMdd}.txt", Instrument.MasterInstrument.Name, d));
            histWriter = new System.IO.StreamWriter(f, false);
            histWriterDate = d;
        }
        histWriter.WriteLine(line);
    }
    catch (Exception ex) { Print("[INDICATOR STATE] HIST_WRITE_ERROR: " + ex.Message); }
}
```
In `State.Terminated`: `if (histWriter != null) { histWriter.Flush(); histWriter.Close(); histWriter = null; }`

### Backfill workflow (the whole point)
1. **One dedicated chart, one instance** (DI-1 rule — never run backfill on the dual-emitter workspace). MNQ 5-min.
2. Data Series → Days to load = target range **+3 extra leading days** (prior-day/weekly levels need warm-up; the first loaded day's context is incomplete by construction).
3. Set `EnableHistoricalDump = true`, OK — historical processing writes `state-MNQ-YYYYMMDD.txt` per session into `data\backfill\`.
4. Set it back to `false`. Done — normal operation resumes, nothing else changed.
5. Export the matching grid range (Trade Performance → custom dates → Trades tab → Export).
6. Upload backfill files + grid export → feature tables are backfilled with every row tagged `mode=HIST`.

### Provenance rules (non-negotiable)
- `mode=HIST` rows are permanently distinguishable from `mode=LIVE` in every analysis; conclusions state which population they draw on.
- Backfilled AVWAP anchors and regime-age counters replay the same code deterministically but were never *observed* live — treated as reconstruction-grade, one notch below live capture.
- Rerunning backfill overwrites (files are derived data, regenerable from bars); live captures are never overwritten.

