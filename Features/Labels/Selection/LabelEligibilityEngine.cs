namespace ClickIt.Features.Labels.Selection
{
    internal static class LabelEligibilityEngine
    {
        internal static bool TryBuildCandidate(
            LabelOnGround label,
            ClickSettings clickSettings,
            Func<LabelOnGround, Entity, bool> isTargetableForClick,
            Func<LabelOnGround, Entity, ClickSettings, string?> resolveMechanicId,
            out Entity? item,
            out string? mechanicId,
            out LabelCandidateRejectReason rejectReason)
        {
            item = DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                ? rawItem as Entity
                : null;
            mechanicId = null;
            rejectReason = LabelCandidateRejectReason.None;

            if (item == null)
            {
                rejectReason = LabelCandidateRejectReason.NullItem;
                return false;
            }

            if (!DynamicAccess.TryReadFloat(item, DynamicAccessProfiles.DistancePlayer, out float distance)
                || distance > clickSettings.ClickDistance)
            {
                rejectReason = LabelCandidateRejectReason.OutOfDistance;
                return false;
            }

            if (!isTargetableForClick(label, item))
            {
                rejectReason = LabelCandidateRejectReason.Untargetable;
                return false;
            }

            mechanicId = resolveMechanicId(label, item, clickSettings);
            if (string.IsNullOrWhiteSpace(mechanicId))
            {
                rejectReason = LabelCandidateRejectReason.NoMechanic;
                return false;
            }

            return true;
        }
    }
}