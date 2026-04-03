namespace ClickIt.Features.Click
{
    internal readonly record struct OffscreenPathingCoordinatorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        PathfindingService PathfindingService,
        OffscreenMechanicTargetSelector TargetSelector,
        OffscreenStickyTargetHandler StickyTargetHandler,
        Func<long> GetStickyOffscreenTargetAddress,
        Action<string> DebugLog,
        Action<string> HoldDebugTelemetryAfterSuccess,
        Action<string, string> PublishClickFlowDebugStage,
        Func<(bool Success, Vector2 TargetScreen)> ResolveOffscreenTargetScreenPointFromPath,
        Func<Entity, (bool Success, Vector2 TargetScreen)> ResolveOffscreenTargetScreenPoint,
        Func<string, Vector2, bool, (bool Success, Vector2 CastPoint, string DebugReason)> TryUseMovementSkillForOffscreenPathing,
        Func<Vector2, bool> PerformPathingClick,
        Func<Vector2, string, bool> PointIsInClickableArea,
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        Func<Vector2, bool> IsInsideWindowInEitherSpace,
        Func<LabelOnGround, bool> ShouldSuppressPathfindingLabel,
        Func<LabelOnGround, string?> GetMechanicIdForLabel,
        Func<LabelOnGround, string?, Vector2, IReadOnlyList<LabelOnGround>?, string?, (bool Success, Vector2 ClickPos)> TryResolveLabelClickPosition,
        Func<Vector2, LabelOnGround, string, bool> ExecuteStickyLabelInteraction,
        Action<string?, LabelOnGround> MarkPendingChestOpenConfirmation,
        Action InvalidateShrineCache);

    internal sealed class OffscreenPathingCoordinator(OffscreenPathingCoordinatorDependencies dependencies)
    {
        private const int OffscreenTargetConfirmationWindowMs = 120;
        private readonly OffscreenPathingCoordinatorDependencies _dependencies = dependencies;
        private long _pendingTargetAddress;
        private string _pendingTargetPath = string.Empty;
        private long _pendingTargetFirstSeenTimestampMs;

        public bool TryWalkTowardOffscreenTarget(Entity? preferredTarget = null)
        {
            if (!_dependencies.Settings.WalkTowardOffscreenLabels.Value)
                return false;

            if (OffscreenPathingMath.ShouldSkipOffscreenPathfindingForRitual(EntityHelpers.IsRitualActive(_dependencies.GameController)))
            {
                ResetPendingTargetConfirmation();
                ClearStickyOffscreenTarget();
                _dependencies.PathfindingService.ClearLatestPath();
                _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Skipping offscreen pathfinding because a RitualBlocker is active.");
                _dependencies.PublishClickFlowDebugStage("OffscreenPathingBlockedByRitual", "RitualBlocker active");
                return false;
            }

            if (_dependencies.TargetSelector.ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable())
            {
                ResetPendingTargetConfirmation();
                ClearStickyOffscreenTarget();
                _dependencies.PathfindingService.ClearLatestPath();
                _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Skipping offscreen pathfinding because a clickable on-screen mechanic is available.");
                return false;
            }

            Entity? target = preferredTarget ?? _dependencies.TargetSelector.ResolveNearestOffscreenWalkTarget();
            if (target == null)
            {
                ResetPendingTargetConfirmation();
                if (preferredTarget != null)
                    ClearStickyOffscreenTarget();

                _dependencies.PathfindingService.ClearLatestPath();
                return false;
            }

            if (!target.IsValid || target.IsHidden || OffscreenPathingMath.IsEntityHiddenByMinimapIcon(target))
            {
                ResetPendingTargetConfirmation();
                ClearStickyOffscreenTarget();
                _dependencies.PathfindingService.ClearLatestPath();
                return false;
            }

            string targetPath = target.Path ?? string.Empty;
            if (preferredTarget == null && ShouldDelayTraversalForPendingTarget(target, targetPath, out long remainingDelayMs))
            {
                _dependencies.PathfindingService.ClearLatestPath();
                _dependencies.PublishClickFlowDebugStage(
                    "OffscreenPathingAwaitingConfirmation",
                    $"target={targetPath} remainingMs={remainingDelayMs}");
                return false;
            }

            ResetPendingTargetConfirmation();
            SetStickyOffscreenTarget(target);

            bool builtPath = _dependencies.PathfindingService.TryBuildPathToTarget(
                _dependencies.GameController,
                target,
                _dependencies.Settings.OffscreenPathfindingSearchBudget.Value);
            if (!builtPath)
                _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Pathfinding route not found; trying directional walk click.");

            (bool resolvedFromPath, Vector2 targetScreen) = builtPath
                ? _dependencies.ResolveOffscreenTargetScreenPointFromPath()
                : (false, default);
            if (!resolvedFromPath)
            {
                (bool success, Vector2 resolvedTargetScreen) = _dependencies.ResolveOffscreenTargetScreenPoint(target);
                if (!success)
                {
                    PublishOffscreenMovementDebug(target, targetPath, builtPath, false, false, targetScreen, default, "ResolveTargetScreenFailed");
                    _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Failed to resolve target screen point.");
                    return false;
                }

                targetScreen = resolvedTargetScreen;
            }

            if (!TryResolveDirectionalWalkClickPosition(targetScreen, targetPath, out Vector2 walkClick))
            {
                PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, false, targetScreen, default, "ResolveClickPointFailed");
                _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Failed to resolve directional click point.");
                return false;
            }

            (bool movementSkillUsed, Vector2 movementSkillCastPoint, string movementSkillDebug) = _dependencies.TryUseMovementSkillForOffscreenPathing(targetPath, targetScreen, builtPath);
            if (movementSkillUsed)
            {
                PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, true, targetScreen, movementSkillCastPoint, "MovementSkillUsed", movementSkillDebug);
                _dependencies.HoldDebugTelemetryAfterSuccess($"Offscreen traversal movement skill used: {targetPath}");
                _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] Used movement skill toward offscreen target: {targetPath}");
                return true;
            }

            if (!string.IsNullOrWhiteSpace(movementSkillDebug))
                _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] Movement skill not used: {movementSkillDebug}");

            PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, true, targetScreen, walkClick, "BeforeClick", movementSkillDebug);

            bool clicked = _dependencies.PerformPathingClick(walkClick);
            if (clicked)
            {
                PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, true, targetScreen, walkClick, "Clicked", movementSkillDebug);
                _dependencies.HoldDebugTelemetryAfterSuccess($"Offscreen traversal click succeeded: {targetPath}");
                _ = _dependencies.PathfindingService.TryBuildPathToTarget(
                    _dependencies.GameController,
                    target,
                    _dependencies.Settings.OffscreenPathfindingSearchBudget.Value);
                _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] Walking toward offscreen target: {targetPath}");
            }
            else
            {
                PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, true, targetScreen, walkClick, "ClickRejected", movementSkillDebug);
            }

            return clicked;
        }

        public bool TryHandleStickyOffscreenTarget(Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (!TryResolveStickyOffscreenTarget(out Entity? stickyTarget) || stickyTarget == null)
                return false;

            if (_dependencies.StickyTargetHandler.TryClickStickyTargetIfPossible(stickyTarget, windowTopLeft, allLabels))
                return true;

            _ = TryWalkTowardOffscreenTarget(stickyTarget);
            return true;
        }

        public void SetStickyOffscreenTarget(Entity target)
            => _dependencies.StickyTargetHandler.SetStickyOffscreenTarget(target);

        public void ClearStickyOffscreenTarget()
            => _dependencies.StickyTargetHandler.ClearStickyOffscreenTarget();

        private bool ShouldDelayTraversalForPendingTarget(Entity target, string targetPath, out long remainingDelayMs)
        {
            var confirmation = OffscreenPathingMath.EvaluateOffscreenTraversalTargetConfirmation(
                target.Address,
                targetPath,
                _pendingTargetAddress,
                _pendingTargetPath,
                _pendingTargetFirstSeenTimestampMs,
                Environment.TickCount64,
                OffscreenTargetConfirmationWindowMs);

            _pendingTargetAddress = confirmation.NextAddress;
            _pendingTargetPath = confirmation.NextPath;
            _pendingTargetFirstSeenTimestampMs = confirmation.NextFirstSeenTimestampMs;
            remainingDelayMs = confirmation.RemainingDelayMs;
            return confirmation.ShouldDelay;
        }

        private void ResetPendingTargetConfirmation()
        {
            _pendingTargetAddress = 0;
            _pendingTargetPath = string.Empty;
            _pendingTargetFirstSeenTimestampMs = 0;
        }

        public bool TryResolveStickyOffscreenTarget(out Entity? target)
            => _dependencies.StickyTargetHandler.TryResolveStickyOffscreenTarget(out target);

        public Entity? FindEntityByAddress(long address)
        {
            return EntityQueryService.FindEntityByAddress(_dependencies.GameController, address);
        }

        public bool IsStickyTarget(Entity? entity)
            => _dependencies.StickyTargetHandler.IsStickyTarget(entity);

        private void PublishOffscreenMovementDebug(
            Entity target,
            string targetPath,
            bool builtPath,
            bool resolvedFromPath,
            bool resolvedClickPoint,
            Vector2 targetScreen,
            Vector2 clickScreen,
            string stage,
            string movementSkillDebug = "")
        {
            var player = _dependencies.GameController.Player;
            Vector2 playerGrid = player != null
                ? new Vector2(player.GridPosNum.X, player.GridPosNum.Y)
                : default;
            Vector2 targetGrid = new(target.GridPosNum.X, target.GridPosNum.Y);
            RectangleF win = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 center = new(win.X + (win.Width * 0.5f), win.Y + (win.Height * 0.5f));

            _dependencies.PathfindingService.SetLatestOffscreenMovementDebug(new OffscreenMovementDebugSnapshot(
                HasData: true,
                Stage: stage,
                TargetPath: targetPath,
                BuiltPath: builtPath,
                ResolvedFromPath: resolvedFromPath,
                ResolvedClickPoint: resolvedClickPoint,
                WindowCenter: center,
                TargetScreen: targetScreen,
                ClickScreen: clickScreen,
                PlayerGrid: playerGrid,
                TargetGrid: targetGrid,
                MovementSkillDebug: movementSkillDebug ?? string.Empty,
                TimestampMs: Environment.TickCount64));
        }

        private bool TryResolveDirectionalWalkClickPosition(Vector2 targetScreen, string targetPath, out Vector2 clickPos)
        {
            RectangleF win = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            return OffscreenProjectionMath.TryResolveDirectionalWalkClickPosition(
                win,
                targetScreen,
                targetPath,
                _dependencies.PointIsInClickableArea,
                out clickPos);
        }

    }
}