namespace ClickIt.Shared.Diagnostics
{
    public sealed class DebugSnapshotStore<TSnapshot>(
        TSnapshot emptySnapshot,
        int trailCapacity,
        Func<TSnapshot, long, TSnapshot> withSequence,
        Func<TSnapshot, string> trailFormatter,
        Func<TSnapshot, string>? dedupKeyExtractor = null)
    {
        private readonly Lock _lock = new();
        private readonly List<string> _trail = [];
        private readonly List<string>? _dedupKeys = dedupKeyExtractor != null ? [] : null;
        private readonly int _trailCapacity = SystemMath.Max(1, trailCapacity);
        private readonly Func<TSnapshot, long, TSnapshot> _withSequence = withSequence ?? throw new ArgumentNullException(nameof(withSequence));
        private readonly Func<TSnapshot, string> _trailFormatter = trailFormatter ?? throw new ArgumentNullException(nameof(trailFormatter));
        private readonly Func<TSnapshot, string>? _dedupKeyExtractor = dedupKeyExtractor;
        private long _sequence;
        private TSnapshot _latest = emptySnapshot;

        public TSnapshot GetLatest()
        {
            lock (_lock)
            {
                return _latest;
            }
        }

        /// <summary>
        /// Returns a snapshot of the trail as a new array. Callers should
        /// access this at most once per frame to avoid repeated allocations.
        /// </summary>
        public IReadOnlyList<string> GetTrail()
        {
            lock (_lock)
            {
                return [.. _trail];
            }
        }

        public void SetLatest(TSnapshot snapshot)
        {
            lock (_lock)
            {
                long nextSequence = _sequence + 1;
                _sequence = nextSequence;

                TSnapshot sequenced = _withSequence(snapshot, nextSequence);
                _latest = sequenced;

                string formatted = _trailFormatter(sequenced);
                // Global dedup: each key appears at most once with a running count, moved to the end on repeat.
                if (_dedupKeys != null && _dedupKeyExtractor != null)
                {
                    string dedupKey = _dedupKeyExtractor(snapshot);
                    int existingIdx = _dedupKeys.LastIndexOf(dedupKey);
                    if (existingIdx >= 0)
                    {
                        string existing = _trail[existingIdx];
                        // A suffix-less entry represents count 1 (the first occurrence); DedupSuffix returns 0 for it, so the first repeat must yield x2, not x1.
                        int count = SystemMath.Max(1, DedupSuffix.TryGetCount(existing, out int existingCount) ? existingCount : 0) + 1;
                        _trail.RemoveAt(existingIdx);
                        _dedupKeys.RemoveAt(existingIdx);
                        formatted = $"{formatted} (x{count})";
                    }
                    _dedupKeys.Add(dedupKey);
                }

                _trail.Add(formatted);
                while (_trail.Count > _trailCapacity)
                {
                    _trail.RemoveAt(0);
                    _dedupKeys?.RemoveAt(0);
                }
            }
        }
    }
}
