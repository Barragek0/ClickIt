using ClickIt.Definitions;
using ClickIt.Services.Label.Classification.Policies;
using ClickIt.Utils;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services.Click.Runtime
{
    internal static class OffscreenPathingMath
    {
        internal const int OffscreenPathfindingTargetSearchDistance = 50000;

        internal static bool ShouldDropStickyTargetForUntargetableEldritchAltar(bool isEldritchAltar, bool isTargetable)
            => isEldritchAltar && !isTargetable;

        internal static bool IsSameEntityAddress(long leftAddress, long rightAddress)
            => leftAddress != 0 && leftAddress == rightAddress;

        internal static bool IsEntityHiddenByMinimapIcon(Entity entity)
        {
            MinimapIcon? minimapIcon = entity.GetComponent<MinimapIcon>();
            return minimapIcon != null && minimapIcon.IsHide;
        }

        internal static bool ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing(
            bool prioritizeOnscreenClickableMechanics,
            bool hasClickableAltar,
            bool hasClickableShrine,
            bool hasClickableLostShipment,
            bool hasClickableSettlersOre)
        {
            return prioritizeOnscreenClickableMechanics
                && (hasClickableAltar
                    || hasClickableShrine
                    || hasClickableLostShipment
                    || hasClickableSettlersOre);
        }

        internal static bool ShouldEvaluateOnscreenMechanicChecks(
            bool prioritizeOnscreenClickableMechanics,
            bool clickShrinesEnabled,
            bool clickLostShipmentEnabled,
            bool clickSettlersOreEnabled,
            bool clickEaterAltarsEnabled,
            bool clickExarchAltarsEnabled)
        {
            if (!prioritizeOnscreenClickableMechanics)
                return false;

            return clickShrinesEnabled
                || clickLostShipmentEnabled
                || clickSettlersOreEnabled
                || clickEaterAltarsEnabled
                || clickExarchAltarsEnabled;
        }

        internal static bool ShouldSkipOffscreenPathfindingForRitual(bool ritualActive)
            => ritualActive;

        internal static bool IsBackedByGroundLabel(long entityAddress, IReadOnlySet<long>? labelEntityAddresses)
        {
            return entityAddress != 0
                && labelEntityAddresses != null
                && labelEntityAddresses.Contains(entityAddress);
        }

        internal static string? GetEldritchAltarMechanicIdForPath(bool clickExarchAltars, bool clickEaterAltars, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (clickExarchAltars && path.Contains(Constants.CleansingFireAltar, StringComparison.OrdinalIgnoreCase))
                return MechanicIds.AltarsSearingExarch;

            if (clickEaterAltars && path.Contains(Constants.TangleAltar, StringComparison.OrdinalIgnoreCase))
                return MechanicIds.AltarsEaterOfWorlds;

            return null;
        }

        internal static bool IsEldritchAltarPath(string path)
            => !string.IsNullOrWhiteSpace(GetEldritchAltarMechanicIdForPath(true, true, path));

        internal static bool ShouldContinuePathfindingWhenLabelActionable(bool labelInWindow, bool labelClickable, bool clickPointResolvable)
            => !(labelInWindow && labelClickable && clickPointResolvable);

        internal static bool ShouldPathfindToEntityAfterClickPointResolveFailure(
            bool walkTowardOffscreenLabelsEnabled,
            bool hasEntity,
            string? mechanicId)
        {
            if (!walkTowardOffscreenLabelsEnabled || !hasEntity || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            return true;
        }

        internal static string? ResolveLabelMechanicIdForVisibleCandidateComparison(
            string? resolvedMechanicId,
            bool hasLabel,
            bool isWorldItemLabel,
            bool clickItemsEnabled)
        {
            if (!string.IsNullOrWhiteSpace(resolvedMechanicId))
                return resolvedMechanicId;

            if (hasLabel && isWorldItemLabel && clickItemsEnabled)
                return MechanicIds.Items;

            return resolvedMechanicId;
        }

        internal static bool ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(
            string? labelMechanicId,
            string? settlersCandidateMechanicId)
        {
            if (!SettlersMechanicPolicy.IsSettlersMechanicId(labelMechanicId)
                || !SettlersMechanicPolicy.IsSettlersMechanicId(settlersCandidateMechanicId))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(labelMechanicId) || string.IsNullOrWhiteSpace(settlersCandidateMechanicId))
                return false;

            return string.Equals(labelMechanicId, settlersCandidateMechanicId, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldForceUiHoverVerificationForLabel(LabelOnGround? label)
        {
            Entity? item = label?.ItemOnGround;
            if (item == null || item.Type != ExileCore.Shared.Enums.EntityType.WorldItem)
                return false;

            return InputHandler.ShouldForceUiHoverVerificationForWorldItem(item.Path, item.RenderName);
        }

        internal static (bool ShouldDelay, long NextAddress, string NextPath, long NextFirstSeenTimestampMs, long RemainingDelayMs)
            EvaluateOffscreenTraversalTargetConfirmation(
                long targetAddress,
                string? targetPath,
                long pendingAddress,
                string? pendingPath,
                long pendingFirstSeenTimestampMs,
                long now,
                int confirmationWindowMs)
        {
            string normalizedPath = targetPath ?? string.Empty;

            if (confirmationWindowMs <= 0)
            {
                return (false, targetAddress, normalizedPath, now, 0);
            }

            bool isSameTarget = IsSameOffscreenTraversalTarget(targetAddress, normalizedPath, pendingAddress, pendingPath);
            if (!isSameTarget)
            {
                return (true, targetAddress, normalizedPath, now, confirmationWindowMs);
            }

            long firstSeen = pendingFirstSeenTimestampMs > 0 ? pendingFirstSeenTimestampMs : now;
            long elapsed = Math.Max(0, now - firstSeen);
            if (elapsed >= confirmationWindowMs)
            {
                return (false, targetAddress, normalizedPath, firstSeen, 0);
            }

            return (true, targetAddress, normalizedPath, firstSeen, confirmationWindowMs - elapsed);
        }

        internal static bool IsSameOffscreenTraversalTarget(long leftAddress, string? leftPath, long rightAddress, string? rightPath)
        {
            if (leftAddress != 0 && rightAddress != 0)
                return leftAddress == rightAddress;

            return string.Equals(leftPath ?? string.Empty, rightPath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        internal static LabelOnGround? FindVisibleLabelForEntity(Entity entity, IReadOnlyList<LabelOnGround>? labels)
        {
            if (entity == null || labels == null || labels.Count == 0)
                return null;

            long entityAddress = entity.Address;
            if (entityAddress == 0)
                return null;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label?.ItemOnGround == null)
                    continue;

                if (label.ItemOnGround.Address == entityAddress)
                    return label;
            }

            return null;
        }
    }
}