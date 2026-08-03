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
        Func<Vector2, string, bool> PointIsInClickableArea,
        Func<Vector2, bool>? IsBlightBuildOrUpgradeIconAt = null,
        IOffscreenRuntimeSeam? RuntimeSeam = null);

    internal sealed class OffscreenPathingCoordinator(OffscreenPathingCoordinatorDependencies dependencies)
    {
        private readonly OffscreenPathingCoordinatorDependencies _dependencies = dependencies;
        private readonly IOffscreenRuntimeSeam _runtimeSeam = dependencies.RuntimeSeam ?? OffscreenRuntimeSeam.Instance;
        private readonly OffscreenTraversalConfirmationGate _traversalConfirmationGate = new();

        private const float BlightIconAvoidOffset = 90f;

        private readonly record struct OffscreenTraversalTargetContext(
            Entity Target,
            string TargetPath);

        public bool TryWalkTowardOffscreenTarget(Entity? preferredTarget = null)
        {
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("WalkTowardEntry",
                string.Format("preferredTarget={0} setting={1}",
                    preferredTarget != null ? DynamicAccess.TryReadString(preferredTarget, DynamicAccessProfiles.Path, out string entryPrefPath) ? entryPrefPath : "set" : "none",
                    _dependencies.Settings.WalkTowardOffscreenLabels.Value), null);

            // Only gate on the general pathfinding setting when no specific
            // target is provided.  Blight-specific pathfinding (with a target)
            // must work even when the general setting is off.
            if (!_dependencies.Settings.WalkTowardOffscreenLabels.Value && preferredTarget == null)
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("WalkTowardDisabled",
                    "WalkTowardOffscreenLabels setting is OFF and no preferredTarget", null);
                return false;
            }

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
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("WalkTowardNoTarget",
                    string.Format("preferredTarget={0} | TryStartTraversal returned no target",
                        preferredTarget != null
                            ? (DynamicAccess.TryReadString(preferredTarget, DynamicAccessProfiles.Path, out string walkPrefPath) ? walkPrefPath : "set")
                            : "none"), null);
                return false;
            }

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

            Vector2 clickPos = ResolveBlightIconSafeClickPosition(walkClick);
            bool clicked = _dependencies.LabelInteraction.PerformMechanicClick(clickPos);
            return HandleTraversalClickResult(context, builtPath, resolvedFromPath, targetScreen, clickPos, movementSkillDebug, clicked);
        }

        // Pathfinding clicks must never land on a blight tower's build/upgrade icon (that would
        // accidentally build or upgrade a tower). When the resolved click would hit one, offset the
        // click toward the screen center so pathfinding continues safely.
        private Vector2 ResolveBlightIconSafeClickPosition(Vector2 walkClick)
        {
            if (_dependencies.IsBlightBuildOrUpgradeIconAt == null || !_dependencies.IsBlightBuildOrUpgradeIconAt(walkClick))
                return walkClick;

            Vector2 offset;
            try
            {
                Size2F win = _dependencies.GameController?.Window.GetWindowRectangleTimeCache.Size ?? default;
                offset = win.Width > 0f && win.Height > 0f
                    ? OffsetAwayFromScreenCenter(walkClick, win, BlightIconAvoidOffset)
                    : new Vector2(walkClick.X + BlightIconAvoidOffset, walkClick.Y + BlightIconAvoidOffset);
            }
            catch
            {
                offset = new Vector2(walkClick.X + BlightIconAvoidOffset, walkClick.Y + BlightIconAvoidOffset);
            }

            _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] Click would hit a blight tower build/upgrade icon — offsetting to ({offset.X:F0},{offset.Y:F0})");
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("BlightIconAvoided", "Pathfinding click offset away from a blight tower icon", null);
            return offset;
        }

        internal static Vector2 OffsetAwayFromScreenCenter(Vector2 point, Size2F window, float distance)
        {
            float dx = point.X < window.Width * 0.5f ? distance : -distance;
            float dy = point.Y < window.Height * 0.5f ? distance : -distance;
            return new Vector2(
                SystemMath.Clamp(point.X + dx, 0f, window.Width),
                SystemMath.Clamp(point.Y + dy, 0f, window.Height));
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
            Entity? player = _runtimeSeam.GetPlayer(_dependencies.GameController);
            Vector2 playerGrid = player != null && _runtimeSeam.TryGetGridPosition(player, out Vector2 resolvedPlayerGrid)
                ? resolvedPlayerGrid
                : default;
            Vector2 targetGrid = _runtimeSeam.TryGetGridPosition(target, out Vector2 resolvedTargetGrid)
                ? resolvedTargetGrid
                : default;
            RectangleF win = _runtimeSeam.GetWindowRectangle(_dependencies.GameController);
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
            RectangleF win = _runtimeSeam.GetWindowRectangle(_dependencies.GameController);
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

            string targetPath = DynamicAccess.TryReadString(target, DynamicAccessProfiles.Path, out string resolvedTargetPath)
                ? resolvedTargetPath
                : string.Empty;
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
            try
            {
                builtPath = _dependencies.PathfindingService.TryBuildPathToTarget(
                    _dependencies.GameController,
                    context.Target,
                    _dependencies.Settings.OffscreenPathfindingSearchBudget.Value);
            }
            catch (Exception ex)
            {
                _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] Path build failed: {ex.Message}");
                builtPath = false;
                return false;
            }

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
                string prefPath = preferredTarget != null
                    ? (DynamicAccess.TryReadString(preferredTarget, DynamicAccessProfiles.Path, out string resolvedPrefPath) ? resolvedPrefPath : "set")
                    : "null";
                _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] ResolveTraversalTarget: preferred={prefPath} -> no target resolved");
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("ResolveTargetNull",
                    $"preferred={prefPath} | ResolveNearestOffscreenWalkTarget returned null", null);
                ResetTraversalState(resetConfirmation: true, clearStickyTarget: preferredTarget != null, clearLatestPath: true);
                return false;
            }

            string targetPath = DynamicAccess.TryReadString(target, DynamicAccessProfiles.Path, out string resolvedTargetPath)
                ? resolvedTargetPath
                : string.Empty;
            float targetDist = DynamicAccess.TryReadFloat(target, DynamicAccessProfiles.DistancePlayer, out float resolvedDist)
                ? resolvedDist
                : -1f;
            _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] ResolveTraversalTarget: found target path={targetPath} dist={targetDist:F1}");

            bool isValid = DynamicAccess.TryReadBool(target, DynamicAccessProfiles.IsValid, out bool resolvedIsValid)
                && resolvedIsValid;
            bool isHidden = DynamicAccess.TryReadBool(target, DynamicAccessProfiles.IsHidden, out bool resolvedIsHidden)
                && resolvedIsHidden;
            if (!isValid || isHidden || OffscreenPathingMath.IsEntityHiddenByMinimapIcon(target))
            {
                _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] ResolveTraversalTarget: target rejected valid={isValid} hidden={isHidden}");
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("ResolveTargetInvalid",
                    $"path={targetPath} dist={targetDist:F1} valid={isValid} hidden={isHidden}", null);
                ResetTraversalState(resetConfirmation: true, clearStickyTarget: true, clearLatestPath: true);
                target = null;
                return false;
            }

            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("ResolveTargetFound",
                $"path={targetPath} dist={targetDist:F1}", null);
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