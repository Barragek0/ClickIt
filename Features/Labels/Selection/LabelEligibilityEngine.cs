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

            if (!isTargetableForClick(label, item))
            {
                rejectReason = LabelCandidateRejectReason.Untargetable;
                return false;
            }

            // Locked strongboxes and Delve chests cannot be opened. Delve also needs a path check because its entity type can differ while the Chest component is present.
            // Check entity type first for the general strongbox rule to avoid component reads on most labels.
            EntityType entityType = DynamicAccess.TryGetDynamicValue(item, DynamicAccessProfiles.Type, out object? rawType)
                && rawType is EntityType resolvedType
                ? resolvedType
                : default;

            if ((entityType == EntityType.Chest && MechanicClassifier.IsLockedStrongbox(item))
                || (!clickSettings.DelveEncounterComplete && MechanicClassifier.IsLockedDelveChest(item)))
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

            return true;
        }
    }
}
