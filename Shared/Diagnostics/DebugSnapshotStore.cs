namespace ClickIt.Shared.Diagnostics
{
    public sealed class DebugSnapshotStore<TSnapshot>
    {
        private readonly object _lock = new();
        private readonly Queue<string> _trail = new();
        private readonly int _trailCapacity;
        private readonly Func<TSnapshot, long, TSnapshot> _withSequence;
        private readonly Func<TSnapshot, string> _trailFormatter;
        private long _sequence;
        private TSnapshot _latest;

        public DebugSnapshotStore(
            TSnapshot emptySnapshot,
            int trailCapacity,
            Func<TSnapshot, long, TSnapshot> withSequence,
            Func<TSnapshot, string> trailFormatter)
        {
            _latest = emptySnapshot;
            _trailCapacity = global::System.Math.Max(1, trailCapacity);
            _withSequence = withSequence ?? throw new ArgumentNullException(nameof(withSequence));
            _trailFormatter = trailFormatter ?? throw new ArgumentNullException(nameof(trailFormatter));
        }

        public TSnapshot GetLatest()
        {
            lock (_lock)
            {
                return _latest;
            }
        }

        public IReadOnlyList<string> GetTrail()
        {
            lock (_lock)
            {
                return _trail.ToArray();
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

                _trail.Enqueue(_trailFormatter(sequenced));
                while (_trail.Count > _trailCapacity)
                {
                    _trail.Dequeue();
                }
            }
        }
    }
}