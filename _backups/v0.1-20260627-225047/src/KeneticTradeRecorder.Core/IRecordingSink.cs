// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Core - IRecordingSink.cs
//
//  PURPOSE:        The single boundary between trade DETECTION (Phase 1) and
//                  trade PERSISTENCE (Phases 3-4). The AddOn pushes immutable
//                  legs + raw executions here and never knows or cares what the
//                  sink does with them. This keeps the recorder's responsibilities
//                  cleanly separated and lets persistence evolve (Output window ->
//                  append-only journal -> SQLite -> Parquet) without touching the
//                  detection layer.
//
//  CONTRACT:       Implementations MUST be safe to call from NinjaTrader data /
//                  connection threads and MUST NOT block the caller. The expected
//                  pattern is enqueue-and-return, draining on a background thread.
// -----------------------------------------------------------------------------
using System;
using KeneticTradeRecorder.Shared;

namespace KeneticTradeRecorder.Core
{
    /// <summary>Receives assembled legs together with the raw execution that produced them.</summary>
    public interface IRecordingSink : IDisposable
    {
        /// <summary>
        /// Records one assembled leg and its originating raw execution. Must not block;
        /// must be thread-safe. The raw <paramref name="source"/> is included so the
        /// sink can persist full provenance, not just the derived leg.
        /// </summary>
        void Record(in AssembledLeg leg, in ExecutionRecord source);
    }
}
