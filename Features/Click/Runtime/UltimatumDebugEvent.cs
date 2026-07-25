namespace ClickIt.Features.Click.Runtime
{
    internal sealed record UltimatumDebugEvent(
        string Stage,
        string Source,
        bool IsPanelVisible,
        bool IsGruelingGauntletActive)
    {
        public bool HasSaturatedChoice { get; init; }
        public string? SaturatedModifier { get; init; }
        public bool ShouldTakeReward { get; init; }
        public string? Action { get; init; }
        public int CandidateCount { get; init; }
        public int SaturatedCandidateCount { get; init; }
        public string? BestModifier { get; init; }
        public int BestPriority { get; init; } = int.MaxValue;
        public bool ClickedChoice { get; init; }
        public bool ClickedConfirm { get; init; }
        public bool ClickedTakeRewards { get; init; }
        public string? Notes { get; init; }
    }
}