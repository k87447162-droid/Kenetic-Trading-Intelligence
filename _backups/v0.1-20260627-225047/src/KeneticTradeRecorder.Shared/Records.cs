// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Shared - Records.cs
//
//  PURPOSE:        Immutable data carriers that cross the boundary between the
//                  NinjaTrader adapter (Phase 1 AddOn) and the pure assembly
//                  logic (Core). Inputs preserve RAW provenance so that derived
//                  fields (e.g. origin classification) can be recomputed later
//                  without re-recording - a core data-integrity requirement.
//  THREAD SAFETY:  All records are immutable; safe to hand to a background
//                  writer thread without copying.
// -----------------------------------------------------------------------------
using System;

namespace KeneticTradeRecorder.Shared
{
    /// <summary>
    /// A single broker execution, normalized for position math but retaining raw
    /// fields. <see cref="SignedQuantity"/> is positive for buys/buy-to-cover and
    /// negative for sells/sell-short; this single signed value fully drives net
    /// position tracking.
    /// </summary>
    public readonly record struct ExecutionRecord(
        ExecutionId ExecutionId,
        OrderId OrderId,
        AccountInstrumentKey Key,
        TradeOrigin Origin,
        int SignedQuantity,
        double Price,
        DateTimeOffset TimeUtc,
        // ----- raw provenance (never interpreted by Core; persisted verbatim) -----
        string RawSignalName,        // NinjaTrader Execution.Name
        string RawOrderName,         // NinjaTrader Order.Name
        string RawFromEntrySignal,   // NinjaTrader Order.FromEntrySignal
        string RawOrderAction,       // NinjaTrader Order.OrderAction
        double Commission)
    {
        /// <summary>Absolute filled quantity (always &gt;= 0).</summary>
        public int AbsQuantity => SignedQuantity < 0 ? -SignedQuantity : SignedQuantity;
    }

    /// <summary>
    /// The result of applying one <see cref="ExecutionRecord"/> to the position
    /// book. A normal execution yields exactly one leg. An execution that REVERSES
    /// through flat yields two legs (the closing portion of the prior trade and the
    /// opening portion of the new one), tied together by <see cref="ReversalGroupId"/>.
    /// The original <see cref="ExecutionId"/> is preserved on every leg so the raw
    /// fact is never lost.
    /// </summary>
    public readonly record struct AssembledLeg(
        TradeId TradeId,
        ExecutionId ExecutionId,
        int LegIndex,
        AccountInstrumentKey Key,
        ExecutionRole Role,
        TradeSide TradeSide,
        int SignedQuantity,
        int PositionBefore,
        int PositionAfter,
        double Price,
        DateTimeOffset TimeUtc,
        TradeOrigin Origin,
        bool OpensTrade,
        bool ClosesTrade,
        bool IsReversalLeg,
        Guid ReversalGroupId)
    {
        /// <summary>Absolute size of this leg.</summary>
        public int AbsQuantity => SignedQuantity < 0 ? -SignedQuantity : SignedQuantity;
    }
}
