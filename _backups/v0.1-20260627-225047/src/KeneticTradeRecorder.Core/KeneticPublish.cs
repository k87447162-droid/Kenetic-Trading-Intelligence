// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Core - KeneticPublish.cs
//
//  The ONE call the SessionLevels indicator makes to feed market context to the
//  recorder. The auto-integration inserts exactly one fully-qualified line right
//  after SessionLevels' existing `Print(line);`:
//
//      KeneticTradeRecorder.Core.KeneticPublish.PublishDiagnostic(line);
//
//  Rationale for this shape:
//    * Fully qualified  -> no `using` directives need to be added to SessionLevels.
//    * Self-contained   -> all parsing/publishing/error-handling lives here in Core,
//                          which is unit-verified; the indicator edit is trivial.
//    * Never throws     -> a parse or registry failure must never disturb chart
//                          rendering (OnRender) or the indicator's own logic.
// -----------------------------------------------------------------------------
using KeneticTradeRecorder.Core.MarketContext;
using KeneticTradeRecorder.Shared;

namespace KeneticTradeRecorder.Core
{
    /// <summary>Entry point invoked by SessionLevels each diagnostic emission.</summary>
    public static class KeneticPublish
    {
        /// <summary>
        /// Parse one [INDICATOR STATE] line and publish the resulting snapshot to the
        /// process-wide registry. Safe to call from the indicator thread; swallows all
        /// exceptions so recording instrumentation can never affect the indicator.
        /// </summary>
        public static void PublishDiagnostic(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            try
            {
                MarketSnapshot snap = DiagnosticLineParser.Parse(line);
                if (snap != null) SnapshotHub.Instance.Publish(snap);
            }
            catch
            {
                // Best-effort observability only.
            }
        }
    }
}
