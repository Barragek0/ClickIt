namespace ClickIt.Features.Observability
{
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
}