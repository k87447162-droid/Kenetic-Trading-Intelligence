// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Core - MarketCalcInputs.cs   (PHASE 2 refactor)
//
//  PURPOSE:
//      The exact set of per-bar PRIMITIVES that SessionLevels already computes,
//      bundled immutably and handed to MarketContextService. This is the seam:
//      everything NT-bound and stateful (volume profile, anchored-VWAP
//      accumulation, swing detection, cumulative delta, EMAs/ATR/OFD, session
//      boundaries) is computed by the indicator and arrives here as data; the pure
//      classification/scoring then runs in MarketContextService with no NT types.
//
//      Result: SessionLevels remains the single source of truth for raw market
//      calculations; the classification engine is reusable and unit-testable.
//
//  SERIES CONVENTION:
//      All *Series lists follow NinjaTrader indexing: index 0 = current bar,
//      index i = i bars ago. Provide at least the depth the classifiers read
//      (Close/High/Low/Volume: 20; EmaFast: 4; DeltaClose: 5; Atr5m: 20).
//
//  THREAD SAFETY:  Immutable once built; safe to pass across threads.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using KeneticTradeRecorder.Shared;

namespace KeneticTradeRecorder.Core
{
    /// <summary>Immutable per-bar primitives for one instrument, consumed by MarketContextService.</summary>
    public sealed record MarketCalcInputs
    {
        // ----- identity & timing -----
        public string Symbol { get; init; } = string.Empty;          // Instrument.FullName
        public DateTimeOffset BarTimeUtc { get; init; }              // Time[0] in UTC
        public DayOfWeek DayOfWeek { get; init; }                    // Time[0].DayOfWeek
        public TimeSpan TimeOfDay { get; init; }                     // Time[0].TimeOfDay
        public int CurrentBar { get; init; }
        public double MinutesIntoSession { get; init; }             // TOD.TotalMinutes - sessionStartTime

        // ----- config / instrument -----
        public double TickSize { get; init; } = 0.01;
        public int SlowEma { get; init; } = 50;
        public double MinGapSize { get; init; }                      // in ticks (multiplied by TickSize)
        public TimeSpan RthStart { get; init; }
        public TimeSpan RthEnd { get; init; }
        public bool IsRth { get; init; }
        public bool SessionInitialized { get; init; }

        // ----- price/volume series (index 0 = current) -----
        public IReadOnlyList<double> Close { get; init; } = Array.Empty<double>();
        public IReadOnlyList<double> Open { get; init; } = Array.Empty<double>();
        public IReadOnlyList<double> High { get; init; } = Array.Empty<double>();
        public IReadOnlyList<double> Low { get; init; } = Array.Empty<double>();
        public IReadOnlyList<double> Volume { get; init; } = Array.Empty<double>();

        // ----- indicator series / values -----
        public IReadOnlyList<double> EmaFast { get; init; } = Array.Empty<double>();   // emaFast (>=4)
        public double EmaMid0 { get; init; }                         // emaMid[0]   (21)
        public double EmaSlow0 { get; init; }                        // emaSlow[0]  (50)
        public double E5 { get; init; }                              // ema5[0]  (reported)
        public double E9 { get; init; }                              // ema9[0]  (reported)
        public IReadOnlyList<double> DeltaClose { get; init; } = Array.Empty<double>(); // ofd24.DeltaClose (>=5; empty if OFD null)
        public IReadOnlyList<double> Atr5m { get; init; } = Array.Empty<double>();      // atr5m (>=20; empty if null)

        // ----- session levels -----
        public double TodayOpen { get; init; }
        public double TodayHigh { get; init; }
        public double TodayLow { get; init; }
        public double TodayPoc { get; init; }
        public double PrevDayOpen { get; init; }
        public double PrevDayHigh { get; init; }
        public double PrevDayLow { get; init; }
        public double PrevDayClose { get; init; }
        public double WeekHigh { get; init; }
        public double WeekLow { get; init; }
        public double MonthHigh { get; init; }
        public double MonthLow { get; init; }

        // ----- volume-profile-derived (need full profile -> computed by indicator) -----
        public double PctAbovePoc { get; init; }                     // 0..100
        public double PctUpperRange { get; init; }                   // 0..100
        public double SessionVolume { get; init; }                   // scanned back to session start

        // ----- delta -----
        public double CumDelta24 { get; init; }
        public double CumDeltaRth { get; init; }
        public double EtDeltaSession { get; init; }
        public bool DivergenceActive { get; init; }                  // IsDivergenceActive() (absolute-bar; stays in indicator)

        // ----- swing primitives (last two by bar recency) -----
        public int SwingHighCount { get; init; }
        public int SwingLowCount { get; init; }
        public double SwingHigh0 { get; init; }                      // most recent swing-high price
        public double SwingHigh1 { get; init; }
        public double SwingLow0 { get; init; }
        public double SwingLow1 { get; init; }

        // ----- stateful counters tracked across bars by the indicator -----
        public int EmaStackAge { get; init; }
        public int RegimeAge { get; init; }
        public bool RegimeChange { get; init; }

        // ----- gap -----
        public double GapPoints { get; init; }
        public double GapPercent { get; init; }

        // ----- anchored VWAPs (built by indicator's stateful accumulation) -----
        public IReadOnlyList<AnchoredVwap> AnchoredVwaps { get; init; } = Array.Empty<AnchoredVwap>();
        public NearestAvwap NearestAvwap { get; init; } = NearestAvwap.None;
        public int AnchoredVwapCount { get; init; }
        public double SessionVwap { get; init; }

        // ----- microstructure -----
        public int TickCount30s { get; init; }
        public int TickCount5mAvg { get; init; }
        public bool L2Available { get; init; }

        // ----- versioning -----
        public int CalculationVersion { get; init; }                 // your indicator's calc-set version
    }
}
