using ClickIt.Shared;

namespace ClickIt.Features.Observability
{
    internal sealed class DebugSnapshotChannel<TSnapshot, TEvent>
    {
        private readonly DebugSnapshotStore<TSnapshot> _store;
        private readonly Func<TEvent, TSnapshot> _eventToSnapshot;

        public DebugSnapshotChannel(
            TSnapshot emptySnapshot,
            int trailCapacity,
            Func<TSnapshot, long, TSnapshot> withSequence,
            Func<TSnapshot, string> formatSnapshot,
            Func<TEvent, TSnapshot> eventToSnapshot)
        {
            _store = new DebugSnapshotStore<TSnapshot>(
                emptySnapshot,
                trailCapacity,
                withSequence,
                formatSnapshot);
            _eventToSnapshot = eventToSnapshot;
        }

        public TSnapshot GetLatest()
            => _store.GetLatest();

        public IReadOnlyList<string> GetTrail()
            => _store.GetTrail();

        public void PublishSnapshot(TSnapshot snapshot)
            => _store.SetLatest(snapshot);

        public void PublishEvent(TEvent debugEvent)
            => _store.SetLatest(_eventToSnapshot(debugEvent));
    }
}