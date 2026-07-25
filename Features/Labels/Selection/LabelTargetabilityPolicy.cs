namespace ClickIt.Features.Labels.Selection
{
    internal static class LabelTargetabilityPolicy
    {
        public static bool IsEntityTargetableForClick(LabelOnGround label, Entity item)
        {
            string path = DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;

            if (!ShouldAllowHarvestRootElementVisibility(path, IsHarvestRootElementVisibleForClick(label)))
                return false;

            if (!RequiresTargetabilityGate(path))
                return true;
            if (!ShouldApplyPetrifiedWoodEntityTargetabilityGate(path))
                return true;

            ResolveLabelEntityTargetableForClick(label, out bool hasLabelEntityTargetable, out bool labelEntityTargetable);
            return ShouldAllowPetrifiedWoodTargetability(hasLabelEntityTargetable, labelEntityTargetable);
        }

        public static bool ShouldAllowHarvestRootElementVisibility(string? path, bool harvestRootElementVisible)
        {
            if (string.IsNullOrWhiteSpace(path) || !MechanicClassifier.IsHarvestPath(path))
                return true;

            return harvestRootElementVisible;
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

            Entity? item = DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                ? rawItem as Entity
                : null;
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

        public static void ResolveLabelEntityTargetableFromRaw(object? rawLabelEntity, out bool hasLabelEntityTargetable, out bool labelEntityTargetable)
        {
            bool resolved = TryResolveLabelEntityTargetableFromRaw(rawLabelEntity, out labelEntityTargetable, out hasLabelEntityTargetable);
            if (!resolved)
            {
                hasLabelEntityTargetable = false;
                labelEntityTargetable = true;
            }
        }

        public static bool ShouldSkipUntargetableEntity(bool hasLabelEntityTargetable, bool labelEntityTargetable, bool itemIsTargetable, bool allowNullEntityFallback = false)
        {
            if (hasLabelEntityTargetable && !labelEntityTargetable)
                return true;

            if (!hasLabelEntityTargetable)
                return !allowNullEntityFallback && !itemIsTargetable;

            return !itemIsTargetable;
        }

        private static bool IsHarvestRootElementVisibleForClick(LabelOnGround label)
        {
            if (!TryResolveLabelRootChild(label, out object? rawChild) || rawChild == null)
            {
                return false;
            }

            return DynamicAccess.TryReadBool(rawChild, DynamicAccessProfiles.IsVisible, out bool isVisible) && isVisible;
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

        private static bool TryResolveLabelRootChild(LabelOnGround label, out object? rawChild)
        {
            rawChild = null;
            return DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabel)
                && DynamicAccess.TryGetDynamicValue(rawLabel, DynamicAccessProfiles.FirstChild, out rawChild);
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