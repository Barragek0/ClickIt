namespace ClickIt.Features.Observability
{
    internal sealed class ClickTelemetryStore
    {
        private const int ClickDebugTrailCapacity = 100;
        private const int RuntimeDebugLogTrailCapacity = 80;
        private const int UltimatumDebugTrailCapacity = 80;
        private readonly ClickItSettings _settings;
        private readonly DebugSnapshotChannel<ClickDebugSnapshot, ClickDebugSnapshot> _clickDebugChannel;
        private readonly DebugSnapshotChannel<RuntimeDebugLogSnapshot, string> _runtimeDebugLogChannel;
        private readonly DebugSnapshotChannel<RuntimeDebugLogSnapshot, string> _craftingLogChannel;
        private readonly DebugSnapshotChannel<UltimatumDebugSnapshot, UltimatumDebugEvent> _ultimatumDebugChannel;

        public ClickTelemetryStore(ClickItSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _clickDebugChannel = new DebugSnapshotChannel<ClickDebugSnapshot, ClickDebugSnapshot>(
                ClickDebugSnapshot.Empty,
                ClickDebugTrailCapacity,
                static (snapshot, sequence) => snapshot with { Sequence = sequence },
                static snapshot => $"{snapshot.Sequence:00000} {snapshot.Stage} | {snapshot.Notes}",
                static snapshot => snapshot,
                dedupKeyExtractor: static snapshot => $"{snapshot.Stage} | {snapshot.Notes}");

            _runtimeDebugLogChannel = CreateLogChannel(RuntimeDebugLogTrailCapacity);
            _craftingLogChannel = CreateLogChannel(40);

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
        internal IReadOnlyList<string> GetCraftingTrail() => _craftingLogChannel.GetTrail();
        internal void PublishCraftingLog(string message) => _craftingLogChannel.PublishEvent(message);

        private static DebugSnapshotChannel<RuntimeDebugLogSnapshot, string> CreateLogChannel(int capacity)
            => new(RuntimeDebugLogSnapshot.Empty, capacity,
                static (snapshot, sequence) => snapshot with { Sequence = sequence },
                static snapshot => $"{snapshot.Sequence:00000} {snapshot.Message}",
                static message => new RuntimeDebugLogSnapshot(true, message, 0, Environment.TickCount64),
                dedupKeyExtractor: static snapshot => snapshot.Message);

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
