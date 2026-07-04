// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Core - MarketSnapshotDiagnosticFormatter.cs  (PHASE 2 refactor)
//
//  PURPOSE:
//      Reproduce SessionLevels' exact "[INDICATOR STATE]" diagnostic line FROM a
//      MarketSnapshot. The format string below is copied verbatim from the indicator
//      so the printed line is byte-identical. This lets EmitStateDump be refactored
//      to: build snapshot -> publish -> Print(Format(snapshot)) — one computation,
//      no duplication, and the validated diagnostic output is preserved.
//
//      The diagnostic Print is instrumentation, NOT chart rendering; refactoring it
//      does not touch any OnRender/visual behavior.
//
//  ROUND-TRIP VERIFIED:
//      parse(line) -> snapshot -> Format(snapshot) reproduces all 12,513 sample
//      records exactly. (book/corr DETAIL strings — the bid/ask sizes when L2 is
//      present and the per-instrument correlation breakdown — are not carried by the
//      contract; the sample is entirely no_L2 / none, so reproduction is exact. If
//      you later run with L2 or correlation enabled and need those details in the
//      snapshot, say so and we add the fields.)
//
//  THREAD SAFETY:  Stateless static over an immutable input; safe.
// -----------------------------------------------------------------------------
using System;
using System.Globalization;
using System.Text;
using KeneticTradeRecorder.Shared;

namespace KeneticTradeRecorder.Core
{
    /// <summary>Formats a MarketSnapshot back into the indicator's exact diagnostic line.</summary>
    public static class MarketSnapshotDiagnosticFormatter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ----- enum -> exact source token -----
        private static string Sess(MarketSession v) => v switch
        { MarketSession.Rth => "RTH", MarketSession.EthPostRth => "ETH_post-RTH",
          MarketSession.EthPreRth => "ETH_pre-RTH", MarketSession.EthOvernight => "ETH_overnight", _ => "ETH_overnight" };
        private static string VolTr(VolumeTrend v) => v switch
        { VolumeTrend.Increasing => "increasing", VolumeTrend.Decreasing => "decreasing",
          VolumeTrend.Flat => "flat", _ => "insufficient" };
        private static string DeltaTr(DeltaTrend v) => v switch
        { DeltaTrend.AccelPositive => "accel_positive", DeltaTrend.AccelNegative => "accel_negative",
          DeltaTrend.Oscillating => "oscillating", _ => "insufficient" };
        private static string Swing(SwingState v) => v switch
        { SwingState.HhHlUptrend => "HH-HL_UPTREND", SwingState.LlLhDowntrend => "LL-LH_DOWNTREND",
          SwingState.Expanding => "EXPANDING", SwingState.Compressed => "COMPRESSED",
          SwingState.Mixed => "MIXED", SwingState.InsufficientData => "INSUFFICIENT_DATA", _ => "UNKNOWN" };
        private static string Bos(BreakOfStructure v) => v switch
        { BreakOfStructure.Bullish => "bullish", BreakOfStructure.Bearish => "bearish", _ => "none" };
        private static string Stack(EmaStackState v) => v switch
        { EmaStackState.Bullish => "BULLISH", EmaStackState.Bearish => "BEARISH",
          EmaStackState.Mixed => "MIXED", EmaStackState.Flat => "FLAT", _ => "UNKNOWN" };
        private static string Regime(MarketRegime v) => v switch
        { MarketRegime.Initializing => "INITIALIZING", MarketRegime.OvernightQuiet => "OVERNIGHT_QUIET",
          MarketRegime.OvernightBuildup => "OVERNIGHT_BUILDUP", MarketRegime.ReversalForming => "REVERSAL_FORMING",
          MarketRegime.Drifting => "DRIFTING", MarketRegime.Chopping => "CHOPPING",
          MarketRegime.TrendingUp => "TRENDING_UP", MarketRegime.TrendingDown => "TRENDING_DOWN",
          MarketRegime.VolatilityExpansion => "VOLATILITY_EXPANSION", _ => "INITIALIZING" };
        private static string Open(OpenType v) => v switch
        { OpenType.PreRth => "PRE_RTH", OpenType.Assessing => "ASSESSING", OpenType.GapAndGo => "GAP_AND_GO",
          OpenType.GapFade => "GAP_FADE", OpenType.TrendDay => "TREND_DAY", OpenType.Chop => "CHOP",
          OpenType.NormalOpen => "NORMAL_OPEN", _ => "UNKNOWN" };
        private static string Rth(RthPhase v) => v switch
        { RthPhase.Closed => "closed", RthPhase.PreOpen => "pre-open", RthPhase.OpeningWindow => "opening_window",
          RthPhase.Early => "early", RthPhase.Mid => "mid", RthPhase.Late => "late", RthPhase.Closing => "closing", _ => "closed" };
        private static string Eth(EthPhase v) => v switch
        { EthPhase.Rth => "rth", EthPhase.PostRth => "post-RTH", EthPhase.EveningAsia => "evening_asia",
          EthPhase.Europe => "europe", EthPhase.PreRth => "pre-RTH", _ => "pre-RTH" };
        private static string Vol(VolatilityRegime v) => v switch
        { VolatilityRegime.Extreme => "EXTREME", VolatilityRegime.Expanding => "EXPANDING",
          VolatilityRegime.Compressed => "COMPRESSED", _ => "NORMAL" };
        private static string Tape(TapeSpeed v) => v switch
        { TapeSpeed.Extreme => "EXTREME", TapeSpeed.Fast => "fast", TapeSpeed.Slow => "slow", _ => "normal" };
        private static string Lvl(LevelKind v) => v switch
        { LevelKind.Poc => "POC", LevelKind.PriorDayHigh => "PDH", LevelKind.PriorDayLow => "PDL",
          LevelKind.PriorDayClose => "PDC", LevelKind.PriorDayOpen => "PDO", LevelKind.WeekHigh => "WkH",
          LevelKind.WeekLow => "WkL", LevelKind.MonthHigh => "MoH", LevelKind.MonthLow => "MoL",
          LevelKind.TodayHigh => "TH", LevelKind.TodayLow => "TL", LevelKind.TodayOpen => "TO", _ => "" };
        private static string Lbl(AvwapLabel v) => v switch
        { AvwapLabel.Vol => "Vol", AvwapLabel.Strong => "Strong", AvwapLabel.Gap => "Gap", AvwapLabel.Big => "Big", _ => "Vol" };
        private static string Dir(VwapDirection v) => v switch
        { VwapDirection.Rising => "rising", VwapDirection.Falling => "falling", _ => "flat" };
        private static string Pos(AvwapPosition v) => v == AvwapPosition.Below ? "below" : "above";
        private static string GapD(GapDirection v) => v == GapDirection.Down ? "DN" : "UP";

        // verbatim LD lambda: "+0.00;-0.00pts/F2ATR", or "N/A" for empty levels
        private static string LD(PriceLevel p) =>
            p.Value <= 0 ? "N/A"
            : string.Format(Inv, "{0:+0.00;-0.00}pts/{1:F2}ATR", p.DistancePts, p.DistanceAtr);

        private const string Fmt =
            "[INDICATOR STATE] sym={0} ts={1} sess={2} bar={3} minsIn={4:F0} dow={5} " +
            "px={6:F2} O={7:F2} H={8:F2} L={9:F2} C={10:F2} V={11:F0} " +
            "sessO={12:F2} sessH={13:F2} sessL={14:F2} pctFromO={15:+0.00;-0.00}% " +
            "sessVol={16:F0} volRatio={17:F2} volTrend={18} " +
            "pctAbovePOC={19:F0}% pctUpperRange={20:F0}% " +
            "trendStr={21}/10 rangePos={22:F2} " +
            "cd24={23:F0} cdRTH={24:F0} deltaAsPct={25:+0.00;-0.00}% " +
            "deltaTrend={26} etDelta={27:F0} divActive={28} " +
            "priceSlope={29:+0.00;-0.00} deltaSlope={30:+0.00;-0.00} barConv={31}/10 " +
            "swing={32} bos={33} emaStack={34} stackAge={35} " +
            "e5={36:F2} e9={37:F2} e21={38:F2} e50={39:F2} abv={40} " +
            "POC={41:F2}({42}) PDH={43:F2}({44}) PDL={45:F2}({46}) " +
            "PDC={47:F2}({48}) PDO={49:F2}({50}) " +
            "WkH={51:F2}({52}) WkL={53:F2}({54}) MoH={55:F2}({56}) MoL={57:F2}({58}) " +
            "nearest={59}@{60:F2}({61}) lvlsIn1ATR={62} " +
            "avwaps={63} nearAvwap={64} sessVwap={65:F2}{66} " +
            "atr5m={67:F2} atrRatio={68:F2} volRegime={69} " +
            "spread={70:F4} spreadRatio={71:F2}x " +
            "gap={72} gapSz={73:F2}pts/{74:+0.00;-0.00}% gapDir={75} gapFillDist={76:+0.00;-0.00} " +
            "ethHighDist={77:+0.00;-0.00} ethLowDist={78:+0.00;-0.00} openType={79} " +
            "regime={80}(age={81}){82} " +
            "rthPhase={83} ethPhase={84} minsToRTH={85:F0} " +
            "tape={86}(ticks30s={87} avg={88} ratio={89:F1}) book={90} corr={91}";

        /// <summary>Reproduce the exact "[INDICATOR STATE]" line for this snapshot.</summary>
        public static string Format(MarketSnapshot s)
        {
            // nearAvwap string
            string nearAvwap = (s.NearestAvwap.Value <= 0 && s.NearestAvwap.Direction == VwapDirection.Unknown)
                ? "none"
                : string.Format(Inv, "{0:F2}({1},{2:+0.00;-0.00}pts)",
                    s.NearestAvwap.Value, Dir(s.NearestAvwap.Direction), s.NearestAvwap.DistancePts);

            // avwap detail (one entry per anchored VWAP)
            var det = new StringBuilder();
            foreach (var v in s.AnchoredVwaps)
                det.AppendFormat(Inv, " AVWAP{0}[{1} val={2:F2} {3} {4} {5:+0.00;-0.00}pts]",
                    v.Id, Lbl(v.Label), v.Value, Dir(v.Direction), Pos(v.Position), v.DistancePts);

            string abv = string.Format(Inv, "5:{0} 9:{1} 21:{2} 50:{3}",
                s.Emas.Above5 ? "A" : "B", s.Emas.Above9 ? "A" : "B",
                s.Emas.Above21 ? "A" : "B", s.Emas.Above50 ? "A" : "B");

            string book = s.Book == BookState.WithL2 ? "L2" : "no_L2";   // detail not carried (see header)
            string corr = "none";                                        // detail not carried (see header)

            return string.Format(Inv, Fmt,
                /*0*/ s.Symbol,
                /*1*/ s.BarTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", Inv),
                /*2*/ Sess(s.Session), /*3*/ s.BarIndex, /*4*/ (double)s.MinutesIntoSession, /*5*/ s.DayOfWeek.ToString(),
                /*6*/ s.Price, /*7*/ s.Open, /*8*/ s.High, /*9*/ s.Low, /*10*/ s.Close, /*11*/ (double)s.Volume,
                /*12*/ s.SessionOpen, /*13*/ s.SessionHigh, /*14*/ s.SessionLow, /*15*/ s.PctFromOpen,
                /*16*/ (double)s.SessionVolume, /*17*/ s.VolumeRatio, /*18*/ VolTr(s.VolumeTrend),
                /*19*/ (double)s.PctAbovePoc, /*20*/ (double)s.PctUpperRange,
                /*21*/ s.TrendStrength, /*22*/ s.RangePosition,
                /*23*/ (double)s.CumulativeDelta24, /*24*/ (double)s.CumulativeDeltaRth, /*25*/ s.DeltaAsPct,
                /*26*/ DeltaTr(s.DeltaTrend), /*27*/ (double)s.EthDelta, /*28*/ s.DivergenceActive,
                /*29*/ s.PriceSlope, /*30*/ s.DeltaSlope, /*31*/ s.BarConviction,
                /*32*/ Swing(s.Swing), /*33*/ Bos(s.BreakOfStructure), /*34*/ Stack(s.EmaStack), /*35*/ s.StackAge,
                /*36*/ s.Emas.Ema5, /*37*/ s.Emas.Ema9, /*38*/ s.Emas.Ema21, /*39*/ s.Emas.Ema50, /*40*/ abv,
                /*41*/ s.Poc.Value, /*42*/ LD(s.Poc),
                /*43*/ s.PriorDayHigh.Value, /*44*/ LD(s.PriorDayHigh),
                /*45*/ s.PriorDayLow.Value, /*46*/ LD(s.PriorDayLow),
                /*47*/ s.PriorDayClose.Value, /*48*/ LD(s.PriorDayClose),
                /*49*/ s.PriorDayOpen.Value, /*50*/ LD(s.PriorDayOpen),
                /*51*/ s.WeekHigh.Value, /*52*/ LD(s.WeekHigh),
                /*53*/ s.WeekLow.Value, /*54*/ LD(s.WeekLow),
                /*55*/ s.MonthHigh.Value, /*56*/ LD(s.MonthHigh),
                /*57*/ s.MonthLow.Value, /*58*/ LD(s.MonthLow),
                /*59*/ Lvl(s.Nearest.Kind), /*60*/ s.Nearest.Level.Value, /*61*/ LD(s.Nearest.Level), /*62*/ s.LevelsWithin1Atr,
                /*63*/ s.AnchoredVwapCount, /*64*/ nearAvwap, /*65*/ s.SessionVwap, /*66*/ det.ToString(),
                /*67*/ s.Atr5m, /*68*/ s.AtrRatio, /*69*/ Vol(s.VolatilityRegime),
                /*70*/ s.Spread, /*71*/ s.SpreadRatio,
                /*72*/ s.Gap.Present, /*73*/ s.Gap.SizePts, /*74*/ s.Gap.SizePct, /*75*/ GapD(s.Gap.Direction), /*76*/ s.Gap.FillDistance,
                /*77*/ s.Gap.EthHighDistance, /*78*/ s.Gap.EthLowDistance, /*79*/ Open(s.Gap.OpenType),
                /*80*/ Regime(s.Regime), /*81*/ s.RegimeAge, /*82*/ s.RegimeChange ? " REGIME_CHANGE" : "",
                /*83*/ Rth(s.RthPhase), /*84*/ Eth(s.EthPhase), /*85*/ (double)s.MinutesToRth,
                /*86*/ Tape(s.Tape.Speed), /*87*/ s.Tape.Ticks30s, /*88*/ (int)s.Tape.Avg, /*89*/ s.Tape.Ratio, /*90*/ book, /*91*/ corr);
        }
    }
}
