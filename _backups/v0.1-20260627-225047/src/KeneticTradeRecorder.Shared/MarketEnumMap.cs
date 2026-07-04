// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Shared - MarketEnumMap.cs   (PHASE 2)
//
//  PURPOSE:
//      Pure, total functions mapping the indicator's raw diagnostic tokens to the
//      strongly-typed enums. One canonical place for "this string means this enum",
//      so the publish hook, the conformance test, and any log backfill agree.
//
//  CONTRACT:
//      Total: an unrecognised token returns the enum's Unknown member (never throws).
//      Unknown in real output is the signal to extend the enum + bump the version.
//
//  THREAD SAFETY:  Stateless static methods over immutable inputs; fully safe.
// -----------------------------------------------------------------------------
using System;

namespace KeneticTradeRecorder.Shared
{
    /// <summary>Token-to-enum mapping for market-context categoricals.</summary>
    public static class MarketEnumMap
    {
        public static MarketSession Session(string? t) => t switch
        {
            "ETH_overnight" => MarketSession.EthOvernight,
            "ETH_pre-RTH"   => MarketSession.EthPreRth,
            "RTH"           => MarketSession.Rth,
            "ETH_post-RTH"  => MarketSession.EthPostRth,
            _ => MarketSession.Unknown
        };

        public static VolumeTrend VolumeTrend(string? t) => t switch
        {
            "increasing" => Shared.VolumeTrend.Increasing,
            "decreasing" => Shared.VolumeTrend.Decreasing,
            "flat"       => Shared.VolumeTrend.Flat,
            _ => Shared.VolumeTrend.Unknown
        };

        public static DeltaTrend DeltaTrend(string? t) => t switch
        {
            "accel_positive" => Shared.DeltaTrend.AccelPositive,
            "accel_negative" => Shared.DeltaTrend.AccelNegative,
            "oscillating"    => Shared.DeltaTrend.Oscillating,
            _ => Shared.DeltaTrend.Unknown
        };

        public static SwingState Swing(string? t) => t switch
        {
            "HH-HL_UPTREND"     => SwingState.HhHlUptrend,
            "LL-LH_DOWNTREND"   => SwingState.LlLhDowntrend,
            "COMPRESSED"        => SwingState.Compressed,
            "EXPANDING"         => SwingState.Expanding,
            "MIXED"             => SwingState.Mixed,
            "INSUFFICIENT_DATA" => SwingState.InsufficientData,
            _ => SwingState.Unknown
        };

        public static BreakOfStructure Bos(string? t) => t switch
        {
            "none" => BreakOfStructure.None,
            // Directional break tokens were not present in the supplied sample.
            // Adjust the right-hand strings to match your indicator if it emits them.
            "up"   or "bull" or "bullish" => BreakOfStructure.Bullish,
            "down" or "bear" or "bearish" => BreakOfStructure.Bearish,
            _ => BreakOfStructure.Unknown
        };

        public static EmaStackState EmaStack(string? t) => t switch
        {
            "BULLISH" => EmaStackState.Bullish,
            "BEARISH" => EmaStackState.Bearish,
            "MIXED"   => EmaStackState.Mixed,
            "FLAT"    => EmaStackState.Flat,
            _ => EmaStackState.Unknown
        };

        public static OpenType OpenType(string? t) => t switch
        {
            "PRE_RTH"     => Shared.OpenType.PreRth,
            "ASSESSING"   => Shared.OpenType.Assessing,
            "GAP_AND_GO"  => Shared.OpenType.GapAndGo,
            "GAP_FADE"    => Shared.OpenType.GapFade,
            "TREND_DAY"   => Shared.OpenType.TrendDay,
            "CHOP"        => Shared.OpenType.Chop,
            "NORMAL_OPEN" => Shared.OpenType.NormalOpen,
            _ => Shared.OpenType.Unknown
        };

        public static MarketRegime Regime(string? t) => t switch
        {
            "INITIALIZING"         => MarketRegime.Initializing,
            "OVERNIGHT_QUIET"      => MarketRegime.OvernightQuiet,
            "OVERNIGHT_BUILDUP"    => MarketRegime.OvernightBuildup,
            "REVERSAL_FORMING"     => MarketRegime.ReversalForming,
            "DRIFTING"             => MarketRegime.Drifting,
            "CHOPPING"             => MarketRegime.Chopping,
            "TRENDING_UP"          => MarketRegime.TrendingUp,
            "TRENDING_DOWN"        => MarketRegime.TrendingDown,
            "VOLATILITY_EXPANSION" => MarketRegime.VolatilityExpansion,
            _ => MarketRegime.Unknown
        };

        public static RthPhase RthPhase(string? t) => t switch
        {
            "closed"         => Shared.RthPhase.Closed,
            "pre-open"       => Shared.RthPhase.PreOpen,
            "opening_window" => Shared.RthPhase.OpeningWindow,
            "early"          => Shared.RthPhase.Early,
            "mid"            => Shared.RthPhase.Mid,
            "late"           => Shared.RthPhase.Late,
            "closing"        => Shared.RthPhase.Closing,
            _ => Shared.RthPhase.Unknown
        };

        public static EthPhase EthPhase(string? t) => t switch
        {
            "evening_asia" => Shared.EthPhase.EveningAsia,
            "europe"       => Shared.EthPhase.Europe,
            "rth"          => Shared.EthPhase.Rth,
            "pre-RTH"      => Shared.EthPhase.PreRth,
            "post-RTH"     => Shared.EthPhase.PostRth,
            _ => Shared.EthPhase.Unknown
        };

        public static VolatilityRegime VolRegime(string? t) => t switch
        {
            "NORMAL"     => VolatilityRegime.Normal,
            "EXPANDING"  => VolatilityRegime.Expanding,
            "COMPRESSED" => VolatilityRegime.Compressed,
            "EXTREME"    => VolatilityRegime.Extreme,
            _ => VolatilityRegime.Unknown
        };

        public static GapDirection GapDir(string? t) => t switch
        {
            "UP" => GapDirection.Up,
            "DN" => GapDirection.Down,
            _ => GapDirection.Unknown
        };

        /// <summary>Maps a level-kind token (as used in nearest=KIND@...).</summary>
        public static LevelKind LevelKind(string? t) => t switch
        {
            "POC" => Shared.LevelKind.Poc,
            "PDH" => Shared.LevelKind.PriorDayHigh,
            "PDL" => Shared.LevelKind.PriorDayLow,
            "PDC" => Shared.LevelKind.PriorDayClose,
            "PDO" => Shared.LevelKind.PriorDayOpen,
            "WkH" => Shared.LevelKind.WeekHigh,
            "WkL" => Shared.LevelKind.WeekLow,
            "MoH" => Shared.LevelKind.MonthHigh,
            "MoL" => Shared.LevelKind.MonthLow,
            "TH"  => Shared.LevelKind.TodayHigh,
            "TL"  => Shared.LevelKind.TodayLow,
            "TO"  => Shared.LevelKind.TodayOpen,
            _ => Shared.LevelKind.Unknown
        };

        public static AvwapLabel AvwapLabel(string? t) => t switch
        {
            "Vol"    => Shared.AvwapLabel.Vol,
            "Strong" => Shared.AvwapLabel.Strong,
            "Gap"    => Shared.AvwapLabel.Gap,
            "Big"    => Shared.AvwapLabel.Big,
            _ => Shared.AvwapLabel.Unknown
        };

        public static VwapDirection VwapDir(string? t) => t switch
        {
            "rising"  => VwapDirection.Rising,
            "falling" => VwapDirection.Falling,
            "flat"    => VwapDirection.Flat,
            _ => VwapDirection.Unknown
        };

        public static AvwapPosition AvwapPos(string? t) => t switch
        {
            "above" => AvwapPosition.Above,
            "below" => AvwapPosition.Below,
            _ => AvwapPosition.Unknown
        };

        public static TapeSpeed TapeSpeed(string? t) => t switch
        {
            "slow"    => Shared.TapeSpeed.Slow,
            "normal"  => Shared.TapeSpeed.Normal,
            "fast"    => Shared.TapeSpeed.Fast,
            "EXTREME" => Shared.TapeSpeed.Extreme,
            _ => Shared.TapeSpeed.Unknown
        };

        public static BookState Book(string? t) => t switch
        {
            "no_L2" => BookState.NoL2,
            "L2"    => BookState.WithL2,
            _ => BookState.Unknown
        };

        public static CorrelationState Correlation(string? t) => t switch
        {
            "none" => CorrelationState.None,
            _ => CorrelationState.Unknown
        };

        /// <summary>Maps a System.DayOfWeek name (token: dow=Monday...).</summary>
        public static DayOfWeek Dow(string? t) =>
            Enum.TryParse<DayOfWeek>(t, ignoreCase: false, out var d) ? d : default;

        /// <summary>EMA above/below code: 'A' = above (true), anything else ('B') = below (false).</summary>
        public static bool AboveCode(string? t) =>
            string.Equals(t, "A", StringComparison.Ordinal);
    }
}
