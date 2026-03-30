using SharpDX;
using ClickIt.Services.Observability;
using ClickIt.Utils;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private const int ClickDebugTrailCapacity = 24;
        private const int RuntimeDebugLogTrailCapacity = 48;
        private const int UltimatumDebugTrailCapacity = 48;
        private readonly DebugSnapshotChannel<ClickDebugSnapshot, ClickDebugSnapshot> _clickDebugChannel = new(
            ClickDebugSnapshot.Empty,
            ClickDebugTrailCapacity,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot => $"{snapshot.Sequence:00000} {snapshot.Stage} | {snapshot.Notes}",
            static snapshot => snapshot);
        private readonly DebugSnapshotChannel<RuntimeDebugLogSnapshot, string> _runtimeDebugLogChannel = new(
            RuntimeDebugLogSnapshot.Empty,
            RuntimeDebugLogTrailCapacity,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot => $"{snapshot.Sequence:00000} {snapshot.Message}",
            static message => new RuntimeDebugLogSnapshot(
                HasData: true,
                Message: message,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        private readonly DebugSnapshotChannel<UltimatumDebugSnapshot, UltimatumDebugEvent> _ultimatumDebugChannel = new(
            UltimatumDebugSnapshot.Empty,
            UltimatumDebugTrailCapacity,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot =>
                $"{snapshot.Sequence:00000} {snapshot.Stage} [{snapshot.Source}] GG={snapshot.IsGruelingGauntletActive} Action={snapshot.Action} "
                + $"Sat={snapshot.HasSaturatedChoice}/{snapshot.SaturatedModifier} Reward={snapshot.ShouldTakeReward} Cands={snapshot.CandidateCount} "
                + $"Clicks C/Q/R={snapshot.ClickedChoice}/{snapshot.ClickedConfirm}/{snapshot.ClickedTakeRewards} | {snapshot.Notes}",
            debugEvent => new UltimatumDebugSnapshot(
                HasData: true,
                Stage: debugEvent.Stage,
                Source: debugEvent.Source,
                IsInitialUltimatumEnabled: settings.IsInitialUltimatumClickEnabled(),
                IsOtherUltimatumEnabled: settings.IsOtherUltimatumClickEnabled(),
                IsPanelVisible: debugEvent.IsPanelVisible,
                IsGruelingGauntletActive: debugEvent.IsGruelingGauntletActive,
                HasSaturatedChoice: debugEvent.HasSaturatedChoice,
                SaturatedModifier: debugEvent.SaturatedModifier ?? string.Empty,
                ShouldTakeReward: debugEvent.ShouldTakeReward,
                Action: debugEvent.Action ?? string.Empty,
                CandidateCount: debugEvent.CandidateCount,
                SaturatedCandidateCount: debugEvent.SaturatedCandidateCount,
                BestModifier: debugEvent.BestModifier ?? string.Empty,
                BestPriority: debugEvent.BestPriority,
                ClickedChoice: debugEvent.ClickedChoice,
                ClickedConfirm: debugEvent.ClickedConfirm,
                ClickedTakeRewards: debugEvent.ClickedTakeRewards,
                Notes: debugEvent.Notes ?? string.Empty,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));

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

        public sealed record UltimatumDebugSnapshot(
            bool HasData,
            string Stage,
            string Source,
            bool IsInitialUltimatumEnabled,
            bool IsOtherUltimatumEnabled,
            bool IsPanelVisible,
            bool IsGruelingGauntletActive,
            bool HasSaturatedChoice,
            string SaturatedModifier,
            bool ShouldTakeReward,
            string Action,
            int CandidateCount,
            int SaturatedCandidateCount,
            string BestModifier,
            int BestPriority,
            bool ClickedChoice,
            bool ClickedConfirm,
            bool ClickedTakeRewards,
            string Notes,
            long Sequence,
            long TimestampMs)
        {
            public static readonly UltimatumDebugSnapshot Empty = new(
                HasData: false,
                Stage: string.Empty,
                Source: string.Empty,
                IsInitialUltimatumEnabled: false,
                IsOtherUltimatumEnabled: false,
                IsPanelVisible: false,
                IsGruelingGauntletActive: false,
                HasSaturatedChoice: false,
                SaturatedModifier: string.Empty,
                ShouldTakeReward: false,
                Action: string.Empty,
                CandidateCount: 0,
                SaturatedCandidateCount: 0,
                BestModifier: string.Empty,
                BestPriority: int.MaxValue,
                ClickedChoice: false,
                ClickedConfirm: false,
                ClickedTakeRewards: false,
                Notes: string.Empty,
                Sequence: 0,
                TimestampMs: 0);
        }

        public ClickDebugSnapshot GetLatestClickDebug()
        {
            return _clickDebugChannel.GetLatest();
        }

        public IReadOnlyList<string> GetLatestClickDebugTrail()
        {
            return _clickDebugChannel.GetTrail();
        }

        public RuntimeDebugLogSnapshot GetLatestRuntimeDebugLog()
        {
            return _runtimeDebugLogChannel.GetLatest();
        }

        public IReadOnlyList<string> GetLatestRuntimeDebugLogTrail()
        {
            return _runtimeDebugLogChannel.GetTrail();
        }

        public UltimatumDebugSnapshot GetLatestUltimatumDebug()
        {
            return _ultimatumDebugChannel.GetLatest();
        }

        public IReadOnlyList<string> GetLatestUltimatumDebugTrail()
        {
            return _ultimatumDebugChannel.GetTrail();
        }

        private void SetLatestClickDebug(ClickDebugSnapshot snapshot)
        {
            if (!ShouldCaptureClickDebug())
                return;

            _clickDebugChannel.PublishSnapshot(snapshot);
        }

        private bool ShouldCaptureClickDebug()
        {
            return settings.DebugMode.Value && settings.DebugShowClicking.Value;
        }

        private bool ShouldCaptureUltimatumDebug()
        {
            return settings.DebugMode.Value && settings.DebugShowUltimatum.Value;
        }

        private void SetLatestUltimatumDebug(UltimatumDebugSnapshot snapshot)
        {
            if (!ShouldCaptureUltimatumDebug())
                return;

            _ultimatumDebugChannel.PublishSnapshot(snapshot);
        }

        private void PublishUltimatumDebug(UltimatumDebugEvent debugEvent)
        {
            if (!ShouldCaptureUltimatumDebug())
                return;

            _ultimatumDebugChannel.PublishEvent(debugEvent);
        }

        private void SetLatestRuntimeDebugLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            _runtimeDebugLogChannel.PublishEvent(message);
        }
    }
}
