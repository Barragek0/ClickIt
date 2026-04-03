namespace ClickIt.Features.Click.Runtime
{
    internal readonly record struct UltimatumGruelingGroundDecision(
        UltimatumGruelingSaturationSummary Saturation,
        bool HasBestChoice,
        Element? BestChoiceElement,
        string BestModifier,
        int BestPriority)
    {
        internal static UltimatumGruelingGroundDecision Empty
            => new(UltimatumGruelingSaturationSummary.Empty, false, null, string.Empty, int.MaxValue);
    }

    internal static class UltimatumGruelingGroundDecisionEngine
    {
        internal static UltimatumGruelingGroundDecision Resolve(
            IReadOnlyList<UltimatumGroundOptionCandidate> candidates,
            bool isGruelingGauntletActive,
            Func<string, bool> shouldTakeRewardForModifier,
            bool canClickTakeReward)
        {
            UltimatumGruelingSaturationSummary saturation = UltimatumGruelingSaturationSummary.Empty;
            if (isGruelingGauntletActive)
            {
                bool hasSaturatedChoice = UltimatumGroundOptionSelector.TryGetFirstSaturated(candidates, out UltimatumGroundOptionCandidate saturatedChoice);
                bool shouldTakeReward = hasSaturatedChoice
                    && shouldTakeRewardForModifier(saturatedChoice.ModifierName);

                GruelingGauntletAction action = UltimatumGruelingGauntletPolicy.DetermineAction(
                    hasSaturatedChoice,
                    shouldTakeReward,
                    canClickTakeReward);

                saturation = new UltimatumGruelingSaturationSummary(
                    hasSaturatedChoice,
                    hasSaturatedChoice ? saturatedChoice.ModifierName : string.Empty,
                    shouldTakeReward,
                    hasSaturatedChoice ? 1 : 0,
                    action);
            }

            bool hasBestChoice = UltimatumGroundOptionSelector.TryGetSelected(candidates, isGruelingGauntletActive, out UltimatumGroundOptionCandidate best);
            return new UltimatumGruelingGroundDecision(
                saturation,
                hasBestChoice,
                hasBestChoice ? best.OptionElement : null,
                hasBestChoice ? best.ModifierName : string.Empty,
                hasBestChoice ? best.PriorityIndex : int.MaxValue);
        }
    }
}