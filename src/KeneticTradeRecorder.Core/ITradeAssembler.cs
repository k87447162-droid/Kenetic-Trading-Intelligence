// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Core - ITradeAssembler.cs
//
//  PURPOSE:        Abstraction over the position-tracking state machine so the
//                  NinjaTrader adapter depends on an interface, not a concrete
//                  type (supports DI and isolated unit testing).
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using KeneticTradeRecorder.Shared;

namespace KeneticTradeRecorder.Core
{
    /// <summary>
    /// Consumes a stream of executions for any number of (account, instrument)
    /// keys and emits classified legs describing how each execution changed the
    /// position. Stateful and ordering-sensitive: callers MUST feed executions
    /// per key in broker time order, and MUST serialize calls (see implementation
    /// notes on thread safety).
    /// </summary>
    public interface ITradeAssembler
    {
        /// <summary>Applies one execution and returns 0..2 resulting legs (2 only on a reversal).</summary>
        IReadOnlyList<AssembledLeg> Apply(in ExecutionRecord execution);

        /// <summary>Current signed net position for a key (positive long, negative short, 0 flat).</summary>
        int GetNetPosition(AccountInstrumentKey key);

        /// <summary>True if a trade is currently open for the key; outputs its id, side, and absolute size.</summary>
        bool TryGetOpenTrade(AccountInstrumentKey key, out TradeId tradeId, out TradeSide side, out int absQuantity);
    }
}
