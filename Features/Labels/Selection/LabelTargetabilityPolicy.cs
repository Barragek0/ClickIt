using ClickIt.Features.Labels.Classification;
using ClickIt.Shared;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Features.Labels.Selection
{
    internal static class LabelTargetabilityPolicy
    {
        public static bool IsEntityTargetableForClick(LabelOnGround label, Entity item)
        {
            string path = item.Path ?? string.Empty;

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

            Entity? item = label.ItemOnGround;
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

            if (DynamicAccess.TryGetDynamicValue(label, l => l.Entity, out object? rawLabelEntity)
                && TryResolveLabelEntityTargetableFromRaw(rawLabelEntity, out bool directTargetable, out bool directHasTargetable)
                && directHasTargetable)
            {
                hasLabelEntityTargetable = true;
                labelEntityTargetable = directTargetable;
                return;
            }

            if (DynamicAccess.TryGetDynamicValue(label, l => l.Label, out object? rawLabelElement)
                && DynamicAccess.TryGetDynamicValue(rawLabelElement, l => l.Entity, out object? rawElementEntity)
                && TryResolveLabelEntityTargetableFromRaw(rawElementEntity, out bool elementTargetable, out bool elementHasTargetable)
                && elementHasTargetable)
            {
                hasLabelEntityTargetable = true;
                labelEntityTargetable = elementTargetable;
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
            => label?.Label?.GetChildAtIndex(0)?.IsVisible == true;

        private static bool TryResolveLabelEntityTargetableFromRaw(object? rawLabelEntity, out bool labelEntityTargetable, out bool hasLabelEntityTargetable)
        {
            hasLabelEntityTargetable = false;
            labelEntityTargetable = true;

            if (rawLabelEntity == null)
                return false;

            if (DynamicAccess.TryReadBool(rawLabelEntity, e => e.IsTargetable, out bool dynamicTargetable))
            {
                hasLabelEntityTargetable = true;
                labelEntityTargetable = dynamicTargetable;
                return true;
            }

            return false;
        }
    }
}