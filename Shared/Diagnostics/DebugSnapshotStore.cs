namespace ClickIt.Shared.Diagnostics
{
    public sealed class DebugSnapshotStore<TSnapshot>(
        TSnapshot emptySnapshot,
        int trailCapacity,
        Func<TSnapshot, long, TSnapshot> withSequence,
        Func<TSnapshot, string> trailFormatter)
    {
        private readonly Lock _lock = new();
        private readonly Queue<string> _trail = new();
        private readonly int _trailCapacity = SystemMath.Max(1, trailCapacity);
        private readonly Func<TSnapshot, long, TSnapshot> _withSequence = withSequence ?? throw new ArgumentNullException(nameof(withSequence));
        private readonly Func<TSnapshot, string> _trailFormatter = trailFormatter ?? throw new ArgumentNullException(nameof(trailFormatter));
        private long _sequence;
        private TSnapshot _latest = emptySnapshot;

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

                _trail.Enqueue(_trailFormatter(sequenced));
                while (_trail.Count > _trailCapacity)
                {
                    _trail.Dequeue();
                }
            }
        }
    }
}