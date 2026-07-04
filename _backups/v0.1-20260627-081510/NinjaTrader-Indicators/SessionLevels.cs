#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class SessionLevels : Indicator
    {
        // --- Session Data ------------------------------------------------------------
        private double previousDayOpen, previousDayClose, previousDayHigh, previousDayLow;
        private double weekHigh, weekLow, previousWeekHigh, previousWeekLow;
        private double monthHigh, monthLow, previousMonthHigh, previousMonthLow;
        private double todayOpen, todayHigh, todayLow, todayPOC;
        private Dictionary<double, double> volumeProfile;
        private DateTime currentSessionDate;
        private bool sessionInitialized;
        private DateTime lastSessionBoundary = DateTime.MinValue;  // true futures session boundary
        private DateTime currentSessionStart = DateTime.MinValue;  // start of current session

        // --- EMAs --------------------------------------------------------------------
        private EMA emaFast, emaMid, emaSlow;

        // --- Anchored VWAP -----------------------------------------------------------
        private class AnchoredVWAP
        {
            public int ID, AnchorBar;
            public DateTime AnchorTime;
            public double AnchorPrice, CumulativeTPV, CumulativeVolume;
            public string TriggerReason;
            public bool IsActive;
        }
        private List<AnchoredVWAP> anchoredVWAPs;
        private int vwapCounter;
        private double avgVolume, avgCandleSize;

        // --- Trendlines --------------------------------------------------------------
        private class TrendlineInfo
        {
            public int ID, CreatedBar;
            public bool IsBullish, IsActive, WasBroken;
            public List<SwingPoint> SwingPoints;
            public int? BrokenBar;
            public double Slope, Intercept;
            public DateTime ScheduledRemoval;
        }
        private class SwingPoint
        {
            public int BarIndex;
            public double Price;
            public bool IsHigh;
        }
        private List<TrendlineInfo> trendlines;
        private int trendlineCounter;
        private List<SwingPoint> swingHighs, swingLows;

        // --- Cumulative Delta Divergence ---------------------------------------------
        private double cumulativeDelta;            // running buy vol - sell vol
        private double prevCumDelta;               // cum delta of previous swing
        private double prevSwingPrice;             // price at previous swing
        private double prevSwingDelta;             // cum delta at previous swing
        private bool   lastSwingWasHigh;
        private int    divLabelCounter;
        // Rolling window for divergence detection
        private double peakPrice, peakDelta, troughPrice, troughDelta;
        private int    peakBar, troughBar;
        private bool   hasPeak, hasTrough;

        // --- Fibonacci from Gaps -----------------------------------------------------
        private class FibLevel
        {
            public string Tag;
            public double Price;
            public string Label;
            public System.Windows.Media.Brush Color;
        }
        private class GapFib
        {
            public int ID;
            public bool IsGapUp;          // true = gap up, false = gap down
            public double GapStart;       // previous session close
            public double GapEnd;         // today open
            public double SwingHigh;      // for extension calc
            public double SwingLow;
            public List<FibLevel> RetraceLevels;
            public List<FibLevel> ExtensionLevels;
            public bool IsActive;
        }
        private List<GapFib> gapFibs;
        private int fibCounter;
        private static readonly double[] FIB_RETRACE = { 0.0, 0.236, 0.382, 0.5, 0.618, 0.786, 1.0 };
        private static readonly double[] FIB_EXTEND  = { 1.272, 1.414, 1.618, 2.0, 2.618 };

        // --- OnRender label list -----------------------------------------------------
        private class RenderLabel
        {
            public string Text;
            public double Price;
            public System.Windows.Media.Color Color;
        }
        private List<RenderLabel> renderLabels = new List<RenderLabel>();

        // --- SharpDX resources -------------------------------------------------------
        private SharpDX.DirectWrite.TextFormat labelTextFormat;
        private SharpDX.DirectWrite.TextFormat instrumentTextFormat;
        private SharpDX.DirectWrite.TextFormat menuTextFormat;
        private SharpDX.DirectWrite.TextFormat menuHeaderFormat;
        private bool dxResourcesCreated = false;

        // --- Spike candle line tracking -----------------------------------------------
        private HashSet<int> bullSpikeBar = new HashSet<int>();
        private HashSet<int> bearSpikeBar = new HashSet<int>();

        // --- Dual cumulative delta  (real OrderFlowCumulativeDelta references) ---------
        // Correct API per NinjaTrader docs:
        //   OrderFlowCumulativeDelta(CumulativeDeltaType.BidAsk, CumulativeDeltaPeriod.Session, 0)
        //   Requires AddDataSeries(BarsPeriodType.Tick, 1) in Configure
        //   OFD is called with BidAsk and Session period
        private OrderFlowCumulativeDelta ofd24;    // CumulativeDeltaPeriod.Session
        private OrderFlowCumulativeDelta ofdRTH;   // CumulativeDeltaPeriod.Session (RTH-only via time filter)
        private bool   isRTH         = false;
        private TimeSpan rthStartSpan;
        private TimeSpan rthEndSpan;
        // Fallback approximation (used if OFD fails)
        private double cumDelta24Approx  = 0;
        private double cumDeltaRTHApprox = 0;
        // Accessors -- read real OFD if available, else approximation
        private double cumDelta24
        {
            get { try { return ofd24  != null ? ofd24.DeltaClose[0]  : cumDelta24Approx;  } catch { return cumDelta24Approx;  } }
        }
        private double cumDeltaRTH
        {
            // RTH is always our own accumulation (only adds delta during RTH hours)
            get { return cumDeltaRTHApprox; }
        }

        // --- Gap / % open -------------------------------------------------------------
        private double gapPoints  = 0;
        private double gapPercent = 0;

        // --- Tier 3: Cross-instrument, tape speed, order book ------------------------
        private int    corrSeries1Idx = -1, corrSeries2Idx = -1, corrSeries3Idx = -1;
        private string corr1Name = "", corr2Name = "", corr3Name = "";
        private int    tickCount30s = 0, tickCountPrev30s = 0, ticksThisBar = 0;
        private DateTime tickWindowStart = DateTime.MinValue;
        private int    tickCount5mAvg = 0;
        private Queue<int> tickCountHistory = new Queue<int>();
        private double bidSizeTop5 = 0, askSizeTop5 = 0, bookImbalance = 0;
        private bool   l2Available = false;

        // --- Proximity menu level -----------------------------------------------------
        private class MenuLevel
        {
            public string Label;
            public double Price, Dist;
            public System.Windows.Media.Color Color;
        }

        // =========================================================================
        #region OnStateChange
        // =========================================================================
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description  = "Session levels with right-pinned labels, auto-extending lines, and auto Fibonacci from gaps";
                Name         = "SessionLevels";
                Calculate    = Calculate.OnBarClose;
                IsOverlay    = true;
                IsAutoScale  = false;
                DrawOnPricePanel        = true;
                DisplayInDataBox        = true;
                PaintPriceMarkers       = true;
                IsSuspendedWhileInactive = true;
                ScaleJustification      = NinjaTrader.Gui.Chart.ScaleJustification.Right;

                AddPlot(new Stroke(Brushes.Cyan,   2), PlotStyle.Line, "EMA Fast");
                AddPlot(new Stroke(Brushes.Orange, 2), PlotStyle.Line, "EMA Mid");
                AddPlot(new Stroke(Brushes.Red,    2), PlotStyle.Line, "EMA Slow");
                // Area fill handled in Configure via AreaBrush = Transparent

                // Previous Day
                ShowPreviousDayLevels = true;
                PrevDayOpenColor      = Brushes.Cyan;
                PrevDayCloseColor     = Brushes.Orange;
                PrevDayHighColor      = Brushes.Lime;
                PrevDayLowColor       = Brushes.Red;

                // Weekly
                ShowWeeklyLevels  = true;
                WeekHighColor     = Brushes.DarkGreen;
                WeekLowColor      = Brushes.DarkRed;
                PrevWeekHighColor = Brushes.MediumSeaGreen;
                PrevWeekLowColor  = Brushes.IndianRed;

                // Monthly
                ShowMonthlyLevels   = true;
                MonthHighColor      = Brushes.DodgerBlue;
                MonthLowColor       = Brushes.DarkOrange;
                PrevMonthHighColor  = Brushes.SteelBlue;
                PrevMonthLowColor   = Brushes.SandyBrown;

                // Today
                ShowTodayOpen      = true;
                TodayOpenColor     = Brushes.White;
                ShowCurrentHighLow = true;
                CurrentHighColor   = Brushes.LimeGreen;
                CurrentLowColor    = Brushes.Tomato;

                // POC
                ShowPOC  = true;
                POCColor = Brushes.Magenta;
                POCWidth = 3;

                // Labels
                ShowLabels  = true;
                LabelFontSize = 11;

                // EMAs
                ShowEMAs     = true;
                FastEMA      = 9;
                MidEMA       = 21;
                SlowEMA      = 50;
                EMAFastColor = Brushes.Cyan;
                EMAMidColor  = Brushes.Orange;
                EMASlowColor = Brushes.Red;

                // Anchored VWAP
                ShowAnchoredVWAP           = true;
                VWAPVolumeSpikeMultiplier  = 2.0;
                VWAPCandleSizeMultiplier   = 2.5;
                VWAPLookbackPeriod         = 20;
                MaxActiveVWAPs             = 5;
                AnchoredVWAPColor          = Brushes.Yellow;
                AnchoredVWAPWidth          = 2;
                ShowVWAPLabels             = true;
                RemoveOldVWAPs             = true;
                VWAPMaxAgeBars             = 200;

                // Trendlines
                ShowTrendlines        = true;
                SwingBars             = 5;
                MinSwingPoints        = 3;
                TrendlineLookback     = 50;
                BullishTrendlineColor = Brushes.Lime;
                BearishTrendlineColor = Brushes.Red;
                TrendlineWidth        = 2;
                TrendlineExtension    = 20;
                AutoDeleteOnBreak     = true;
                RemovalDelayMinutes   = 5;
                MinTrendlineAngle     = 5.0;
                ShowTrendlineAlerts   = true;
                HighlightNearTrendline    = true;
                NearTrendlineDistance     = 5;

                // Fibonacci
                ShowFibonacci         = true;
                MinGapSize            = 4.0;    // minimum gap in ticks to trigger Fibonacci
                FibRetraceBullColor   = Brushes.DeepSkyBlue;
                FibRetraceBearColor   = Brushes.OrangeRed;
                FibExtendBullColor    = Brushes.Aquamarine;
                FibExtendBearColor    = Brushes.Gold;
                FibLineWidth          = 1;
                ShowFibLabels         = true;
                MaxFibSets            = 3;

                // Instrumentation (opt-in)
                EnableStateDump      = false;
                DumpIntervalRTH      = 5;    // every 5 minutes during RTH
                DumpIntervalETH      = 5;    // every bar during ETH (same as RTH)
                CorrInstrument1  = "";
                CorrInstrument2  = "";
                CorrInstrument3  = "";

                // Delta Divergence
                ShowDeltaDivergence = true;
                DeltaDivBullColor   = Brushes.Lime;
                DeltaDivBearColor   = Brushes.OrangeRed;
                DeltaDivFontSize    = 10;

                // Spike candle lines
                ShowSpikeCandleLines = true;
                BullSpikeColor       = Brushes.Lime;
                BearSpikeColor       = Brushes.Red;

                // Proximity menu
                ShowProximityMenu = true;
                ProximityPoints   = 20.0;

                // Gap label
                ShowGapLabel = true;

                // Dual delta panel
                ShowDeltaPanel   = true;

                // RTH times in YOUR local chart timezone (Mountain Time defaults)
                // Equities (ES/MES): 7:30 AM - 2:00 PM MT
                // Gold (GC/MGC):     6:30 AM - 11:30 AM MT
                // Change these to match your instrument
                RTHStartHour   = 7;
                RTHStartMinute = 30;
                RTHEndHour     = 14;
                RTHEndMinute   = 0;
            }
            else if (State == State.Configure)
            {
                Plots[0].Brush = EMAFastColor;
                Plots[1].Brush = EMAMidColor;
                Plots[2].Brush = EMASlowColor;
                // Note: white band between EMA lines is a NinjaTrader chart setting.
                // In chart properties, set "Plot area opacity" to 0 to remove it.

                // OFD requires a 1-tick series to be pre-loaded by the host indicator
                // Without this, NT throws a warning and OFD may not calculate correctly
                AddDataSeries(Data.BarsPeriodType.Tick, 1);
                // Cross-instrument correlation (Tier 3) -- blank = skip
                if (!string.IsNullOrEmpty(CorrInstrument1)) { try { AddDataSeries(CorrInstrument1, BarsPeriod); corrSeries1Idx = 2; corr1Name = CorrInstrument1; } catch { } }
                if (!string.IsNullOrEmpty(CorrInstrument2)) { try { AddDataSeries(CorrInstrument2, BarsPeriod); corrSeries2Idx = corrSeries1Idx >= 0 ? 3 : 2; corr2Name = CorrInstrument2; } catch { } }
                if (!string.IsNullOrEmpty(CorrInstrument3)) { try { AddDataSeries(CorrInstrument3, BarsPeriod); corrSeries3Idx = corrSeries2Idx >= 0 ? corrSeries2Idx+1 : corrSeries1Idx >= 0 ? 3 : 2; corr3Name = CorrInstrument3; } catch { } }
            }
            else if (State == State.DataLoaded)
            {
                volumeProfile      = new Dictionary<double, double>();
                sessionInitialized = false;
                currentSessionDate = DateTime.MinValue;

                emaFast = EMA(FastEMA);
                emaMid  = EMA(MidEMA);
                emaSlow = EMA(SlowEMA);

                anchoredVWAPs = new List<AnchoredVWAP>();
                trendlines    = new List<TrendlineInfo>();
                swingHighs    = new List<SwingPoint>();
                swingLows     = new List<SwingPoint>();
                gapFibs       = new List<GapFib>();
                renderLabels  = new List<RenderLabel>();
                cumulativeDelta = 0; prevCumDelta = 0; prevSwingPrice = 0; prevSwingDelta = 0;
                divLabelCounter = 0; hasPeak = false; hasTrough = false;
                ema5 = EMA(5); ema9 = EMA(9); atr5m = ATR(14);
                lastBosBar = -1; lastBosDir = "none"; etDeltaSession = 0;
                lastStateDumpBar = -1; lastDumpTime = DateTime.MinValue; sessionStartBar = 0; sessionStartTime = 0;
                lastRegime = ""; regimeAge = 0; lastEmaStack = ""; emaStackAge = 0;
                cumDelta24Approx = 0; cumDeltaRTHApprox = 0; isRTH = false;
                gapPoints = 0; gapPercent = 0;
                rthStartSpan = new TimeSpan(RTHStartHour, RTHStartMinute, 0);
                rthEndSpan   = new TimeSpan(RTHEndHour,   RTHEndMinute,   0);
                bullSpikeBar = new HashSet<int>(); bearSpikeBar = new HashSet<int>();
                tickCount30s = 0; tickCountPrev30s = 0; ticksThisBar = 0;
                tickWindowStart = DateTime.MinValue; tickCount5mAvg = 0;
                tickCountHistory = new Queue<int>();
                bidSizeTop5 = 0; askSizeTop5 = 0; bookImbalance = 0; l2Available = false;

                // Instantiate the real OrderFlowCumulativeDelta indicators
                try
                {
                    // Session period = cumulative delta that resets each full session (24hr)
                    ofd24  = OrderFlowCumulativeDelta(BarsArray[0], CumulativeDeltaType.BidAsk,
                                CumulativeDeltaPeriod.Session, 0);
                    // We track RTH ourselves by zeroing cumDeltaRTHApprox at session open
                    // and only accumulating delta during RTH hours from ofd24 delta-per-bar
                    ofdRTH = null; // RTH is derived -- see UpdateDualDelta()
                    Print("SessionLevels: OrderFlowCumulativeDelta loaded OK.");
                }
                catch (Exception ex)
                {
                    Print("SessionLevels: OFD load failed, using approximation. " + ex.Message);
                    ofd24  = null;
                    ofdRTH = null;
                }
            }

            else if (State == State.Terminated)
            {
                DisposeDXResources();
            }
        }
        #endregion

        // =========================================================================
        #region OnBarUpdate
        // =========================================================================
        protected override void OnBarUpdate()
        {
            try
            {
                // Ignore the 1-tick series added for OFD -- only process primary bars
                if (BarsInProgress != 0) return;

                if (CurrentBar < Math.Max(SlowEMA, 20)) return;

                // EMA plots
                if (ShowEMAs && CurrentBar >= SlowEMA)
                {
                    Values[0][0] = emaFast[0];
                    Values[1][0] = emaMid[0];
                    Values[2][0] = emaSlow[0];
                }

                // Use Bars.IsFirstBarOfSession for proper 24/7 futures session detection
                // This respects the actual CME session boundary (5 PM CT), not midnight
                bool isNewSession = false;
                try
                {
                    isNewSession = Bars.IsFirstBarOfSession
                        && Time[0] != lastSessionBoundary;
                }
                catch
                {
                    // Fallback: calendar date change (less accurate overnight)
                    isNewSession = Time[0].Date != currentSessionDate;
                }
                if (isNewSession)
                {
                    lastSessionBoundary = Time[0];
                    HandleNewSession();
                }

                UpdateDailyStats();

                if (CurrentBar >= VWAPLookbackPeriod)
                    CalculateAverages();

                if (ShowAnchoredVWAP && CurrentBar >= VWAPLookbackPeriod)
                {
                    DetectVWAPTriggers();
                    UpdateAnchoredVWAPs();
                    CleanupOldVWAPs();
                }

                if (ShowTrendlines && CurrentBar >= SwingBars * 2 + 1)
                {
                    DetectSwingPoints();
                    UpdateTrendlines();
                    CheckTrendlineBreaks();
                    ProcessScheduledRemovals();
                }

                // Delta panel updates EVERY tick (live)
                UpdateDualDelta();
                // Tape speed: tick counting requires OnEachTick (disabled on OnBarClose)

                // Everything else only needs to run on bar close
                if (IsFirstTickOfBar)
                {
                    // Tape speed: store per-bar tick count
                    tickCountHistory.Enqueue(ticksThisBar);
                    if (tickCountHistory.Count > 20) tickCountHistory.Dequeue();
                    tickCount5mAvg = tickCountHistory.Count > 0 ? (int)tickCountHistory.Average() : 0;
                    ticksThisBar = 0;

                    // Cumulative delta divergence
                    UpdateCumulativeDelta();
                    DetectDeltaDivergence();

                    // State dump
                    MaybeEmitStateDump();

                    // Spike candle coloring
                    MarkSpikeBars();

                    // Gap stats
                    if (previousDayClose > 0 && todayOpen > 0)
                    { gapPoints = todayOpen - previousDayClose; gapPercent = gapPoints / previousDayClose * 100.0; }

                    // Draw horizontal lines (extend automatically)
                    DrawSessionLines();

                    if (ShowTrendlines)    DrawTrendlines();
                    if (ShowAnchoredVWAP) DrawAnchoredVWAPs();
                    if (ShowFibonacci)    DrawFibLines();
                }

                // Labels rebuild every tick so proximity menu and delta stay current
                BuildRenderLabels();
            }
            catch (Exception ex)
            {
                Print("SessionLevels OnBarUpdate ERROR: " + ex.Message);
            }
        }
        #endregion

        // =========================================================================
        #region OnMarketDepth  (Tier 3: order book imbalance -- requires Level 2)
        // =========================================================================
        protected override void OnMarketDepth(MarketDepthEventArgs e)
        {
            try
            {
                l2Available = true;
                if (e.MarketDataType == MarketDataType.Ask && e.Position < 5)
                { if (e.Position == 0) askSizeTop5 = e.Volume; else askSizeTop5 += e.Volume * 0.2; }
                else if (e.MarketDataType == MarketDataType.Bid && e.Position < 5)
                { if (e.Position == 0) bidSizeTop5 = e.Volume; else bidSizeTop5 += e.Volume * 0.2; }
                double total = bidSizeTop5 + askSizeTop5;
                bookImbalance = total > 0 ? (bidSizeTop5 - askSizeTop5) / total : 0;
            }
            catch { }
        }
        #endregion

        // =========================================================================
        #region OnRender  -  instrument banner + proximity menu + delta panel + labels
        // =========================================================================
        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);
            if (RenderTarget == null) return;
            try
            {
                EnsureDXResources();

                // -- Layout constants ----------------------------------------------
                // ChartControl.CanvasRight gives us the pixel x where the price axis starts
                // so our panel never overlaps the axis.
                float axisLeft  = (float)chartControl.CanvasRight;   // right edge of chart canvas
                float ph        = (float)chartScale.Height;
                const float PANEL_W   = 240f;   // info panel width in pixels
                const float PAD       = 6f;     // inner padding
                const float LINE_GAP  = 3f;     // extra gap between rows
                const float SEC_GAP   = 7f;     // gap between sections
                float panelLeft = axisLeft - PANEL_W - 2f;  // panel sits just inside the axis

                // -- Local helper: one row in the panel ---------------------------
                Func<string, float, SharpDX.Color4, SharpDX.Color4, SharpDX.DirectWrite.TextFormat, float> row =
                    (txt, ry, bgCol, fgCol, fmt) =>
                {
                    using (var layout = new SharpDX.DirectWrite.TextLayout(
                        NinjaTrader.Core.Globals.DirectWriteFactory, txt, fmt,
                        PANEL_W - PAD * 2f, 60f))
                    {
                        layout.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;
                        float th = layout.Metrics.Height;
                        // Dark background spanning the full panel width
                        using (var bg = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, bgCol))
                            RenderTarget.FillRectangle(
                                new SharpDX.RectangleF(panelLeft, ry, PANEL_W, th + LINE_GAP), bg);
                        // Colored text
                        using (var fg = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, fgCol))
                            RenderTarget.DrawTextLayout(
                                new SharpDX.Vector2(panelLeft + PAD, ry + 1f), layout, fg);
                        return th + LINE_GAP;
                    }
                };

                // -- Separator line ------------------------------------------------
                Action<float> sep = (sy) =>
                {
                    using (var pen = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                        new SharpDX.Color4(0.4f, 0.4f, 0.4f, 0.6f)))
                        RenderTarget.DrawLine(
                            new SharpDX.Vector2(panelLeft, sy),
                            new SharpDX.Vector2(panelLeft + PANEL_W, sy), pen, 1f);
                };

                // Common background colors
                var BG_DARK   = new SharpDX.Color4(0.05f, 0.05f, 0.08f, 0.90f);
                var BG_HEADER = new SharpDX.Color4(0.10f, 0.10f, 0.18f, 0.95f);

                float y = 4f;

                // ================================================================
                // 1.  INSTRUMENT BANNER
                // ================================================================
                string rthTag = isRTH ? " [RTH]" : " [EXT]";
                string banner = string.Format("{0}  |  {1}{2}",
                    Instrument.FullName, BarsPeriod.ToString(), rthTag);
                y += row(banner, y, BG_HEADER,
                    new SharpDX.Color4(1f, 0.85f, 0f, 1f),
                    instrumentTextFormat) + 1f;

                // ================================================================
                // 2.  GAP / % OPEN
                // ================================================================
                if (ShowGapLabel && previousDayClose > 0)
                {
                    sep(y); y += 1f;
                    bool gapUp  = gapPoints >= 0;
                    string gTxt = string.Format("{0}  {1:+0.00;-0.00} pts   {2:+0.00;-0.00}%",
                        gapUp ? "GAP UP" : "GAP DN", gapPoints, gapPercent);
                    SharpDX.Color4 gCol = gapUp
                        ? new SharpDX.Color4(0.2f, 1f, 0.4f, 1f)
                        : new SharpDX.Color4(1f, 0.35f, 0.35f, 1f);
                    y += row(gTxt, y, BG_DARK, gCol, menuTextFormat) + 1f;
                }

                // ================================================================
                // 3.  ORDER FLOW DELTA
                // ================================================================
                if (ShowDeltaPanel)
                {
                    sep(y); y += 1f;
                    y += row("ORDER FLOW DELTA", y, BG_HEADER,
                        new SharpDX.Color4(0.75f, 0.75f, 1f, 1f), menuHeaderFormat) + 1f;

                    // 24hr = full session running delta (from real OFD - resets at session open)
                    // RTH  = delta accumulated ONLY during RTH hours (our accumulator)
                    // EXT  = 24hr - RTH = extended/overnight session portion
                    // Net  = RTH - EXT: positive = RTH buying dominated, negative = EXT dominated
                    double total24 = cumDelta24;
                    double rthDelta = cumDeltaRTH;
                    double extDelta = total24 - rthDelta;
                    double netDelta = rthDelta - extDelta;

                    Func<double, SharpDX.Color4> dCol = v =>
                        v >= 0 ? new SharpDX.Color4(0.25f, 1f, 0.4f, 1f)
                               : new SharpDX.Color4(1f, 0.3f, 0.3f, 1f);
                    Func<double, string> dFmt = v =>
                        string.Format("{0}{1:N0}", v >= 0 ? "+" : "", v);
                    Func<string, double, string> dRow = (lbl, v) =>
                        string.Format("  {0,-5}  {1,10}", lbl, dFmt(v));

                    y += row(dRow("24hr",  total24),   y, BG_DARK, dCol(total24),  menuTextFormat);
                    y += row(dRow("RTH",   rthDelta),  y, BG_DARK, dCol(rthDelta), menuTextFormat);
                    y += row(dRow("EXT",   extDelta),  y, BG_DARK, dCol(extDelta), menuTextFormat);

                    using (var sp2 = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                        new SharpDX.Color4(0.4f, 0.4f, 0.4f, 0.5f)))
                        RenderTarget.DrawLine(new SharpDX.Vector2(panelLeft + 6f, y),
                            new SharpDX.Vector2(panelLeft + PANEL_W - 6f, y), sp2, 1f);
                    y += 2f;

                    // Net label tells you which side dominated
                    string netLbl  = netDelta >= 0 ? "Net RTH>" : "Net EXT>";
                    y += row(string.Format("  {0,-8} {1,8}", netLbl, dFmt(netDelta)), y, BG_DARK,
                        netDelta >= 0
                            ? new SharpDX.Color4(0f, 1f, 0.7f, 1f)
                            : new SharpDX.Color4(1f, 0.5f, 0.15f, 1f),
                        menuTextFormat);
                    y += SEC_GAP;
                }

                // ================================================================
                // 4.  PROXIMITY MENU
                // ================================================================
                if (ShowProximityMenu && Close.Count > 0)
                {
                    double cur  = Close[0];
                    var near    = BuildProximityLevels(cur);
                    if (near.Count > 0)
                    {
                        sep(y); y += 1f;
                        string phdr = string.Format("NEAR PRICE  +/-{0:F0} pts", ProximityPoints);
                        y += row(phdr, y, BG_HEADER,
                            new SharpDX.Color4(1f, 0.9f, 0.4f, 1f), menuHeaderFormat) + 1f;

                        foreach (var lvl in near.OrderBy(l => l.Dist))
                        {
                            string arrow = lvl.Price > cur ? "^" : "v";
                            string lTxt  = string.Format("{0} {1,-12}  {2:F2}  {3:+0.00;-0.00}",
                                arrow, lvl.Label, lvl.Price, lvl.Price - cur);
                            y += row(lTxt, y, BG_DARK,
                                new SharpDX.Color4(
                                    lvl.Color.R / 255f,
                                    lvl.Color.G / 255f,
                                    lvl.Color.B / 255f, 1f),
                                menuTextFormat);
                        }
                    }
                }

                // ================================================================
                // 5.  SESSION LEVEL LABELS  (drawn on price action, left of panel)
                // ================================================================
                if (ShowLabels && renderLabels != null && renderLabels.Count > 0)
                {
                    float xLabel  = panelLeft - 6f;  // sits just to the left of the panel
                    var sorted    = renderLabels.OrderByDescending(l => l.Price).ToList();
                    float lastLY  = float.MinValue;
                    float spacing = LabelFontSize + 5f;

                    foreach (var lbl in sorted)
                    {
                        float ly = (float)chartScale.GetYByValue(lbl.Price) - (LabelFontSize / 2f);
                        if (lastLY != float.MinValue && ly < lastLY + spacing) ly = lastLY + spacing;
                        if (ly < 0 || ly > ph) { lastLY = ly; continue; }
                        lastLY = ly;

                        using (var layout = new SharpDX.DirectWrite.TextLayout(
                            NinjaTrader.Core.Globals.DirectWriteFactory,
                            lbl.Text, labelTextFormat, 300f, 30f))
                        {
                            layout.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;
                            float tw = layout.Metrics.Width;
                            float th = layout.Metrics.Height;
                            // Right-align text so it ends flush at xLabel
                            float lx = xLabel - tw - 4f;
                            using (var bg = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                                new SharpDX.Color4(0f, 0f, 0f, 0.70f)))
                                RenderTarget.FillRectangle(
                                    new SharpDX.RectangleF(lx - 2f, ly - 1f, tw + 6f, th + 2f), bg);
                            using (var fg = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                                new SharpDX.Color4(
                                    lbl.Color.R / 255f,
                                    lbl.Color.G / 255f,
                                    lbl.Color.B / 255f, 1f)))
                                RenderTarget.DrawTextLayout(
                                    new SharpDX.Vector2(lx, ly), layout, fg);
                        }
                    }
                }
            }
            catch (Exception ex) { Print("SessionLevels OnRender ERROR: " + ex.Message); }
        }

        private void EnsureDXResources()
        {
            if (dxResourcesCreated) return;
            Func<string, float, SharpDX.DirectWrite.FontWeight, SharpDX.DirectWrite.TextFormat> mkFmt = (face, sz, wt) =>
            {
                var f = new SharpDX.DirectWrite.TextFormat(NinjaTrader.Core.Globals.DirectWriteFactory,
                    face, wt, SharpDX.DirectWrite.FontStyle.Normal, SharpDX.DirectWrite.FontStretch.Normal, sz);
                f.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
                f.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Near;
                return f;
            };
            labelTextFormat      = mkFmt("Arial",    LabelFontSize,     SharpDX.DirectWrite.FontWeight.Bold);
            instrumentTextFormat = mkFmt("Arial",    LabelFontSize + 3, SharpDX.DirectWrite.FontWeight.Bold);
            menuTextFormat       = mkFmt("Consolas", LabelFontSize - 1, SharpDX.DirectWrite.FontWeight.Normal);
            menuHeaderFormat     = mkFmt("Arial",    LabelFontSize - 1, SharpDX.DirectWrite.FontWeight.Bold);
            dxResourcesCreated   = true;
        }

        private void DisposeDXResources()
        {
            foreach (var f in new[] { labelTextFormat, instrumentTextFormat, menuTextFormat, menuHeaderFormat })
                if (f != null) f.Dispose();
            labelTextFormat = instrumentTextFormat = menuTextFormat = menuHeaderFormat = null;
            dxResourcesCreated = false;
        }
        #endregion

        // =========================================================================
        #region BuildRenderLabels  -  collects all labels for OnRender
        // =========================================================================
        private void BuildRenderLabels()
        {
            renderLabels.Clear();
            if (!ShowLabels) return;

            Action<string, double, System.Windows.Media.Brush> add = (text, price, brush) =>
            {
                if (price <= 0) return;
                var sc = ((System.Windows.Media.SolidColorBrush)brush).Color;
                renderLabels.Add(new RenderLabel { Text = text, Price = price, Color = sc });
            };

            if (ShowPreviousDayLevels && previousDayOpen > 0)
            {
                add(string.Format("Prev O  {0:F2}", previousDayOpen),  previousDayOpen,  PrevDayOpenColor);
                add(string.Format("Prev C  {0:F2}", previousDayClose), previousDayClose, PrevDayCloseColor);
                add(string.Format("Prev H  {0:F2}", previousDayHigh),  previousDayHigh,  PrevDayHighColor);
                add(string.Format("Prev L  {0:F2}", previousDayLow),   previousDayLow,   PrevDayLowColor);
            }

            if (ShowWeeklyLevels)
            {
                if (weekHigh > 0) { add(string.Format("Wk H  {0:F2}", weekHigh), weekHigh, WeekHighColor); }
                if (weekLow  > 0) { add(string.Format("Wk L  {0:F2}", weekLow),  weekLow,  WeekLowColor); }
                if (previousWeekHigh > 0) add(string.Format("1W H  {0:F2}", previousWeekHigh), previousWeekHigh, PrevWeekHighColor);
                if (previousWeekLow  > 0) add(string.Format("1W L  {0:F2}", previousWeekLow),  previousWeekLow,  PrevWeekLowColor);
            }

            if (ShowMonthlyLevels)
            {
                if (monthHigh > 0) add(string.Format("Mo H  {0:F2}", monthHigh), monthHigh, MonthHighColor);
                if (monthLow  > 0) add(string.Format("Mo L  {0:F2}", monthLow),  monthLow,  MonthLowColor);
                if (previousMonthHigh > 0) add(string.Format("1M H  {0:F2}", previousMonthHigh), previousMonthHigh, PrevMonthHighColor);
                if (previousMonthLow  > 0) add(string.Format("1M L  {0:F2}", previousMonthLow),  previousMonthLow,  PrevMonthLowColor);
            }

            if (ShowTodayOpen && todayOpen > 0)
                add(string.Format("Today O  {0:F2}", todayOpen), todayOpen, TodayOpenColor);

            if (ShowCurrentHighLow)
            {
                if (todayHigh > 0) add(string.Format("Today H  {0:F2}", todayHigh), todayHigh, CurrentHighColor);
                if (todayLow  > 0) add(string.Format("Today L  {0:F2}", todayLow),  todayLow,  CurrentLowColor);
            }

            if (ShowPOC && todayPOC > 0)
                add(string.Format("POC  {0:F2}", todayPOC), todayPOC, POCColor);

            // Fibonacci labels intentionally omitted - lines only, no text
        }
        #endregion

        // =========================================================================
        #region DrawSessionLines  -  HorizontalLine extends automatically
        // =========================================================================
        private void DrawSessionLines()
        {
            if (!sessionInitialized) return;

            // Helper for drawing a horizontal line that fills the entire chart width
            // HorizontalLine in NinjaTrader draws across the ENTIRE chart by default
            if (ShowPreviousDayLevels && previousDayOpen > 0)
            {
                Draw.HorizontalLine(this, "PDO",  previousDayOpen,  PrevDayOpenColor,  DashStyleHelper.Dash, 3);
                Draw.HorizontalLine(this, "PDC",  previousDayClose, PrevDayCloseColor, DashStyleHelper.Dash, 3);
                Draw.HorizontalLine(this, "PDH",  previousDayHigh,  PrevDayHighColor,  DashStyleHelper.Solid, 4);
                Draw.HorizontalLine(this, "PDL",  previousDayLow,   PrevDayLowColor,   DashStyleHelper.Solid, 4);
            }

            if (ShowWeeklyLevels)
            {
                if (weekHigh > 0) Draw.HorizontalLine(this, "WH",  weekHigh,         WeekHighColor,     DashStyleHelper.Dot,  3);
                if (weekLow  > 0) Draw.HorizontalLine(this, "WL",  weekLow,          WeekLowColor,      DashStyleHelper.Dot,  3);
                if (previousWeekHigh > 0) Draw.HorizontalLine(this, "PWH", previousWeekHigh, PrevWeekHighColor, DashStyleHelper.Dot, 2);
                if (previousWeekLow  > 0) Draw.HorizontalLine(this, "PWL", previousWeekLow,  PrevWeekLowColor,  DashStyleHelper.Dot, 2);
            }

            if (ShowMonthlyLevels)
            {
                if (monthHigh > 0) Draw.HorizontalLine(this, "MH",  monthHigh,         MonthHighColor,     DashStyleHelper.Dash, 3);
                if (monthLow  > 0) Draw.HorizontalLine(this, "ML",  monthLow,          MonthLowColor,      DashStyleHelper.Dash, 3);
                if (previousMonthHigh > 0) Draw.HorizontalLine(this, "PMH", previousMonthHigh, PrevMonthHighColor, DashStyleHelper.Dash, 2);
                if (previousMonthLow  > 0) Draw.HorizontalLine(this, "PML", previousMonthLow,  PrevMonthLowColor,  DashStyleHelper.Dash, 2);
            }

            if (ShowTodayOpen && todayOpen > 0)
                Draw.HorizontalLine(this, "TO",  todayOpen, TodayOpenColor, DashStyleHelper.Solid, 3);

            if (ShowCurrentHighLow)
            {
                if (todayHigh > 0) Draw.HorizontalLine(this, "CH", todayHigh, CurrentHighColor, DashStyleHelper.Solid, 4);
                if (todayLow  > 0) Draw.HorizontalLine(this, "CL", todayLow,  CurrentLowColor,  DashStyleHelper.Solid, 4);
            }

            if (ShowPOC && todayPOC > 0)
                Draw.HorizontalLine(this, "POC", todayPOC, POCColor, DashStyleHelper.Solid, POCWidth);
        }
        #endregion

        // =========================================================================
        #region Fibonacci from Gaps
        // =========================================================================

        /// <summary>Called once per new session to detect gap and build Fib levels.</summary>
        private void DetectAndBuildGapFib()
        {
            if (!ShowFibonacci) return;
            if (previousDayClose <= 0 || todayOpen <= 0) return;

            double gap    = todayOpen - previousDayClose;
            double minGap = MinGapSize * TickSize;

            if (Math.Abs(gap) < minGap) return;   // gap too small

            bool isGapUp = gap > 0;

            // Swing hi/lo for the gap range
            double gapHigh = Math.Max(previousDayClose, todayOpen);
            double gapLow  = Math.Min(previousDayClose, todayOpen);
            double gapSize = gapHigh - gapLow;

            // Build retracement levels  (measured INTO the gap)
            var retraces = new List<FibLevel>();
            foreach (double ratio in FIB_RETRACE)
            {
                double price = isGapUp
                    ? gapHigh - ratio * gapSize    // retrace down from top of gap
                    : gapLow  + ratio * gapSize;   // retrace up   from bottom of gap
                retraces.Add(new FibLevel
                {
                    Tag   = string.Format("FR_{0}_{1}", fibCounter, ratio),
                    Price = price,
                    Label = string.Format("{0:P1}", ratio).Replace(" ", ""),
                    Color = isGapUp ? FibRetraceBullColor : FibRetraceBearColor
                });
            }

            // Build extension levels (projected BEYOND the gap)
            var extends = new List<FibLevel>();
            foreach (double ratio in FIB_EXTEND)
            {
                double price = isGapUp
                    ? gapLow  + ratio * gapSize    // extend upward beyond gap top
                    : gapHigh - ratio * gapSize;   // extend downward beyond gap bottom
                extends.Add(new FibLevel
                {
                    Tag   = string.Format("FE_{0}_{1}", fibCounter, ratio),
                    Price = price,
                    Label = string.Format("{0:P1}", ratio).Replace(" ", ""),
                    Color = isGapUp ? FibExtendBullColor : FibExtendBearColor
                });
            }

            // Trim old sets beyond MaxFibSets
            var activeSets = gapFibs.Where(g => g.IsActive).ToList();
            if (activeSets.Count >= MaxFibSets)
                activeSets.OrderBy(g => g.ID).First().IsActive = false;

            gapFibs.Add(new GapFib
            {
                ID              = fibCounter++,
                IsGapUp         = isGapUp,
                GapStart        = previousDayClose,
                GapEnd          = todayOpen,
                SwingHigh       = gapHigh,
                SwingLow        = gapLow,
                RetraceLevels   = retraces,
                ExtensionLevels = extends,
                IsActive        = true
            });

            if (State == State.Realtime) Print(string.Format("{0} GAP {1} detected: {2:F2} -> {3:F2}  ({4:F2} pts)",
                Time[0], isGapUp ? "UP" : "DOWN", previousDayClose, todayOpen, Math.Abs(gap)));
        }

        private void DrawFibLines()
        {
            foreach (var gf in gapFibs.Where(g => g.IsActive))
            {
                System.Windows.Media.Brush rColor = gf.IsGapUp ? FibRetraceBullColor : FibRetraceBearColor;
                System.Windows.Media.Brush eColor = gf.IsGapUp ? FibExtendBullColor  : FibExtendBearColor;

                foreach (var fl in gf.RetraceLevels)
                    Draw.HorizontalLine(this, fl.Tag, fl.Price, rColor, DashStyleHelper.DashDotDot, FibLineWidth);

                foreach (var fl in gf.ExtensionLevels)
                    Draw.HorizontalLine(this, fl.Tag, fl.Price, eColor, DashStyleHelper.DashDot, FibLineWidth);
            }
        }
        #endregion

        // =========================================================================
        #region Session Helpers
        // =========================================================================
        private void HandleNewSession()
        {
            currentSessionDate = Time[0].Date;
            currentSessionStart = Time[0];
            if (State == State.Realtime) Print(string.Format("=== NEW SESSION: {0} {1} Bar {2} ===",
                currentSessionDate.ToShortDateString(),
                Time[0].ToString("HH:mm"), CurrentBar));

            RemoveDrawObjects();
            anchoredVWAPs.Clear();
            trendlines.Clear();
            swingHighs.Clear();
            swingLows.Clear();

            CalculatePreviousDayLevels();
            CalculateWeeklyLevels();
            CalculateMonthlyLevels();

            todayOpen = Open[0];
            todayHigh = High[0];
            todayLow  = Low[0];
            todayPOC  = 0;
            volumeProfile.Clear();
            sessionInitialized = true;
            // Reset per-session accumulators
            cumDelta24Approx  = 0;
            cumDeltaRTHApprox = 0;
            etDeltaSession    = 0;
            sessionStartBar   = CurrentBar;
            sessionStartTime  = Time[0].TimeOfDay.TotalMinutes;
            // Re-read RTH time props each session in case user changed them
            rthStartSpan = new TimeSpan(RTHStartHour, RTHStartMinute, 0);
            rthEndSpan   = new TimeSpan(RTHEndHour,   RTHEndMinute,   0);
            bullSpikeBar.Clear();
            bearSpikeBar.Clear();

            // Detect gap and build Fibonacci levels for this session
            DetectAndBuildGapFib();
        }

        private void CalculatePreviousDayLevels()
        {
            if (CurrentBar < 10) return;

            // Use Bars.IsFirstBarOfSession to find the previous session boundary
            // This handles 24/7 futures correctly (session boundary at 5 PM CT, not midnight)
            bool found = false;
            bool inPrevSession = false;
            previousDayHigh  = 0;
            previousDayLow   = double.MaxValue;
            previousDayOpen  = 0;
            previousDayClose = 0;

            for (int i = 1; i < Math.Min(CurrentBar, 2000); i++)
            {
                bool isSessionStart = false;
                try { isSessionStart = Bars.IsFirstBarOfSessionByIndex(CurrentBar - i); }
                catch { isSessionStart = (i > 1 && Time[i].Date != Time[i-1].Date); }

                if (!inPrevSession && isSessionStart)
                {
                    // This is the start of the previous session -- we are now inside it
                    inPrevSession = true;
                    found = false;
                }
                else if (inPrevSession && isSessionStart)
                {
                    // Hit the session before the previous one -- stop
                    break;
                }

                if (inPrevSession)
                {
                    if (!found) { previousDayClose = Close[i]; found = true; }
                    previousDayHigh = Math.Max(previousDayHigh, High[i]);
                    previousDayLow  = Math.Min(previousDayLow,  Low[i]);
                    previousDayOpen = Open[i];
                }
            }

            // Fallback: if session boundary detection failed, use calendar date
            if (!found)
            {
                DateTime targetDate = DateTime.MinValue;
                for (int i = 1; i < Math.Min(CurrentBar, 500); i++)
                {
                    if (Time[i].Date < Time[0].Date) { targetDate = Time[i].Date; break; }
                }
                if (targetDate != DateTime.MinValue)
                {
                    for (int i = 1; i < Math.Min(CurrentBar, 500); i++)
                    {
                        if (Time[i].Date == targetDate)
                        {
                            if (!found) { previousDayClose = Close[i]; found = true; }
                            previousDayHigh = Math.Max(previousDayHigh, High[i]);
                            previousDayLow  = Math.Min(previousDayLow,  Low[i]);
                            previousDayOpen = Open[i];
                        }
                        else if (found && Time[i].Date < targetDate) break;
                    }
                }
            }

            if (previousDayLow == double.MaxValue) previousDayLow = 0;
        }

        private void UpdateDailyStats()
        {
            if (!sessionInitialized)
            {
                currentSessionDate = Time[0].Date;
                todayOpen = Open[0]; todayHigh = High[0]; todayLow = Low[0];
                sessionInitialized = true;
            }
            todayHigh = Math.Max(todayHigh, High[0]);
            todayLow  = Math.Min(todayLow,  Low[0]);
            UpdateVolumeProfile();
            CalculatePOC();
        }

        private void CalculateWeeklyLevels()
        {
            if (CurrentBar < 10) return;
            weekHigh = High[0]; weekLow = Low[0];
            previousWeekHigh = 0; previousWeekLow = double.MaxValue;
            DateTime cwStart = GetWeekStart(currentSessionDate);
            DateTime pwStart = cwStart.AddDays(-7), pwEnd = cwStart.AddDays(-1);
            for (int i = 0; i < Math.Min(CurrentBar, 500); i++)
            {
                DateTime d = Time[i].Date;
                if (d >= cwStart) { weekHigh = Math.Max(weekHigh, High[i]); weekLow = Math.Min(weekLow, Low[i]); }
                else if (d >= pwStart && d <= pwEnd) { previousWeekHigh = Math.Max(previousWeekHigh, High[i]); previousWeekLow = Math.Min(previousWeekLow, Low[i]); }
            }
            if (previousWeekLow == double.MaxValue) previousWeekLow = 0;
        }

        private void CalculateMonthlyLevels()
        {
            if (CurrentBar < 10) return;
            monthHigh = High[0]; monthLow = Low[0];
            previousMonthHigh = 0; previousMonthLow = double.MaxValue;
            DateTime cmStart  = new DateTime(currentSessionDate.Year, currentSessionDate.Month, 1);
            DateTime pmStart  = cmStart.AddMonths(-1), pmEnd = cmStart.AddDays(-1);
            for (int i = 0; i < Math.Min(CurrentBar, 1000); i++)
            {
                DateTime d = Time[i].Date;
                if (d >= cmStart) { monthHigh = Math.Max(monthHigh, High[i]); monthLow = Math.Min(monthLow, Low[i]); }
                else if (d >= pmStart && d <= pmEnd) { previousMonthHigh = Math.Max(previousMonthHigh, High[i]); previousMonthLow = Math.Min(previousMonthLow, Low[i]); }
            }
            if (previousMonthLow == double.MaxValue) previousMonthLow = 0;
        }

        private DateTime GetWeekStart(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }

        private void UpdateVolumeProfile()
        {
            int levels = (int)Math.Max(1, (High[0] - Low[0]) / TickSize);
            double volPerLevel = Volume[0] / levels;
            for (double price = Low[0]; price <= High[0]; price += TickSize)
            {
                double rp = Math.Round(price / TickSize) * TickSize;
                if (!volumeProfile.ContainsKey(rp)) volumeProfile[rp] = 0;
                volumeProfile[rp] += volPerLevel;
            }
        }

        private void CalculatePOC()
        {
            if (volumeProfile.Count > 0)
                todayPOC = volumeProfile.OrderByDescending(x => x.Value).First().Key;
        }
        #endregion

        // =========================================================================
        #region Anchored VWAP
        // =========================================================================
        private void CalculateAverages()
        {
            double vs = 0, ss = 0;
            for (int i = 0; i < VWAPLookbackPeriod; i++) { vs += Volume[i]; ss += High[i] - Low[i]; }
            avgVolume = vs / VWAPLookbackPeriod;
            avgCandleSize = ss / VWAPLookbackPeriod;
        }

        private void DetectVWAPTriggers()
        {
            if (avgVolume == 0 || avgCandleSize == 0) return;
            bool triggered = false; string reason = "";

            if (Volume[0] >= avgVolume * VWAPVolumeSpikeMultiplier)
            { triggered = true; reason = string.Format("Vol Spike ({0:F1}x)", Volume[0] / avgVolume); }

            double cs = High[0] - Low[0];
            if (cs >= avgCandleSize * VWAPCandleSizeMultiplier)
            { triggered = true; if (reason != "") reason += " + "; reason += string.Format("Big Candle ({0:F1}x)", cs / avgCandleSize); }

            if (CurrentBar > 0)
            {
                double gapSize = Math.Abs(Open[0] - Close[1]);
                if (gapSize >= avgCandleSize * 1.5)
                { triggered = true; if (reason != "") reason += " + "; reason += "Gap"; }
            }

            double body = Math.Abs(Close[0] - Open[0]), wick = cs - body;
            if (body > 0 && wick > 0 && body / wick > 3.0 && cs >= avgCandleSize * 1.5)
            { triggered = true; if (reason != "") reason += " + "; reason += "Strong Move"; }

            if (!triggered) return;

            var active = anchoredVWAPs.Where(v => v.IsActive).ToList();
            if (active.Count >= MaxActiveVWAPs)
                active.OrderBy(v => v.AnchorBar).First().IsActive = false;

            anchoredVWAPs.Add(new AnchoredVWAP
            {
                ID = vwapCounter++, AnchorBar = CurrentBar, AnchorTime = Time[0],
                AnchorPrice = Close[0], TriggerReason = reason,
                CumulativeTPV = (High[0] + Low[0] + Close[0]) / 3.0 * Volume[0],
                CumulativeVolume = Volume[0], IsActive = true
            });
            if (State == State.Realtime) Print(string.Format("{0} AVWAP: {1}", Time[0], reason));
        }

        private void UpdateAnchoredVWAPs()
        {
            foreach (var v in anchoredVWAPs.Where(x => x.IsActive && CurrentBar > x.AnchorBar))
            {
                double tp = (High[0] + Low[0] + Close[0]) / 3.0;
                v.CumulativeTPV    += tp * Volume[0];
                v.CumulativeVolume += Volume[0];
            }
        }

        private void CleanupOldVWAPs()
        {
            if (!RemoveOldVWAPs) return;
            foreach (var v in anchoredVWAPs.Where(x => x.IsActive && CurrentBar - x.AnchorBar > VWAPMaxAgeBars))
                v.IsActive = false;
        }

        private void DrawAnchoredVWAPs()
        {
            foreach (var vwap in anchoredVWAPs.Where(v => v.IsActive))
            {
                if (vwap.CumulativeVolume == 0) continue;
                int barsAgo = CurrentBar - vwap.AnchorBar;
                if (barsAgo >= 0 && barsAgo <= CurrentBar)
                {
                    // Arrow below the candle
                    Draw.ArrowUp(this, "VA_" + vwap.ID, true, barsAgo, Low[barsAgo] - 2 * TickSize, AnchoredVWAPColor);
                    // Reason text just below the arrow, right at the candle
                    if (ShowVWAPLabels && barsAgo <= 100)
                        Draw.Text(this, "VT_" + vwap.ID, vwap.TriggerReason,
                            barsAgo, Low[barsAgo] - 4 * TickSize, AnchoredVWAPColor);
                }

                for (int i = vwap.AnchorBar; i < CurrentBar; i++)
                {
                    int b1 = CurrentBar - i, b2 = CurrentBar - (i + 1);
                    if (b1 < 0 || b2 < 0) continue;
                    double v1 = GetVWAPAt(vwap, i), v2 = GetVWAPAt(vwap, i + 1);
                    if (v1 > 0 && v2 > 0)
                        Draw.Line(this, "VL_" + vwap.ID + "_" + i, false, b1, v1, b2, v2,
                            AnchoredVWAPColor, DashStyleHelper.Solid, AnchoredVWAPWidth);
                }
            }
        }

        private double GetVWAPAt(AnchoredVWAP vwap, int barIndex)
        {
            if (barIndex < vwap.AnchorBar || barIndex > CurrentBar) return 0;
            double cumTPV = 0, cumVol = 0;
            for (int i = vwap.AnchorBar; i <= barIndex && i <= CurrentBar; i++)
            {
                int ago = CurrentBar - i;
                if (ago < 0) continue;
                double tp = (High[ago] + Low[ago] + Close[ago]) / 3.0;
                cumTPV += tp * Volume[ago]; cumVol += Volume[ago];
            }
            return cumVol == 0 ? 0 : cumTPV / cumVol;
        }
        #endregion

        // =========================================================================
        #region Trendlines
        // =========================================================================
        private void DetectSwingPoints()
        {
            if (CurrentBar < SwingBars * 2 + 1) return;

            bool isSH = true, isSL = true;
            for (int i = 1; i <= SwingBars; i++)
            {
                if (High[SwingBars] <= High[SwingBars - i] || High[SwingBars] <= High[SwingBars + i]) { isSH = false; break; }
            }
            for (int i = 1; i <= SwingBars; i++)
            {
                if (Low[SwingBars] >= Low[SwingBars - i] || Low[SwingBars] >= Low[SwingBars + i]) { isSL = false; break; }
            }

            if (isSH)
            {
                swingHighs.Add(new SwingPoint { BarIndex = CurrentBar - SwingBars, Price = High[SwingBars], IsHigh = true });
                while (swingHighs.Count > 20) swingHighs.RemoveAt(0);
            }
            if (isSL)
            {
                swingLows.Add(new SwingPoint { BarIndex = CurrentBar - SwingBars, Price = Low[SwingBars], IsHigh = false });
                while (swingLows.Count > 20) swingLows.RemoveAt(0);
            }
        }

        private void UpdateTrendlines()
        {
            if (CurrentBar < SlowEMA) return;
            bool bull = emaFast[0] > emaMid[0] && emaMid[0] > emaSlow[0];
            bool bear = emaFast[0] < emaMid[0] && emaMid[0] < emaSlow[0];
            if (bull && swingLows.Count  >= MinSwingPoints) CreateOrUpdateTrendline(true);
            if (bear && swingHighs.Count >= MinSwingPoints) CreateOrUpdateTrendline(false);
        }

        private void CreateOrUpdateTrendline(bool isBullish)
        {
            var swings = (isBullish ? swingLows : swingHighs)
                .Where(s => s.BarIndex >= CurrentBar - TrendlineLookback)
                .OrderByDescending(s => s.BarIndex).Take(MinSwingPoints).ToList();
            if (swings.Count < MinSwingPoints) return;

            double sx = 0, sy = 0, sxy = 0, sx2 = 0;
            int n = swings.Count;
            foreach (var s in swings)
            {
                double x = CurrentBar - s.BarIndex;
                sx += x; sy += s.Price; sxy += x * s.Price; sx2 += x * x;
            }
            double denom = n * sx2 - sx * sx;
            if (Math.Abs(denom) < 0.0001) return;
            double slope     = (n * sxy - sx * sy) / denom;
            double intercept = (sy - slope * sx) / n;
            double angle     = Math.Atan(Math.Abs(slope) / TickSize) * (180.0 / Math.PI);
            if (angle < MinTrendlineAngle) return;

            var existing = trendlines.FirstOrDefault(t => t.IsBullish == isBullish && t.IsActive && !t.WasBroken);
            if (existing != null)
            {
                existing.SwingPoints = swings; existing.Slope = slope; existing.Intercept = intercept;
            }
            else
            {
                trendlines.Add(new TrendlineInfo
                {
                    ID = trendlineCounter++, IsBullish = isBullish, SwingPoints = swings,
                    CreatedBar = CurrentBar, Slope = slope, Intercept = intercept,
                    IsActive = true, WasBroken = false
                });
            }
        }

        private void CheckTrendlineBreaks()
        {
            foreach (var tl in trendlines.Where(t => t.IsActive && !t.WasBroken).ToList())
            {
                double tp = GetTrendlinePrice(tl, CurrentBar);
                bool broken = tl.IsBullish ? Close[0] < tp : Close[0] > tp;
                if (broken)
                {
                    tl.BrokenBar = CurrentBar; tl.WasBroken = true;
                    if (AutoDeleteOnBreak) tl.ScheduledRemoval = Time[0].AddMinutes(RemovalDelayMinutes);
                    if (ShowTrendlineAlerts)
                        Alert("TLBreak_" + tl.ID, Priority.Medium,
                              (tl.IsBullish ? "Bullish" : "Bearish") + " Trendline Broken!", "", 5, Brushes.Yellow, Brushes.Black);
                }
            }
            foreach (var tl in trendlines.Where(t => t.WasBroken && t.IsActive).ToList())
            {
                double tp = GetTrendlinePrice(tl, CurrentBar);
                bool back = tl.IsBullish ? Close[0] > tp : Close[0] < tp;
                if (back) { tl.WasBroken = false; tl.BrokenBar = null; tl.ScheduledRemoval = DateTime.MinValue; }
            }
        }

        private void ProcessScheduledRemovals()
        {
            if (!AutoDeleteOnBreak) return;
            foreach (var tl in trendlines.Where(t => t.ScheduledRemoval != DateTime.MinValue && Time[0] >= t.ScheduledRemoval))
                tl.IsActive = false;
        }

        private double GetTrendlinePrice(TrendlineInfo tl, int barIndex)
        {
            double x = CurrentBar - barIndex;
            return tl.Slope * x + tl.Intercept;
        }

        private void DrawTrendlines()
        {
            foreach (var tl in trendlines.Where(t => t.IsActive))
            {
                if (tl.SwingPoints == null || tl.SwingPoints.Count < 2) continue;
                var oldest = tl.SwingPoints.OrderBy(s => s.BarIndex).First();
                var newest = tl.SwingPoints.OrderByDescending(s => s.BarIndex).First();
                int startAgo = CurrentBar - oldest.BarIndex;
                int endAgo   = Math.Max(0, CurrentBar - newest.BarIndex - TrendlineExtension);
                if (startAgo > CurrentBar || startAgo < 0) continue;
                double sp = GetTrendlinePrice(tl, oldest.BarIndex);
                double ep = GetTrendlinePrice(tl, newest.BarIndex + TrendlineExtension);
                System.Windows.Media.Brush col = tl.IsBullish ? BullishTrendlineColor : BearishTrendlineColor;
                Draw.Line(this, "TL_" + tl.ID, false, startAgo, sp, endAgo, ep, col, DashStyleHelper.Dot, TrendlineWidth);
                foreach (var sw in tl.SwingPoints)
                {
                    int ago = CurrentBar - sw.BarIndex;
                    if (ago >= 0 && ago <= CurrentBar)
                        Draw.Dot(this, "TLS_" + tl.ID + "_" + sw.BarIndex, true, ago, sw.Price, col);
                }
            }
        }
        #endregion

        // =========================================================================
        #region Cumulative Delta Divergence
        // =========================================================================

        /// <summary>
        /// Approximates buy/sell volume using candle structure:
        /// bullish bars  -> body % of range goes to buy side
        /// bearish bars  -> body % of range goes to sell side
        /// </summary>
        private void UpdateCumulativeDelta()
        {
            double range = High[0] - Low[0];
            if (range < TickSize) return;

            double buyVol, sellVol;
            if (Close[0] >= Open[0])   // bull bar
            {
                double bullRatio = (Close[0] - Low[0]) / range;
                buyVol  = Volume[0] * bullRatio;
                sellVol = Volume[0] * (1.0 - bullRatio);
            }
            else                        // bear bar
            {
                double bearRatio = (High[0] - Close[0]) / range;
                sellVol = Volume[0] * bearRatio;
                buyVol  = Volume[0] * (1.0 - bearRatio);
            }
            cumulativeDelta += buyVol - sellVol;
        }

        /// <summary>
        /// Uses swing highs/lows (same ones used by trendlines) to detect:
        ///   Bullish divergence  - price makes lower low, cum delta makes higher low  (hidden sell pressure drying up)
        ///   Bearish divergence  - price makes higher high, cum delta makes lower high (hidden buy pressure waning)
        /// Places a text alert label directly on the chart at the divergence bar.
        /// </summary>
        private void DetectDeltaDivergence()
        {
            if (!ShowDeltaDivergence) return;
            if (CurrentBar < SwingBars * 2 + 5) return;

            // -- Track swings using the same swing lists populated by DetectSwingPoints --
            // Swing Highs -> bearish divergence check
            if (swingHighs.Count >= 2)
            {
                var latest = swingHighs[swingHighs.Count - 1];
                var prev   = swingHighs[swingHighs.Count - 2];
                int latestAgo = CurrentBar - latest.BarIndex;
                int prevAgo   = CurrentBar - prev.BarIndex;

                if (latestAgo >= 0 && latestAgo <= CurrentBar &&
                    prevAgo   >= 0 && prevAgo   <= CurrentBar)
                {
                    double latestDelta = GetDeltaAtBar(latest.BarIndex);
                    double prevDelta   = GetDeltaAtBar(prev.BarIndex);

                    // Bearish div: price HH but delta LH
                    if (latest.Price > prev.Price && latestDelta < prevDelta)
                    {
                        string tag = "BearDiv_" + divLabelCounter++;
                        Draw.Text(this, tag, "[BEAR DIV] D-weak", latestAgo, High[latestAgo] + 4 * TickSize, DeltaDivBearColor);
                    }
                }
            }

            // Swing Lows -> bullish divergence check
            if (swingLows.Count >= 2)
            {
                var latest = swingLows[swingLows.Count - 1];
                var prev   = swingLows[swingLows.Count - 2];
                int latestAgo = CurrentBar - latest.BarIndex;
                int prevAgo   = CurrentBar - prev.BarIndex;

                if (latestAgo >= 0 && latestAgo <= CurrentBar &&
                    prevAgo   >= 0 && prevAgo   <= CurrentBar)
                {
                    double latestDelta = GetDeltaAtBar(latest.BarIndex);
                    double prevDelta   = GetDeltaAtBar(prev.BarIndex);

                    // Bullish div: price LL but delta HL
                    if (latest.Price < prev.Price && latestDelta > prevDelta)
                    {
                        string tag = "BullDiv_" + divLabelCounter++;
                        Draw.Text(this, tag, "[BULL DIV] D-strong", latestAgo, Low[latestAgo] - 6 * TickSize, DeltaDivBullColor);
                    }
                }
            }
        }

        /// <summary>Approximates the cumulative delta value at a historical bar index.</summary>
        private double GetDeltaAtBar(int barIndex)
        {
            // Walk from that bar to current, subtract contributions added after it
            // Simplification: use the running cumulativeDelta and subtract recent bars
            // For efficiency, we track a rolling delta series via the swing list timestamps
            double delta = cumulativeDelta;
            for (int i = 0; i < CurrentBar - barIndex && i <= CurrentBar; i++)
            {
                double range = High[i] - Low[i];
                if (range < TickSize) continue;
                double buyVol, sellVol;
                if (Close[i] >= Open[i])
                {
                    double r = (Close[i] - Low[i]) / range;
                    buyVol = Volume[i] * r; sellVol = Volume[i] * (1.0 - r);
                }
                else
                {
                    double r = (High[i] - Close[i]) / range;
                    sellVol = Volume[i] * r; buyVol = Volume[i] * (1.0 - r);
                }
                delta -= (buyVol - sellVol);
            }
            return delta;
        }

        #endregion

        // =========================================================================
        #region Spike Candle Lines
        // =========================================================================
        private void MarkSpikeBars()
        {
            if (!ShowSpikeCandleLines || !ShowAnchoredVWAP) return;
            var last = anchoredVWAPs.LastOrDefault(v => v.AnchorBar == CurrentBar);
            if (last == null) return;
            bool bull = Close[0] >= Open[0];
            string tag = "SL_" + CurrentBar;
            System.Windows.Media.Brush col = bull ? BullSpikeColor : BearSpikeColor;
            Draw.Line(this, tag, false, 0, High[0], 0, Low[0], col, DashStyleHelper.Solid, 3);
        }
        #endregion

        // =========================================================================
        #region Dual Delta (RTH vs 24hr)
        // =========================================================================
        private void UpdateDualDelta()
        {
            // RTH flag -- uses configurable session times
            TimeSpan barTime = Time[0].TimeOfDay;
            isRTH = (barTime >= rthStartSpan && barTime < rthEndSpan);

            if (ofd24 != null)
            {
                // ofd24.DeltaClose[0] with CumulativeDeltaPeriod.Session =
                // the running cumulative session delta, resets at new session.
                // Read directly by the cumDelta24 property.
                // For RTH: add net delta change of each tick/bar only during RTH hours.
                try
                {
                    if (ofd24.DeltaClose.Count >= 2)
                    {
                        double barDelta = ofd24.DeltaClose[0] - ofd24.DeltaClose[1];
                        if (isRTH && !double.IsNaN(barDelta) && !double.IsInfinity(barDelta))
                            cumDeltaRTHApprox += barDelta;
                    }
                    else if (ofd24.DeltaClose.Count == 1 && isRTH)
                    {
                        cumDeltaRTHApprox = ofd24.DeltaClose[0];
                    }
                }
                catch { /* OFD warming up */ }
            }
            else
            {
                // Fallback: candle-structure approximation
                double range = High[0] - Low[0];
                if (range < TickSize) return;
                double mid  = (High[0] + Low[0]) / 2.0;
                double body = Math.Abs(Close[0] - Open[0]);
                double buyRatio = Close[0] > Open[0]
                    ? 0.5 + 0.4 * (body / range) + 0.1 * ((Close[0] - mid) / range)
                    : 0.5 - 0.4 * (body / range) + 0.1 * ((Close[0] - mid) / range);
                buyRatio = Math.Max(0.02, Math.Min(0.98, buyRatio));
                double delta = Volume[0] * (2.0 * buyRatio - 1.0);
                cumDelta24Approx += delta;
                if (isRTH) cumDeltaRTHApprox += delta;
            }
        }
        #endregion

        // =========================================================================
        #region Proximity Level Builder
        // =========================================================================
        private List<MenuLevel> BuildProximityLevels(double curPrice)
        {
            var list = new List<MenuLevel>();
            Action<string, double, System.Windows.Media.Brush> tryAdd = (lbl, price, brush) =>
            {
                if (price <= 0) return;
                double dist = Math.Abs(price - curPrice);
                if (dist > ProximityPoints) return;
                var sc = ((System.Windows.Media.SolidColorBrush)brush).Color;
                list.Add(new MenuLevel { Label = lbl, Price = price, Dist = dist, Color = sc });
            };
            if (ShowPreviousDayLevels && previousDayOpen > 0)
            {
                tryAdd("Prev Open",  previousDayOpen,  PrevDayOpenColor);
                tryAdd("Prev Close", previousDayClose, PrevDayCloseColor);
                tryAdd("Prev High",  previousDayHigh,  PrevDayHighColor);
                tryAdd("Prev Low",   previousDayLow,   PrevDayLowColor);
            }
            if (ShowWeeklyLevels)  { tryAdd("Wk High", weekHigh, WeekHighColor); tryAdd("Wk Low", weekLow, WeekLowColor); tryAdd("PWk High", previousWeekHigh, PrevWeekHighColor); tryAdd("PWk Low", previousWeekLow, PrevWeekLowColor); }
            if (ShowMonthlyLevels) { tryAdd("Mo High", monthHigh, MonthHighColor); tryAdd("Mo Low", monthLow, MonthLowColor); tryAdd("PMo High", previousMonthHigh, PrevMonthHighColor); tryAdd("PMo Low", previousMonthLow, PrevMonthLowColor); }
            if (ShowTodayOpen && todayOpen > 0) tryAdd("Today Open", todayOpen, TodayOpenColor);
            if (ShowCurrentHighLow) { tryAdd("Today High", todayHigh, CurrentHighColor); tryAdd("Today Low", todayLow, CurrentLowColor); }
            if (ShowPOC && todayPOC > 0) tryAdd("POC", todayPOC, POCColor);
            if (ShowFibonacci)
            {
                foreach (var gf in gapFibs.Where(g => g.IsActive))
                {
                    string pfx = gf.IsGapUp ? "\u2191Fib" : "\u2193Fib";
                    System.Windows.Media.Brush rc = gf.IsGapUp ? FibRetraceBullColor : FibRetraceBearColor;
                    System.Windows.Media.Brush ec = gf.IsGapUp ? FibExtendBullColor  : FibExtendBearColor;
                    foreach (var fl in gf.RetraceLevels)   tryAdd(pfx + "R " + fl.Label, fl.Price, rc);
                    foreach (var fl in gf.ExtensionLevels) tryAdd(pfx + "E " + fl.Label, fl.Price, ec);
                }
            }
            return list;
        }
        #endregion


        // =========================================================================
        #region Indicator State Dump  (Phase 1 Instrumentation)
        // =========================================================================

        // -- State tracking fields ----------------------------------------------
        private string  lastRegime        = "";
        private int     regimeAge         = 0;          // bars in current regime
        private int     emaStackAge       = 0;
        private string  lastEmaStack      = "";
        private int     lastStateDumpBar  = -1;
        private DateTime lastDumpTime       = DateTime.MinValue;
        private int     sessionStartBar   = 0;
        private double  sessionStartTime  = 0;          // stored as TOD minutes
        private int     emaFast9Bar       = 0;          // bar index when 9-EMA was added
        private EMA     ema5;                           // added in DataLoaded
        private EMA     ema9;
        private ATR     atr30s;                         // 30-sec ATR - requires a 30-sec series
        private ATR     atr5m;                          // 5-min ATR on primary series
        private double  etDeltaSession    = 0;          // running ETH delta (resets at RTH open)
        private double  prevRegimeCumDelta = 0;
        private int     lastBosBar        = -1;
        private string  lastBosDir        = "none";

        // -- Cadence constants -------------------------------------------------
        // Dump every N bars (approximate -- not time-based to keep it simple)
        private const int RTH_DUMP_BARS = 1;   // every bar in RTH (bar=5min on your chart)
        private const int ETH_DUMP_BARS = 3;   // every 3 bars in ETH (~15 min on 5-min chart)

        /// <summary>Called every bar close. Decides whether to emit a state dump.</summary>
        /// <summary>
        /// Called once at Realtime transition.
        /// Emits every bar of the CURRENT 24hr session from its open until now --
        /// e.g. if ETH opened Sunday 5 PM and you start at Monday 7 AM,
        /// you get all bars from Sunday 5 PM through the last historical bar.
        /// Then live bars follow immediately after with no gap.
        /// </summary>
        private void MaybeEmitStateDump()
        {
            if (!EnableStateDump) return;
            if (State != State.Realtime) return;  // live data only

            // Time-based cadence -- works correctly on tick, second, and minute charts
            // RTH: every DumpIntervalRTH minutes. ETH: every DumpIntervalETH minutes.
            int intervalMins = isRTH ? DumpIntervalRTH : DumpIntervalETH;
            bool dueToDump = lastStateDumpBar < 0   // never dumped yet
                || (Time[0] - lastDumpTime).TotalMinutes >= intervalMins;

            // Always log on regime change regardless of cadence
            string regime = ComputeRegime();
            bool regimeChange = regime != lastRegime;
            if (regimeChange) { lastRegime = regime; regimeAge = 0; }
            else regimeAge++;

            // Update EMA stack age
            string stack = GetEmaStack();
            if (stack != lastEmaStack) { emaStackAge = 0; lastEmaStack = stack; }
            else emaStackAge++;

            if (!dueToDump && !regimeChange) return;

            lastStateDumpBar = CurrentBar;
            lastDumpTime     = Time[0];

            try { EmitStateDump(regime, regimeChange); }
            catch (Exception ex) { Print("[INDICATOR STATE] DUMP_ERROR: " + ex.Message); }
        }

        private void EmitStateDump(string regime, bool regimeChange)
        {
            // -- 1. Identity ----------------------------------------------------
            string symbol      = Instrument.FullName;
            string ts          = Time[0].ToString("yyyy-MM-dd HH:mm:ss");
            string sessionType = GetSessionType();
            int    barNum      = CurrentBar;
            double minsInSess  = (Time[0].TimeOfDay.TotalMinutes - sessionStartTime);

            // -- 2. Price & Bar Data --------------------------------------------
            double curPrice    = Close[0];
            double pctFromOpen = todayOpen > 0 ? (curPrice - todayOpen) / todayOpen * 100.0 : 0;

            // -- 3. Volume Profile ---------------------------------------------
            // Session volume: scan back to session start (respects 24/7 boundary)
            double sessVol = 0;
            for (int i = 0; i < Math.Min(CurrentBar, 2000); i++)
            {
                bool isSessStart = false;
                try { isSessStart = i > 0 && Bars.IsFirstBarOfSessionByIndex(CurrentBar - i); }
                catch { isSessStart = i > 0 && Time[i].Date != Time[i > 0 ? i-1 : 0].Date; }
                if (isSessStart) break;
                sessVol += Volume[i];
            }

            double volAbovePOC = 0, volBelowPOC = 0;
            if (todayPOC > 0 && volumeProfile.Count > 0)
                foreach (var kv in volumeProfile)
                { if (kv.Key >= todayPOC) volAbovePOC += kv.Value; else volBelowPOC += kv.Value; }
            double totalVolSplit  = volAbovePOC + volBelowPOC;
            double pctAbovePOC   = totalVolSplit > 0 ? volAbovePOC / totalVolSplit * 100.0 : 50.0;

            double rangeMid = (todayHigh + todayLow) / 2.0;
            double volUpper = 0, volLower = 0;
            foreach (var kv in volumeProfile)
            { if (kv.Key >= rangeMid) volUpper += kv.Value; else volLower += kv.Value; }
            double totalRangeVol = volUpper + volLower;
            double pctUpperRange = totalRangeVol > 0 ? volUpper / totalRangeVol * 100.0 : 50.0;

            string volTrend = GetVolumeTrend(5);
            double volAvg20 = 0;
            for (int i = 0; i < Math.Min(20, CurrentBar); i++) volAvg20 += Volume[i];
            volAvg20 /= Math.Max(1, Math.Min(20, CurrentBar));
            double volRatio = volAvg20 > 0 ? Volume[0] / volAvg20 : 1.0;

            // -- TIER 2: Trend strength score (0-10) --------------------------
            // EMA slope (2) + delta consistency (3) + bar range trend (2) + swing structure (3)
            int trendStrength = ComputeTrendStrength();

            // -- TIER 2: Range expansion vs contraction ------------------------
            // 5-bar avg range / 20-bar avg range  (0=tight, 1=wide)
            double range5avg = 0, range20avg = 0;
            for (int i = 0; i < Math.Min(5,  CurrentBar); i++) range5avg  += High[i] - Low[i];
            for (int i = 0; i < Math.Min(20, CurrentBar); i++) range20avg += High[i] - Low[i];
            range5avg  /= Math.Max(1, Math.Min(5,  CurrentBar));
            range20avg /= Math.Max(1, Math.Min(20, CurrentBar));
            double rangePos = range20avg > 0 ? Math.Min(2.0, range5avg / range20avg) : 1.0;

            // -- 4. Delta ------------------------------------------------------
            double cd24         = cumDelta24;
            double deltaAsPct   = sessVol > 0 ? cd24 / sessVol * 100.0 : 0;
            string deltaBarTrend = GetDeltaBarTrend(5);
            bool   divActive    = IsDivergenceActive();

            // TIER 4: delta slope vs price slope
            double priceSlope = 0, deltaSlope = 0;
            int    slopeBars  = Math.Min(5, CurrentBar);
            if (slopeBars >= 2)
            {
                priceSlope = (Close[0] - Close[slopeBars - 1]) / slopeBars;
                if (ofd24 != null && ofd24.DeltaClose.Count >= slopeBars)
                    try { deltaSlope = (ofd24.DeltaClose[0] - ofd24.DeltaClose[slopeBars - 1]) / slopeBars; }
                    catch { }
            }

            // TIER 4: Bar conviction score (0-10)
            // close near extreme (3) + volume spike (3) + delta matches bar color (4)
            int barConv = ComputeBarConviction(volAvg20);

            // -- 5. Structure --------------------------------------------------
            string swingStr  = GetSwingStructure();
            string bos       = GetBOS();
            string emaStack  = GetEmaStack();

            double e5  = ema5   != null && CurrentBar >= 5  ? ema5[0]   : 0;
            double e9  = ema9   != null && CurrentBar >= 9  ? ema9[0]   : 0;
            double e21 = emaMid != null && CurrentBar >= 21 ? emaMid[0] : 0;
            double e50 = emaSlow!= null && CurrentBar >= 50 ? emaSlow[0]: 0;

            string aboveBelow = string.Format("5:{0} 9:{1} 21:{2} 50:{3}",
                curPrice > e5  ? "A" : "B", curPrice > e9  ? "A" : "B",
                curPrice > e21 ? "A" : "B", curPrice > e50 ? "A" : "B");

            // -- 6. Levels -----------------------------------------------------
            double atr5mVal = atr5m != null && CurrentBar >= 14 ? atr5m[0] : (todayHigh - todayLow);
            if (atr5mVal <= 0) atr5mVal = 1.0;

            Func<double, string> LD = (lvl) =>
            {
                if (lvl <= 0) return "N/A";
                double d = curPrice - lvl;
                return string.Format("{0:+0.00;-0.00}pts/{1:F2}ATR", d, Math.Abs(d) / atr5mVal);
            };

            var allLevels    = new System.Collections.Generic.List<(string name, double price)>
            {
                ("PDH", previousDayHigh), ("PDL", previousDayLow),
                ("PDO", previousDayOpen), ("PDC", previousDayClose),
                ("WkH", weekHigh),        ("WkL", weekLow),
                ("MoH", monthHigh),       ("MoL", monthLow),
                ("POC", todayPOC),        ("TO",  todayOpen),
                ("TH",  todayHigh),       ("TL",  todayLow),
            };
            var validLevels      = allLevels.Where(l => l.price > 0).ToList();
            var nearestLvl       = validLevels.OrderBy(l => Math.Abs(l.price - curPrice)).FirstOrDefault();
            int levelsWithin1ATR = validLevels.Count(l => Math.Abs(l.price - curPrice) <= atr5mVal);

            // -- 7. AVWAPs -- TIER 1 enhanced ---------------------------------
            int    avwapCount   = anchoredVWAPs != null ? anchoredVWAPs.Count(v => v.IsActive) : 0;
            string avwapDetail  = "";
            double nearAvwapVal = 0, nearAvwapDist = double.MaxValue;
            string nearAvwapSlope = "flat";
            double sessionVwapVal = 0;  // session-open AVWAP if any

            if (anchoredVWAPs != null)
            {
                foreach (var v in anchoredVWAPs.Where(x => x.IsActive))
                {
                    double vval  = GetVWAPAt(v, CurrentBar);
                    if (vval <= 0) continue;
                    // slope: compare current val to 3-bar-ago val
                    double vval3 = GetVWAPAt(v, Math.Max(v.AnchorBar, CurrentBar - 3));
                    string slope = vval > vval3 + TickSize ? "rising"
                                 : vval < vval3 - TickSize ? "falling" : "flat";
                    string rel   = curPrice > vval ? "above" : "below";
                    double dist  = curPrice - vval;

                    avwapDetail += string.Format(" AVWAP{0}[{1} val={2:F2} {3} {4} {5:+0.00;-0.00}pts]",
                        v.ID, v.TriggerReason.Split(' ')[0], vval, slope, rel, dist);

                    if (Math.Abs(dist) < nearAvwapDist)
                    {
                        nearAvwapDist  = Math.Abs(dist);
                        nearAvwapVal   = vval;
                        nearAvwapSlope = slope;
                    }

                    if (v.TriggerReason.Contains("Gap") || v.AnchorBar == sessionStartBar)
                        sessionVwapVal = vval;
                }
            }
            string nearAvwapStr = nearAvwapDist < double.MaxValue
                ? string.Format("{0:F2}({1},{2:+0.00;-0.00}pts)", nearAvwapVal, nearAvwapSlope, curPrice - nearAvwapVal)
                : "none";

            // -- 8. Volatility + TIER 1 ATR ratio -----------------------------
            double atr5val  = atr5mVal;
            double atrAvg20 = 0;
            if (atr5m != null)
                for (int i = 0; i < Math.Min(20, CurrentBar); i++) atrAvg20 += atr5m[i];
            atrAvg20 /= Math.Max(1, Math.Min(20, CurrentBar));
            double atrRatio  = atrAvg20 > 0 ? atr5val / atrAvg20 : 1.0;
            string volRegime = atrRatio > 2.0 ? "EXTREME" : atrRatio > 1.5 ? "EXPANDING"
                             : atrRatio < 0.7 ? "COMPRESSED" : "NORMAL";

            // Bid-ask spread proxy (TIER 1) -- use TickSize as baseline
            // NinjaTrader doesn't expose raw spread without L2, but we can approximate
            // from typical gap between bars vs TickSize
            double spreadProxy   = TickSize;  // minimum 1 tick always
            double typicalSpread = TickSize;
            double spreadRatio   = 1.0;

            // -- 10. Gap Context -----------------------------------------------
            bool   gapDetected  = Math.Abs(gapPoints) >= MinGapSize * TickSize;
            bool   gapUp2       = gapPoints > 0;
            double gapFillPrice = previousDayClose;
            double gapFillDist  = gapDetected ? curPrice - gapFillPrice : 0;

            // TIER 2: ETH high/low distance ------------------------------------
            // ETH H/L = todayHigh/todayLow when not yet in RTH (or track separately)
            double ethHigh = todayHigh, ethLow = todayLow;  // best proxy on primary series
            double ethHighDist = curPrice - ethHigh;
            double ethLowDist  = curPrice - ethLow;

            // TIER 2: Open type -- assessed from first bar + delta direction ----
            string openType = GetOpenType();

            // -- 14. Time of Day -----------------------------------------------
            string rthPhase  = GetRTHPhase();
            string ethPhase  = GetETHPhase();
            TimeSpan rthOpen = rthStartSpan;
            TimeSpan nowTOD  = Time[0].TimeOfDay;
            double minsToRTH = isRTH ? 0
                : (nowTOD < rthOpen
                    ? (rthOpen - nowTOD).TotalMinutes
                    : (rthOpen.Add(TimeSpan.FromDays(1)) - nowTOD).TotalMinutes);
            string dayOfWeek = Time[0].DayOfWeek.ToString();

            // -- Tier 3: Cross-instrument correlation -------------------------
            string corrStr = "";
            for (int ci = 0; ci < 3; ci++)
            {
                int    idx = ci==0 ? corrSeries1Idx : ci==1 ? corrSeries2Idx : corrSeries3Idx;
                string nm  = ci==0 ? corr1Name : ci==1 ? corr2Name : corr3Name;
                if (idx < 0 || string.IsNullOrEmpty(nm)) continue;
                try {
                    if (BarsArray.Length > idx && BarsArray[idx].Count > 1) {
                        double cc = BarsArray[idx].GetClose(BarsArray[idx].Count-1);
                        double cc1 = BarsArray[idx].GetClose(BarsArray[idx].Count-2);
                        double cp = cc1>0 ? (cc-cc1)/cc1*100.0 : 0;
                        corrStr += string.Format(" {0}={1:F2}({2:+0.00;-0.00}%,{3})", nm, cc, cp, ((cp>0)==(pctFromOpen>0)) ? "AGREE" : "DIVERGE");
                    }
                } catch { }
            }
            if (string.IsNullOrEmpty(corrStr)) corrStr = "none";

            // -- Tier 3: Tape speed --------------------------------------------
            double tapeRatio = tickCount5mAvg > 0 ? (double)tickCount30s / Math.Max(1.0, tickCount5mAvg/10.0) : 1.0;
            string tapeSpeed = tapeRatio >= 3.0 ? "EXTREME" : tapeRatio >= 1.5 ? "fast" : tapeRatio <= 0.4 ? "slow" : "normal";

            // -- Tier 3: Order book --------------------------------------------
            string bookStr = l2Available
                ? string.Format("{0:+0.00;-0.00}(bid={1:F0} ask={2:F0})", bookImbalance, bidSizeTop5, askSizeTop5)
                : "no_L2";

            // -- BUILD COMPACT ONE-LINER ---------------------------------------
            string line = string.Format(
                "[INDICATOR STATE] sym={0} ts={1} sess={2} bar={3} minsIn={4:F0} dow={5} " +
                // 2. Price
                "px={6:F2} O={7:F2} H={8:F2} L={9:F2} C={10:F2} V={11:F0} " +
                "sessO={12:F2} sessH={13:F2} sessL={14:F2} pctFromO={15:+0.00;-0.00}% " +
                // 3. Volume
                "sessVol={16:F0} volRatio={17:F2} volTrend={18} " +
                "pctAbovePOC={19:F0}% pctUpperRange={20:F0}% " +
                "trendStr={21}/10 rangePos={22:F2} " +
                // 4. Delta
                "cd24={23:F0} cdRTH={24:F0} deltaAsPct={25:+0.00;-0.00}% " +
                "deltaTrend={26} etDelta={27:F0} divActive={28} " +
                "priceSlope={29:+0.00;-0.00} deltaSlope={30:+0.00;-0.00} barConv={31}/10 " +
                // 5. Structure
                "swing={32} bos={33} emaStack={34} stackAge={35} " +
                "e5={36:F2} e9={37:F2} e21={38:F2} e50={39:F2} abv={40} " +
                // 6. Levels
                "POC={41:F2}({42}) PDH={43:F2}({44}) PDL={45:F2}({46}) " +
                "PDC={47:F2}({48}) PDO={49:F2}({50}) " +
                "WkH={51:F2}({52}) WkL={53:F2}({54}) MoH={55:F2}({56}) MoL={57:F2}({58}) " +
                "nearest={59}@{60:F2}({61}) lvlsIn1ATR={62} " +
                // 7. AVWAPs
                "avwaps={63} nearAvwap={64} sessVwap={65:F2}{66} " +
                // 8. Volatility
                "atr5m={67:F2} atrRatio={68:F2} volRegime={69} " +
                "spread={70:F4} spreadRatio={71:F2}x " +
                // 10. Gap
                "gap={72} gapSz={73:F2}pts/{74:+0.00;-0.00}% gapDir={75} gapFillDist={76:+0.00;-0.00} " +
                // TIER 2: ETH H/L, open type
                "ethHighDist={77:+0.00;-0.00} ethLowDist={78:+0.00;-0.00} openType={79} " +
                // 12. Regime
                "regime={80}(age={81}){82} " +
                // 14. Time
                "rthPhase={83} ethPhase={84} minsToRTH={85:F0} " +
                "tape={86}(ticks30s={87} avg={88} ratio={89:F1}) book={90} corr={91}",

                // Values
                symbol, ts, sessionType, barNum, minsInSess, dayOfWeek,
                curPrice, Open[0], High[0], Low[0], Close[0], Volume[0],
                todayOpen, todayHigh, todayLow, pctFromOpen,
                sessVol, volRatio, volTrend,
                pctAbovePOC, pctUpperRange,
                trendStrength, rangePos,
                cd24, cumDeltaRTH, deltaAsPct,
                deltaBarTrend, etDeltaSession, divActive,
                priceSlope, deltaSlope, barConv,
                swingStr, bos, emaStack, emaStackAge,
                e5, e9, e21, e50, aboveBelow,
                todayPOC,         LD(todayPOC),
                previousDayHigh,  LD(previousDayHigh),
                previousDayLow,   LD(previousDayLow),
                previousDayClose, LD(previousDayClose),
                previousDayOpen,  LD(previousDayOpen),
                weekHigh,         LD(weekHigh),
                weekLow,          LD(weekLow),
                monthHigh,        LD(monthHigh),
                monthLow,         LD(monthLow),
                nearestLvl.name,  nearestLvl.price, LD(nearestLvl.price), levelsWithin1ATR,
                avwapCount, nearAvwapStr, sessionVwapVal, avwapDetail,
                atr5val, atrRatio, volRegime,
                spreadProxy, spreadRatio,
                gapDetected, Math.Abs(gapPoints), gapPercent, gapUp2 ? "UP" : "DN", gapFillDist,
                ethHighDist, ethLowDist, openType,
                regime, regimeAge, regimeChange ? " REGIME_CHANGE" : "",
                rthPhase, ethPhase, minsToRTH,
                tapeSpeed, tickCount30s, tickCount5mAvg, tapeRatio, bookStr, corrStr
            );

            Print(line);
        }

        // -- TIER 4: Trend strength composite score -----------------------------
        private int ComputeTrendStrength()
        {
            int score = 0;
            if (CurrentBar < SlowEMA) return score;

            // EMA slope: fast EMA direction over 3 bars (2 pts)
            if (emaFast != null && emaFast.Count > 3)
            {
                double slope = emaFast[0] - emaFast[3];
                if (Math.Abs(slope) > TickSize * 2) score += 2;
                else if (Math.Abs(slope) > TickSize) score += 1;
            }

            // EMA stack clean (2 pts)
            string st = GetEmaStack();
            if (st == "BULLISH" || st == "BEARISH") score += 2;

            // Delta consistency over 5 bars (3 pts)
            if (ofd24 != null && ofd24.DeltaClose.Count >= 5)
            {
                try
                {
                    int posBars = 0, negBars = 0;
                    for (int i = 0; i < 5; i++)
                    {
                        double bd = i < 4 ? ofd24.DeltaClose[i] - ofd24.DeltaClose[i + 1] : 0;
                        if (bd > 0) posBars++; else if (bd < 0) negBars++;
                    }
                    int consistent = Math.Max(posBars, negBars);
                    if (consistent >= 4) score += 3;
                    else if (consistent >= 3) score += 2;
                    else if (consistent >= 2) score += 1;
                }
                catch { }
            }

            // Swing structure clean (3 pts)
            string ss = GetSwingStructure();
            if (ss == "HH-HL_UPTREND" || ss == "LL-LH_DOWNTREND") score += 3;
            else if (ss == "EXPANDING") score += 1;

            return Math.Min(score, 10);
        }

        // -- TIER 4: Bar conviction score ---------------------------------------
        private int ComputeBarConviction(double volAvg)
        {
            int score = 0;
            double range = High[0] - Low[0];
            if (range <= 0) return 0;

            // Close near high or low of bar (3 pts)
            double closePos = (Close[0] - Low[0]) / range;  // 0=bottom, 1=top
            bool isBull = Close[0] >= Open[0];
            if (isBull  && closePos >= 0.7) score += 3;
            else if (!isBull && closePos <= 0.3) score += 3;
            else if ((isBull && closePos >= 0.5) || (!isBull && closePos <= 0.5)) score += 1;

            // Volume spike (3 pts)
            if (volAvg > 0)
            {
                double vr = Volume[0] / volAvg;
                if (vr >= 2.0) score += 3;
                else if (vr >= 1.5) score += 2;
                else if (vr >= 1.1) score += 1;
            }

            // Delta direction matches bar color (4 pts)
            if (ofd24 != null && ofd24.DeltaClose.Count >= 2)
            {
                try
                {
                    double barDelta = ofd24.DeltaClose[0] - ofd24.DeltaClose[1];
                    bool deltaPositive = barDelta > 0;
                    if (isBull == deltaPositive) score += 4;
                    else score += 0;  // divergence = no conviction
                }
                catch { }
            }

            return Math.Min(score, 10);
        }

        // -- TIER 2: Open type classification -----------------------------------
        private string GetOpenType()
        {
            if (!sessionInitialized || todayOpen <= 0) return "UNKNOWN";
            double minsIn = (Time[0].TimeOfDay - rthStartSpan).TotalMinutes;
            if (!isRTH || minsIn < 0) return "PRE_RTH";

            bool  gapExists  = Math.Abs(gapPoints) >= MinGapSize * TickSize;
            bool  gapUp2     = gapPoints > 0;
            bool  aboveOpen  = Close[0] > todayOpen;

            // After first few bars we can classify
            if (minsIn < 5) return "ASSESSING";

            double cd24 = cumDelta24;

            if (gapExists)
            {
                // Gap and go: price moving away from fill
                bool movingAwayFromFill = gapUp2 ? Close[0] > todayOpen : Close[0] < todayOpen;
                if (movingAwayFromFill && cd24 != 0 &&
                    (gapUp2 ? cd24 > 0 : cd24 < 0)) return "GAP_AND_GO";
                return "GAP_FADE";
            }

            string ss = GetSwingStructure();
            if (ss.Contains("UPTREND") || ss.Contains("DOWNTREND"))
            {
                if (Math.Abs(gapPoints) < TickSize * 2) return "TREND_DAY";
            }

            double atr5val = atr5m != null && CurrentBar >= 14 ? atr5m[0] : (High[0] - Low[0]);
            double range   = todayHigh - todayLow;
            if (range < atr5val * 0.5) return "CHOP";

            return "NORMAL_OPEN";
        }

        // -- Helper: compute regime --------------------------------------------- ---------------------------------------------
        private string ComputeRegime()
        {
            if (!sessionInitialized || CurrentBar < Math.Max(SlowEMA, 20)) return "INITIALIZING";

            string stack   = GetEmaStack();
            double atr5val = atr5m != null && CurrentBar >= 14 ? atr5m[0] : (High[0] - Low[0]);
            double atrAvg  = 0;
            if (atr5m != null) for (int i = 0; i < Math.Min(20, CurrentBar); i++) atrAvg += atr5m[i];
            atrAvg /= Math.Max(1, Math.Min(20, CurrentBar));
            double atrRatio = atrAvg > 0 ? atr5val / atrAvg : 1.0;

            bool bullStack = stack == "BULLISH";
            bool bearStack = stack == "BEARISH";
            double cd24 = cumDelta24;

            // Divergence
            if (IsDivergenceActive()) return "REVERSAL_FORMING";

            // Extreme volatility
            if (atrRatio > 2.0) return "VOLATILITY_EXPANSION";

            // Trending
            if (bullStack && emaStackAge >= 5 && cd24 > 0 && GetSwingStructure().Contains("HH"))
                return "TRENDING_UP";
            if (bearStack && emaStackAge >= 5 && cd24 < 0 && GetSwingStructure().Contains("LL"))
                return "TRENDING_DOWN";

            // Low volume drift
            double volAvg = 0;
            for (int i = 0; i < Math.Min(20, CurrentBar); i++) volAvg += Volume[i];
            volAvg /= Math.Max(1, Math.Min(20, CurrentBar));
            if (Volume[0] < volAvg * 0.7 && atrRatio < 0.9) return "DRIFTING";

            // Chop
            if (atrRatio < 0.9 && (stack == "MIXED" || stack == "FLAT")) return "CHOPPING";

            // ETH-specific
            if (!isRTH)
            {
                double absDelta = Math.Abs(etDeltaSession);
                double etVol = 0;
                for (int i = 0; i < Math.Min(CurrentBar, 200); i++)
                { if (!isRTH) etVol += Volume[i]; else break; }
                if (etVol > 0 && absDelta / etVol > 0.3 && atrRatio >= 0.9)
                    return "OVERNIGHT_BUILDUP";
                return "OVERNIGHT_QUIET";
            }

            return "CHOPPING";
        }

        // -- Helper: EMA stack --------------------------------------------------
        private string GetEmaStack()
        {
            if (emaFast == null || emaMid == null || emaSlow == null) return "UNKNOWN";
            if (CurrentBar < SlowEMA) return "UNKNOWN";
            double f = emaFast[0], m = emaMid[0], s = emaSlow[0];
            if (f > m && m > s) return "BULLISH";
            if (f < m && m < s) return "BEARISH";
            if (Math.Abs(f - m) < TickSize * 3 && Math.Abs(m - s) < TickSize * 3) return "FLAT";
            return "MIXED";
        }

        // -- Helper: swing structure --------------------------------------------
        private string GetSwingStructure()
        {
            if (swingHighs == null || swingLows == null) return "UNKNOWN";
            if (swingHighs.Count < 2 || swingLows.Count < 2) return "INSUFFICIENT_DATA";

            var sh = swingHighs.OrderByDescending(s => s.BarIndex).Take(2).ToList();
            var sl = swingLows.OrderByDescending(s => s.BarIndex).Take(2).ToList();

            bool hh = sh[0].Price > sh[1].Price;
            bool hl = sl[0].Price > sl[1].Price;
            bool lh = sh[0].Price < sh[1].Price;
            bool ll = sl[0].Price < sl[1].Price;

            if (hh && hl) return "HH-HL_UPTREND";
            if (ll && lh) return "LL-LH_DOWNTREND";
            if (hh && ll) return "EXPANDING";
            if (lh && hl) return "COMPRESSED";
            return "MIXED";
        }

        // -- Helper: break of structure -----------------------------------------
        private string GetBOS()
        {
            if (lastBosBar < 0) return "none";
            int age = CurrentBar - lastBosBar;
            if (age > 20) return "none";
            return string.Format("{0}(bar-{1})", lastBosDir, age);
        }

        // -- Helper: volume trend last N bars ----------------------------------
        private string GetVolumeTrend(int bars)
        {
            if (CurrentBar < bars) return "insufficient";
            double first = Volume[bars - 1], last2 = Volume[0];
            double mid   = Volume[bars / 2];
            if (last2 > first * 1.1) return "increasing";
            if (last2 < first * 0.9) return "decreasing";
            return "flat";
        }

        // -- Helper: delta bar trend last N bars -------------------------------
        private string GetDeltaBarTrend(int bars)
        {
            if (ofd24 == null || ofd24.DeltaClose.Count < bars) return "insufficient";
            try
            {
                double d0 = ofd24.DeltaClose[0];
                double dN = ofd24.DeltaClose[Math.Min(bars - 1, ofd24.DeltaClose.Count - 1)];
                double change = d0 - dN;
                if (Math.Abs(change) < 10) return "oscillating";
                return change > 0 ? "accel_positive" : "accel_negative";
            }
            catch { return "unknown"; }
        }

        // -- Helper: divergence active -----------------------------------------
        private bool IsDivergenceActive()
        {
            if (swingHighs == null || swingLows == null || ofd24 == null) return false;
            try
            {
                if (swingHighs.Count >= 2)
                {
                    var sh0 = swingHighs[swingHighs.Count - 1];
                    var sh1 = swingHighs[swingHighs.Count - 2];
                    if (sh0.Price > sh1.Price &&
                        GetDeltaAtBar(sh0.BarIndex) < GetDeltaAtBar(sh1.BarIndex))
                        return true;
                }
                if (swingLows.Count >= 2)
                {
                    var sl0 = swingLows[swingLows.Count - 1];
                    var sl1 = swingLows[swingLows.Count - 2];
                    if (sl0.Price < sl1.Price &&
                        GetDeltaAtBar(sl0.BarIndex) > GetDeltaAtBar(sl1.BarIndex))
                        return true;
                }
            }
            catch { }
            return false;
        }

        // -- Helper: RTH phase -------------------------------------------------
        private string GetRTHPhase()
        {
            if (!isRTH) return "closed";
            double minsIn = (Time[0].TimeOfDay - rthStartSpan).TotalMinutes;
            double rthLen = (rthEndSpan - rthStartSpan).TotalMinutes;
            if (minsIn < 0)   return "pre-open";
            if (minsIn < 10)  return "opening_window";
            if (minsIn < 60)  return "early";
            if (minsIn < rthLen - 60) return "mid";
            if (minsIn < rthLen - 10) return "late";
            return "closing";
        }

        // -- Helper: ETH phase -------------------------------------------------
        private string GetETHPhase()
        {
            if (isRTH) return "rth";
            TimeSpan t = Time[0].TimeOfDay;
            // Mountain time references (adjust if needed)
            // Post-RTH:  14:00-17:00 MT
            // Evening:   17:00-22:00 MT
            // Asia:      22:00-03:00 MT
            // Europe:    03:00-07:30 MT
            // Pre-RTH:   07:30-market open MT
            double h = t.TotalHours;
            if (h >= 14 && h < 17) return "post-RTH";
            if (h >= 17 || h < 3)  return "evening_asia";
            if (h >= 3  && h < 7)  return "europe";
            return "pre-RTH";
        }

        // -- Helper: session type string ---------------------------------------
        private string GetSessionType()
        {
            if (isRTH) return "RTH";
            TimeSpan t = Time[0].TimeOfDay;
            double h = t.TotalHours;
            if (h >= 14 && h < 17) return "ETH_post-RTH";
            if (h < rthStartSpan.TotalHours) return "ETH_pre-RTH";
            return "ETH_overnight";
        }

        #endregion

        // =========================================================================
        #region Properties
        // =========================================================================

        // -- Previous Day ----------------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show Prev Day Levels", Order=1, GroupName="1. Previous Day")]
        public bool ShowPreviousDayLevels { get; set; }

        [XmlIgnore][Display(Name="Prev Open Color",  Order=2, GroupName="1. Previous Day")] public System.Windows.Media.Brush PrevDayOpenColor  { get; set; }
        [Browsable(false)] public string PrevDayOpenColorSerializable  { get { return Serialize.BrushToString(PrevDayOpenColor);  } set { PrevDayOpenColor  = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Prev Close Color", Order=3, GroupName="1. Previous Day")] public System.Windows.Media.Brush PrevDayCloseColor { get; set; }
        [Browsable(false)] public string PrevDayCloseColorSerializable { get { return Serialize.BrushToString(PrevDayCloseColor); } set { PrevDayCloseColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Prev High Color",  Order=4, GroupName="1. Previous Day")] public System.Windows.Media.Brush PrevDayHighColor  { get; set; }
        [Browsable(false)] public string PrevDayHighColorSerializable  { get { return Serialize.BrushToString(PrevDayHighColor);  } set { PrevDayHighColor  = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Prev Low Color",   Order=5, GroupName="1. Previous Day")] public System.Windows.Media.Brush PrevDayLowColor   { get; set; }
        [Browsable(false)] public string PrevDayLowColorSerializable   { get { return Serialize.BrushToString(PrevDayLowColor);   } set { PrevDayLowColor   = Serialize.StringToBrush(value); } }

        // -- Weekly ----------------------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show Weekly Levels", Order=1, GroupName="2. Weekly")]
        public bool ShowWeeklyLevels { get; set; }

        [XmlIgnore][Display(Name="Week High Color",      Order=2, GroupName="2. Weekly")] public System.Windows.Media.Brush WeekHighColor     { get; set; }
        [Browsable(false)] public string WeekHighColorSerializable     { get { return Serialize.BrushToString(WeekHighColor);     } set { WeekHighColor     = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Week Low Color",       Order=3, GroupName="2. Weekly")] public System.Windows.Media.Brush WeekLowColor      { get; set; }
        [Browsable(false)] public string WeekLowColorSerializable      { get { return Serialize.BrushToString(WeekLowColor);      } set { WeekLowColor      = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Prev Week High Color", Order=4, GroupName="2. Weekly")] public System.Windows.Media.Brush PrevWeekHighColor { get; set; }
        [Browsable(false)] public string PrevWeekHighColorSerializable { get { return Serialize.BrushToString(PrevWeekHighColor); } set { PrevWeekHighColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Prev Week Low Color",  Order=5, GroupName="2. Weekly")] public System.Windows.Media.Brush PrevWeekLowColor  { get; set; }
        [Browsable(false)] public string PrevWeekLowColorSerializable  { get { return Serialize.BrushToString(PrevWeekLowColor);  } set { PrevWeekLowColor  = Serialize.StringToBrush(value); } }

        // -- Monthly ---------------------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show Monthly Levels", Order=1, GroupName="3. Monthly")]
        public bool ShowMonthlyLevels { get; set; }

        [XmlIgnore][Display(Name="Month High Color",      Order=2, GroupName="3. Monthly")] public System.Windows.Media.Brush MonthHighColor     { get; set; }
        [Browsable(false)] public string MonthHighColorSerializable     { get { return Serialize.BrushToString(MonthHighColor);     } set { MonthHighColor     = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Month Low Color",       Order=3, GroupName="3. Monthly")] public System.Windows.Media.Brush MonthLowColor      { get; set; }
        [Browsable(false)] public string MonthLowColorSerializable      { get { return Serialize.BrushToString(MonthLowColor);      } set { MonthLowColor      = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Prev Month High Color", Order=4, GroupName="3. Monthly")] public System.Windows.Media.Brush PrevMonthHighColor { get; set; }
        [Browsable(false)] public string PrevMonthHighColorSerializable { get { return Serialize.BrushToString(PrevMonthHighColor); } set { PrevMonthHighColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Prev Month Low Color",  Order=5, GroupName="3. Monthly")] public System.Windows.Media.Brush PrevMonthLowColor  { get; set; }
        [Browsable(false)] public string PrevMonthLowColorSerializable  { get { return Serialize.BrushToString(PrevMonthLowColor);  } set { PrevMonthLowColor  = Serialize.StringToBrush(value); } }

        // -- Today -----------------------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show Today Open",     Order=1, GroupName="4. Today")] public bool ShowTodayOpen      { get; set; }
        [XmlIgnore][Display(Name="Today Open Color",  Order=2, GroupName="4. Today")] public System.Windows.Media.Brush TodayOpenColor    { get; set; }
        [Browsable(false)] public string TodayOpenColorSerializable    { get { return Serialize.BrushToString(TodayOpenColor);    } set { TodayOpenColor    = Serialize.StringToBrush(value); } }
        [NinjaScriptProperty][Display(Name="Show Current H/L",    Order=3, GroupName="4. Today")] public bool ShowCurrentHighLow  { get; set; }
        [XmlIgnore][Display(Name="Current High Color", Order=4, GroupName="4. Today")] public System.Windows.Media.Brush CurrentHighColor  { get; set; }
        [Browsable(false)] public string CurrentHighColorSerializable  { get { return Serialize.BrushToString(CurrentHighColor);  } set { CurrentHighColor  = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Current Low Color",  Order=5, GroupName="4. Today")] public System.Windows.Media.Brush CurrentLowColor   { get; set; }
        [Browsable(false)] public string CurrentLowColorSerializable   { get { return Serialize.BrushToString(CurrentLowColor);   } set { CurrentLowColor   = Serialize.StringToBrush(value); } }

        // -- POC -------------------------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show POC", Order=1, GroupName="5. POC")] public bool ShowPOC { get; set; }
        [XmlIgnore][Display(Name="POC Color", Order=2, GroupName="5. POC")] public System.Windows.Media.Brush POCColor { get; set; }
        [Browsable(false)] public string POCColorSerializable { get { return Serialize.BrushToString(POCColor); } set { POCColor = Serialize.StringToBrush(value); } }
        [Range(1,10)][Display(Name="POC Width", Order=3, GroupName="5. POC")] public int POCWidth { get; set; }

        // -- Labels ----------------------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show Labels", Order=1, GroupName="6. Labels")] public bool ShowLabels { get; set; }
        [Range(8,20)][Display(Name="Label Font Size", Order=2, GroupName="6. Labels")] public int LabelFontSize { get; set; }

        // -- EMAs ------------------------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show EMAs", Order=1, GroupName="7. EMAs")] public bool ShowEMAs { get; set; }
        [Range(1,100)][Display(Name="Fast EMA",  Order=2, GroupName="7. EMAs")] public int FastEMA { get; set; }
        [Range(1,100)][Display(Name="Mid EMA",   Order=3, GroupName="7. EMAs")] public int MidEMA  { get; set; }
        [Range(1,200)][Display(Name="Slow EMA",  Order=4, GroupName="7. EMAs")] public int SlowEMA { get; set; }
        [XmlIgnore][Display(Name="Fast EMA Color", Order=5, GroupName="7. EMAs")] public System.Windows.Media.Brush EMAFastColor { get; set; }
        [Browsable(false)] public string EMAFastColorSerializable { get { return Serialize.BrushToString(EMAFastColor); } set { EMAFastColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Mid EMA Color",  Order=6, GroupName="7. EMAs")] public System.Windows.Media.Brush EMAMidColor  { get; set; }
        [Browsable(false)] public string EMAMidColorSerializable  { get { return Serialize.BrushToString(EMAMidColor);  } set { EMAMidColor  = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Slow EMA Color", Order=7, GroupName="7. EMAs")] public System.Windows.Media.Brush EMASlowColor { get; set; }
        [Browsable(false)] public string EMASlowColorSerializable { get { return Serialize.BrushToString(EMASlowColor); } set { EMASlowColor = Serialize.StringToBrush(value); } }

        // -- Anchored VWAP ---------------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show Anchored VWAP", Order=1, GroupName="8. Anchored VWAP")] public bool ShowAnchoredVWAP { get; set; }
        [Range(1.5,5.0)][Display(Name="Volume Spike Multiplier",  Order=2, GroupName="8. Anchored VWAP")] public double VWAPVolumeSpikeMultiplier { get; set; }
        [Range(1.5,5.0)][Display(Name="Candle Size Multiplier",   Order=3, GroupName="8. Anchored VWAP")] public double VWAPCandleSizeMultiplier  { get; set; }
        [Range(10,100)][Display(Name="Lookback Period",           Order=4, GroupName="8. Anchored VWAP")] public int    VWAPLookbackPeriod         { get; set; }
        [Range(1,20)][Display(Name="Max Active VWAPs",            Order=5, GroupName="8. Anchored VWAP")] public int    MaxActiveVWAPs             { get; set; }
        [XmlIgnore][Display(Name="VWAP Color", Order=6, GroupName="8. Anchored VWAP")] public System.Windows.Media.Brush AnchoredVWAPColor { get; set; }
        [Browsable(false)] public string AnchoredVWAPColorSerializable { get { return Serialize.BrushToString(AnchoredVWAPColor); } set { AnchoredVWAPColor = Serialize.StringToBrush(value); } }
        [Range(1,5)][Display(Name="VWAP Width",      Order=7, GroupName="8. Anchored VWAP")] public int  AnchoredVWAPWidth { get; set; }
        [Display(Name="Show VWAP Labels",             Order=8, GroupName="8. Anchored VWAP")] public bool ShowVWAPLabels   { get; set; }
        [Display(Name="Remove Old VWAPs",             Order=9, GroupName="8. Anchored VWAP")] public bool RemoveOldVWAPs   { get; set; }
        [Range(50,500)][Display(Name="VWAP Max Age (bars)", Order=10, GroupName="8. Anchored VWAP")] public int VWAPMaxAgeBars { get; set; }

        // -- Trendlines ------------------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show Trendlines", Order=1, GroupName="9. Trendlines")] public bool ShowTrendlines { get; set; }
        [Range(1,20)][Display(Name="Swing Bars",       Order=2, GroupName="9. Trendlines")] public int    SwingBars             { get; set; }
        [Range(2,10)][Display(Name="Min Swing Points", Order=3, GroupName="9. Trendlines")] public int    MinSwingPoints        { get; set; }
        [Range(10,200)][Display(Name="Lookback",       Order=4, GroupName="9. Trendlines")] public int    TrendlineLookback     { get; set; }
        [XmlIgnore][Display(Name="Bullish Color",      Order=5, GroupName="9. Trendlines")] public System.Windows.Media.Brush  BullishTrendlineColor { get; set; }
        [Browsable(false)] public string BullishTrendlineColorSerializable { get { return Serialize.BrushToString(BullishTrendlineColor); } set { BullishTrendlineColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Bearish Color",      Order=6, GroupName="9. Trendlines")] public System.Windows.Media.Brush  BearishTrendlineColor { get; set; }
        [Browsable(false)] public string BearishTrendlineColorSerializable { get { return Serialize.BrushToString(BearishTrendlineColor); } set { BearishTrendlineColor = Serialize.StringToBrush(value); } }
        [Range(1,10)][Display(Name="Line Width",       Order=7, GroupName="9. Trendlines")] public int    TrendlineWidth        { get; set; }
        [Range(5,100)][Display(Name="Extension Bars",  Order=8, GroupName="9. Trendlines")] public int    TrendlineExtension    { get; set; }
        [Display(Name="Auto Delete On Break",          Order=9, GroupName="9. Trendlines")] public bool   AutoDeleteOnBreak     { get; set; }
        [Range(1,30)][Display(Name="Removal Delay (min)", Order=10, GroupName="9. Trendlines")] public int RemovalDelayMinutes  { get; set; }
        [Range(0,45)][Display(Name="Min Angle (deg)",  Order=11, GroupName="9. Trendlines")] public double MinTrendlineAngle    { get; set; }
        [Display(Name="Show Break Alerts",             Order=12, GroupName="9. Trendlines")] public bool   ShowTrendlineAlerts  { get; set; }
        [Display(Name="Highlight Near Trendline",      Order=13, GroupName="9. Trendlines")] public bool   HighlightNearTrendline { get; set; }
        [Range(1,50)][Display(Name="Near Distance (ticks)", Order=14, GroupName="9. Trendlines")] public int NearTrendlineDistance { get; set; }

        // -- Fibonacci -------------------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show Auto Fibonacci", Order=1, GroupName="10. Fibonacci")]
        public bool ShowFibonacci { get; set; }

        [Range(1,100)][Display(Name="Min Gap Size (ticks)", Order=2, GroupName="10. Fibonacci")]
        public double MinGapSize { get; set; }

        [XmlIgnore][Display(Name="Retrace Bull Color", Order=3, GroupName="10. Fibonacci")] public System.Windows.Media.Brush FibRetraceBullColor { get; set; }
        [Browsable(false)] public string FibRetraceBullColorSerializable { get { return Serialize.BrushToString(FibRetraceBullColor); } set { FibRetraceBullColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name="Retrace Bear Color", Order=4, GroupName="10. Fibonacci")] public System.Windows.Media.Brush FibRetraceBearColor { get; set; }
        [Browsable(false)] public string FibRetraceBearColorSerializable { get { return Serialize.BrushToString(FibRetraceBearColor); } set { FibRetraceBearColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name="Extension Bull Color", Order=5, GroupName="10. Fibonacci")] public System.Windows.Media.Brush FibExtendBullColor { get; set; }
        [Browsable(false)] public string FibExtendBullColorSerializable { get { return Serialize.BrushToString(FibExtendBullColor); } set { FibExtendBullColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name="Extension Bear Color", Order=6, GroupName="10. Fibonacci")] public System.Windows.Media.Brush FibExtendBearColor { get; set; }
        [Browsable(false)] public string FibExtendBearColorSerializable { get { return Serialize.BrushToString(FibExtendBearColor); } set { FibExtendBearColor = Serialize.StringToBrush(value); } }

        [Range(1,5)][Display(Name="Fib Line Width",  Order=7, GroupName="10. Fibonacci")] public int  FibLineWidth  { get; set; }
        [Display(Name="Show Fib Labels",             Order=8, GroupName="10. Fibonacci")] public bool ShowFibLabels { get; set; }
        [Range(1,10)][Display(Name="Max Fib Sets",   Order=9, GroupName="10. Fibonacci")] public int  MaxFibSets   { get; set; }

        // -- Delta Divergence ------------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show Delta Divergence", Order=1, GroupName="11. Delta Divergence")]
        public bool ShowDeltaDivergence { get; set; }

        [XmlIgnore][Display(Name="Bull Div Color", Order=2, GroupName="11. Delta Divergence")]
        public System.Windows.Media.Brush DeltaDivBullColor { get; set; }
        [Browsable(false)]
        public string DeltaDivBullColorSerializable
        { get { return Serialize.BrushToString(DeltaDivBullColor); } set { DeltaDivBullColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name="Bear Div Color", Order=3, GroupName="11. Delta Divergence")]
        public System.Windows.Media.Brush DeltaDivBearColor { get; set; }
        [Browsable(false)]
        public string DeltaDivBearColorSerializable
        { get { return Serialize.BrushToString(DeltaDivBearColor); } set { DeltaDivBearColor = Serialize.StringToBrush(value); } }

        [Range(8,20)][Display(Name="Div Label Font Size", Order=4, GroupName="11. Delta Divergence")]
        public int DeltaDivFontSize { get; set; }

        // -- Phase 1 Instrumentation --------------------------------------------
        [NinjaScriptProperty]
        [Display(Name="Enable State Dump Log", Order=1, GroupName="0. Instrumentation")]
        public bool EnableStateDump { get; set; }

        [Range(1, 60)]
        [Display(Name="RTH Log Interval (minutes)", Order=2, GroupName="0. Instrumentation")]
        public int DumpIntervalRTH { get; set; }

        [Range(1, 60)]
        [Display(Name="ETH Log Interval (minutes)", Order=3, GroupName="0. Instrumentation")]
        public int DumpIntervalETH { get; set; }


        // -- Tier 3: Cross-Instrument Correlation ------------------------------
        [Display(Name="Corr Instrument 1 (e.g. MES 06-26)", Order=1, GroupName="0b. Correlation")]
        public string CorrInstrument1 { get; set; }

        [Display(Name="Corr Instrument 2 (e.g. MNQ 06-26)", Order=2, GroupName="0b. Correlation")]
        public string CorrInstrument2 { get; set; }

        [Display(Name="Corr Instrument 3 (e.g. M2K 06-26)", Order=3, GroupName="0b. Correlation")]
        public string CorrInstrument3 { get; set; }

        // -- 12. Spike Candle Lines ------------------------------------------------
        [Display(Name="Show Spike Candle Lines", Order=1, GroupName="12. Spike Candle Lines")]
        public bool ShowSpikeCandleLines { get; set; }

        [XmlIgnore][Display(Name="Bull Spike Color", Order=2, GroupName="12. Spike Candle Lines")]
        public System.Windows.Media.Brush BullSpikeColor { get; set; }
        [Browsable(false)] public string BullSpikeColorSerializable
        { get { return Serialize.BrushToString(BullSpikeColor); } set { BullSpikeColor = Serialize.StringToBrush(value); } }

        [XmlIgnore][Display(Name="Bear Spike Color", Order=3, GroupName="12. Spike Candle Lines")]
        public System.Windows.Media.Brush BearSpikeColor { get; set; }
        [Browsable(false)] public string BearSpikeColorSerializable
        { get { return Serialize.BrushToString(BearSpikeColor); } set { BearSpikeColor = Serialize.StringToBrush(value); } }

        // -- 13. Proximity Menu ----------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show Proximity Menu", Order=1, GroupName="13. Proximity Menu")]
        public bool ShowProximityMenu { get; set; }

        [Range(1.0, 500.0)][Display(Name="Proximity Range (points)", Order=2, GroupName="13. Proximity Menu")]
        public double ProximityPoints { get; set; }

        // -- 14. Gap / % Open ------------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show Gap / % Open Label", Order=1, GroupName="14. Gap Label")]
        public bool ShowGapLabel { get; set; }

        // -- 15. Dual Delta Panel --------------------------------------------------
        [NinjaScriptProperty][Display(Name="Show Delta Panel (RTH vs 24hr)", Order=1, GroupName="15. Delta Panel")]
        public bool ShowDeltaPanel { get; set; }

        // -- 16. RTH Session Times (in your chart's LOCAL timezone) ----------------
        [Display(Name="RTH Start Hour (local time)", Order=1, GroupName="17. RTH Times")]
        [Range(0, 23)]
        public int RTHStartHour { get; set; }

        [Display(Name="RTH Start Minute", Order=2, GroupName="17. RTH Times")]
        [Range(0, 59)]
        public int RTHStartMinute { get; set; }

        [Display(Name="RTH End Hour (local time)", Order=3, GroupName="17. RTH Times")]
        [Range(0, 23)]
        public int RTHEndHour { get; set; }

        [Display(Name="RTH End Minute", Order=4, GroupName="17. RTH Times")]
        [Range(0, 59)]
        public int RTHEndMinute { get; set; }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private SessionLevels[] cacheSessionLevels;
		public SessionLevels SessionLevels(bool showPreviousDayLevels, bool showWeeklyLevels, bool showMonthlyLevels, bool showTodayOpen, bool showCurrentHighLow, bool showPOC, bool showLabels, bool showEMAs, bool showAnchoredVWAP, bool showTrendlines, bool showFibonacci, bool showDeltaDivergence, bool enableStateDump, bool showProximityMenu, bool showGapLabel, bool showDeltaPanel)
		{
			return SessionLevels(Input, showPreviousDayLevels, showWeeklyLevels, showMonthlyLevels, showTodayOpen, showCurrentHighLow, showPOC, showLabels, showEMAs, showAnchoredVWAP, showTrendlines, showFibonacci, showDeltaDivergence, enableStateDump, showProximityMenu, showGapLabel, showDeltaPanel);
		}

		public SessionLevels SessionLevels(ISeries<double> input, bool showPreviousDayLevels, bool showWeeklyLevels, bool showMonthlyLevels, bool showTodayOpen, bool showCurrentHighLow, bool showPOC, bool showLabels, bool showEMAs, bool showAnchoredVWAP, bool showTrendlines, bool showFibonacci, bool showDeltaDivergence, bool enableStateDump, bool showProximityMenu, bool showGapLabel, bool showDeltaPanel)
		{
			if (cacheSessionLevels != null)
				for (int idx = 0; idx < cacheSessionLevels.Length; idx++)
					if (cacheSessionLevels[idx] != null && cacheSessionLevels[idx].ShowPreviousDayLevels == showPreviousDayLevels && cacheSessionLevels[idx].ShowWeeklyLevels == showWeeklyLevels && cacheSessionLevels[idx].ShowMonthlyLevels == showMonthlyLevels && cacheSessionLevels[idx].ShowTodayOpen == showTodayOpen && cacheSessionLevels[idx].ShowCurrentHighLow == showCurrentHighLow && cacheSessionLevels[idx].ShowPOC == showPOC && cacheSessionLevels[idx].ShowLabels == showLabels && cacheSessionLevels[idx].ShowEMAs == showEMAs && cacheSessionLevels[idx].ShowAnchoredVWAP == showAnchoredVWAP && cacheSessionLevels[idx].ShowTrendlines == showTrendlines && cacheSessionLevels[idx].ShowFibonacci == showFibonacci && cacheSessionLevels[idx].ShowDeltaDivergence == showDeltaDivergence && cacheSessionLevels[idx].EnableStateDump == enableStateDump && cacheSessionLevels[idx].ShowProximityMenu == showProximityMenu && cacheSessionLevels[idx].ShowGapLabel == showGapLabel && cacheSessionLevels[idx].ShowDeltaPanel == showDeltaPanel && cacheSessionLevels[idx].EqualsInput(input))
						return cacheSessionLevels[idx];
			return CacheIndicator<SessionLevels>(new SessionLevels(){ ShowPreviousDayLevels = showPreviousDayLevels, ShowWeeklyLevels = showWeeklyLevels, ShowMonthlyLevels = showMonthlyLevels, ShowTodayOpen = showTodayOpen, ShowCurrentHighLow = showCurrentHighLow, ShowPOC = showPOC, ShowLabels = showLabels, ShowEMAs = showEMAs, ShowAnchoredVWAP = showAnchoredVWAP, ShowTrendlines = showTrendlines, ShowFibonacci = showFibonacci, ShowDeltaDivergence = showDeltaDivergence, EnableStateDump = enableStateDump, ShowProximityMenu = showProximityMenu, ShowGapLabel = showGapLabel, ShowDeltaPanel = showDeltaPanel }, input, ref cacheSessionLevels);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.SessionLevels SessionLevels(bool showPreviousDayLevels, bool showWeeklyLevels, bool showMonthlyLevels, bool showTodayOpen, bool showCurrentHighLow, bool showPOC, bool showLabels, bool showEMAs, bool showAnchoredVWAP, bool showTrendlines, bool showFibonacci, bool showDeltaDivergence, bool enableStateDump, bool showProximityMenu, bool showGapLabel, bool showDeltaPanel)
		{
			return indicator.SessionLevels(Input, showPreviousDayLevels, showWeeklyLevels, showMonthlyLevels, showTodayOpen, showCurrentHighLow, showPOC, showLabels, showEMAs, showAnchoredVWAP, showTrendlines, showFibonacci, showDeltaDivergence, enableStateDump, showProximityMenu, showGapLabel, showDeltaPanel);
		}

		public Indicators.SessionLevels SessionLevels(ISeries<double> input , bool showPreviousDayLevels, bool showWeeklyLevels, bool showMonthlyLevels, bool showTodayOpen, bool showCurrentHighLow, bool showPOC, bool showLabels, bool showEMAs, bool showAnchoredVWAP, bool showTrendlines, bool showFibonacci, bool showDeltaDivergence, bool enableStateDump, bool showProximityMenu, bool showGapLabel, bool showDeltaPanel)
		{
			return indicator.SessionLevels(input, showPreviousDayLevels, showWeeklyLevels, showMonthlyLevels, showTodayOpen, showCurrentHighLow, showPOC, showLabels, showEMAs, showAnchoredVWAP, showTrendlines, showFibonacci, showDeltaDivergence, enableStateDump, showProximityMenu, showGapLabel, showDeltaPanel);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.SessionLevels SessionLevels(bool showPreviousDayLevels, bool showWeeklyLevels, bool showMonthlyLevels, bool showTodayOpen, bool showCurrentHighLow, bool showPOC, bool showLabels, bool showEMAs, bool showAnchoredVWAP, bool showTrendlines, bool showFibonacci, bool showDeltaDivergence, bool enableStateDump, bool showProximityMenu, bool showGapLabel, bool showDeltaPanel)
		{
			return indicator.SessionLevels(Input, showPreviousDayLevels, showWeeklyLevels, showMonthlyLevels, showTodayOpen, showCurrentHighLow, showPOC, showLabels, showEMAs, showAnchoredVWAP, showTrendlines, showFibonacci, showDeltaDivergence, enableStateDump, showProximityMenu, showGapLabel, showDeltaPanel);
		}

		public Indicators.SessionLevels SessionLevels(ISeries<double> input , bool showPreviousDayLevels, bool showWeeklyLevels, bool showMonthlyLevels, bool showTodayOpen, bool showCurrentHighLow, bool showPOC, bool showLabels, bool showEMAs, bool showAnchoredVWAP, bool showTrendlines, bool showFibonacci, bool showDeltaDivergence, bool enableStateDump, bool showProximityMenu, bool showGapLabel, bool showDeltaPanel)
		{
			return indicator.SessionLevels(input, showPreviousDayLevels, showWeeklyLevels, showMonthlyLevels, showTodayOpen, showCurrentHighLow, showPOC, showLabels, showEMAs, showAnchoredVWAP, showTrendlines, showFibonacci, showDeltaDivergence, enableStateDump, showProximityMenu, showGapLabel, showDeltaPanel);
		}
	}
}

#endregion
