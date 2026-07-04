// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Core - SnapshotRegistry.cs   (PHASE 2)
//
//  PURPOSE:
//      Persistent ISnapshotRegistry: a per-instrument rolling history of immutable
//      MarketSnapshots (current + previous + bounded history), plus a process-wide
//      default (SnapshotHub) shared by the indicator (producer) and the AddOn /
//      research (consumers) within the NinjaTrader process.
//
//  DESIGN:
//      Each instrument has a fixed-capacity ring buffer guarded by its own lock.
//      Publish is O(1) (write head + advance). Reads (Current/Previous/History) take
//      the lock briefly and return immutable snapshots / a stable copy. Publishing
//      happens ~once per bar and reads at fill/research time, so contention is low.
//      Because MarketSnapshot is immutable, returned references are safe to share.
//
//  THREAD SAFETY:  Fully concurrent across instruments (ConcurrentDictionary) and
//      within an instrument (per-stream lock).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using KeneticTradeRecorder.Shared;

namespace KeneticTradeRecorder.Core
{
    /// <summary>Rolling per-instrument snapshot history with current/previous accessors.</summary>
    public sealed class SnapshotRegistry : ISnapshotRegistry
    {
        /// <summary>Default per-instrument history depth when none is specified.</summary>
        public const int DefaultCapacity = 512;

        private readonly int _capacity;
        private readonly ConcurrentDictionary<string, Stream> _streams =
            new ConcurrentDictionary<string, Stream>(StringComparer.Ordinal);

        public SnapshotRegistry(int historyCapacity = DefaultCapacity)
        {
            if (historyCapacity < 2) historyCapacity = 2;   // need at least current + previous
            _capacity = historyCapacity;
        }

        /// <inheritdoc/>
        public int HistoryCapacity => _capacity;

        /// <inheritdoc/>
        public void Publish(MarketSnapshot snapshot)
        {
            if (snapshot is null || string.IsNullOrEmpty(snapshot.Symbol)) return;
            var stream = _streams.GetOrAdd(snapshot.Symbol, _ => new Stream(_capacity));
            stream.Add(snapshot);
        }

        /// <inheritdoc/>
        public MarketSnapshot? Current(string instrument) =>
            !string.IsNullOrEmpty(instrument) && _streams.TryGetValue(instrument, out var s) ? s.Current() : null;

        /// <inheritdoc/>
        public MarketSnapshot? Previous(string instrument) =>
            !string.IsNullOrEmpty(instrument) && _streams.TryGetValue(instrument, out var s) ? s.Previous() : null;

        /// <inheritdoc/>
        public IReadOnlyList<MarketSnapshot> History(string instrument) =>
            !string.IsNullOrEmpty(instrument) && _streams.TryGetValue(instrument, out var s)
                ? s.SnapshotNewestFirst()
                : Array.Empty<MarketSnapshot>();

        /// <inheritdoc/>
        public bool HasContext(string instrument) =>
            !string.IsNullOrEmpty(instrument) && _streams.ContainsKey(instrument);

        /// <inheritdoc/>
        public int Count => _streams.Count;

        /// <inheritdoc/>
        public IReadOnlyCollection<string> Instruments => new List<string>(_streams.Keys);

        /// <summary>Clears all instruments. Intended for tests/reset.</summary>
        public void Clear() => _streams.Clear();

        // -- per-instrument circular buffer (newest at _head) --
        private sealed class Stream
        {
            private readonly object _gate = new object();
            private readonly MarketSnapshot[] _ring;
            private int _count;
            private int _head = -1;   // index of most recent; -1 = empty

            public Stream(int capacity) { _ring = new MarketSnapshot[capacity]; }

            public void Add(MarketSnapshot s)
            {
                lock (_gate)
                {
                    _head = (_head + 1) % _ring.Length;
                    _ring[_head] = s;
                    if (_count < _ring.Length) _count++;
                }
            }

            public MarketSnapshot? Current()
            {
                lock (_gate) { return _count > 0 ? _ring[_head] : null; }
            }

            public MarketSnapshot? Previous()
            {
                lock (_gate)
                {
                    if (_count < 2) return null;
                    int p = (_head - 1 + _ring.Length) % _ring.Length;
                    return _ring[p];
                }
            }

            public IReadOnlyList<MarketSnapshot> SnapshotNewestFirst()
            {
                lock (_gate)
                {
                    var outp = new MarketSnapshot[_count];
                    for (int i = 0; i < _count; i++)
                        outp[i] = _ring[(_head - i + _ring.Length) % _ring.Length];
                    return outp;
                }
            }
        }
    }

    /// <summary>
    /// Process-wide default snapshot registry. The indicator publishes to Instance;
    /// the AddOn and research read from Instance. Set <see cref="DefaultHistoryCapacity"/>
    /// BEFORE first access to configure the rolling-history depth.
    /// </summary>
    public static class SnapshotHub
    {
        private static readonly object _gate = new object();
        private static ISnapshotRegistry? _instance;

        /// <summary>History depth used when the hub instance is first created. Default 512.</summary>
        public static int DefaultHistoryCapacity { get; set; } = SnapshotRegistry.DefaultCapacity;

        /// <summary>The shared default registry (created lazily on first access).</summary>
        public static ISnapshotRegistry Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (_gate) { return _instance ??= new SnapshotRegistry(DefaultHistoryCapacity); }
            }
        }
    }
}
