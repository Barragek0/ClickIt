namespace ClickIt.Features.Labels.Selection
{
    internal static class LabelTargetabilityPolicy
    {
        public static bool IsEntityTargetableForClick(LabelOnGround label, Entity item)
        {
            string path = DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;

            // Fast path: WorldItem entities without settlers markers are always targetable.
            // This skips the expensive DLR reads in settlers targetability checks for the common case.
            EntityType type = DynamicAccess.TryGetDynamicValue(item, DynamicAccessProfiles.Type, out object? rawType)
                && rawType is EntityType resolvedType
                ? resolvedType
                : default;

            if (type == EntityType.WorldItem
                && !MechanicClassifier.IsSettlersOrePath(path)
                && !MechanicClassifier.IsSettlersPetrifiedWoodPath(path))
            {
                return true;
            }

            if (!RequiresTargetabilityGate(path))
                return true;
            if (!ShouldApplyPetrifiedWoodEntityTargetabilityGate(path))
                return true;

            ResolveLabelEntityTargetableForClick(label, out bool hasLabelEntityTargetable, out bool labelEntityTargetable);
            return ShouldAllowPetrifiedWoodTargetability(hasLabelEntityTargetable, labelEntityTargetable);
        }

        public static bool RequiresTargetabilityGate(string path)
            => !string.IsNullOrEmpty(path) && MechanicClassifier.IsSettlersOrePath(path);

        public static bool ShouldApplyPetrifiedWoodEntityTargetabilityGate(string? path)
            => !string.IsNullOrWhiteSpace(path) && MechanicClassifier.IsSettlersPetrifiedWoodPath(path);

        public static bool ShouldAllowPetrifiedWoodTargetability(bool hasLabelEntityTargetable, bool labelEntityTargetable)
            => !hasLabelEntityTargetable || labelEntityTargetable;

        public static void ResolveLabelEntityTargetableForClick(LabelOnGround label, out bool hasLabelEntityTargetable, out bool labelEntityTargetable)
        {
            hasLabelEntityTargetable = false;
            labelEntityTargetable = true;

            DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item);
            if (item != null)
            {
                Targetable? targetable = item.GetComponent<Targetable>();
                if (targetable != null)
                {
                    hasLabelEntityTargetable = true;
                    labelEntityTargetable = targetable.isTargetable;
                    return;
                }
            }

            if (TryResolveLabelBackedEntityTargetable(label, out bool resolvedTargetable, out bool resolvedHasTargetable)
                && resolvedHasTargetable)
            {
                hasLabelEntityTargetable = true;
                labelEntityTargetable = resolvedTargetable;
            }
        }

        private static bool TryResolveLabelBackedEntityTargetable(LabelOnGround label, out bool labelEntityTargetable, out bool hasLabelEntityTargetable)
        {
            labelEntityTargetable = true;
            hasLabelEntityTargetable = false;

            if (DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Entity, out object? rawLabelEntity)
                && TryResolveLabelEntityTargetableFromRaw(rawLabelEntity, out labelEntityTargetable, out hasLabelEntityTargetable)
                && hasLabelEntityTargetable)
            {
                return true;
            }

            return DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabelElement)
                && DynamicAccess.TryGetDynamicValue(rawLabelElement, DynamicAccessProfiles.Entity, out object? rawElementEntity)
                && TryResolveLabelEntityTargetableFromRaw(rawElementEntity, out labelEntityTargetable, out hasLabelEntityTargetable)
                && hasLabelEntityTargetable;
        }

        private static bool TryResolveLabelEntityTargetableFromRaw(object? rawLabelEntity, out bool labelEntityTargetable, out bool hasLabelEntityTargetable)
        {
            hasLabelEntityTargetable = false;
            labelEntityTargetable = true;

            if (rawLabelEntity == null)
                return false;

            if (DynamicAccess.TryReadBool(rawLabelEntity, DynamicAccessProfiles.IsTargetable, out bool dynamicTargetable))
            {
                hasLabelEntityTargetable = true;
                labelEntityTargetable = dynamicTargetable;
                return true;
            }

            return false;
        }
    }
}
