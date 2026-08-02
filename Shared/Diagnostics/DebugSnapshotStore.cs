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
        private readonly List<string> _trail = [];     // stores formatted entries
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

        /// <summary>
        /// Returns the internal trail list directly without allocation.
        /// The caller must not modify the returned list. Use this on hot
        /// render paths where the small allocation of GetTrail() adds up.
        /// </summary>
        internal List<string> GetTrailUnsafe()
        {
            lock (_lock)
            {
                return _trail;
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

                // Global dedup: each unique dedup key appears at most once
                // in the trail, with a running count.  When a message repeats
                // (even with other messages in between), the existing entry is
                // updated with the new count and moved to the end, preserving
                // the "most recently active" ordering.
                if (_dedupKeyExtractor != null)
                {
                    string newDedupKey = _dedupKeyExtractor(snapshot);
                    int existingIdx = FindDedupEntry(newDedupKey);
                    if (existingIdx >= 0)
                    {
                        string existing = _trail[existingIdx];
                        int count = ExtractCount(existing) + 1;
                        _trail.RemoveAt(existingIdx);
                        formatted = $"{formatted} (x{count})";
                    }
                }

                _trail.Add(formatted);
                while (_trail.Count > _trailCapacity)
                {
                    _trail.RemoveAt(0);
                }
            }
        }

        private int FindDedupEntry(string dedupKey)
        {
            for (int i = _trail.Count - 1; i >= 0; i--)
            {
                if (DebugSnapshotStore<TSnapshot>.StripDedupKey(_trail[i]) == dedupKey)
                    return i;
            }
            return -1;
        }

        private static string StripDedupKey(string formatted)
        {
            int spaceIdx = formatted.IndexOf(' ');
            if (spaceIdx > 0 && spaceIdx < 8)
                formatted = formatted[(spaceIdx + 1)..];

            int parenIdx = formatted.LastIndexOf(" (x", StringComparison.Ordinal);
            if (parenIdx > 0)
            {
                int closeParen = formatted.IndexOf(')', parenIdx);
                if (closeParen == formatted.Length - 1)
                    return formatted[..parenIdx];
            }
            return formatted;
        }

        private static int ExtractCount(string formatted)
        {
            int parenIdx = formatted.LastIndexOf(" (x", StringComparison.Ordinal);
            if (parenIdx > 0)
            {
                int closeParen = formatted.IndexOf(')', parenIdx);
                if (closeParen == formatted.Length - 1)
                {
                    int start = parenIdx + 3;
                    string numPart = formatted[start..closeParen];
                    if (int.TryParse(numPart, out int count))
                        return count;
                }
            }
            return 0;
        }
    }
}