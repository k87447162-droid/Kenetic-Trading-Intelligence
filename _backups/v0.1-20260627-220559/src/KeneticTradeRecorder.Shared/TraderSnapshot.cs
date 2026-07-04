// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Shared - TraderSnapshot.cs   (PHASE 2)
//
//  PURPOSE:
//      Immutable HUMAN-DECISION-STATE component of a MarketContext. This is the
//      novel piece of the platform's long-term goal: a digital twin of the
//      discretionary trader, not just a record of indicator values. It captures what
//      the human was intending/feeling/doing around a decision, alongside the
//      MarketSnapshot (what the market was doing) and TradeSnapshot (what the
//      position was doing).
//
//  STATUS: deliberately minimal scaffold. These fields are the obvious first
//      observations; the set will grow as capture mechanisms are built. Everything
//      is optional so a TraderSnapshot can be partially populated, and versioned so
//      additions are non-breaking. Nothing here influences trading.
//
//  THREAD SAFETY:  Immutable; safe to share.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace KeneticTradeRecorder.Shared
{
    /// <summary>Immutable snapshot of the human trader's decision state at one instant.</summary>
    public sealed record TraderSnapshot
    {
        /// <summary>Version of this snapshot's shape.</summary>
        public const int CurrentVersion = 1;

        /// <summary>When this decision state was captured (UTC).</summary>
        public DateTimeOffset AsOfUtc { get; init; }

        /// <summary>Free-form discretionary intent tags (e.g. "with-trend", "fade", "breakout-retest").</summary>
        public IReadOnlyList<string> Intents { get; init; } = Array.Empty<string>();

        /// <summary>Self-reported confidence in the decision, 0..1, if captured.</summary>
        public double? ConfidenceZeroToOne { get; init; }

        /// <summary>Time from setup recognition to action, in milliseconds, if measured (proxy for hesitation).</summary>
        public int? DecisionLatencyMs { get; init; }

        /// <summary>Free-form note the trader attached to the decision.</summary>
        public string? Note { get; init; }

        /// <summary>References to screenshots captured around the decision (paths / ids).</summary>
        public IReadOnlyList<string> ScreenshotRefs { get; init; } = Array.Empty<string>();

        /// <summary>Shape version stamp.</summary>
        public int SnapshotVersion { get; init; } = CurrentVersion;
    }
}
