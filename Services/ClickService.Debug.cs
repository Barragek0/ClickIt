using SharpDX;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private readonly object _clickDebugLock = new();
        private ClickDebugSnapshot _lastClickDebug = ClickDebugSnapshot.Empty;
        private readonly Queue<string> _clickDebugTrail = new();
        private long _clickDebugSequence;
        private const int ClickDebugTrailCapacity = 24;

        public sealed record ClickDebugSnapshot(
            bool HasData,
            string Stage,
            string MechanicId,
            string EntityPath,
            float Distance,
            Vector2 WorldScreenRaw,
            Vector2 WorldScreenAbsolute,
            Vector2 ResolvedClickPoint,
            bool Resolved,
            bool CenterInWindow,
            bool CenterClickable,
            bool ResolvedInWindow,
            bool ResolvedClickable,
            string Notes,
            long Sequence,
            long TimestampMs)
        {
            public static readonly ClickDebugSnapshot Empty = new(
                HasData: false,
                Stage: string.Empty,
                MechanicId: string.Empty,
                EntityPath: string.Empty,
                Distance: 0f,
                WorldScreenRaw: default,
                WorldScreenAbsolute: default,
                ResolvedClickPoint: default,
                Resolved: false,
                CenterInWindow: false,
                CenterClickable: false,
                ResolvedInWindow: false,
                ResolvedClickable: false,
                Notes: string.Empty,
                Sequence: 0,
                TimestampMs: 0);
        }

        public ClickDebugSnapshot GetLatestClickDebug()
        {
            lock (_clickDebugLock)
            {
                return _lastClickDebug;
            }
        }

        public IReadOnlyList<string> GetLatestClickDebugTrail()
        {
            lock (_clickDebugLock)
            {
                return _clickDebugTrail.ToArray();
            }
        }

        private void SetLatestClickDebug(ClickDebugSnapshot snapshot)
        {
            lock (_clickDebugLock)
            {
                long nextSequence = _clickDebugSequence + 1;
                _clickDebugSequence = nextSequence;

                ClickDebugSnapshot sequenced = snapshot with { Sequence = nextSequence };
                _lastClickDebug = sequenced;

                string trailEntry = $"{sequenced.Sequence:00000} {sequenced.Stage} | {sequenced.Notes}";
                _clickDebugTrail.Enqueue(trailEntry);
                while (_clickDebugTrail.Count > ClickDebugTrailCapacity)
                {
                    _clickDebugTrail.Dequeue();
                }
            }
        }
    }
}
