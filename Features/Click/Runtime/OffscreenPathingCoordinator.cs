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

        private readonly record struct OffscreenTraversalTargetContext(
            Entity Target,
            string TargetPath);

        public bool TryWalkTowardOffscreenTarget(Entity? preferredTarget = null)
        {
            if (!_dependencies.Settings.WalkTowardOffscreenLabels.Value)
                return false;

            if (OffscreenPathingMath.ShouldSkipOffscreenPathfindingForRitual(EntityHelpers.IsRitualActive(_dependencies.GameController)))
                return AbortOffscreenPathingForBlocker(
                    "[TryWalkTowardOffscreenTarget] Skipping offscreen pathfinding because a RitualBlocker is active.",
                    "OffscreenPathingBlockedByRitual",
                    "RitualBlocker active");

            if (_dependencies.OnscreenMechanicPathingBlocker.ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable())
                return AbortOffscreenPathingForBlocker(
                    "[TryWalkTowardOffscreenTarget] Skipping offscreen pathfinding because a clickable on-screen mechanic is available.",
                    null,
                    null);

            if (!TryStartTraversal(preferredTarget, out OffscreenTraversalTargetContext context))
                return false;

            if (!TryBuildTraversalPath(context, out bool builtPath))
                return false;
            if (!TryResolveTraversalClick(context, builtPath, out bool resolvedFromPath, out Vector2 targetScreen, out Vector2 walkClick))
                return false;

            (bool movementSkillUsed, Vector2 movementSkillCastPoint, string movementSkillDebug) = TryUseMovementSkillForOffscreenPathing(context.TargetPath, targetScreen, builtPath);
            if (movementSkillUsed)
                return HandleSuccessfulTraversalMovementSkill(context, builtPath, resolvedFromPath, targetScreen, movementSkillCastPoint, movementSkillDebug);

            if (!string.IsNullOrWhiteSpace(movementSkillDebug))
                _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] Movement skill not used: {movementSkillDebug}");

            PublishOffscreenMovementDebug(context.Target, context.TargetPath, builtPath, resolvedFromPath, true, targetScreen, walkClick, "BeforeClick", movementSkillDebug);

            bool clicked = _dependencies.LabelInteraction.PerformMechanicClick(walkClick);
            return HandleTraversalClickResult(context, builtPath, resolvedFromPath, targetScreen, walkClick, movementSkillDebug, clicked);
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

        public void CancelTraversalState()
            => ResetTraversalState(resetConfirmation: true, clearStickyTarget: true, clearLatestPath: true);

        public bool IsStickyTarget(Entity? entity)
            => _dependencies.StickyTargetHandler.IsStickyTarget(entity);

        private bool HandleSuccessfulTraversalMovementSkill(
            OffscreenTraversalTargetContext context,
            bool builtPath,
            bool resolvedFromPath,
            Vector2 targetScreen,
            Vector2 movementSkillCastPoint,
            string movementSkillDebug)
        {
            PublishOffscreenMovementDebug(
                context.Target,
                context.TargetPath,
                builtPath,
                resolvedFromPath,
                true,
                targetScreen,
                movementSkillCastPoint,
                "MovementSkillUsed",
                movementSkillDebug);
            _dependencies.HoldDebugTelemetryAfterSuccess($"Offscreen traversal movement skill used: {context.TargetPath}");
            _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] Used movement skill toward offscreen target: {context.TargetPath}");
            return true;
        }

        private bool HandleTraversalClickResult(
            OffscreenTraversalTargetContext context,
            bool builtPath,
            bool resolvedFromPath,
            Vector2 targetScreen,
            Vector2 walkClick,
            string movementSkillDebug,
            bool clicked)
        {
            if (!clicked)
            {
                PublishOffscreenMovementDebug(context.Target, context.TargetPath, builtPath, resolvedFromPath, true, targetScreen, walkClick, "ClickRejected", movementSkillDebug);
                return false;
            }

            PublishOffscreenMovementDebug(context.Target, context.TargetPath, builtPath, resolvedFromPath, true, targetScreen, walkClick, "Clicked", movementSkillDebug);
            _dependencies.HoldDebugTelemetryAfterSuccess($"Offscreen traversal click succeeded: {context.TargetPath}");
            _ = _dependencies.PathfindingService.TryBuildPathToTarget(
                _dependencies.GameController,
                context.Target,
                _dependencies.Settings.OffscreenPathfindingSearchBudget.Value);
            _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] Walking toward offscreen target: {context.TargetPath}");
            return true;
        }

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

        private bool TryStartTraversal(Entity? preferredTarget, out OffscreenTraversalTargetContext context)
        {
            context = default;

            if (!TryResolveTraversalTarget(preferredTarget, out Entity? target) || target == null)
                return false;

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
            context = new OffscreenTraversalTargetContext(target, targetPath);
            return true;
        }

        private bool TryBuildTraversalPath(OffscreenTraversalTargetContext context, out bool builtPath)
        {
            builtPath = _dependencies.PathfindingService.TryBuildPathToTarget(
                _dependencies.GameController,
                context.Target,
                _dependencies.Settings.OffscreenPathfindingSearchBudget.Value);
            if (builtPath)
                return true;

            PathfindingDebugSnapshot pathfindingSnapshot = _dependencies.PathfindingService.GetDebugSnapshot();
            if (OffscreenPathingMath.ShouldBlockOffscreenTraversalAfterPathBuildFailure(pathfindingSnapshot.LastFailureReason))
            {
                PublishOffscreenMovementDebug(context.Target, context.TargetPath, builtPath, false, false, default, default, "BlockedNoRoute", pathfindingSnapshot.LastFailureReason);
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("OffscreenPathingBlockedNoRoute", $"target={context.TargetPath}");
                _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Skipping offscreen traversal because A* did not find a route.");
                return false;
            }

            _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Pathfinding route not found; trying directional walk click.");
            return true;
        }

        private bool TryResolveTraversalClick(
            OffscreenTraversalTargetContext context,
            bool builtPath,
            out bool resolvedFromPath,
            out Vector2 targetScreen,
            out Vector2 walkClick)
        {
            walkClick = default;

            (resolvedFromPath, targetScreen) = builtPath
                ? TryResolveOffscreenTargetScreenPointFromPath()
                : (false, default);
            if (!resolvedFromPath)
            {
                (bool success, Vector2 resolvedTargetScreen) = TryResolveOffscreenTargetScreenPoint(context.Target);
                if (!success)
                {
                    PublishOffscreenMovementDebug(context.Target, context.TargetPath, builtPath, false, false, targetScreen, default, "ResolveTargetScreenFailed");
                    _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Failed to resolve target screen point.");
                    return false;
                }

                targetScreen = resolvedTargetScreen;
            }

            if (TryResolveDirectionalWalkClickPosition(targetScreen, context.TargetPath, out walkClick))
                return true;

            PublishOffscreenMovementDebug(context.Target, context.TargetPath, builtPath, resolvedFromPath, false, targetScreen, default, "ResolveClickPointFailed");
            _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Failed to resolve directional click point.");
            return false;
        }

        private bool TryResolveTraversalTarget(Entity? preferredTarget, out Entity? target)
        {
            target = preferredTarget ?? _dependencies.TraversalTargetResolver.ResolveNearestOffscreenWalkTarget();
            if (target == null)
            {
                ResetTraversalState(resetConfirmation: true, clearStickyTarget: preferredTarget != null, clearLatestPath: true);
                return false;
            }

            if (!target.IsValid || target.IsHidden || OffscreenPathingMath.IsEntityHiddenByMinimapIcon(target))
            {
                ResetTraversalState(resetConfirmation: true, clearStickyTarget: true, clearLatestPath: true);
                target = null;
                return false;
            }

            return true;
        }

        private bool AbortOffscreenPathingForBlocker(string debugMessage, string? debugStage, string? debugDetails)
        {
            CancelTraversalState();
            _dependencies.DebugLog(debugMessage);
            if (!string.IsNullOrWhiteSpace(debugStage))
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(debugStage, debugDetails ?? string.Empty);

            return false;
        }

        private void ResetTraversalState(bool resetConfirmation, bool clearStickyTarget, bool clearLatestPath)
        {
            if (resetConfirmation)
                _traversalConfirmationGate.Reset();
            if (clearStickyTarget)
                ClearStickyOffscreenTarget();
            if (clearLatestPath)
                _dependencies.PathfindingService.ClearLatestPath();
        }

    }
}