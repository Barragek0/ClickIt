using SharpDX;
using ClickIt.Utils;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private const int ClickDebugTrailCapacity = 24;
        private const int RuntimeDebugLogTrailCapacity = 48;
        private readonly DebugSnapshotStore<ClickDebugSnapshot> _clickDebugStore = new(
            ClickDebugSnapshot.Empty,
            ClickDebugTrailCapacity,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot => $"{snapshot.Sequence:00000} {snapshot.Stage} | {snapshot.Notes}");
        private readonly DebugSnapshotStore<RuntimeDebugLogSnapshot> _runtimeDebugLogStore = new(
            RuntimeDebugLogSnapshot.Empty,
            RuntimeDebugLogTrailCapacity,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot => $"{snapshot.Sequence:00000} {snapshot.Message}");

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

        public sealed record RuntimeDebugLogSnapshot(
            bool HasData,
            string Message,
            long Sequence,
            long TimestampMs)
        {
            public static readonly RuntimeDebugLogSnapshot Empty = new(
                HasData: false,
                Message: string.Empty,
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

        public RuntimeDebugLogSnapshot GetLatestRuntimeDebugLog()
        {
            return _runtimeDebugLogStore.GetLatest();
        }

        public IReadOnlyList<string> GetLatestRuntimeDebugLogTrail()
        {
            return _runtimeDebugLogStore.GetTrail();
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

        private void SetLatestRuntimeDebugLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            _runtimeDebugLogStore.SetLatest(new RuntimeDebugLogSnapshot(
                HasData: true,
                Message: message,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        }
    }
}
