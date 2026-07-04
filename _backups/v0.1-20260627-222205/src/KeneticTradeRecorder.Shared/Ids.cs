// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Shared - Ids.cs
//
//  PURPOSE:        Strongly-typed identifiers (so an ExecutionId can never be
//                  passed where a TradeId is expected) and a DETERMINISTIC trade
//                  id generator. Determinism matters: TradeIds must be
//                  reproducible if the same execution stream is replayed from raw
//                  data. We therefore avoid String.GetHashCode (randomized per
//                  process in modern .NET) and use FNV-1a instead.
//  THREAD SAFETY:  All types here are immutable value types / pure functions.
// -----------------------------------------------------------------------------
using System;

namespace KeneticTradeRecorder.Shared
{
    /// <summary>Stable identifier for an assembled trade (round trip flat -&gt; ... -&gt; flat).</summary>
    public readonly record struct TradeId(string Value)
    {
        public override string ToString() => Value;
    }

    /// <summary>Identifier of a single broker execution (sourced from NinjaTrader Execution.ExecutionId).</summary>
    public readonly record struct ExecutionId(string Value)
    {
        public override string ToString() => Value;
    }

    /// <summary>Identifier of the order an execution filled against (NinjaTrader Order.OrderId).</summary>
    public readonly record struct OrderId(string Value)
    {
        public override string ToString() => Value;
    }

    /// <summary>
    /// The unit of position bookkeeping: a single account trading a single instrument.
    /// Positions, trades, and net size are always tracked per (Account, Instrument).
    /// </summary>
    public readonly record struct AccountInstrumentKey(string Account, string Instrument)
    {
        public override string ToString() => $"{Account}/{Instrument}";
    }

    /// <summary>Deterministic identifier construction. Pure; no hidden state.</summary>
    public static class IdFactory
    {
        /// <summary>
        /// Builds a stable, reproducible TradeId from the trade's opening execution.
        /// Same (key, openingExecutionId) =&gt; same TradeId on every replay.
        /// </summary>
        /// <param name="key">Account + instrument the trade belongs to.</param>
        /// <param name="openingExecutionId">ExecutionId of the execution that opened the trade.</param>
        /// <param name="perKeyOrdinal">Human-readable sequence number within the key (cosmetic only).</param>
        public static TradeId NewTradeId(AccountInstrumentKey key, ExecutionId openingExecutionId, long perKeyOrdinal)
        {
            string seed = key.Account + "|" + key.Instrument + "|" + openingExecutionId.Value;
            string hash = Fnv1a64Hex(seed);
            string instrumentTag = Sanitize(key.Instrument);
            return new TradeId($"T-{instrumentTag}-{perKeyOrdinal:D4}-{hash}");
        }

        /// <summary>FNV-1a 64-bit hash rendered as lowercase hex. Deterministic across processes and platforms.</summary>
        public static string Fnv1a64Hex(string input)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            // Hash UTF-8 bytes so the result is encoding-stable.
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input ?? string.Empty);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }
            return hash.ToString("x16");
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "UNK";
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (!ok) chars[i] = '_';
            }
            return new string(chars);
        }
    }
}
