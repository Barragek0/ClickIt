using ClickIt.Services.Click.Runtime;
using System;
using System.Collections.Generic;

namespace ClickIt.Services.Observability
{
    internal sealed class ClickTelemetryStore
    {
        private const int ClickDebugTrailCapacity = 24;
        private const int RuntimeDebugLogTrailCapacity = 48;
        private const int UltimatumDebugTrailCapacity = 48;
        private readonly ClickItSettings _settings;
        private readonly DebugSnapshotChannel<ClickDebugSnapshot, ClickDebugSnapshot> _clickDebugChannel;
        private readonly DebugSnapshotChannel<RuntimeDebugLogSnapshot, string> _runtimeDebugLogChannel;
        private readonly DebugSnapshotChannel<UltimatumDebugSnapshot, UltimatumDebugEvent> _ultimatumDebugChannel;

        public ClickTelemetryStore(ClickItSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _clickDebugChannel = new DebugSnapshotChannel<ClickDebugSnapshot, ClickDebugSnapshot>(
                ClickDebugSnapshot.Empty,
                ClickDebugTrailCapacity,
                static (snapshot, sequence) => snapshot with { Sequence = sequence },
                static snapshot => $"{snapshot.Sequence:00000} {snapshot.Stage} | {snapshot.Notes}",
                static snapshot => snapshot);

            _runtimeDebugLogChannel = new DebugSnapshotChannel<RuntimeDebugLogSnapshot, string>(
                RuntimeDebugLogSnapshot.Empty,
                RuntimeDebugLogTrailCapacity,
                static (snapshot, sequence) => snapshot with { Sequence = sequence },
                static snapshot => $"{snapshot.Sequence:00000} {snapshot.Message}",
                static message => new RuntimeDebugLogSnapshot(
                    HasData: true,
                    Message: message,
                    Sequence: 0,
                    TimestampMs: Environment.TickCount64));

            _ultimatumDebugChannel = new DebugSnapshotChannel<UltimatumDebugSnapshot, UltimatumDebugEvent>(
                UltimatumDebugSnapshot.Empty,
                UltimatumDebugTrailCapacity,
                static (snapshot, sequence) => snapshot with { Sequence = sequence },
                static snapshot =>
                    $"{snapshot.Sequence:00000} {snapshot.Stage} [{snapshot.Source}] GG={snapshot.IsGruelingGauntletActive} Action={snapshot.Action} "
                    + $"Sat={snapshot.HasSaturatedChoice}/{snapshot.SaturatedModifier} Reward={snapshot.ShouldTakeReward} Cands={snapshot.CandidateCount} "
                    + $"Clicks C/Q/R={snapshot.ClickedChoice}/{snapshot.ClickedConfirm}/{snapshot.ClickedTakeRewards} | {snapshot.Notes}",
                BuildUltimatumSnapshotFromEvent);
        }

        public ClickDebugSnapshot GetLatestClickDebug() => _clickDebugChannel.GetLatest();

        public IReadOnlyList<string> GetLatestClickDebugTrail() => _clickDebugChannel.GetTrail();

        public RuntimeDebugLogSnapshot GetLatestRuntimeDebugLog() => _runtimeDebugLogChannel.GetLatest();

        public IReadOnlyList<string> GetLatestRuntimeDebugLogTrail() => _runtimeDebugLogChannel.GetTrail();

        public UltimatumDebugSnapshot GetLatestUltimatumDebug() => _ultimatumDebugChannel.GetLatest();

        public IReadOnlyList<string> GetLatestUltimatumDebugTrail() => _ultimatumDebugChannel.GetTrail();

        public void PublishClickSnapshot(ClickDebugSnapshot snapshot) => _clickDebugChannel.PublishSnapshot(snapshot);

        public void PublishUltimatumSnapshot(UltimatumDebugSnapshot snapshot) => _ultimatumDebugChannel.PublishSnapshot(snapshot);

        public void PublishUltimatumEvent(UltimatumDebugEvent debugEvent) => _ultimatumDebugChannel.PublishEvent(debugEvent);

        public void PublishRuntimeLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            _runtimeDebugLogChannel.PublishEvent(message);
        }

        private UltimatumDebugSnapshot BuildUltimatumSnapshotFromEvent(UltimatumDebugEvent debugEvent)
        {
            return new UltimatumDebugSnapshot(
                HasData: true,
                Stage: debugEvent.Stage,
                Source: debugEvent.Source,
                IsInitialUltimatumEnabled: _settings.IsInitialUltimatumClickEnabled(),
                IsOtherUltimatumEnabled: _settings.IsOtherUltimatumClickEnabled(),
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
                TimestampMs: Environment.TickCount64);
        }
    }
}