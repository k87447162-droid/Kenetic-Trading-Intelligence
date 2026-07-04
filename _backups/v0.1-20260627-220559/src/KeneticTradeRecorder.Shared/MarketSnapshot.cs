// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Shared - MarketSnapshot.cs   (PHASE 2)
//
//  PURPOSE:
//      One immutable, strongly-typed value capturing the COMPLETE market context
//      the indicator computes for an instrument at a single bar. This is the only
//      object that crosses the indicator -> recorder boundary.
//
//  DESIGN PRINCIPLES (per project rules):
//      * Immutable. Built once (init-only); never mutated after publish. Safe to
//        hand between threads by reference.
//      * The indicator is the SOLE OWNER of every calculation. This type only holds
//        the already-computed results; it contains NO market math. Nothing here is
//        recomputed by the recorder -> zero duplication.
//      * Versioned. SnapshotVersion tracks this contract's shape; CalculationVersion
//        is stamped by the indicator so the math that produced a snapshot is known.
//      * Dependency-free. No NinjaTrader, no NuGet (IReadOnlyList instead of
//        ImmutableArray) so it compiles under netstandard2.0 for NT8 and for tests.
//
//  FIELD SET:
//      Mirrors the 74 fields proven present in every one of 12,513 real
//      [INDICATOR STATE] records. Names are the descriptive equivalents of the
//      terse log tokens (token shown in a comment beside each field).
//
//  THREAD SAFETY:  Immutable; inherently safe to publish/read concurrently.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace KeneticTradeRecorder.Shared
{
    /// <summary>A reference price level with its signed distance from current price.</summary>
    /// <param name="Value">The level's price.</param>
    /// <param name="DistancePts">price - level, signed (token: the +/-Npts component).</param>
    /// <param name="DistanceAtr">Distance magnitude expressed in ATR multiples (token: the NATR component).</param>
    public readonly record struct PriceLevel(double Value, double DistancePts, double DistanceAtr)
    {
        /// <summary>An empty/unset level.</summary>
        public static readonly PriceLevel None = new PriceLevel(0d, 0d, 0d);
    }

    /// <summary>The single nearest reference level, with its kind. Token: nearest=KIND@val(+pts/ATR).</summary>
    public readonly record struct NearestLevel(LevelKind Kind, PriceLevel Level)
    {
        public static readonly NearestLevel None = new NearestLevel(LevelKind.Unknown, PriceLevel.None);
    }

    /// <summary>One anchored VWAP. Token: AVWAPnn[Label val=V dir above|below +/-Npts].</summary>
    /// <param name="Id">The numeric anchor id (the nn in AVWAPnn).</param>
    /// <param name="Label">Anchor label (Vol/Strong/Gap/Big).</param>
    /// <param name="Value">The VWAP value.</param>
    /// <param name="Direction">Slope direction (rising/falling/flat).</param>
    /// <param name="Position">Price above or below this VWAP.</param>
    /// <param name="DistancePts">price - vwap, signed.</param>
    public readonly record struct AnchoredVwap(
        int Id,
        AvwapLabel Label,
        double Value,
        VwapDirection Direction,
        AvwapPosition Position,
        double DistancePts);

    /// <summary>The nearest anchored VWAP summary. Token: nearAvwap=val(dir,+/-Npts).</summary>
    public readonly record struct NearestAvwap(double Value, VwapDirection Direction, double DistancePts)
    {
        public static readonly NearestAvwap None = new NearestAvwap(0d, VwapDirection.Unknown, 0d);
    }

    /// <summary>EMA values and price's above/below position for each. Tokens: e5/e9/e21/e50 and abv=5:A/B ...</summary>
    /// <remarks>AboveN is true when price is above EMA-N (token code 'A'), false when below ('B').</remarks>
    public readonly record struct EmaState(
        double Ema5, double Ema9, double Ema21, double Ema50,
        bool Above5, bool Above9, bool Above21, bool Above50)
    {
        public static readonly EmaState None =
            new EmaState(0, 0, 0, 0, false, false, false, false);
    }

    /// <summary>Tape (time-and-sales) reading. Token: tape=SPEED(ticks30s=N avg=M ratio=R).</summary>
    public readonly record struct TapeReading(TapeSpeed Speed, int Ticks30s, double Avg, double Ratio)
    {
        public static readonly TapeReading None = new TapeReading(TapeSpeed.Unknown, 0, 0d, 0d);
    }

    /// <summary>Gap context for the session. Tokens: gap=, gapSz=Npts/+P%, gapDir=, gapFillDist=, ethHighDist=, ethLowDist=, openType=.</summary>
    public readonly record struct GapInfo(
        bool Present,
        double SizePts,
        double SizePct,
        GapDirection Direction,
        double FillDistance,
        double EthHighDistance,
        double EthLowDistance,
        OpenType OpenType)
    {
        public static readonly GapInfo None =
            new GapInfo(false, 0d, 0d, GapDirection.None, 0d, 0d, 0d, OpenType.Unknown);
    }

    /// <summary>
    /// Immutable, complete MARKET STATE for one instrument at one bar. This is the
    /// market-state component of a <see cref="MarketContext"/> (which also carries
    /// TradeSnapshot and TraderSnapshot). Constructed via object-initializer
    /// (init-only properties), built once by the indicator from values it already
    /// computed, then published to the ISnapshotRegistry.
    /// </summary>
    public sealed record MarketSnapshot
    {
        /// <summary>Version of THIS contract's shape. Bump when fields are added/changed.</summary>
        public const int CurrentSnapshotVersion = 1;

        // ----- identity & timing -----
        /// <summary>Instrument key (e.g. "MNQ 09-26"). Token: sym=. Also the registry key.</summary>
        public string Symbol { get; init; } = string.Empty;
        /// <summary>Bar timestamp in UTC. (Token ts= is exchange-local; publisher converts.)</summary>
        public DateTimeOffset BarTimeUtc { get; init; }
        /// <summary>Session bucket. Token: sess=.</summary>
        public MarketSession Session { get; init; } = MarketSession.Unknown;
        /// <summary>Bar ordinal within the data series. Token: bar=.</summary>
        public int BarIndex { get; init; }
        /// <summary>Minutes into the session; negative before the reference open. Token: minsIn=.</summary>
        public int MinutesIntoSession { get; init; }
        /// <summary>Day of week of the bar. Token: dow=.</summary>
        public DayOfWeek DayOfWeek { get; init; }

        // ----- current bar price & volume -----
        /// <summary>Current price. Token: px=.</summary>
        public double Price { get; init; }
        /// <summary>Bar open. Token: O=.</summary>
        public double Open { get; init; }
        /// <summary>Bar high. Token: H=.</summary>
        public double High { get; init; }
        /// <summary>Bar low. Token: L=.</summary>
        public double Low { get; init; }
        /// <summary>Bar close. Token: C=.</summary>
        public double Close { get; init; }
        /// <summary>Bar volume. Token: V=.</summary>
        public long Volume { get; init; }

        // ----- session aggregates -----
        /// <summary>Session open. Token: sessO=.</summary>
        public double SessionOpen { get; init; }
        /// <summary>Session high. Token: sessH=.</summary>
        public double SessionHigh { get; init; }
        /// <summary>Session low. Token: sessL=.</summary>
        public double SessionLow { get; init; }
        /// <summary>Session cumulative volume. Token: sessVol=.</summary>
        public long SessionVolume { get; init; }

        // ----- derived price / range / trend -----
        /// <summary>Percent move from session open (e.g. +2.73 means +2.73%). Token: pctFromO=.</summary>
        public double PctFromOpen { get; init; }
        /// <summary>Current vs typical volume ratio. Token: volRatio=.</summary>
        public double VolumeRatio { get; init; }
        /// <summary>Short-horizon volume trend. Token: volTrend=.</summary>
        public VolumeTrend VolumeTrend { get; init; } = VolumeTrend.Unknown;
        /// <summary>Percent of session range above POC (0..100). Token: pctAbovePOC=.</summary>
        public int PctAbovePoc { get; init; }
        /// <summary>Percent into the upper range (0..100). Token: pctUpperRange=.</summary>
        public int PctUpperRange { get; init; }
        /// <summary>Composite trend strength, 0..10. Token: trendStr=n/10.</summary>
        public int TrendStrength { get; init; }
        /// <summary>Position within range (token can exceed 1). Token: rangePos=.</summary>
        public double RangePosition { get; init; }

        // ----- order flow / delta -----
        /// <summary>Cumulative delta (24h window). Token: cd24=.</summary>
        public long CumulativeDelta24 { get; init; }
        /// <summary>Cumulative delta (RTH window). Token: cdRTH=.</summary>
        public long CumulativeDeltaRth { get; init; }
        /// <summary>Delta as percent. Token: deltaAsPct=.</summary>
        public double DeltaAsPct { get; init; }
        /// <summary>Delta acceleration character. Token: deltaTrend=.</summary>
        public DeltaTrend DeltaTrend { get; init; } = DeltaTrend.Unknown;
        /// <summary>ETH-session delta. Token: etDelta=.</summary>
        public long EthDelta { get; init; }
        /// <summary>Whether a price/delta divergence is active. Token: divActive=.</summary>
        public bool DivergenceActive { get; init; }
        /// <summary>Price slope. Token: priceSlope=.</summary>
        public double PriceSlope { get; init; }
        /// <summary>Delta slope. Token: deltaSlope=.</summary>
        public double DeltaSlope { get; init; }
        /// <summary>Bar conviction, 0..10. Token: barConv=n/10.</summary>
        public int BarConviction { get; init; }

        // ----- structure -----
        /// <summary>Swing-structure classification. Token: swing=.</summary>
        public SwingState Swing { get; init; } = SwingState.Unknown;
        /// <summary>Break-of-structure state. Token: bos=.</summary>
        public BreakOfStructure BreakOfStructure { get; init; } = BreakOfStructure.Unknown;
        /// <summary>EMA stack alignment. Token: emaStack=.</summary>
        public EmaStackState EmaStack { get; init; } = EmaStackState.Unknown;
        /// <summary>Bars since the current stack alignment began. Token: stackAge=.</summary>
        public int StackAge { get; init; }
        /// <summary>EMA values + above/below flags. Tokens: e5/e9/e21/e50, abv=.</summary>
        public EmaState Emas { get; init; } = EmaState.None;

        // ----- reference levels -----
        /// <summary>Point of control. Token: POC=.</summary>
        public PriceLevel Poc { get; init; } = PriceLevel.None;
        /// <summary>Prior-day high. Token: PDH=.</summary>
        public PriceLevel PriorDayHigh { get; init; } = PriceLevel.None;
        /// <summary>Prior-day low. Token: PDL=.</summary>
        public PriceLevel PriorDayLow { get; init; } = PriceLevel.None;
        /// <summary>Prior-day close. Token: PDC=.</summary>
        public PriceLevel PriorDayClose { get; init; } = PriceLevel.None;
        /// <summary>Prior-day open. Token: PDO=.</summary>
        public PriceLevel PriorDayOpen { get; init; } = PriceLevel.None;
        /// <summary>Week high. Token: WkH=.</summary>
        public PriceLevel WeekHigh { get; init; } = PriceLevel.None;
        /// <summary>Week low. Token: WkL=.</summary>
        public PriceLevel WeekLow { get; init; } = PriceLevel.None;
        /// <summary>Month high. Token: MoH=.</summary>
        public PriceLevel MonthHigh { get; init; } = PriceLevel.None;
        /// <summary>Month low. Token: MoL=.</summary>
        public PriceLevel MonthLow { get; init; } = PriceLevel.None;
        /// <summary>The single nearest level + its kind. Token: nearest=.</summary>
        public NearestLevel Nearest { get; init; } = NearestLevel.None;
        /// <summary>Count of reference levels within 1 ATR. Token: lvlsIn1ATR=.</summary>
        public int LevelsWithin1Atr { get; init; }

        // ----- VWAPs -----
        /// <summary>Number of anchored VWAPs tracked. Token: avwaps=.</summary>
        public int AnchoredVwapCount { get; init; }
        /// <summary>Nearest anchored VWAP summary. Token: nearAvwap=.</summary>
        public NearestAvwap NearestAvwap { get; init; } = NearestAvwap.None;
        /// <summary>Session VWAP (0 when not applicable). Token: sessVwap=.</summary>
        public double SessionVwap { get; init; }
        /// <summary>The anchored VWAPs detailed in the line. Tokens: AVWAPnn[...]. Never null.</summary>
        public IReadOnlyList<AnchoredVwap> AnchoredVwaps { get; init; } = System.Array.Empty<AnchoredVwap>();

        // ----- volatility & spread -----
        /// <summary>5-minute ATR. Token: atr5m=.</summary>
        public double Atr5m { get; init; }
        /// <summary>ATR vs typical ratio. Token: atrRatio=.</summary>
        public double AtrRatio { get; init; }
        /// <summary>Volatility regime. Token: volRegime=.</summary>
        public VolatilityRegime VolatilityRegime { get; init; } = VolatilityRegime.Unknown;
        /// <summary>Current spread (price units). Token: spread=.</summary>
        public double Spread { get; init; }
        /// <summary>Spread vs typical ratio. Token: spreadRatio= (e.g. 1.00x).</summary>
        public double SpreadRatio { get; init; }

        // ----- gap -----
        /// <summary>Gap context. Tokens: gap/gapSz/gapDir/gapFillDist/ethHighDist/ethLowDist/openType.</summary>
        public GapInfo Gap { get; init; } = GapInfo.None;

        // ----- regime & clock -----
        /// <summary>High-level regime. Token: regime=NAME(age=n).</summary>
        public MarketRegime Regime { get; init; } = MarketRegime.Unknown;
        /// <summary>Bars in the current regime. Token: the age=n inside regime=.</summary>
        public int RegimeAge { get; init; }
        /// <summary>Whether the regime changed on this bar. Token: presence of REGIME_CHANGE.</summary>
        public bool RegimeChange { get; init; }
        /// <summary>Phase within RTH. Token: rthPhase=.</summary>
        public RthPhase RthPhase { get; init; } = RthPhase.Unknown;
        /// <summary>Phase within ETH. Token: ethPhase=.</summary>
        public EthPhase EthPhase { get; init; } = EthPhase.Unknown;
        /// <summary>Minutes until RTH open (0 during RTH). Token: minsToRTH=.</summary>
        public int MinutesToRth { get; init; }

        // ----- microstructure -----
        /// <summary>Tape reading. Token: tape=.</summary>
        public TapeReading Tape { get; init; } = TapeReading.None;
        /// <summary>Order-book availability. Token: book=.</summary>
        public BookState Book { get; init; } = BookState.Unknown;
        /// <summary>Cross-instrument correlation state. Token: corr=.</summary>
        public CorrelationState Correlation { get; init; } = CorrelationState.Unknown;

        // ----- metadata -----
        /// <summary>Shape version of this snapshot (defaults to the current contract version).</summary>
        public int SnapshotVersion { get; init; } = CurrentSnapshotVersion;
        /// <summary>
        /// Version of the indicator calculation set that produced this snapshot.
        /// Stamped by the indicator so the math behind a stored snapshot is identifiable.
        /// </summary>
        public int CalculationVersion { get; init; }
        /// <summary>
        /// Optional verbatim source line this snapshot was built/parsed from. Empty for
        /// live-published snapshots; populated by the diagnostic-log parser (backfill).
        /// Kept for forensic reproducibility; never interpreted.
        /// </summary>
        public string RawDiagnostic { get; init; } = string.Empty;
    }
}
