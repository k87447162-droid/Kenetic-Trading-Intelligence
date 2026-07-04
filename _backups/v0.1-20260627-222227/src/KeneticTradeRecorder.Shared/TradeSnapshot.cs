// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Shared - TradeSnapshot.cs   (PHASE 2)
//
//  PURPOSE:
//      Immutable TRADE-STATE component of a MarketContext: what the position/order
//      situation is at a point in time. Pairs with MarketSnapshot (market state) and
//      TraderSnapshot (human decision state) inside MarketContext.
//
//  STATUS: foundational scaffold. Fields here are grounded in existing identity and
//      enum vocabulary; more (per-leg fills, working orders, ATM bracket state) will
//      be added as the recorder threads trade state through. Immutable + versioned so
//      additions are non-breaking.
//
//  THREAD SAFETY:  Immutable; safe to share.
// -----------------------------------------------------------------------------
using System;

namespace KeneticTradeRecorder.Shared
{
    /// <summary>Immutable trade/position state for one instrument at one instant.</summary>
    public sealed record TradeSnapshot
    {
        /// <summary>Version of this snapshot's shape.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Account + instrument this trade state belongs to.</summary>
        public AccountInstrumentKey Key { get; init; } = new AccountInstrumentKey(string.Empty, string.Empty);

        /// <summary>Identifier of the trade (lifecycle) this state belongs to, if assembled.</summary>
        public TradeId TradeId { get; init; }

        /// <summary>Directional side of the current net position.</summary>
        public TradeSide Side { get; init; } = TradeSide.Flat;

        /// <summary>Signed net position quantity (+ long, - short, 0 flat).</summary>
        public int SignedPosition { get; init; }

        /// <summary>Average entry price of the current position (0 when flat).</summary>
        public double AveragePrice { get; init; }

        /// <summary>How this trade originated (manual / strategy / ATM), best-effort.</summary>
        public TradeOrigin Origin { get; init; } = TradeOrigin.Unknown;

        /// <summary>When this trade state was captured (UTC).</summary>
        public DateTimeOffset AsOfUtc { get; init; }

        /// <summary>Shape version stamp.</summary>
        public int SnapshotVersion { get; init; } = CurrentVersion;
    }
}
