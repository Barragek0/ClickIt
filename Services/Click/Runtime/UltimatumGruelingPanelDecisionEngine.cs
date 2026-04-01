using ExileCore.PoEMemory;

namespace ClickIt.Services.Click.Runtime
{
    internal readonly record struct UltimatumGruelingPanelDecision(
        UltimatumGruelingSaturationSummary Saturation,
        bool HasBestChoice,
        Element? BestChoiceElement,
        string BestModifier,
        int BestPriority)
    {
        internal static UltimatumGruelingPanelDecision Empty
            => new(UltimatumGruelingSaturationSummary.Empty, false, null, string.Empty, int.MaxValue);
    }

    internal static class UltimatumGruelingPanelDecisionEngine
    {
        internal static UltimatumGruelingPanelDecision Resolve(
            IReadOnlyList<UltimatumPanelChoiceCandidate> candidates,
            bool isGruelingGauntletActive,
            Func<string, bool> shouldTakeRewardForModifier,
            bool canClickTakeReward)
        {
            UltimatumGruelingSaturationSummary saturation = UltimatumGruelingGauntletPolicy.ResolvePanelSaturationSummary(
                candidates,
                shouldTakeRewardForModifier,
                canClickTakeReward);

            bool hasBestChoice = UltimatumPanelChoiceSelector.TryGetSelected(candidates, isGruelingGauntletActive, out UltimatumPanelChoiceCandidate best);
            return new UltimatumGruelingPanelDecision(
                saturation,
                hasBestChoice,
                hasBestChoice ? best.ChoiceElement : null,
                hasBestChoice ? best.ModifierName : string.Empty,
                hasBestChoice ? best.PriorityIndex : int.MaxValue);
        }
    }
}