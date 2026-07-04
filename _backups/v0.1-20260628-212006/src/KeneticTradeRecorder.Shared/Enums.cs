// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Shared - Enums.cs
//
//  PURPOSE:        Canonical, strongly-typed domain vocabulary. Enums are used
//                  instead of strings everywhere a fixed set of values exists.
//  THREAD SAFETY:  Value types; inherently safe to share.
// -----------------------------------------------------------------------------
namespace KeneticTradeRecorder.Shared
{
    /// <summary>
    /// Best-effort classification of how an execution originated.
    /// NOTE: This is a DERIVED feature, not a raw fact. NinjaTrader does not
    /// expose a fully reliable manual/strategy/ATM flag, so origin is computed by
    /// a classifier from raw order/execution provenance. Raw provenance is always
    /// persisted so the classifier can be re-run/improved without re-recording.
    /// </summary>
    public enum TradeOrigin
    {
        /// <summary>Could not be determined from available provenance.</summary>
        Unknown = 0,
        /// <summary>Discretionary order placed by a human (Chart Trader / SuperDOM / etc.).</summary>
        Manual = 1,
        /// <summary>Order placed by a running NinjaScript strategy.</summary>
        Strategy = 2,
        /// <summary>Order placed via an ATM strategy template.</summary>
        Atm = 3
    }

    /// <summary>Directional side of a position or trade. Backing values are signed for math convenience.</summary>
    public enum TradeSide
    {
        /// <summary>No position.</summary>
        Flat = 0,
        /// <summary>Net long.</summary>
        Long = 1,
        /// <summary>Net short.</summary>
        Short = -1
    }

    /// <summary>
    /// The role an execution (or a split leg of one) plays within a trade's lifecycle.
    /// Determined from net-position transitions, never from order names alone.
    /// </summary>
    public enum ExecutionRole
    {
        /// <summary>Default / not yet classified.</summary>
        Unknown = 0,
        /// <summary>Opened a new trade from flat.</summary>
        Entry = 1,
        /// <summary>Increased absolute size of an existing position in the same direction.</summary>
        ScaleIn = 2,
        /// <summary>Reduced absolute size of an existing position without closing it.</summary>
        ScaleOut = 3,
        /// <summary>Reduced the position to exactly flat, closing the trade.</summary>
        Exit = 4
    }
}
