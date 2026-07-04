// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Shared - TradeIntelligenceRecord.cs   (PHASE 2 scaffold)
//
//  PURPOSE:
//      The platform's eventual centerpiece. Every trade should ultimately produce ONE
//      immutable record capturing everything observed about it, so research and AI can
//      reconstruct, analyze, and learn from discretionary decisions.
//
//      This file establishes the RECORD SHAPE (the system) so future work plugs into a
//      stable home (the features). It deliberately does NOT implement the facet
//      internals yet — those are separate, reviewable phases. Components that already
//      have real types use them; the rest are minimal, clearly-marked scaffolds.
//
//      Composition (per the platform design):
//        Trade               -> TradeSnapshot      (real)
//        Market Context      -> MarketSnapshot     (real; market state at the moment)
//        Order Flow Context  -> OrderFlowContext   (scaffold)
//        Structure Context   -> StructureContext   (scaffold)
//        Event Context       -> EventContext       (scaffold)
//        Trader Context      -> TraderSnapshot     (real)
//        Outcome             -> TradeOutcome       (scaffold)
//        Replay Timeline     -> ReplayTimeline     (scaffold)
//        Screenshot Refs     -> IReadOnlyList<string> (real)
//        Research Tags       -> IReadOnlyList<string> (real)
//        AI Summary          -> string             (real)
//
//  All fields are optional so a record can be assembled incrementally over a trade's
//  life; everything is immutable and versioned so additions never break stored data.
//
//  THREAD SAFETY:  Immutable; safe to share.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace KeneticTradeRecorder.Shared
{
    // ===== placeholder facets (scaffolds — internals designed in later phases) =====

    /// <summary>SCAFFOLD: order-flow context (future: cumulative-delta path, absorption, imbalances, DOM).</summary>
    public sealed record OrderFlowContext
    {
        public DateTimeOffset AsOfUtc { get; init; }
        public int Version { get; init; } = 1;
    }

    /// <summary>SCAFFOLD: structure context (future: swing-structure path, levels in play, break-of-structure).</summary>
    public sealed record StructureContext
    {
        public DateTimeOffset AsOfUtc { get; init; }
        public int Version { get; init; } = 1;
    }

    /// <summary>SCAFFOLD: event context (future: economic events, session transitions, news flags).</summary>
    public sealed record EventContext
    {
        public DateTimeOffset AsOfUtc { get; init; }
        public int Version { get; init; } = 1;
    }

    /// <summary>SCAFFOLD: trade outcome (future: realized ticks, MAE/MFE, duration, exit reason).</summary>
    public sealed record TradeOutcome
    {
        public DateTimeOffset? ClosedAtUtc { get; init; }
        public int Version { get; init; } = 1;
    }

    /// <summary>SCAFFOLD: replay timeline (future: ordered bar/tick references to reconstruct the trade).</summary>
    public sealed record ReplayTimeline
    {
        public DateTimeOffset AsOfUtc { get; init; }
        public IReadOnlyList<DateTimeOffset> Marks { get; init; } = Array.Empty<DateTimeOffset>();
        public int Version { get; init; } = 1;
    }

    // ===== the record =====

    /// <summary>
    /// One immutable, complete intelligence record for a single trade. Assembled
    /// incrementally; persisted as the platform's primary research artifact.
    /// </summary>
    public sealed record TradeIntelligenceRecord
    {
        /// <summary>Version of this record's shape.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Stable identity of the trade this record describes.</summary>
        public TradeId TradeId { get; init; }

        /// <summary>Account + instrument, for indexing/queries.</summary>
        public AccountInstrumentKey Key { get; init; } = new AccountInstrumentKey(string.Empty, string.Empty);

        /// <summary>When this record was first created (UTC).</summary>
        public DateTimeOffset CreatedAtUtc { get; init; }

        // ----- the eleven components -----
        /// <summary>Trade/position state.</summary>
        public TradeSnapshot? Trade { get; init; }
        /// <summary>Market state at the decisive moment.</summary>
        public MarketSnapshot? MarketContext { get; init; }
        /// <summary>Order-flow context (scaffold).</summary>
        public OrderFlowContext? OrderFlow { get; init; }
        /// <summary>Structure context (scaffold).</summary>
        public StructureContext? Structure { get; init; }
        /// <summary>Event context (scaffold).</summary>
        public EventContext? Event { get; init; }
        /// <summary>Human decision state.</summary>
        public TraderSnapshot? Trader { get; init; }
        /// <summary>Outcome (scaffold).</summary>
        public TradeOutcome? Outcome { get; init; }
        /// <summary>Replay timeline (scaffold).</summary>
        public ReplayTimeline? Replay { get; init; }
        /// <summary>Screenshot references (paths / ids).</summary>
        public IReadOnlyList<string> ScreenshotReferences { get; init; } = Array.Empty<string>();
        /// <summary>Research tags applied to this trade.</summary>
        public IReadOnlyList<string> ResearchTags { get; init; } = Array.Empty<string>();
        /// <summary>AI-generated narrative summary, if produced.</summary>
        public string? AiSummary { get; init; }

        /// <summary>Shape version stamp.</summary>
        public int RecordVersion { get; init; } = CurrentVersion;
    }
}
