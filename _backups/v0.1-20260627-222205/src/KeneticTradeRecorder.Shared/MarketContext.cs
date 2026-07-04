// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Shared - MarketContext.cs   (PHASE 2)
//
//  PURPOSE:
//      The composite "context" object the platform evolves toward: a digital twin of
//      a discretionary decision moment, combining
//          * MarketSnapshot  (what the MARKET was doing)
//          * TradeSnapshot   (what the POSITION was doing)
//          * TraderSnapshot  (what the HUMAN was intending/doing)
//      MarketSnapshot is the raw market-state component; MarketContext is the whole.
//
//      The continuous market-state stream lives in the ISnapshotRegistry as
//      MarketSnapshots (one per bar). A MarketContext is assembled at a decision/trade
//      point by attaching the trade and trader state to the relevant MarketSnapshot.
//
//  STATUS: Market is always present; Trade/Trader are optional (null until captured),
//      so a MarketContext is useful even when only market state exists.
//
//  THREAD SAFETY:  Immutable; safe to share.
// -----------------------------------------------------------------------------
using System;

namespace KeneticTradeRecorder.Shared
{
    /// <summary>Composite of market, trade, and trader state at one moment.</summary>
    public sealed record MarketContext(
        MarketSnapshot Market,
        TradeSnapshot? Trade,
        TraderSnapshot? Trader,
        DateTimeOffset AssembledAtUtc)
    {
        /// <summary>Version of this composite's shape.</summary>
        public const int CurrentContextVersion = 1;

        /// <summary>Shape version stamp.</summary>
        public int ContextVersion { get; init; } = CurrentContextVersion;

        /// <summary>Create a market-only context (no trade/trader state yet).</summary>
        public static MarketContext FromMarket(MarketSnapshot market, DateTimeOffset assembledAtUtc) =>
            new MarketContext(market, null, null, assembledAtUtc);

        /// <summary>Return a copy with trade state attached.</summary>
        public MarketContext WithTrade(TradeSnapshot trade) => this with { Trade = trade };

        /// <summary>Return a copy with trader state attached.</summary>
        public MarketContext WithTrader(TraderSnapshot trader) => this with { Trader = trader };
    }
}
