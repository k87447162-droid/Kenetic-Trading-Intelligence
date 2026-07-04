// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Core - TradeAssembler.cs
//
//  PURPOSE:
//      Pure (NinjaTrader-independent) state machine that reconstructs trade
//      lifecycles from a stream of executions. For each execution it determines
//      whether the execution opened a trade (Entry), added to it (ScaleIn),
//      trimmed it (ScaleOut), closed it (Exit), or reversed it (a split into an
//      Exit leg + an Entry leg). Assigns deterministic, reproducible TradeIds.
//
//  RESPONSIBILITIES:
//      - Maintain signed net position per (account, instrument).
//      - Classify each execution by net-position transition (NOT by order name).
//      - Open/close logical trades and hand back immutable legs.
//
//  EXPLICITLY NOT RESPONSIBLE FOR:
//      - Talking to NinjaTrader, persistence, threading, market context, or PnL.
//      - Classifying origin (manual/strategy/ATM): that is decided upstream by the
//        adapter and merely carried through here.
//
//  INPUTS:         ExecutionRecord (signed quantity drives all math).
//  OUTPUTS:        0..2 AssembledLeg per execution.
//
//  THREAD SAFETY:  NOT thread-safe by itself. It holds mutable per-key state. The
//                  Phase 1 AddOn serializes all Apply() calls under a single lock
//                  because NinjaTrader execution events may arrive on background
//                  connection threads. Keeping the lock here would couple the pure
//                  logic to a synchronization policy; instead the OWNER serializes.
//
//  PERFORMANCE:    O(1) per execution. One Dictionary lookup; allocates only the
//                  small returned list (1 or 2 entries). No LINQ on the hot path.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using KeneticTradeRecorder.Shared;

namespace KeneticTradeRecorder.Core
{
    /// <inheritdoc cref="ITradeAssembler"/>
    public sealed class TradeAssembler : ITradeAssembler
    {
        /// <summary>Mutable bookkeeping for a single (account, instrument) key.</summary>
        private sealed class KeyState
        {
            public int NetPosition;          // signed
            public bool HasOpenTrade;
            public TradeId OpenTradeId;
            public TradeSide OpenSide;
            public long TradeOrdinal;         // cosmetic per-key counter
        }

        private readonly Dictionary<AccountInstrumentKey, KeyState> _states =
            new Dictionary<AccountInstrumentKey, KeyState>();

        /// <inheritdoc/>
        public IReadOnlyList<AssembledLeg> Apply(in ExecutionRecord exec)
        {
            // A zero-quantity execution cannot change position; ignore defensively.
            if (exec.SignedQuantity == 0)
                return Array.Empty<AssembledLeg>();

            if (!_states.TryGetValue(exec.Key, out KeyState? st) || st is null)
            {
                st = new KeyState();
                _states[exec.Key] = st;
            }

            int before = st.NetPosition;
            int after = before + exec.SignedQuantity;

            // CASE 1 - Opening from flat: a brand-new trade.
            if (before == 0)
            {
                return One(OpenTrade(st, in exec, before, after,
                                     reversal: false, reversalGroup: Guid.Empty, legIndex: 0));
            }

            // CASE 2 - Closing to exactly flat: the trade ends here.
            if (after == 0)
            {
                return One(CloseTrade(st, in exec, before, after,
                                      reversal: false, reversalGroup: Guid.Empty, legIndex: 0));
            }

            int signBefore = Sign(before);
            int signAfter = Sign(after);

            // CASE 3 - Same direction, position changed but not closed: scale in or out.
            if (signBefore == signAfter)
            {
                int absBefore = before < 0 ? -before : before;
                int absAfter = after < 0 ? -after : after;
                ExecutionRole role = absAfter > absBefore ? ExecutionRole.ScaleIn : ExecutionRole.ScaleOut;

                var leg = new AssembledLeg(
                    TradeId: st.OpenTradeId,
                    ExecutionId: exec.ExecutionId,
                    LegIndex: 0,
                    Key: exec.Key,
                    Role: role,
                    TradeSide: st.OpenSide,
                    SignedQuantity: exec.SignedQuantity,
                    PositionBefore: before,
                    PositionAfter: after,
                    Price: exec.Price,
                    TimeUtc: exec.TimeUtc,
                    Origin: exec.Origin,
                    OpensTrade: false,
                    ClosesTrade: false,
                    IsReversalLeg: false,
                    ReversalGroupId: Guid.Empty);

                st.NetPosition = after;
                return One(leg);
            }

            // CASE 4 - Reversal: the execution crosses through flat to the opposite
            // side. Split into two legs sharing one reversal-group id:
            //   Leg A: the portion that flattens the existing trade  -> Exit.
            //   Leg B: the residual that opens the opposite trade     -> Entry.
            Guid group = Guid.NewGuid();

            // Leg A flattens: signed qty = -before (brings position to 0).
            var closeLeg = CloseTrade(st, MakeSyntheticLeg(in exec, -before), before, 0,
                                      reversal: true, reversalGroup: group, legIndex: 0);

            // Leg B opens the new side with the residual = after.
            var openLeg = OpenTrade(st, MakeSyntheticLeg(in exec, after), 0, after,
                                    reversal: true, reversalGroup: group, legIndex: 1);

            return new[] { closeLeg, openLeg };
        }

        /// <inheritdoc/>
        public int GetNetPosition(AccountInstrumentKey key)
            => _states.TryGetValue(key, out var st) ? st.NetPosition : 0;

        /// <inheritdoc/>
        public bool TryGetOpenTrade(AccountInstrumentKey key, out TradeId tradeId, out TradeSide side, out int absQuantity)
        {
            if (_states.TryGetValue(key, out var st) && st.HasOpenTrade)
            {
                tradeId = st.OpenTradeId;
                side = st.OpenSide;
                int net = st.NetPosition;
                absQuantity = net < 0 ? -net : net;
                return true;
            }
            tradeId = default;
            side = TradeSide.Flat;
            absQuantity = 0;
            return false;
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        /// <summary>Opens a new trade on the key and returns the Entry leg.</summary>
        private static AssembledLeg OpenTrade(
            KeyState st, in ExecutionRecord exec, int before, int after,
            bool reversal, Guid reversalGroup, int legIndex)
        {
            st.TradeOrdinal++;
            TradeId id = IdFactory.NewTradeId(exec.Key, exec.ExecutionId, st.TradeOrdinal);
            TradeSide side = after > 0 ? TradeSide.Long : TradeSide.Short;

            st.NetPosition = after;
            st.HasOpenTrade = true;
            st.OpenTradeId = id;
            st.OpenSide = side;

            return new AssembledLeg(
                TradeId: id,
                ExecutionId: exec.ExecutionId,
                LegIndex: legIndex,
                Key: exec.Key,
                Role: ExecutionRole.Entry,
                TradeSide: side,
                SignedQuantity: exec.SignedQuantity,
                PositionBefore: before,
                PositionAfter: after,
                Price: exec.Price,
                TimeUtc: exec.TimeUtc,
                Origin: exec.Origin,
                OpensTrade: true,
                ClosesTrade: false,
                IsReversalLeg: reversal,
                ReversalGroupId: reversalGroup);
        }

        /// <summary>Closes the open trade on the key and returns the Exit leg.</summary>
        private static AssembledLeg CloseTrade(
            KeyState st, in ExecutionRecord exec, int before, int after,
            bool reversal, Guid reversalGroup, int legIndex)
        {
            TradeId id = st.OpenTradeId;
            TradeSide side = st.OpenSide;

            var leg = new AssembledLeg(
                TradeId: id,
                ExecutionId: exec.ExecutionId,
                LegIndex: legIndex,
                Key: exec.Key,
                Role: ExecutionRole.Exit,
                TradeSide: side,
                SignedQuantity: exec.SignedQuantity,
                PositionBefore: before,
                PositionAfter: after,
                Price: exec.Price,
                TimeUtc: exec.TimeUtc,
                Origin: exec.Origin,
                OpensTrade: false,
                ClosesTrade: true,
                IsReversalLeg: reversal,
                ReversalGroupId: reversalGroup);

            st.NetPosition = after;            // 0
            st.HasOpenTrade = false;
            st.OpenTradeId = default;
            st.OpenSide = TradeSide.Flat;
            return leg;
        }

        /// <summary>
        /// Produces a copy of <paramref name="exec"/> with a substituted signed
        /// quantity, used to represent one half of a split reversal while keeping
        /// the original ExecutionId, price, time, and provenance intact.
        /// </summary>
        private static ExecutionRecord MakeSyntheticLeg(in ExecutionRecord exec, int signedQty)
            => exec with { SignedQuantity = signedQty };

        private static IReadOnlyList<AssembledLeg> One(AssembledLeg leg)
            => new[] { leg };

        private static int Sign(int x) => x > 0 ? 1 : (x < 0 ? -1 : 0);
    }
}
