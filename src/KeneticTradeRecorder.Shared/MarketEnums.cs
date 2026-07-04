// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Shared - MarketEnums.cs   (PHASE 2)
//
//  PURPOSE:
//      Strongly-typed vocabulary for the market-context snapshot. Every categorical
//      field the indicator emits is modelled as an enum instead of a raw string.
//
//  PROVENANCE:
//      The member sets below were derived empirically from 12,513 real
//      [INDICATOR STATE] diagnostic records (symbols MCL/MNQ/MGC/MBT/MYM/MES).
//      Where a concept obviously admits more states than appeared in that sample
//      (e.g. break-of-structure direction, tape speed), the additional members are
//      included and explicitly annotated "not observed in sample" so the publish
//      mapping is complete and future indicator output does not silently fall to
//      Unknown. No value here is invented data; these are concept domains.
//
//  CONTRACT RULE:
//      Every enum has Unknown = 0 as its default. A token the mapper does not
//      recognise resolves to Unknown rather than throwing, so a snapshot is always
//      constructible; Unknown is the signal to extend the enum + bump the version.
//
//  THREAD SAFETY:  Value types; inherently safe to share.
// -----------------------------------------------------------------------------
namespace KeneticTradeRecorder.Shared
{
    /// <summary>Trading session bucket. Token: sess=. Observed: 4.</summary>
    public enum MarketSession
    {
        Unknown = 0,
        EthOvernight,   // ETH_overnight
        EthPreRth,      // ETH_pre-RTH
        Rth,            // RTH
        EthPostRth      // ETH_post-RTH
    }

    /// <summary>Short-horizon volume trend. Token: volTrend=. Observed: 3.</summary>
    public enum VolumeTrend
    {
        Unknown = 0,
        Increasing,     // increasing
        Decreasing,     // decreasing
        Flat            // flat
    }

    /// <summary>Cumulative-delta acceleration character. Token: deltaTrend=. Observed: 3.</summary>
    public enum DeltaTrend
    {
        Unknown = 0,
        AccelPositive,  // accel_positive
        AccelNegative,  // accel_negative
        Oscillating     // oscillating
    }

    /// <summary>Swing-structure classification. Token: swing=. Observed: 6.</summary>
    public enum SwingState
    {
        Unknown = 0,
        HhHlUptrend,        // HH-HL_UPTREND
        LlLhDowntrend,      // LL-LH_DOWNTREND
        Compressed,         // COMPRESSED
        Expanding,          // EXPANDING
        Mixed,              // MIXED
        InsufficientData    // INSUFFICIENT_DATA
    }

    /// <summary>
    /// Break-of-structure state. Token: bos=. Observed in sample: only "none".
    /// Bullish/Bearish are included as the obvious directional break states this
    /// concept admits (NOT observed in the supplied sample) so the publish mapping
    /// is complete; confirm the exact tokens your indicator emits.
    /// </summary>
    public enum BreakOfStructure
    {
        Unknown = 0,
        None,           // none            (observed)
        Bullish,        // (not observed in sample) e.g. "up"/"bull"
        Bearish         // (not observed in sample) e.g. "down"/"bear"
    }

    /// <summary>EMA stack alignment. Token: emaStack=. Observed: 4.</summary>
    public enum EmaStackState
    {
        Unknown = 0,
        Bullish,        // BULLISH
        Bearish,        // BEARISH
        Mixed,          // MIXED
        Flat            // FLAT
    }

    /// <summary>Opening behaviour classification. Token: openType=. Source: GetOpenType().</summary>
    public enum OpenType
    {
        Unknown = 0,        // UNKNOWN
        PreRth,             // PRE_RTH
        Assessing,          // ASSESSING
        GapAndGo,           // GAP_AND_GO
        GapFade,            // GAP_FADE
        TrendDay,           // TREND_DAY    (RTH trend, no gap; not in sample)
        Chop,               // CHOP         (range < 0.5*ATR; not in sample)
        NormalOpen          // NORMAL_OPEN  (default RTH; not in sample)
    }

    /// <summary>High-level regime. Token: regime=NAME(age=n). Source: ComputeRegime().</summary>
    public enum MarketRegime
    {
        Unknown = 0,
        Initializing,       // INITIALIZING       (warmup; not in sample)
        OvernightQuiet,     // OVERNIGHT_QUIET
        OvernightBuildup,   // OVERNIGHT_BUILDUP   (ETH delta/vol threshold; not in sample)
        ReversalForming,    // REVERSAL_FORMING
        Drifting,           // DRIFTING
        Chopping,           // CHOPPING
        TrendingUp,         // TRENDING_UP
        TrendingDown,       // TRENDING_DOWN
        VolatilityExpansion // VOLATILITY_EXPANSION
    }

    /// <summary>Phase within the RTH session. Token: rthPhase=. Source: GetRTHPhase().</summary>
    public enum RthPhase
    {
        Unknown = 0,
        Closed,         // closed
        PreOpen,        // pre-open       (isRTH true but minsIn<0; not in sample)
        OpeningWindow,  // opening_window
        Early,          // early
        Mid,            // mid
        Late,           // late
        Closing         // closing
    }

    /// <summary>Phase within the ETH (overnight) session. Token: ethPhase=. Observed: 5.</summary>
    public enum EthPhase
    {
        Unknown = 0,
        EveningAsia,    // evening_asia
        Europe,         // europe
        Rth,            // rth
        PreRth,         // pre-RTH
        PostRth         // post-RTH
    }

    /// <summary>Volatility regime. Token: volRegime=. Observed: 4.</summary>
    public enum VolatilityRegime
    {
        Unknown = 0,
        Normal,         // NORMAL
        Expanding,      // EXPANDING
        Compressed,     // COMPRESSED
        Extreme         // EXTREME
    }

    /// <summary>Gap direction. Token: gapDir=. Observed: UP, DN. None when no gap.</summary>
    public enum GapDirection
    {
        Unknown = 0,
        None,
        Up,             // UP
        Down            // DN
    }

    /// <summary>
    /// Reference-level kind (for nearest= and the named level slots).
    /// Tokens observed in nearest=: POC, TH, TO, TL, WkH, WkL, PDH, PDL, PDC, PDO, MoH, MoL.
    /// TH/TL/TO = today's High/Low/Open.
    /// </summary>
    public enum LevelKind
    {
        Unknown = 0,
        Poc,            // POC
        PriorDayHigh,   // PDH
        PriorDayLow,    // PDL
        PriorDayClose,  // PDC
        PriorDayOpen,   // PDO
        WeekHigh,       // WkH
        WeekLow,        // WkL
        MonthHigh,      // MoH
        MonthLow,       // MoL
        TodayHigh,      // TH
        TodayLow,       // TL
        TodayOpen       // TO
    }

    /// <summary>Anchored-VWAP anchor label. Token: AVWAPnn[Label ...]. Observed: 4.</summary>
    public enum AvwapLabel
    {
        Unknown = 0,
        Vol,            // Vol
        Strong,         // Strong
        Gap,            // Gap
        Big             // Big
    }

    /// <summary>VWAP slope direction (anchored + nearAvwap). Observed: rising, falling, flat.</summary>
    public enum VwapDirection
    {
        Unknown = 0,
        Rising,         // rising
        Falling,        // falling
        Flat            // flat
    }

    /// <summary>Price position relative to an anchored VWAP. Observed: above, below.</summary>
    public enum AvwapPosition
    {
        Unknown = 0,
        Above,          // above
        Below           // below
    }

    /// <summary>
    /// Tape speed. Token: tape=SPEED(...). Source: tapeRatio thresholds in EmitStateDump.
    /// Emitted values: slow, normal, fast, EXTREME. (Only 'slow' appeared in the sample.)
    /// </summary>
    public enum TapeSpeed
    {
        Unknown = 0,
        Slow,           // slow
        Normal,         // normal
        Fast,           // fast
        Extreme         // EXTREME
    }

    /// <summary>
    /// Order-book availability. Token: book=. Observed: no_L2.
    /// WithL2 included as the obvious counterpart (NOT observed in sample).
    /// </summary>
    public enum BookState
    {
        Unknown = 0,
        NoL2,           // no_L2 (observed)
        WithL2          // (not observed in sample)
    }

    /// <summary>
    /// Cross-instrument correlation state. Token: corr=. Observed: none.
    /// </summary>
    public enum CorrelationState
    {
        Unknown = 0,
        None            // none (observed)
    }
}
