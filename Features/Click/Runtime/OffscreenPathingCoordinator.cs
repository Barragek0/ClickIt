namespace ClickIt.Features.Click.Runtime
{
    internal readonly record struct OffscreenPathingCoordinatorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        PathfindingService PathfindingService,
        OnscreenMechanicPathingBlocker OnscreenMechanicPathingBlocker,
        OffscreenTraversalTargetResolver TraversalTargetResolver,
        OffscreenStickyTargetHandler StickyTargetHandler,
        OffscreenTargetResolver TargetResolver,
        MovementSkillCoordinator MovementSkills,
        ClickLabelInteractionService LabelInteraction,
        Action<string> DebugLog,
        Action<string> HoldDebugTelemetryAfterSuccess,
        ClickDebugPublicationService ClickDebugPublisher,
        Func<Vector2, string, bool> PointIsInClickableArea);

    internal sealed class OffscreenPathingCoordinator(OffscreenPathingCoordinatorDependencies dependencies)
    {
        private readonly OffscreenPathingCoordinatorDependencies _dependencies = dependencies;
        private readonly OffscreenTraversalConfirmationGate _traversalConfirmationGate = new();

        public bool TryWalkTowardOffscreenTarget(Entity? preferredTarget = null)
        {
            if (!_dependencies.Settings.WalkTowardOffscreenLabels.Value)
                return false;

            if (OffscreenPathingMath.ShouldSkipOffscreenPathfindingForRitual(EntityHelpers.IsRitualActive(_dependencies.GameController)))
            {
                _traversalConfirmationGate.Reset();
                ClearStickyOffscreenTarget();
                _dependencies.PathfindingService.ClearLatestPath();
                _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Skipping offscreen pathfinding because a RitualBlocker is active.");
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("OffscreenPathingBlockedByRitual", "RitualBlocker active");
                return false;
            }

            if (_dependencies.OnscreenMechanicPathingBlocker.ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable())
            {
                _traversalConfirmationGate.Reset();
                ClearStickyOffscreenTarget();
                _dependencies.PathfindingService.ClearLatestPath();
                _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Skipping offscreen pathfinding because a clickable on-screen mechanic is available.");
                return false;
            }

            Entity? target = preferredTarget ?? _dependencies.TraversalTargetResolver.ResolveNearestOffscreenWalkTarget();
            if (target == null)
            {
                _traversalConfirmationGate.Reset();
                if (preferredTarget != null)
                    ClearStickyOffscreenTarget();

                _dependencies.PathfindingService.ClearLatestPath();
                return false;
            }

            if (!target.IsValid || target.IsHidden || OffscreenPathingMath.IsEntityHiddenByMinimapIcon(target))
            {
                _traversalConfirmationGate.Reset();
                ClearStickyOffscreenTarget();
                _dependencies.PathfindingService.ClearLatestPath();
                return false;
            }

            string targetPath = target.Path ?? string.Empty;
            if (preferredTarget == null && _traversalConfirmationGate.ShouldDelay(target, targetPath, out long remainingDelayMs))
            {
                _dependencies.PathfindingService.ClearLatestPath();
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                    "OffscreenPathingAwaitingConfirmation",
                    $"target={targetPath} remainingMs={remainingDelayMs}");
                return false;
            }

            _traversalConfirmationGate.Reset();
            SetStickyOffscreenTarget(target);

            bool builtPath = _dependencies.PathfindingService.TryBuildPathToTarget(
                _dependencies.GameController,
                target,
                _dependencies.Settings.OffscreenPathfindingSearchBudget.Value);
            if (!builtPath)
                _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Pathfinding route not found; trying directional walk click.");

            (bool resolvedFromPath, Vector2 targetScreen) = builtPath
                ? TryResolveOffscreenTargetScreenPointFromPath()
                : (false, default);
            if (!resolvedFromPath)
            {
                (bool success, Vector2 resolvedTargetScreen) = TryResolveOffscreenTargetScreenPoint(target);
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

            (bool movementSkillUsed, Vector2 movementSkillCastPoint, string movementSkillDebug) = TryUseMovementSkillForOffscreenPathing(targetPath, targetScreen, builtPath);
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

            bool clicked = _dependencies.LabelInteraction.PerformMechanicClick(walkClick);
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
            if (!_dependencies.StickyTargetHandler.TryResolveStickyOffscreenTarget(out Entity? stickyTarget) || stickyTarget == null)
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

        private (bool Success, Vector2 TargetScreen) TryResolveOffscreenTargetScreenPointFromPath()
        {
            bool success = _dependencies.TargetResolver.TryResolveOffscreenTargetScreenPointFromPath(out Vector2 targetScreen);
            return (success, targetScreen);
        }

        private (bool Success, Vector2 TargetScreen) TryResolveOffscreenTargetScreenPoint(Entity target)
        {
            bool success = _dependencies.TargetResolver.TryResolveOffscreenTargetScreenPoint(target, out Vector2 targetScreen);
            return (success, targetScreen);
        }

        private (bool Success, Vector2 CastPoint, string DebugReason) TryUseMovementSkillForOffscreenPathing(string targetPath, Vector2 targetScreen, bool builtPath)
        {
            bool success = _dependencies.MovementSkills.TryUseMovementSkillForOffscreenPathing(targetPath, targetScreen, builtPath, out Vector2 castPoint, out string debugReason);
            return (success, castPoint, debugReason);
        }

    }
}