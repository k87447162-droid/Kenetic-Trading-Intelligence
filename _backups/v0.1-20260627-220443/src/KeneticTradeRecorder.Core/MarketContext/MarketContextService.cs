// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Core - MarketContextService.cs   (PHASE 2 refactor)
//
//  PURPOSE:
//      The reusable market-context calculation engine, extracted from SessionLevels.
//      Every classifier and scoring routine is ported VERBATIM from the indicator's
//      "Indicator State Dump" region; the only change is that inputs come from a
//      MarketCalcInputs value instead of NinjaTrader `this`-state. There is now ONE
//      authoritative implementation of each calculation, and it is unit-testable
//      and reusable (e.g. replay/backtest) outside the chart.
//
//      SessionLevels keeps computing the NT-bound stateful primitives (volume
//      profile, anchored VWAP, swing detection, cumulative delta, EMAs/ATR/OFD,
//      session boundaries) and calls BuildSnapshot to assemble the immutable
//      MarketSnapshot. The recorder consumes that snapshot. No calculation is
//      duplicated or reimplemented.
//
//  FIDELITY NOTES (preserved quirks from the source, intentionally):
//      * TWO different ATR fallbacks exist in the source and are kept distinct:
//          - levels / atrRatio / volRegime fallback: (TodayHigh - TodayLow)
//          - ComputeRegime / GetOpenType fallback:   (High[0]  - Low[0])
//      * Backward loops are additionally bounded by the series Count so short test
//        windows cannot throw; with full windows the results are identical to NT.
//
//  THREAD SAFETY:  Stateless static methods over immutable inputs; fully safe.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using KeneticTradeRecorder.Shared;

namespace KeneticTradeRecorder.Core
{
    /// <summary>Pure engine: MarketCalcInputs -> MarketSnapshot. Classifiers ported verbatim from SessionLevels.</summary>
    public static class MarketContextService
    {
        // ---- small bounded helpers (loops match source but never over-index) ----
        private static double Sum(IReadOnlyList<double> s, int n)
        {
            double t = 0; int c = Math.Min(n, s.Count);
            for (int i = 0; i < c; i++) t += s[i];
            return t;
        }
        private static double Avg(IReadOnlyList<double> s, int n)
        {
            int c = Math.Min(n, s.Count);
            return c > 0 ? Sum(s, c) / c : 0;
        }

        // =====================================================================
        //  CLASSIFIERS (verbatim ports; token strings identical to the indicator)
        // =====================================================================

        /// <summary>Verbatim GetEmaStack().</summary>
        public static string EmaStackToken(MarketCalcInputs inp)
        {
            if (inp.EmaFast.Count == 0) return "UNKNOWN";
            if (inp.CurrentBar < inp.SlowEma) return "UNKNOWN";
            double f = inp.EmaFast[0], m = inp.EmaMid0, s = inp.EmaSlow0;
            if (f > m && m > s) return "BULLISH";
            if (f < m && m < s) return "BEARISH";
            if (Math.Abs(f - m) < inp.TickSize * 3 && Math.Abs(m - s) < inp.TickSize * 3) return "FLAT";
            return "MIXED";
        }

        /// <summary>Verbatim GetSwingStructure().</summary>
        public static string SwingStructureToken(MarketCalcInputs inp)
        {
            if (inp.SwingHighCount < 2 || inp.SwingLowCount < 2) return "INSUFFICIENT_DATA";
            bool hh = inp.SwingHigh0 > inp.SwingHigh1;
            bool hl = inp.SwingLow0 > inp.SwingLow1;
            bool lh = inp.SwingHigh0 < inp.SwingHigh1;
            bool ll = inp.SwingLow0 < inp.SwingLow1;
            if (hh && hl) return "HH-HL_UPTREND";
            if (ll && lh) return "LL-LH_DOWNTREND";
            if (hh && ll) return "EXPANDING";
            if (lh && hl) return "COMPRESSED";
            return "MIXED";
        }

        /// <summary>Verbatim GetVolumeTrend(bars).</summary>
        public static string VolumeTrendToken(MarketCalcInputs inp, int bars)
        {
            if (inp.CurrentBar < bars || inp.Volume.Count < bars) return "insufficient";
            double first = inp.Volume[bars - 1], last2 = inp.Volume[0];
            if (last2 > first * 1.1) return "increasing";
            if (last2 < first * 0.9) return "decreasing";
            return "flat";
        }

        /// <summary>Verbatim GetDeltaBarTrend(bars).</summary>
        public static string DeltaBarTrendToken(MarketCalcInputs inp, int bars)
        {
            if (inp.DeltaClose.Count < bars) return "insufficient";
            double d0 = inp.DeltaClose[0];
            double dN = inp.DeltaClose[Math.Min(bars - 1, inp.DeltaClose.Count - 1)];
            double change = d0 - dN;
            if (Math.Abs(change) < 10) return "oscillating";
            return change > 0 ? "accel_positive" : "accel_negative";
        }

        /// <summary>Verbatim ComputeTrendStrength().</summary>
        public static int ComputeTrendStrength(MarketCalcInputs inp)
        {
            int score = 0;
            if (inp.CurrentBar < inp.SlowEma) return score;

            if (inp.EmaFast.Count > 3)
            {
                double slope = inp.EmaFast[0] - inp.EmaFast[3];
                if (Math.Abs(slope) > inp.TickSize * 2) score += 2;
                else if (Math.Abs(slope) > inp.TickSize) score += 1;
            }

            string st = EmaStackToken(inp);
            if (st == "BULLISH" || st == "BEARISH") score += 2;

            if (inp.DeltaClose.Count >= 5)
            {
                int posBars = 0, negBars = 0;
                for (int i = 0; i < 5; i++)
                {
                    double bd = i < 4 ? inp.DeltaClose[i] - inp.DeltaClose[i + 1] : 0;
                    if (bd > 0) posBars++; else if (bd < 0) negBars++;
                }
                int consistent = Math.Max(posBars, negBars);
                if (consistent >= 4) score += 3;
                else if (consistent >= 3) score += 2;
                else if (consistent >= 2) score += 1;
            }

            string ss = SwingStructureToken(inp);
            if (ss == "HH-HL_UPTREND" || ss == "LL-LH_DOWNTREND") score += 3;
            else if (ss == "EXPANDING") score += 1;

            return Math.Min(score, 10);
        }

        /// <summary>Verbatim ComputeBarConviction(volAvg).</summary>
        public static int ComputeBarConviction(MarketCalcInputs inp, double volAvg)
        {
            int score = 0;
            double range = inp.High[0] - inp.Low[0];
            if (range <= 0) return 0;

            double closePos = (inp.Close[0] - inp.Low[0]) / range;
            bool isBull = inp.Close[0] >= inp.Open[0];
            if (isBull && closePos >= 0.7) score += 3;
            else if (!isBull && closePos <= 0.3) score += 3;
            else if ((isBull && closePos >= 0.5) || (!isBull && closePos <= 0.5)) score += 1;

            if (volAvg > 0)
            {
                double vr = inp.Volume[0] / volAvg;
                if (vr >= 2.0) score += 3;
                else if (vr >= 1.5) score += 2;
                else if (vr >= 1.1) score += 1;
            }

            if (inp.DeltaClose.Count >= 2)
            {
                double barDelta = inp.DeltaClose[0] - inp.DeltaClose[1];
                bool deltaPositive = barDelta > 0;
                if (isBull == deltaPositive) score += 4;
            }

            return Math.Min(score, 10);
        }

        /// <summary>Verbatim ComputeRegime(). NOTE: ATR fallback here is (High[0]-Low[0]).</summary>
        public static string RegimeToken(MarketCalcInputs inp)
        {
            if (!inp.SessionInitialized || inp.CurrentBar < Math.Max(inp.SlowEma, 20)) return "INITIALIZING";

            string stack = EmaStackToken(inp);
            double atr5val = inp.Atr5m.Count > 0 && inp.CurrentBar >= 14 ? inp.Atr5m[0] : (inp.High[0] - inp.Low[0]);
            double atrAvg = inp.Atr5m.Count > 0 ? Avg(inp.Atr5m, Math.Min(20, inp.CurrentBar)) : 0;
            double atrRatio = atrAvg > 0 ? atr5val / atrAvg : 1.0;

            bool bullStack = stack == "BULLISH";
            bool bearStack = stack == "BEARISH";
            double cd24 = inp.CumDelta24;

            if (inp.DivergenceActive) return "REVERSAL_FORMING";
            if (atrRatio > 2.0) return "VOLATILITY_EXPANSION";

            if (bullStack && inp.EmaStackAge >= 5 && cd24 > 0 && SwingStructureToken(inp).Contains("HH"))
                return "TRENDING_UP";
            if (bearStack && inp.EmaStackAge >= 5 && cd24 < 0 && SwingStructureToken(inp).Contains("LL"))
                return "TRENDING_DOWN";

            double volAvg = Avg(inp.Volume, Math.Min(20, inp.CurrentBar));
            if (inp.Volume.Count > 0 && inp.Volume[0] < volAvg * 0.7 && atrRatio < 0.9) return "DRIFTING";

            if (atrRatio < 0.9 && (stack == "MIXED" || stack == "FLAT")) return "CHOPPING";

            if (!inp.IsRth)
            {
                double absDelta = Math.Abs(inp.EtDeltaSession);
                double etVol = 0;
                int lim = Math.Min(Math.Min(inp.CurrentBar, 200), inp.Volume.Count);
                for (int i = 0; i < lim; i++) { if (!inp.IsRth) etVol += inp.Volume[i]; else break; }
                if (etVol > 0 && absDelta / etVol > 0.3 && atrRatio >= 0.9) return "OVERNIGHT_BUILDUP";
                return "OVERNIGHT_QUIET";
            }

            return "CHOPPING";
        }

        /// <summary>Verbatim GetOpenType(). NOTE: ATR fallback here is (High[0]-Low[0]).</summary>
        public static string OpenTypeToken(MarketCalcInputs inp)
        {
            if (!inp.SessionInitialized || inp.TodayOpen <= 0) return "UNKNOWN";
            double minsIn = (inp.TimeOfDay - inp.RthStart).TotalMinutes;
            if (!inp.IsRth || minsIn < 0) return "PRE_RTH";

            bool gapExists = Math.Abs(inp.GapPoints) >= inp.MinGapSize * inp.TickSize;
            bool gapUp2 = inp.GapPoints > 0;

            if (minsIn < 5) return "ASSESSING";

            double cd24 = inp.CumDelta24;

            if (gapExists)
            {
                bool movingAwayFromFill = gapUp2 ? inp.Close[0] > inp.TodayOpen : inp.Close[0] < inp.TodayOpen;
                if (movingAwayFromFill && cd24 != 0 && (gapUp2 ? cd24 > 0 : cd24 < 0)) return "GAP_AND_GO";
                return "GAP_FADE";
            }

            string ss = SwingStructureToken(inp);
            if (ss.Contains("UPTREND") || ss.Contains("DOWNTREND"))
            {
                if (Math.Abs(inp.GapPoints) < inp.TickSize * 2) return "TREND_DAY";
            }

            double atr5val = inp.Atr5m.Count > 0 && inp.CurrentBar >= 14 ? inp.Atr5m[0] : (inp.High[0] - inp.Low[0]);
            double range = inp.TodayHigh - inp.TodayLow;
            if (range < atr5val * 0.5) return "CHOP";

            return "NORMAL_OPEN";
        }

        /// <summary>Verbatim GetRTHPhase().</summary>
        public static string RthPhaseToken(MarketCalcInputs inp)
        {
            if (!inp.IsRth) return "closed";
            double minsIn = (inp.TimeOfDay - inp.RthStart).TotalMinutes;
            double rthLen = (inp.RthEnd - inp.RthStart).TotalMinutes;
            if (minsIn < 0) return "pre-open";
            if (minsIn < 10) return "opening_window";
            if (minsIn < 60) return "early";
            if (minsIn < rthLen - 60) return "mid";
            if (minsIn < rthLen - 10) return "late";
            return "closing";
        }

        /// <summary>Verbatim GetETHPhase().</summary>
        public static string EthPhaseToken(MarketCalcInputs inp)
        {
            if (inp.IsRth) return "rth";
            double h = inp.TimeOfDay.TotalHours;
            if (h >= 14 && h < 17) return "post-RTH";
            if (h >= 17 || h < 3) return "evening_asia";
            if (h >= 3 && h < 7) return "europe";
            return "pre-RTH";
        }

        /// <summary>Verbatim GetSessionType().</summary>
        public static string SessionTypeToken(MarketCalcInputs inp)
        {
            if (inp.IsRth) return "RTH";
            double h = inp.TimeOfDay.TotalHours;
            if (h >= 14 && h < 17) return "ETH_post-RTH";
            if (h < inp.RthStart.TotalHours) return "ETH_pre-RTH";
            return "ETH_overnight";
        }

        /// <summary>Verbatim volRegime threshold.</summary>
        public static string VolRegimeToken(double atrRatio) =>
            atrRatio > 2.0 ? "EXTREME" : atrRatio > 1.5 ? "EXPANDING" : atrRatio < 0.7 ? "COMPRESSED" : "NORMAL";

        /// <summary>Verbatim tapeSpeed threshold.</summary>
        public static string TapeSpeedToken(double tapeRatio) =>
            tapeRatio >= 3.0 ? "EXTREME" : tapeRatio >= 1.5 ? "fast" : tapeRatio <= 0.4 ? "slow" : "normal";

        /// <summary>Convenience: regime as an enum.</summary>
        public static MarketRegime Regime(MarketCalcInputs inp) => MarketEnumMap.Regime(RegimeToken(inp));

        /// <summary>Convenience: EMA stack as an enum.</summary>
        public static EmaStackState EmaStack(MarketCalcInputs inp) => MarketEnumMap.EmaStack(EmaStackToken(inp));

        // =====================================================================
        //  SNAPSHOT ASSEMBLY (numeric fields computed verbatim from the dump)
        // =====================================================================

        private static PriceLevel MakeLevel(double price, double cur, double atr)
        {
            if (price <= 0) return PriceLevel.None;            // dump prints "N/A" for these
            double d = cur - price;                            // verbatim LD: d = curPrice - lvl
            return new PriceLevel(price, d, Math.Abs(d) / atr);
        }

        /// <summary>
        /// Build the immutable snapshot from one bar's primitives. All numeric fields
        /// and classifications are computed exactly as the indicator's state dump does.
        /// </summary>
        public static MarketSnapshot BuildSnapshot(MarketCalcInputs inp) =>
            BuildSnapshot(inp, RegimeToken(inp));

        /// <summary>
        /// Overload that uses a caller-supplied regime token instead of recomputing it.
        /// SessionLevels uses this to preserve the source's ordering quirk: ComputeRegime()
        /// runs BEFORE emaStackAge is incremented, so the regime must be computed with the
        /// pre-update stack age, while the printed RegimeAge/StackAge use the post-update
        /// values carried on <paramref name="inp"/>. Everything else is computed from inp.
        /// </summary>
        public static MarketSnapshot BuildSnapshot(MarketCalcInputs inp, string regimeToken)
        {
            double cur = inp.Close[0];

            // --- volume / range / delta numerics (verbatim) ---
            double volAvg20 = Avg(inp.Volume, 20);
            double volRatio = volAvg20 > 0 ? inp.Volume[0] / volAvg20 : 1.0;
            double pctFromOpen = inp.TodayOpen > 0 ? (cur - inp.TodayOpen) / inp.TodayOpen * 100.0 : 0;

            double range5avg = 0;
            { int c = Math.Min(5, inp.High.Count); for (int i = 0; i < c; i++) range5avg += inp.High[i] - inp.Low[i]; range5avg /= Math.Max(1, c); }
            double range20avg = 0; { int c = Math.Min(20, inp.High.Count); for (int i = 0; i < c; i++) range20avg += inp.High[i] - inp.Low[i]; range20avg /= Math.Max(1, c); }
            double rangePos = range20avg > 0 ? Math.Min(2.0, range5avg / range20avg) : 1.0;

            double deltaAsPct = inp.SessionVolume > 0 ? inp.CumDelta24 / inp.SessionVolume * 100.0 : 0;

            double priceSlope = 0, deltaSlope = 0;
            int slopeBars = Math.Min(5, inp.CurrentBar);
            if (slopeBars >= 2)
            {
                if (inp.Close.Count >= slopeBars) priceSlope = (inp.Close[0] - inp.Close[slopeBars - 1]) / slopeBars;
                if (inp.DeltaClose.Count >= slopeBars) deltaSlope = (inp.DeltaClose[0] - inp.DeltaClose[slopeBars - 1]) / slopeBars;
            }

            // --- ATR / volatility (levels fallback = TodayHigh-TodayLow) ---
            double atr5mVal = inp.Atr5m.Count > 0 && inp.CurrentBar >= 14 ? inp.Atr5m[0] : (inp.TodayHigh - inp.TodayLow);
            if (atr5mVal <= 0) atr5mVal = 1.0;
            double atrAvg20 = inp.Atr5m.Count > 0 ? Avg(inp.Atr5m, Math.Min(20, inp.CurrentBar)) : 0;
            double atrRatio = atrAvg20 > 0 ? atr5mVal / atrAvg20 : 1.0;

            // --- classifications (verbatim tokens -> enums) ---
            var session   = MarketEnumMap.Session(SessionTypeToken(inp));
            var volTrend  = MarketEnumMap.VolumeTrend(VolumeTrendToken(inp, 5));
            var deltaTr   = MarketEnumMap.DeltaTrend(DeltaBarTrendToken(inp, 5));
            var swing     = MarketEnumMap.Swing(SwingStructureToken(inp));
            var emaStack  = MarketEnumMap.EmaStack(EmaStackToken(inp));
            var regime    = MarketEnumMap.Regime(regimeToken);
            var openType  = MarketEnumMap.OpenType(OpenTypeToken(inp));
            var rthPhase  = MarketEnumMap.RthPhase(RthPhaseToken(inp));
            var ethPhase  = MarketEnumMap.EthPhase(EthPhaseToken(inp));
            var volRegime = MarketEnumMap.VolRegime(VolRegimeToken(atrRatio));
            int trendStr  = ComputeTrendStrength(inp);
            int barConv   = ComputeBarConviction(inp, volAvg20);

            // --- EMAs + above/below (verbatim: curPrice > eN) ---
            var emas = new EmaState(
                inp.E5, inp.E9, inp.EmaMid0, inp.EmaSlow0,
                cur > inp.E5, cur > inp.E9, cur > inp.EmaMid0, cur > inp.EmaSlow0);

            // --- nearest level over valid (price>0) levels (verbatim selection) ---
            var allLevels = new List<(string name, double price)>
            {
                ("PDH", inp.PrevDayHigh), ("PDL", inp.PrevDayLow),
                ("PDO", inp.PrevDayOpen), ("PDC", inp.PrevDayClose),
                ("WkH", inp.WeekHigh),    ("WkL", inp.WeekLow),
                ("MoH", inp.MonthHigh),   ("MoL", inp.MonthLow),
                ("POC", inp.TodayPoc),    ("TO",  inp.TodayOpen),
                ("TH",  inp.TodayHigh),   ("TL",  inp.TodayLow),
            };
            var valid = allLevels.Where(l => l.price > 0).ToList();
            var nrst = valid.OrderBy(l => Math.Abs(l.price - cur)).FirstOrDefault();
            int lvlsIn1Atr = valid.Count(l => Math.Abs(l.price - cur) <= atr5mVal);
            var nearest = nrst.price > 0
                ? new NearestLevel(MarketEnumMap.LevelKind(nrst.name), MakeLevel(nrst.price, cur, atr5mVal))
                : NearestLevel.None;

            // --- gap (verbatim) ---
            bool gapDetected = Math.Abs(inp.GapPoints) >= inp.MinGapSize * inp.TickSize;
            bool gapUp2 = inp.GapPoints > 0;
            double gapFillDist = gapDetected ? cur - inp.PrevDayClose : 0;
            var gap = new GapInfo(
                gapDetected, Math.Abs(inp.GapPoints), inp.GapPercent,
                gapUp2 ? GapDirection.Up : GapDirection.Down,
                gapFillDist, cur - inp.TodayHigh, cur - inp.TodayLow, openType);

            // --- minsToRTH (verbatim) ---
            double minsToRth = inp.IsRth ? 0
                : (inp.TimeOfDay < inp.RthStart
                    ? (inp.RthStart - inp.TimeOfDay).TotalMinutes
                    : (inp.RthStart.Add(TimeSpan.FromDays(1)) - inp.TimeOfDay).TotalMinutes);

            // --- tape (verbatim) ---
            double tapeRatio = inp.TickCount5mAvg > 0
                ? (double)inp.TickCount30s / Math.Max(1.0, inp.TickCount5mAvg / 10.0) : 1.0;
            var tape = new TapeReading(
                MarketEnumMap.TapeSpeed(TapeSpeedToken(tapeRatio)),
                inp.TickCount30s, inp.TickCount5mAvg, tapeRatio);

            return new MarketSnapshot
            {
                Symbol = inp.Symbol,
                BarTimeUtc = inp.BarTimeUtc,
                Session = session,
                BarIndex = inp.CurrentBar,
                MinutesIntoSession = (int)Math.Round(inp.MinutesIntoSession),
                DayOfWeek = inp.DayOfWeek,

                Price = cur, Open = inp.Open[0], High = inp.High[0], Low = inp.Low[0], Close = inp.Close[0],
                Volume = (long)inp.Volume[0],

                SessionOpen = inp.TodayOpen, SessionHigh = inp.TodayHigh, SessionLow = inp.TodayLow,
                SessionVolume = (long)inp.SessionVolume,

                PctFromOpen = pctFromOpen,
                VolumeRatio = volRatio,
                VolumeTrend = volTrend,
                PctAbovePoc = (int)Math.Round(inp.PctAbovePoc),
                PctUpperRange = (int)Math.Round(inp.PctUpperRange),
                TrendStrength = trendStr,
                RangePosition = rangePos,

                CumulativeDelta24 = (long)inp.CumDelta24,
                CumulativeDeltaRth = (long)inp.CumDeltaRth,
                DeltaAsPct = deltaAsPct,
                DeltaTrend = deltaTr,
                EthDelta = (long)inp.EtDeltaSession,
                DivergenceActive = inp.DivergenceActive,
                PriceSlope = priceSlope,
                DeltaSlope = deltaSlope,
                BarConviction = barConv,

                Swing = swing,
                BreakOfStructure = BreakOfStructure.None,   // current indicator always emits "none"
                EmaStack = emaStack,
                StackAge = inp.EmaStackAge,
                Emas = emas,

                Poc = MakeLevel(inp.TodayPoc, cur, atr5mVal),
                PriorDayHigh = MakeLevel(inp.PrevDayHigh, cur, atr5mVal),
                PriorDayLow = MakeLevel(inp.PrevDayLow, cur, atr5mVal),
                PriorDayClose = MakeLevel(inp.PrevDayClose, cur, atr5mVal),
                PriorDayOpen = MakeLevel(inp.PrevDayOpen, cur, atr5mVal),
                WeekHigh = MakeLevel(inp.WeekHigh, cur, atr5mVal),
                WeekLow = MakeLevel(inp.WeekLow, cur, atr5mVal),
                MonthHigh = MakeLevel(inp.MonthHigh, cur, atr5mVal),
                MonthLow = MakeLevel(inp.MonthLow, cur, atr5mVal),
                Nearest = nearest,
                LevelsWithin1Atr = lvlsIn1Atr,

                AnchoredVwapCount = inp.AnchoredVwapCount,
                NearestAvwap = inp.NearestAvwap,
                SessionVwap = inp.SessionVwap,
                AnchoredVwaps = inp.AnchoredVwaps,

                Atr5m = atr5mVal,
                AtrRatio = atrRatio,
                VolatilityRegime = volRegime,
                Spread = inp.TickSize,        // spreadProxy = TickSize
                SpreadRatio = 1.0,            // verbatim

                Gap = gap,

                Regime = regime,
                RegimeAge = inp.RegimeAge,
                RegimeChange = inp.RegimeChange,
                RthPhase = rthPhase,
                EthPhase = ethPhase,
                MinutesToRth = (int)Math.Round(minsToRth),

                Tape = tape,
                Book = inp.L2Available ? BookState.WithL2 : BookState.NoL2,
                Correlation = CorrelationState.None,

                CalculationVersion = inp.CalculationVersion,
            };
        }
    }
}
