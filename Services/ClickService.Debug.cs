using SharpDX;
using ClickIt.Utils;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private const int ClickDebugTrailCapacity = 24;
        private readonly DebugSnapshotStore<ClickDebugSnapshot> _clickDebugStore = new(
            ClickDebugSnapshot.Empty,
            ClickDebugTrailCapacity,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot => $"{snapshot.Sequence:00000} {snapshot.Stage} | {snapshot.Notes}");

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
            return _clickDebugStore.GetLatest();
        }

        public IReadOnlyList<string> GetLatestClickDebugTrail()
        {
            return _clickDebugStore.GetTrail();
        }

        private void SetLatestClickDebug(ClickDebugSnapshot snapshot)
        {
            if (!ShouldCaptureClickDebug())
                return;

            _clickDebugStore.SetLatest(snapshot);
        }

        private bool ShouldCaptureClickDebug()
        {
            return settings.DebugMode.Value && settings.DebugShowClicking.Value;
        }
    }
}
