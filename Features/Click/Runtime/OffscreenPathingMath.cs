namespace ClickIt.Features.Click.Runtime
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
            if (!DynamicAccess.TryGetComponent(entity, out MinimapIcon? minimapIcon)
                || minimapIcon == null)
                return false;


            return DynamicAccess.TryReadBool(minimapIcon, DynamicAccessProfiles.IsHide, out bool isHiddenByMinimap)
                && isHiddenByMinimap;
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
            return prioritizeOnscreenClickableMechanics
                && (clickShrinesEnabled
                || clickLostShipmentEnabled
                || clickSettlersOreEnabled
                || clickEaterAltarsEnabled
                || clickExarchAltarsEnabled);
        }

        internal static bool ShouldSkipOffscreenPathfindingForRitual(bool ritualActive)
            => ritualActive;

        internal static bool ShouldBlockOffscreenTraversalAfterPathBuildFailure(string? failureReason)
            => string.Equals(failureReason, PathfindingService.AStarNoRouteFailureReason, StringComparison.Ordinal);

        // After an A* no-route block, the same target cannot become reachable for a while, so the coordinator skips re-running the doomed full-budget search during the backoff window.
        internal static bool ShouldApplyNoRouteBackoff(
            long targetAddress,
            long blockedTargetAddress,
            long now,
            long blockedAtMs,
            int backoffMs)
            => blockedTargetAddress != 0
                && targetAddress == blockedTargetAddress
                && (now - blockedAtMs) < backoffMs;

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

        // Continue pathfinding until the label is clickable in place; blight additionally requires the full label on screen.
        internal static bool ShouldContinuePathfindingForLabel(
            bool blightRequiresFullLabel,
            bool labelInWindow,
            bool labelClickable,
            bool clickPointResolvable,
            float distance,
            int clickDistance)
        {
            if (blightRequiresFullLabel && (!labelInWindow || !labelClickable))
                return true;

            return distance > clickDistance || !clickPointResolvable;
        }

        internal static bool ShouldPathfindToEntityAfterClickPointResolveFailure(
            bool walkTowardOffscreenLabelsEnabled,
            bool hasEntity,
            string? mechanicId)
        {
            return walkTowardOffscreenLabelsEnabled && hasEntity && !string.IsNullOrWhiteSpace(mechanicId);
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
                return false;


            if (string.IsNullOrWhiteSpace(labelMechanicId) || string.IsNullOrWhiteSpace(settlersCandidateMechanicId))
                return false;


            return string.Equals(labelMechanicId, settlersCandidateMechanicId, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldForceUiHoverVerificationForLabel(LabelOnGround? label)
        {
            DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item);
            if (item == null || !TryReadEntityType(item, out EntityType itemType) || itemType != EntityType.WorldItem)
                return false;


            string itemPath = DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path)
                ? path
                : string.Empty;
            string renderName = DynamicAccess.TryReadString(item, DynamicAccessProfiles.RenderName, out string name)
                ? name
                : string.Empty;

            return WorldItemUiHoverPolicy.ShouldForceUiHoverVerificationForWorldItem(itemPath, renderName);
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
                return (false, targetAddress, normalizedPath, now, 0);


            bool isSameTarget = IsSameOffscreenTraversalTarget(targetAddress, normalizedPath, pendingAddress, pendingPath);
            if (!isSameTarget)
                return (true, targetAddress, normalizedPath, now, confirmationWindowMs);


            long firstSeen = pendingFirstSeenTimestampMs > 0 ? pendingFirstSeenTimestampMs : now;
            long elapsed = SystemMath.Max(0, now - firstSeen);
            if (elapsed >= confirmationWindowMs)
                return (false, targetAddress, normalizedPath, firstSeen, 0);


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


            if (!DynamicAccess.TryReadEntityAddress(entity, out long entityAddress) || entityAddress == 0)
                return null;


            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item);
                if (item == null || !DynamicAccess.TryReadEntityAddress(item, out long itemAddress))
                    continue;


                if (itemAddress == entityAddress)
                    return label;

            }

            return null;
        }

        private static bool TryReadEntityType(Entity? entity, out EntityType entityType)
        {
            entityType = default;
            if (!DynamicAccess.TryGetDynamicValue(entity, DynamicAccessProfiles.Type, out object? rawType) || rawType == null)
                return false;


            switch (rawType)
            {
                case EntityType typedEntityType:
                    entityType = typedEntityType;
                    return true;
                case int intEntityType:
                    entityType = (EntityType)intEntityType;
                    return true;
                case uint uintEntityType:
                    entityType = (EntityType)uintEntityType;
                    return true;
                case short shortEntityType:
                    entityType = (EntityType)shortEntityType;
                    return true;
                case ushort ushortEntityType:
                    entityType = (EntityType)ushortEntityType;
                    return true;
                case byte byteEntityType:
                    entityType = (EntityType)byteEntityType;
                    return true;
                case sbyte sbyteEntityType:
                    entityType = (EntityType)sbyteEntityType;
                    return true;
                default:
                    return false;
            }
        }

        internal static bool TryResolveDirectionalWalkClickPosition(
            RectangleF windowRect,
            Vector2 targetScreen,
            string targetPath,
            Func<Vector2, string, bool> pointIsInClickableArea,
            out Vector2 clickPos)
        {
            clickPos = default;
            if (windowRect.Width <= 0 || windowRect.Height <= 0)
                return false;

            float insetX = SystemMath.Max(28f, windowRect.Width * 0.10f);
            float insetY = SystemMath.Max(28f, windowRect.Height * 0.10f);
            float safeLeft = windowRect.Left + insetX;
            float safeRight = windowRect.Right - insetX;
            float safeTop = windowRect.Top + insetY;
            float safeBottom = windowRect.Bottom - insetY;

            Vector2 center = new(windowRect.X + (windowRect.Width * 0.5f), windowRect.Y + (windowRect.Height * 0.5f));
            Vector2 direction = targetScreen - center;
            float lenSq = (direction.X * direction.X) + (direction.Y * direction.Y);
            if (lenSq < 1f)
                return false;

            // Search from the target back toward the screen center, with the clamped target as the primary fallback.
            for (int i = 0; i <= 7; i++)
            {
                float t = 1.05f - (i * 0.1f);
                Vector2 candidate = center + (direction * t);
                if (!IsInsideWindow(windowRect, candidate))
                    continue;
                if (candidate.X < safeLeft || candidate.X > safeRight || candidate.Y < safeTop || candidate.Y > safeBottom)
                    continue;
                if (!pointIsInClickableArea(candidate, targetPath))
                    continue;

                clickPos = candidate;
                return true;
            }

            Vector2 clamped = new(
                SystemMath.Clamp(targetScreen.X, safeLeft, safeRight),
                SystemMath.Clamp(targetScreen.Y, safeTop, safeBottom));
            if (pointIsInClickableArea(clamped, targetPath))
            {
                clickPos = clamped;
                return true;
            }

            for (int i = 2; i >= 0; i--)
            {
                float t = 0.25f - ((2 - i) * 0.1f);
                Vector2 candidate = center + (direction * t);
                if (!IsInsideWindow(windowRect, candidate))
                    continue;
                if (candidate.X < safeLeft || candidate.X > safeRight || candidate.Y < safeTop || candidate.Y > safeBottom)
                    continue;
                if (!pointIsInClickableArea(candidate, targetPath))
                    continue;

                clickPos = candidate;
                return true;
            }

            return false;
        }

        internal static bool IsInsideWindow(RectangleF window, Vector2 point)
            => point.X >= window.Left && point.X <= window.Right && point.Y >= window.Top && point.Y <= window.Bottom;

        internal static Vector2 GetWindowCenter(RectangleF window)
            => new(window.X + (window.Width * 0.5f), window.Y + (window.Height * 0.5f));

        internal static bool IsFinite(Vector2 point)
            => !float.IsNaN(point.X) && !float.IsInfinity(point.X) && !float.IsNaN(point.Y) && !float.IsInfinity(point.Y);

        internal static bool IsNearCorner(Vector2 point, RectangleF window)
        {
            float marginX = window.Width * 0.05f;
            float marginY = window.Height * 0.05f;

            bool nearHorizontal = point.X <= window.Left + marginX || point.X >= window.Right - marginX;
            bool nearVertical = point.Y <= window.Top + marginY || point.Y >= window.Bottom - marginY;
            return nearHorizontal && nearVertical;
        }

        internal static int CountRemainingPathNodes(IReadOnlyList<PathfindingService.GridPoint>? path, int nearestIndex)
        {
            if (path == null || path.Count == 0 || nearestIndex < 0)
                return 0;

            int index = SystemMath.Min(path.Count - 1, nearestIndex);
            return SystemMath.Max(0, path.Count - (index + 1));
        }

        internal static int FindClosestPathIndexToPlayer(IReadOnlyList<PathfindingService.GridPoint> path, PathfindingService.GridPoint playerGrid)
        {
            if (path == null || path.Count == 0)
                return -1;

            int bestIndex = -1;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                int distance = SystemMath.Abs(path[i].X - playerGrid.X) + SystemMath.Abs(path[i].Y - playerGrid.Y);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestIndex = i;
            }

            return bestIndex;
        }

        internal static bool TryGetSmoothedPathDirection(
            IReadOnlyList<PathfindingService.GridPoint> path,
            PathfindingService.GridPoint playerGrid,
            int nearestIndex,
            out float deltaX,
            out float deltaY)
        {
            deltaX = 0f;
            deltaY = 0f;

            if (path == null || path.Count < 2 || nearestIndex < 0)
                return false;

            int start = SystemMath.Min(path.Count - 1, nearestIndex + 1);
            int end = SystemMath.Min(path.Count - 1, nearestIndex + 8);
            if (end < start)
                return false;

            float weightedX = 0f;
            float weightedY = 0f;
            float weightTotal = 0f;

            for (int i = start; i <= end; i++)
            {
                PathfindingService.GridPoint node = path[i];
                float dx = node.X - playerGrid.X;
                float dy = node.Y - playerGrid.Y;
                if (SystemMath.Abs(dx) + SystemMath.Abs(dy) < 0.001f)
                    continue;

                float weight = i - start + 1f;
                weightedX += dx * weight;
                weightedY += dy * weight;
                weightTotal += weight;
            }

            if (weightTotal <= 0f)
                return false;

            deltaX = weightedX / weightTotal;
            deltaY = weightedY / weightTotal;
            return SystemMath.Abs(deltaX) + SystemMath.Abs(deltaY) >= 0.001f;
        }

        internal static bool TryComputeGridDirectionPoint(Vector2 center, float deltaGridX, float deltaGridY, float radius, out Vector2 point)
        {
            point = default;
            if (radius <= 0f)
                return false;

            Vector2 axis = new(deltaGridX - deltaGridY, -(deltaGridX + deltaGridY) * 0.65f);
            float lengthSquared = (axis.X * axis.X) + (axis.Y * axis.Y);
            if (lengthSquared < 0.001f)
                return false;

            float invLength = 1f / MathF.Sqrt(lengthSquared);
            point = center + new Vector2(axis.X * invLength * radius, axis.Y * invLength * radius);
            return true;
        }
    }
}
