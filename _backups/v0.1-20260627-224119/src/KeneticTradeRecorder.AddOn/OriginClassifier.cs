// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.AddOn - OriginClassifier.cs
//
//  PURPOSE:
//      Best-effort classification of an execution as Manual / Strategy / ATM.
//
//  WHY THIS IS A SEPARATE, VERSIONED CLASS:
//      NinjaTrader does NOT expose a single reliable flag distinguishing manual,
//      strategy, and ATM orders. Any classification is therefore a HEURISTIC and a
//      DERIVED feature, not a raw fact. Per the project's data principles, derived
//      features must be (a) reproducible from stored raw inputs and (b) versioned,
//      so the rule can improve later WITHOUT corrupting or re-recording history.
//      The recorder persists the raw inputs this method reads (FromEntrySignal,
//      Order.Name, Execution.Name) alongside the classified origin and this
//      Version, so any row can be re-classified after the fact.
//
//  RULE (v2):
//      1) FromEntrySignal present  -> Strategy   (a NinjaScript strategy stamps this
//                                                 on entries and their linked exits)
//      2) order/exec named like a stop or target -> Atm  (ATM-template fingerprint)
//      3) otherwise                -> Manual     (safe default on a discretionary
//                                                 platform; re-derivable from raw)
//
//  CONFIDENCE:     MEDIUM. Still a heuristic; treat origin as advisory. v1 returned
//                  Unknown for hand-placed orders whose Order.Name was non-empty;
//                  v2 makes Manual the default so the common case is labelled.
//  THREAD SAFETY:  Pure function over its inputs; safe to call from any thread.
// -----------------------------------------------------------------------------
using System;
using KeneticTradeRecorder.Shared;

namespace KeneticTradeRecorder.AddOn
{
    /// <summary>Heuristic, versioned origin classifier. See file header for rationale and caveats.</summary>
    public static class OriginClassifier
    {
        /// <summary>Bump this whenever the rule changes; the value is persisted with every classified row.</summary>
        public const int Version = 2;

        /// <summary>Classifies origin from raw provenance (the exact fields that are persisted).</summary>
        /// <param name="fromEntrySignal">NinjaTrader Order.FromEntrySignal (empty for most manual orders).</param>
        /// <param name="orderName">NinjaTrader Order.Name.</param>
        /// <param name="executionName">NinjaTrader Execution.Name (often carries ATM/strategy signal labels).</param>
        public static TradeOrigin Classify(string fromEntrySignal, string orderName, string executionName)
        {
            string entry = (fromEntrySignal ?? string.Empty).Trim();
            string oName = (orderName ?? string.Empty).Trim();
            string eName = (executionName ?? string.Empty).Trim();

            // 1) Strongest signal: a NinjaScript strategy stamps FromEntrySignal on the
            //    orders it manages (entries and their linked stop/target exits).
            if (entry.Length > 0)
                return TradeOrigin.Strategy;

            // 2) No strategy signal, but the order/execution is named like an auto
            //    stop/target: the ATM-template fingerprint on a discretionary entry.
            if (LooksLikeAtm(oName) || LooksLikeAtm(eName))
                return TradeOrigin.Atm;

            // 3) Otherwise treat as a human-placed (manual) order.
            return TradeOrigin.Manual;
        }

        private static bool LooksLikeAtm(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            // Common ATM-generated signal name fragments. Intentionally conservative.
            // VERIFY/EXTEND against the ATM template names you actually use.
            return name.IndexOf("Stop", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Target", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Profit Target", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Stop Loss", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
