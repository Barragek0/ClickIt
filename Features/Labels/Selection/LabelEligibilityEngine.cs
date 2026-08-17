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
            DynamicAccess.TryGetLabelItemOnGround(label, out item);
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

            // A locked strongbox (the strongbox overlay's red frame) cannot be opened, so it is never a click candidate even when a mechanic would otherwise match.
            if (MechanicClassifier.IsLockedStrongbox(item))
            {
                rejectReason = LabelCandidateRejectReason.LockedChest;
                return false;
            }

            mechanicId = resolveMechanicId(label, item, clickSettings);
            if (string.IsNullOrWhiteSpace(mechanicId))
            {
                rejectReason = LabelCandidateRejectReason.NoMechanic;
                return false;
            }

            // When lifeforce estimation is active, reject all harvest labels here so they don't go through the normal label pipeline. The dedicated click path in InteractionExecutionEngine handles harvest clicking directly (like the altar pattern).
            if (clickSettings.HarvestLabelSelectionBlocked
                && string.Equals(mechanicId, MechanicIds.Harvest, StringComparison.OrdinalIgnoreCase))
            {
                rejectReason = LabelCandidateRejectReason.NoMechanic;
                return false;
            }

            return true;
        }
    }
}
