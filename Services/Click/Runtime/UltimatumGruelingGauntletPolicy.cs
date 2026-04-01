using ClickIt.Utils;

namespace ClickIt.Services.Click.Runtime
{
    internal enum GruelingGauntletAction
    {
        ConfirmOnly = 1,
        TakeRewards = 2
    }

    internal readonly record struct UltimatumGruelingSaturationSummary(
        bool HasSaturatedChoice,
        string SaturatedModifier,
        bool ShouldTakeReward,
        int SaturatedCandidateCount,
        GruelingGauntletAction Action)
    {
        internal static UltimatumGruelingSaturationSummary Empty
            => new(false, string.Empty, false, 0, GruelingGauntletAction.ConfirmOnly);
    }

    internal static class UltimatumGruelingGauntletPolicy
    {
        internal static GruelingGauntletAction DetermineAction(bool hasSaturatedChoice, bool shouldTakeReward, bool canClickTakeReward)
        {
            return hasSaturatedChoice && shouldTakeReward && canClickTakeReward
                ? GruelingGauntletAction.TakeRewards
                : GruelingGauntletAction.ConfirmOnly;
        }

        internal static UltimatumGruelingSaturationSummary ResolvePanelSaturationSummary(
            IReadOnlyList<UltimatumPanelChoiceCandidate> candidates,
            Func<string, bool> shouldTakeRewardForModifier,
            bool canClickTakeReward)
        {
            UltimatumPanelChoiceCollector.ResolveGruelingSaturation(
                candidates,
                shouldTakeRewardForModifier,
                out bool hasSaturatedChoice,
                out string saturatedModifier,
                out bool shouldTakeReward,
                out int saturatedCandidateCount);

            GruelingGauntletAction action = DetermineAction(hasSaturatedChoice, shouldTakeReward, canClickTakeReward);
            return new UltimatumGruelingSaturationSummary(
                hasSaturatedChoice,
                saturatedModifier,
                shouldTakeReward,
                saturatedCandidateCount,
                action);
        }

        internal static bool ShouldSuppressClick(bool shouldTakeReward, bool canClickTakeReward)
            => shouldTakeReward && !canClickTakeReward;

        internal static bool ShouldTreatChoiceAsSaturated(bool hasSaturationState, bool isSaturated, bool fallbackVisible)
            => hasSaturationState ? isSaturated : fallbackVisible;

        internal static bool TryReadChoiceSaturation(object choiceElement, out bool isSaturated)
        {
            isSaturated = false;
            if (!DynamicObjectAdapter.TryGetValue(choiceElement, s => s.IsSaturated, out object? rawSaturated) || rawSaturated == null)
                return false;

            if (rawSaturated is bool boolValue)
            {
                isSaturated = boolValue;
                return true;
            }

            try
            {
                isSaturated = Convert.ToBoolean(rawSaturated);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool ContainsAtlasPassiveSkillId(object atlasPassiveIds, int targetId)
        {
            foreach (object? entry in UltimatumUiTreeResolver.EnumerateObjects(atlasPassiveIds))
            {
                if (entry == null)
                    continue;

                if (entry is int intId && intId == targetId)
                    return true;

                try
                {
                    int converted = Convert.ToInt32(entry);
                    if (converted == targetId)
                        return true;
                }
                catch
                {
                }
            }

            return false;
        }
    }
}