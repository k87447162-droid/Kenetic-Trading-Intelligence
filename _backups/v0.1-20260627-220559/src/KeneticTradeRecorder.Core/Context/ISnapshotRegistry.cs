// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Core - ISnapshotRegistry.cs   (PHASE 2)
//
//  PURPOSE:
//      A PERSISTENT registry of market state. The indicator publishes one immutable
//      MarketSnapshot per bar; the registry keeps a per-instrument rolling history
//      and exposes the Current snapshot, the Previous snapshot, and the full history
//      so research can analyze state transitions (regime changes, delta acceleration,
//      conviction shifts, ...) WITHOUT redesigning the architecture later.
//
//      MarketSnapshot is the raw MARKET-STATE object. It is the market component of
//      the larger MarketContext (MarketSnapshot + TradeSnapshot + TraderSnapshot).
//      The registry tracks the continuous market-state stream; trade/trader state is
//      sparse and assembled into a MarketContext at decision/trade time.
//
//  HISTORY ORDER:
//      History(instrument)[0] is the MOST RECENT snapshot (== Current). Higher
//      indices are progressively older. Adjacent pairs (History[i], History[i+1])
//      are (newer, older) — convenient for transition detection.
//
//  THREAD SAFETY:
//      Implementations MUST be safe for concurrent Publish (indicator thread) and
//      reads (recorder/research threads). Snapshots are immutable; reads return
//      stable copies.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using KeneticTradeRecorder.Shared;

namespace KeneticTradeRecorder.Core
{
    /// <summary>Persistent per-instrument registry of market-state snapshots (current/previous/history).</summary>
    public interface ISnapshotRegistry
    {
        /// <summary>Append the latest snapshot for snapshot.Symbol. Non-blocking; thread-safe.</summary>
        void Publish(MarketSnapshot snapshot);

        /// <summary>Most recent snapshot for the instrument, or null if none.</summary>
        MarketSnapshot? Current(string instrument);

        /// <summary>The snapshot immediately before Current, or null if fewer than two exist.</summary>
        MarketSnapshot? Previous(string instrument);

        /// <summary>
        /// Rolling history for the instrument, newest-first ([0] == Current), up to
        /// HistoryCapacity entries. Returns an empty list if the instrument is unknown.
        /// The returned list is a stable copy; safe to enumerate.
        /// </summary>
        IReadOnlyList<MarketSnapshot> History(string instrument);

        /// <summary>Maximum number of snapshots retained per instrument.</summary>
        int HistoryCapacity { get; }

        /// <summary>True if at least one snapshot has been published for the instrument.</summary>
        bool HasContext(string instrument);

        /// <summary>Number of instruments currently tracked.</summary>
        int Count { get; }

        /// <summary>Copy of the tracked instrument keys; safe to enumerate.</summary>
        IReadOnlyCollection<string> Instruments { get; }
    }
}
