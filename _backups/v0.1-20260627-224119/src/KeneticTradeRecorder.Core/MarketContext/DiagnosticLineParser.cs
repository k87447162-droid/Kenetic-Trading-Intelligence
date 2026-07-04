// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Core - MarketContext/DiagnosticLineParser.cs
//
//  Parses ONE [INDICATOR STATE] diagnostic line into a MarketSnapshot using the
//  same MarketEnumMap the contract ships. This is the verified parser (validated
//  field-for-field against all 12,513 real diagnostic lines, with byte-exact
//  formatter round-trip). It is used on the LIVE path: the SessionLevels indicator
//  already builds this exact line each emission, and KeneticPublish hands it here
//  to produce the snapshot published to the registry. Total / never throws on
//  malformed input (missing tokens default to neutral values).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using KeneticTradeRecorder.Shared;

namespace KeneticTradeRecorder.Core.MarketContext
{
    /// <summary>Turns a single [INDICATOR STATE] line into a <see cref="MarketSnapshot"/>.</summary>
    public static class DiagnosticLineParser
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static string? M(string line, string pattern, int g = 1)
        {
            var m = Regex.Match(line, pattern);
            return m.Success ? m.Groups[g].Value : null;
        }
        private static double D(string? s) =>
            string.IsNullOrEmpty(s) ? 0d : double.Parse(s, NumberStyles.Float, Inv);
        private static long L(string? s) =>
            string.IsNullOrEmpty(s) ? 0L : long.Parse(s, NumberStyles.Integer, Inv);
        private static int I(string? s) =>
            string.IsNullOrEmpty(s) ? 0 : int.Parse(s, NumberStyles.Integer, Inv);

        private static PriceLevel Level(string line, string key)
        {
            var m = Regex.Match(line, key + @"=([-\d.]+)\(([-+][\d.]+)pts/([\d.]+)ATR\)");
            return m.Success
                ? new PriceLevel(D(m.Groups[1].Value), D(m.Groups[2].Value), D(m.Groups[3].Value))
                : PriceLevel.None;
        }

        public static MarketSnapshot Parse(string line)
        {
            // identity & timing
            string sym = M(line, @"sym=(.+?) ts=") ?? "";
            string tsRaw = M(line, @"ts=(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)") ?? "";
            DateTimeOffset ts = DateTime.TryParseExact(tsRaw, "yyyy-MM-dd HH:mm:ss", Inv,
                DateTimeStyles.None, out var dt) ? new DateTimeOffset(dt, TimeSpan.Zero) : default;

            // EMA above/below codes
            var abv = Regex.Match(line, @"abv=5:(\w) 9:(\w) 21:(\w) 50:(\w)");
            var emas = new EmaState(
                D(M(line, @" e5=([-\d.]+)")), D(M(line, @" e9=([-\d.]+)")),
                D(M(line, @" e21=([-\d.]+)")), D(M(line, @" e50=([-\d.]+)")),
                MarketEnumMap.AboveCode(abv.Groups[1].Value), MarketEnumMap.AboveCode(abv.Groups[2].Value),
                MarketEnumMap.AboveCode(abv.Groups[3].Value), MarketEnumMap.AboveCode(abv.Groups[4].Value));

            // nearest level
            var nm = Regex.Match(line, @"nearest=([A-Za-z]+)@([-\d.]+)\(([-+][\d.]+)pts/([\d.]+)ATR\)");
            var nearest = nm.Success
                ? new NearestLevel(MarketEnumMap.LevelKind(nm.Groups[1].Value),
                    new PriceLevel(D(nm.Groups[2].Value), D(nm.Groups[3].Value), D(nm.Groups[4].Value)))
                : NearestLevel.None;

            // nearest avwap
            var nav = Regex.Match(line, @"nearAvwap=([-\d.]+)\((\w+),([-+][\d.]+)pts\)");
            var nearAvwap = nav.Success
                ? new NearestAvwap(D(nav.Groups[1].Value), MarketEnumMap.VwapDir(nav.Groups[2].Value), D(nav.Groups[3].Value))
                : NearestAvwap.None;

            // anchored vwaps (0..n)
            var avwaps = new List<AnchoredVwap>();
            foreach (Match a in Regex.Matches(line,
                @"AVWAP(\d+)\[(\w+) val=([-\d.]+) (\w+) (above|below) ([-+][\d.]+)pts\]"))
            {
                avwaps.Add(new AnchoredVwap(
                    I(a.Groups[1].Value),
                    MarketEnumMap.AvwapLabel(a.Groups[2].Value),
                    D(a.Groups[3].Value),
                    MarketEnumMap.VwapDir(a.Groups[4].Value),
                    MarketEnumMap.AvwapPos(a.Groups[5].Value),
                    D(a.Groups[6].Value)));
            }

            // gap
            var gsz = Regex.Match(line, @"gapSz=([\d.]+)pts/([-+][\d.]+)%");
            var gap = new GapInfo(
                string.Equals(M(line, @"gap=(\w+)"), "True", StringComparison.Ordinal),
                gsz.Success ? D(gsz.Groups[1].Value) : 0d,
                gsz.Success ? D(gsz.Groups[2].Value) : 0d,
                MarketEnumMap.GapDir(M(line, @"gapDir=(\w+)")),
                D(M(line, @"gapFillDist=([-+]?[\d.]+)")),
                D(M(line, @"ethHighDist=([-+]?[\d.]+)")),
                D(M(line, @"ethLowDist=([-+]?[\d.]+)")),
                MarketEnumMap.OpenType(M(line, @"openType=(\S+)")));

            // regime + change flag
            var rm = Regex.Match(line, @"regime=([A-Z_]+)\(age=(\d+)\)");
            var regime = MarketEnumMap.Regime(rm.Groups[1].Value);
            int regimeAge = I(rm.Groups[2].Value);
            bool regimeChange = line.Contains("REGIME_CHANGE");

            // tape
            var tp = Regex.Match(line, @"tape=(\w+)\(ticks30s=(\d+) avg=(\d+) ratio=([\d.]+)\)");
            var tape = tp.Success
                ? new TapeReading(MarketEnumMap.TapeSpeed(tp.Groups[1].Value),
                    I(tp.Groups[2].Value), D(tp.Groups[3].Value), D(tp.Groups[4].Value))
                : TapeReading.None;

            return new MarketSnapshot
            {
                Symbol = sym,
                BarTimeUtc = ts,
                Session = MarketEnumMap.Session(M(line, @"sess=(\S+)")),
                BarIndex = I(M(line, @" bar=(\d+)")),
                MinutesIntoSession = I(M(line, @"minsIn=(-?\d+)")),
                DayOfWeek = MarketEnumMap.Dow(M(line, @"dow=(\w+)")),

                Price = D(M(line, @" px=([-\d.]+)")),
                Open = D(M(line, @" O=([-\d.]+)")),
                High = D(M(line, @" H=([-\d.]+)")),
                Low = D(M(line, @" L=([-\d.]+)")),
                Close = D(M(line, @" C=([-\d.]+)")),
                Volume = L(M(line, @" V=(\d+)")),

                SessionOpen = D(M(line, @"sessO=([-\d.]+)")),
                SessionHigh = D(M(line, @"sessH=([-\d.]+)")),
                SessionLow = D(M(line, @"sessL=([-\d.]+)")),
                SessionVolume = L(M(line, @"sessVol=(\d+)")),

                PctFromOpen = D(M(line, @"pctFromO=([-+][\d.]+)%")),
                VolumeRatio = D(M(line, @"volRatio=([\d.]+)")),
                VolumeTrend = MarketEnumMap.VolumeTrend(M(line, @"volTrend=(\S+)")),
                PctAbovePoc = I(M(line, @"pctAbovePOC=(\d+)%")),
                PctUpperRange = I(M(line, @"pctUpperRange=(\d+)%")),
                TrendStrength = I(M(line, @"trendStr=(\d+)/10")),
                RangePosition = D(M(line, @"rangePos=([-\d.]+)")),

                CumulativeDelta24 = L(M(line, @"cd24=(-?\d+)")),
                CumulativeDeltaRth = L(M(line, @"cdRTH=(-?\d+)")),
                DeltaAsPct = D(M(line, @"deltaAsPct=([-+][\d.]+)%")),
                DeltaTrend = MarketEnumMap.DeltaTrend(M(line, @"deltaTrend=(\S+)")),
                EthDelta = L(M(line, @"etDelta=(-?\d+)")),
                DivergenceActive = string.Equals(M(line, @"divActive=(\w+)"), "True", StringComparison.Ordinal),
                PriceSlope = D(M(line, @"priceSlope=([-+]?[\d.]+)")),
                DeltaSlope = D(M(line, @"deltaSlope=([-+]?[\d.]+)")),
                BarConviction = I(M(line, @"barConv=(\d+)/10")),

                Swing = MarketEnumMap.Swing(M(line, @"swing=(\S+)")),
                BreakOfStructure = MarketEnumMap.Bos(M(line, @"bos=(\S+)")),
                EmaStack = MarketEnumMap.EmaStack(M(line, @"emaStack=(\S+)")),
                StackAge = I(M(line, @"stackAge=(\d+)")),
                Emas = emas,

                Poc = Level(line, "POC"),
                PriorDayHigh = Level(line, "PDH"),
                PriorDayLow = Level(line, "PDL"),
                PriorDayClose = Level(line, "PDC"),
                PriorDayOpen = Level(line, "PDO"),
                WeekHigh = Level(line, "WkH"),
                WeekLow = Level(line, "WkL"),
                MonthHigh = Level(line, "MoH"),
                MonthLow = Level(line, "MoL"),
                Nearest = nearest,
                LevelsWithin1Atr = I(M(line, @"lvlsIn1ATR=(\d+)")),

                AnchoredVwapCount = I(M(line, @"avwaps=(\d+)")),
                NearestAvwap = nearAvwap,
                SessionVwap = D(M(line, @"sessVwap=([-\d.]+)")),
                AnchoredVwaps = avwaps,

                Atr5m = D(M(line, @"atr5m=([\d.]+)")),
                AtrRatio = D(M(line, @"atrRatio=([\d.]+)")),
                VolatilityRegime = MarketEnumMap.VolRegime(M(line, @"volRegime=(\S+)")),
                Spread = D(M(line, @"spread=([\d.]+)")),
                SpreadRatio = D(M(line, @"spreadRatio=([\d.]+)x")),

                Gap = gap,

                Regime = regime,
                RegimeAge = regimeAge,
                RegimeChange = regimeChange,
                RthPhase = MarketEnumMap.RthPhase(M(line, @"rthPhase=(\S+)")),
                EthPhase = MarketEnumMap.EthPhase(M(line, @"ethPhase=(\S+)")),
                MinutesToRth = I(M(line, @"minsToRTH=(-?\d+)")),

                Tape = tape,
                Book = MarketEnumMap.Book(M(line, @"book=(\S+)")),
                Correlation = MarketEnumMap.Correlation(M(line, @"corr=(\S+)")),

                CalculationVersion = 0,
                RawDiagnostic = line
            };
        }
    }
}
