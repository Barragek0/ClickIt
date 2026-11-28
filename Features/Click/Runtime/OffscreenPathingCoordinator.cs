namespace ClickIt.Features.Click.Runtime
{
    internal readonly record struct OffscreenPathingCoordinatorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        PathfindingService PathfindingService,
        OnscreenMechanicPathingBlocker OnscreenMechanicPathingBlocker,
        OffscreenTraversalTargetResolver TraversalTargetResolver,
        OffscreenStickyTargetHandler StickyTargetHandler,
        MovementSkillCoordinator MovementSkills,
        ClickLabelInteractionService LabelInteraction,
        Action<string> DebugLog,
        Action<string> HoldDebugTelemetryAfterSuccess,
        ClickDebugPublicationService ClickDebugPublisher,
        Func<Vector2, string, bool> PointIsInClickableArea,
        ClickSuccessAnchor? ClickSuccessAnchor = null,
        Func<Vector2, bool>? IsBlightBuildOrUpgradeIconAt = null,
        IOffscreenRuntimeSeam? RuntimeSeam = null,
        Func<bool>? IsBlightActive = null,
        Func<Vector2, Element?, float, bool>? IsSafeBlightWalkHover = null,
        Func<RectangleF, bool>? IsBlightMenuRegionClickable = null,
        Func<IReadOnlyList<LabelOnGround>?>? GetDelveLabels = null);

    internal sealed class OffscreenPathingCoordinator(OffscreenPathingCoordinatorDependencies dependencies)
    {
        private readonly OffscreenPathingCoordinatorDependencies _dependencies = dependencies;
        private readonly IOffscreenRuntimeSeam _runtimeSeam = dependencies.RuntimeSeam ?? OffscreenRuntimeSeam.Instance;
        private readonly OffscreenTraversalConfirmationGate _traversalConfirmationGate = new();

        private const float BlightIconAvoidOffset = 90f;
        private const float BlightWalkDistance = 220f;
        private const int MaxBlightWalkWaypoints = 32;
        private readonly (Vector2 Point, long Time)[] _rejectedBlightPoints = new (Vector2, long)[5];
        private int _nextRejectedBlightPoint;
        private (Vector2 Origin, Vector2 Click, long Since) _blightWalkAttempt;

        // Last target that an A* no-route block rejected, so the coordinator can skip re-running the doomed full-budget search for the same target during the backoff window.
        private long _blockedTargetAddress;
        private long _blockedAtMs;
        private Vector2? _blockedPlayerGrid;
        private bool _walkRejectedNoRoute;
        private const int NoRouteBackoffMs = 2500;

        // The traversal target's per-tick validation reads (Path/DistancePlayer/IsValid/IsHidden) are DLR-bound dynamic reads (~9-14KB each). While walking, the same target persists across ticks, so cache them keyed by the target's address for a short window.
        private readonly record struct CachedTraversalReads(long Address, string? Path, float Distance, bool IsValid, bool IsHidden, long AtMs);
        private CachedTraversalReads _cachedTraversalReads;
        private const long TraversalReadCacheWindowMs = 150;

        private CachedTraversalReads ReadTraversalTarget(Entity target)
        {
            long address = DynamicAccess.TryGetDynamicValue(target, DynamicAccessProfiles.Address, out object? rawAddress)
                && rawAddress != null
                ? Convert.ToInt64(rawAddress)
                : 0;
            long now = Environment.TickCount64;
            if (address != 0 && _cachedTraversalReads.Address == address && now - _cachedTraversalReads.AtMs < TraversalReadCacheWindowMs)
                return _cachedTraversalReads;

            string path = DynamicAccess.TryReadString(target, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            float distance = DynamicAccess.TryReadFloat(target, DynamicAccessProfiles.DistancePlayer, out float resolvedDist)
                ? resolvedDist
                : -1f;
            bool isValid = DynamicAccess.TryReadBool(target, DynamicAccessProfiles.IsValid, out bool rawValid) && rawValid;
            bool isHidden = DynamicAccess.TryReadBool(target, DynamicAccessProfiles.IsHidden, out bool rawHidden) && rawHidden;

            _cachedTraversalReads = new CachedTraversalReads(address, path, distance, isValid, isHidden, now);
            return _cachedTraversalReads;
        }

        private readonly record struct OffscreenTraversalTargetContext(
            Entity Target,
            string TargetPath);

        private void AddPathfindingStage(string message) => _dependencies.PathfindingService.AddDebugStage(message);

        public bool TryWalkTowardOffscreenTarget(Entity? preferredTarget = null, bool walkingToBlightTower = false, RectangleF? blightRegion = null)
        {
            _walkRejectedNoRoute = false;
            if (_dependencies.ClickDebugPublisher.ShouldCaptureClickDebug())
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("WalkTowardEntry",
                    string.Format("preferredTarget={0} setting={1}",
                        preferredTarget != null ? DynamicAccess.TryReadString(preferredTarget, DynamicAccessProfiles.Path, out string entryPrefPath) ? entryPrefPath : "set" : "none",
                        _dependencies.Settings.WalkTowardOffscreenLabels.Value), null);

            // Only gate on the general pathfinding setting when no specific target is provided.  Blight-specific pathfinding (with a target) must work even when the general setting is off.
            if (!_dependencies.Settings.WalkTowardOffscreenLabels.Value && preferredTarget == null)
            {
                AddPathfindingStage("Walk: disabled - WalkTowardOffscreenLabels setting is off");
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
                if (_dependencies.ClickDebugPublisher.ShouldCaptureClickDebug())
                    _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("WalkTowardNoTarget",
                        string.Format("preferredTarget={0} | TryStartTraversal returned no target",
                            preferredTarget != null
                                ? (DynamicAccess.TryReadString(preferredTarget, DynamicAccessProfiles.Path, out string walkPrefPath) ? walkPrefPath : "set")
                                : "none"), null);
                return false;
            }

            if (!TryBuildTraversalPath(context, out bool builtPath))
                return false;

            if (walkingToBlightTower || _dependencies.IsBlightActive?.Invoke() == true)
            {
                if (!_runtimeSeam.TryProjectWorldToScreen(_dependencies.GameController, context.Target, out Vector2 target))
                    return false;
                IReadOnlyList<PathfindingService.GridPoint>? route = builtPath ? _dependencies.PathfindingService.GetLatestGridPath() : null;
                return TryWalkInBlight(target, context.TargetPath, blightRegion, route);
            }

            // A movement click can activate the Crawler even when traversal has selected loot.
            LabelOnGround? crawler = FindVisibleCrawlerLabel(_dependencies.GetDelveLabels?.Invoke());
            RectangleF crawlerRect = default;
            if (crawler != null && !LabelGeometry.TryGetLabelRect(crawler, out crawlerRect))
                return false;

            if (!TryResolveTraversalClick(context, builtPath, crawlerRect, out bool resolvedFromPath, out Vector2 targetScreen, out Vector2 walkClick))
                return false;

            bool movementSkillUsed = _dependencies.MovementSkills.TryUseMovementSkillForOffscreenPathing(context.TargetPath, targetScreen, builtPath, out Vector2 movementSkillCastPoint, out string movementSkillDebug);
            if (movementSkillUsed)
            {
                AddPathfindingStage($"Walk: movement skill cast toward {context.TargetPath}");
                return HandleSuccessfulTraversalMovementSkill(context, builtPath, resolvedFromPath, targetScreen, movementSkillCastPoint, movementSkillDebug);
            }

            if (!string.IsNullOrWhiteSpace(movementSkillDebug))
            {
                AddPathfindingStage($"Walk: movement skill not used - {movementSkillDebug}");
                _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] Movement skill not used: {movementSkillDebug}");
            }

            PublishOffscreenMovementDebug(context.Target, context.TargetPath, builtPath, resolvedFromPath, true, targetScreen, walkClick, "BeforeClick", movementSkillDebug);

            // Pickup-to-next-pathfinding latency at the point the first walk click actually lands: the true delay between the last successful click (item picked up) and pathfinding resuming.
            long now = Environment.TickCount64;
            long lastClickAtMs = _dependencies.ClickSuccessAnchor?.Value ?? 0;
            if (lastClickAtMs > 0 && now - lastClickAtMs < 5000)
            {
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("WalkStartLatency",
                    $"{now - lastClickAtMs}ms since last successful click | target={context.TargetPath}", null);
            }

            Vector2 clickPos = ResolveBlightIconSafeClickPosition(walkClick, targetScreen, context.TargetPath);
            if (crawler != null && LabelClickPointSearch.IsPointInsideRect(clickPos, crawlerRect))
                return false;

            RectangleF window = _runtimeSeam.GetWindowRectangle(_dependencies.GameController);
            Func<Element?, bool>? validateSettledHover = crawler == null ? null : _ =>
            {
                if (!LabelGeometry.TryGetLabelRect(crawler, out RectangleF currentRect)
                    || LabelClickPointSearch.IsPointInsideRect(clickPos, currentRect))
                    return false;

                SystemDrawingPoint cursor = Mouse.GetCursorPosition();
                RectangleF currentWindow = _runtimeSeam.GetWindowRectangle(_dependencies.GameController);
                if (currentWindow.Width <= 0 || currentWindow.Height <= 0)
                    return false;
                var clientCursor = new Vector2(cursor.X - currentWindow.X, cursor.Y - currentWindow.Y);
                return !LabelClickPointSearch.IsPointInsideRect(clientCursor, currentRect);
            };
            bool clicked = _dependencies.LabelInteraction.PerformMechanicClick(
                clickPos + new Vector2(window.X, window.Y), interval: IntervalKind.Walk,
                validateSettledHover: validateSettledHover, clientClickPosition: clickPos);
            AddPathfindingStage(clicked
                ? $"Walk: click executed ({clickPos.X:F0},{clickPos.Y:F0})"
                : $"Walk: click REJECTED ({clickPos.X:F0},{clickPos.Y:F0})");
            return HandleTraversalClickResult(context, builtPath, resolvedFromPath, targetScreen, clickPos, movementSkillDebug, clicked);
        }

        private static LabelOnGround? FindVisibleCrawlerLabel(IReadOnlyList<LabelOnGround>? labels)
        {
            if (labels == null)
                return null;

            foreach (LabelOnGround label in labels)
            {
                if (DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item)
                    && DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path)
                    && MechanicIds.IsDelveCrawlerPath(path)
                    && DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? root)
                    && DynamicAccess.TryReadBool(root, DynamicAccessProfiles.IsVisible, out bool visible)
                    && visible)
                    return label;
            }

            return null;
        }

        // Position-only walk fallback for blight foundations whose entity has streamed out.
        public bool TryWalkTowardGridPosition(NumVector2 gridPos, RectangleF? blightRegion = null)
        {
            // Mirror the entity-walk's safety gates so the fallback never walks when the entity walk would have been aborted (ritual active, or a clickable on-screen mechanic available).
            if (OffscreenPathingMath.ShouldSkipOffscreenPathfindingForRitual(EntityHelpers.IsRitualActive(_dependencies.GameController)))
                return AbortOffscreenPathingForBlocker(
                    "[TryWalkTowardGridPosition] Skipping position walk because a RitualBlocker is active.",
                    "OffscreenPathingBlockedByRitual",
                    "RitualBlocker active");

            if (_dependencies.OnscreenMechanicPathingBlocker.ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable())
                return AbortOffscreenPathingForBlocker(
                    "[TryWalkTowardGridPosition] Skipping position walk because a clickable on-screen mechanic is available.",
                    null,
                    null);

            try
            {
                GameController? gameController = _dependencies.GameController;
                Camera? camera = gameController?.Game?.IngameState?.Camera;
                if (gameController == null || camera == null)
                    return false;

                float scale = 1f / PoeMapExtension.WorldToGridConversion;
                if (gameController.Player == null)
                    return false;
                NumVector2 raw = camera.WorldToScreen(new System.Numerics.Vector3(
                    gridPos.X * scale, gridPos.Y * scale, gameController.Player.PosNum.Z));
                Vector2 targetScreen = new(raw.X, raw.Y);
                bool clicked = TryWalkInBlight(targetScreen, "blight-foundation", blightRegion);
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                    "BlightBuildWalk",
                    clicked
                        ? $"directional walk toward ({gridPos.X:F0},{gridPos.Y:F0})"
                        : $"walk click rejected ({gridPos.X:F0},{gridPos.Y:F0})",
                    MechanicIds.Blight);
                return clicked;
            }
            catch
            {
                return false;
            }
        }

        private bool TryWalkInBlight(Vector2 target, string path, RectangleF? popup, IReadOnlyList<PathfindingService.GridPoint>? route = null)
        {
            GameController controller = _dependencies.GameController;
            Entity? playerEntity = _runtimeSeam.GetPlayer(controller);
            if (playerEntity == null || !_runtimeSeam.TryProjectWorldToScreen(controller, playerEntity, out Vector2 player))
                return false;

            RectangleF window = _runtimeSeam.GetWindowRectangle(controller);
            long now = Environment.TickCount64;
            int recentlyRejected = 0, blockedArea = 0, towerControl = 0, allowed = 0;
            bool CanClick(Vector2 point)
            {
                if (IsRecentlyRejectedBlightPoint(point, _rejectedBlightPoints, now))
                {
                    recentlyRejected++;
                    return false;
                }
                if (!_dependencies.PointIsInClickableArea(point, path))
                {
                    blockedArea++;
                    return false;
                }
                if (_dependencies.IsBlightBuildOrUpgradeIconAt?.Invoke(point) != false)
                {
                    towerControl++;
                    return false;
                }
                allowed++;
                return true;
            }

            Vector2 click;
            Vector2? walkOrigin = null;
            if (route is { Count: > 1 })
            {
                if (controller.IngameState?.Camera is not { } camera
                    || !_runtimeSeam.TryGetGridPosition(playerEntity, out Vector2 playerGrid))
                    return false;

                walkOrigin = playerGrid;
                if (_blightWalkAttempt.Since != 0 && now - _blightWalkAttempt.Since >= 450
                    && Vector2.DistanceSquared(playerGrid, _blightWalkAttempt.Origin) < 1f)
                {
                    RememberRejectedBlightPoint(_blightWalkAttempt.Click, now);
                    _blightWalkAttempt = default;
                    AddPathfindingStage("Blight walk: no movement after click; trying another point");
                }

                // Project the nearby grid nodes with the game camera. The debug screen path
                // approximates each axis independently and is unsuitable for tight corners.
                var playerWorld = playerEntity.PosNum;
                float scale = 1f / PoeMapExtension.WorldToGridConversion;
                Span<Vector2> waypoints = stackalloc Vector2[MaxBlightWalkWaypoints];
                int count = Math.Min(route.Count - 1, waypoints.Length);
                for (int i = 0; i < count; i++)
                {
                    PathfindingService.GridPoint node = route[i + 1];
                    NumVector2 screen = camera.WorldToScreen(new System.Numerics.Vector3(
                        playerWorld.X + (node.X - playerGrid.X) * scale,
                        playerWorld.Y + (node.Y - playerGrid.Y) * scale,
                        playerWorld.Z));
                    waypoints[i] = new Vector2(screen.X, screen.Y);
                }

                if (!TryResolveBlightRoutePoint(player, waypoints[..count], popup, window.Width, window.Height,
                    CanClick, _dependencies.IsBlightMenuRegionClickable, out click))
                {
                    if (!TryResolveBlightWalkPoint(player, target, popup, window.Width, window.Height,
                        CanClick, _dependencies.IsBlightMenuRegionClickable, out click, requirePopupFit: popup != null)
                        && !TryResolveBlightWalkPoint(player, target, popup, window.Width, window.Height,
                            CanClick, _dependencies.IsBlightMenuRegionClickable, out click))
                    {
                        string control = popup is { } rect
                            ? $"({rect.X:F0},{rect.Y:F0},{rect.Width:F0},{rect.Height:F0})" : "unavailable";
                        AddPathfindingStage($"Blight walk: no safe waypoint or repositioning point; player=({player.X:F0},{player.Y:F0}) target=({target.X:F0},{target.Y:F0}) window={window.Width:F0}x{window.Height:F0} control={control} waypoints={count} recent={recentlyRejected} area={blockedArea} tower={towerControl} allowed={allowed}");
                        return false;
                    }
                    AddPathfindingStage("Blight walk: repositioning after route points were blocked");
                }
            }
            else if (!TryResolveBlightWalkPoint(player, target, popup, window.Width, window.Height,
                CanClick, _dependencies.IsBlightMenuRegionClickable, out click))
            {
                return false;
            }

            bool clicked = TryBlightGroundClick(click, IntervalKind.Walk);
            if (clicked && walkOrigin is { } origin)
            {
                long since = _blightWalkAttempt.Since != 0 && Vector2.DistanceSquared(origin, _blightWalkAttempt.Origin) < 1f
                    ? _blightWalkAttempt.Since : Environment.TickCount64;
                _blightWalkAttempt = (origin, click, since);
            }
            return clicked;
        }

        internal static bool TryResolveBlightRoutePoint(
            Vector2 player, ReadOnlySpan<Vector2> waypoints, RectangleF? popup, float width, float height,
            Func<Vector2, bool> canClick, Func<RectangleF, bool>? popupClickable, out Vector2 point)
        {
            point = default;
            Vector2 previous = player;
            float distance = 0f;
            int inRange = 0;
            foreach (Vector2 waypoint in waypoints)
            {
                distance += Vector2.Distance(previous, waypoint);
                if (!float.IsFinite(distance) || distance > BlightWalkDistance)
                    break;

                previous = waypoint;
                inRange++;
            }

            bool found = false;
            if (popup is { } control && popupClickable != null)
            {
                // Stop as soon as a safe route point is expected to reveal the whole
                // control. A longer walk can carry the player past a usable tower.
                for (int i = 0; i < inRange; i++)
                {
                    Vector2 candidate = waypoints[i];
                    if (candidate.X < 0 || candidate.Y < 0 || candidate.X >= width || candidate.Y >= height
                        || Vector2.DistanceSquared(player, candidate) < 8f * 8f)
                        continue;
                    Vector2 offset = candidate - player;
                    RectangleF predicted = new(control.X - offset.X, control.Y - offset.Y, control.Width, control.Height);
                    if (popupClickable(predicted) && canClick(candidate))
                    {
                        point = candidate;
                        return true;
                    }
                }
            }

            for (int i = inRange - 1; i >= 0; i--)
            {
                Vector2 candidate = waypoints[i];
                if (candidate.X < 0 || candidate.Y < 0 || candidate.X >= width || candidate.Y >= height
                    || Vector2.DistanceSquared(player, candidate) < 8f * 8f || !canClick(candidate))
                    continue;

                if (!found)
                    point = candidate;
                found = true;
                Vector2 offset = candidate - player;
                if (popup is not { } bounds || popupClickable?.Invoke(
                    new RectangleF(bounds.X - offset.X, bounds.Y - offset.Y, bounds.Width, bounds.Height)) == true)
                {
                    point = candidate;
                    return true;
                }
            }
            return found;
        }

        internal bool TryBlightGroundClick(Vector2 point, IntervalKind interval)
        {
            GameController controller = _dependencies.GameController;
            if (controller.Player == null || _dependencies.IsSafeBlightWalkHover is not { } isSafeHover)
                return false;
            var before = controller.Player.PosNum;
            string? rejection = null;
            RectangleF window = _runtimeSeam.GetWindowRectangle(controller);
            if (window.Width <= 0 || window.Height <= 0)
                return false;
            bool clicked = _dependencies.LabelInteraction.PerformMechanicClick(point + new Vector2(window.X, window.Y), interval, hover =>
            {
                if (controller.Player == null || controller.IngameState?.Camera is not { } camera)
                    return false;
                var groundLabels = controller.IngameState.IngameUi?.ItemsOnGroundLabelElement;
                if (groundLabels == null || IsAuguryCardHoverPath(groundLabels.ItemOnHoverPath))
                {
                    rejection = "Blight walk: ground hover unavailable or Augury object under cursor";
                    return false;
                }
                float drift = NumVector2.Distance(camera.WorldToScreen(before), camera.WorldToScreen(controller.Player.PosNum));
                // Reserve room for another input frame's worth of drift and re-read the
                // live icon geometry after the cursor has settled.
                if (float.IsFinite(drift) && drift <= 100f && isSafeHover(point, hover, drift))
                    return true;
                rejection = "Blight walk: settled hover or camera drift rejected the point";
                return false;
            }, clientClickPosition: point);
            if (!clicked && rejection != null)
            {
                RememberRejectedBlightPoint(point, Environment.TickCount64);
                AddPathfindingStage(rejection);
            }
            return clicked;
        }

        private void RememberRejectedBlightPoint(Vector2 point, long now)
        {
            _rejectedBlightPoints[_nextRejectedBlightPoint] = (point, now);
            _nextRejectedBlightPoint = (_nextRejectedBlightPoint + 1) % _rejectedBlightPoints.Length;
        }

        internal static bool IsAuguryCardHoverPath(string? path)
            => path?.Contains("Terrain/Leagues/Augury/", StringComparison.OrdinalIgnoreCase) == true
                && path.Contains("/AuguryCardObject", StringComparison.OrdinalIgnoreCase);

        internal static bool IsRecentlyRejectedBlightPoint(Vector2 point, ReadOnlySpan<(Vector2 Point, long Time)> rejected, long now)
        {
            foreach ((Vector2 previous, long time) in rejected)
                if (time > 0 && now >= time && now - time <= 2000 && Vector2.DistanceSquared(point, previous) <= 30f * 30f)
                    return true;
            return false;
        }

        internal static bool TryResolveBlightWalkPoint(
            Vector2 player, Vector2 target, RectangleF? popup, float width, float height,
            Func<Vector2, bool> canClick, Func<RectangleF, bool>? popupClickable, out Vector2 point,
            bool requirePopupFit = false)
        {
            point = default;
            Vector2 direction = target - player;
            float distance = BlightWalkDistance;
            if (popup is { } region && width > region.Width + 48 && height > region.Height + 48)
            {
                // Center the popup within the playable viewport, using a short approach
                // for slight clipping and bounded steps for a distant tower.
                Vector2 center = new(region.X + region.Width / 2f, region.Y + region.Height / 2f);
                Vector2 desiredCenter = new(
                    Math.Clamp(center.X, region.Width / 2f + 24, width - region.Width / 2f - 24),
                    Math.Clamp(center.Y, region.Height / 2f + 24, height - region.Height / 2f - 24));
                Vector2 shift = center - desiredCenter;
                if (shift.LengthSquared() < 1f)
                    shift = center - new Vector2(width / 2f, height / 2f);
                if (shift.LengthSquared() >= 1f)
                {
                    direction = shift;
                    distance = Math.Clamp(shift.Length() + 32f, 80f, 260f);
                }
            }

            float length = direction.Length();
            if (!float.IsFinite(length) || length < 1f)
                return false;
            direction /= length;
            Vector2 perpendicular = new(-direction.Y, direction.X);
            bool found = false;
            ReadOnlySpan<float> offsets = [0f, -0.4f, 0.4f, -0.8f, 0.8f, -1.5f, 1.5f, -2.5f, 2.5f];
            ReadOnlySpan<float> distanceFactors = [1f, 0.7f, 0.45f, 1.25f];
            foreach (float distanceFactor in distanceFactors)
            {
                foreach (float side in offsets)
                {
                    Vector2 offset = Vector2.Normalize(direction + (perpendicular * side)) * (distance * distanceFactor);
                    Vector2 candidate = player + offset;
                    if (candidate.X < 0 || candidate.Y < 0 || candidate.X >= width || candidate.Y >= height || !canClick(candidate))
                        continue;
                    if (!found && !requirePopupFit)
                        point = candidate;
                    found = true;
                    if (popup is not { } bounds || popupClickable?.Invoke(
                        new RectangleF(bounds.X - offset.X, bounds.Y - offset.Y, bounds.Width, bounds.Height)) == true)
                    {
                        point = candidate;
                        return true;
                    }
                }
            }
            return found && !requirePopupFit;
        }

        // Build/upgrade icons are PADDED UNCLICKABLE BOXES: walk the click back along the player->target line until it is outside every box and clickable.
        private Vector2 ResolveBlightIconSafeClickPosition(Vector2 walkClick, Vector2 targetScreen, string targetPath)
        {
            if (_dependencies.IsBlightBuildOrUpgradeIconAt == null || !_dependencies.IsBlightBuildOrUpgradeIconAt(walkClick))
                return walkClick;

            Vector2 offset = walkClick;
            try
            {
                Size2F win = GameWindowRectResolver.Resolve(_dependencies.GameController).Size;
                bool hasWindow = win.Width > 0f && win.Height > 0f;

                offset = hasWindow
                    ? ResolveSafeClickAlongPath(targetScreen, win, point =>
                        !_dependencies.IsBlightBuildOrUpgradeIconAt(point)
                        && _dependencies.PointIsInClickableArea(point, targetPath))
                    : new Vector2(walkClick.X + BlightIconAvoidOffset, walkClick.Y + BlightIconAvoidOffset);

                // The path search falls back to the target when nothing else matched — never click a point that is still on/near an icon: push it away from the screen center instead.
                if (hasWindow && _dependencies.IsBlightBuildOrUpgradeIconAt(offset))
                    offset = EscapeIconBox(offset, win);
            }
            catch
            {
                offset = new Vector2(walkClick.X + BlightIconAvoidOffset, walkClick.Y + BlightIconAvoidOffset);
            }

            AddPathfindingStage($"Walk: click offset from blight icon -> ({offset.X:F0},{offset.Y:F0})");
            _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] Click would hit a blight tower build/upgrade icon - offsetting to ({offset.X:F0},{offset.Y:F0})");
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("BlightIconAvoided", "Pathfinding click offset away from a blight tower icon", null);
            return offset;
        }

        // Pushes a point away from the screen center in increasing steps until it clears every icon box — the last-resort escape when no point along the player→target line is safe.
        private Vector2 EscapeIconBox(Vector2 point, Size2F win)
        {
            for (float d = BlightIconAvoidOffset; d <= BlightIconAvoidOffset * 4f; d += BlightIconAvoidOffset)
            {
                Vector2 candidate = OffsetAwayFromScreenCenter(point, win, d);
                if (!_dependencies.IsBlightBuildOrUpgradeIconAt!(candidate))
                    return candidate;
            }
            return point;
        }

        internal static Vector2 ResolveSafeClickAlongPath(Vector2 targetScreen, Size2F window, Func<Vector2, bool> isClickable)
        {
            Vector2 center = new(window.Width * 0.5f, window.Height * 0.5f);
            Vector2 direction = center - targetScreen;
            float lenSq = (direction.X * direction.X) + (direction.Y * direction.Y);
            if (lenSq < 1f)
                return targetScreen;

            float len = MathF.Sqrt(lenSq);
            Vector2 unit = new(direction.X / len, direction.Y / len);
            for (float d = BlightIconAvoidOffset; d <= 400f; d += 50f)
            {
                Vector2 candidate = new(
                    SystemMath.Clamp(targetScreen.X + (unit.X * d), 0f, window.Width),
                    SystemMath.Clamp(targetScreen.Y + (unit.Y * d), 0f, window.Height));
                if (isClickable(candidate))
                    return candidate;
            }

            return targetScreen;
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

            if (DynamicAccess.TryReadString(stickyTarget, DynamicAccessProfiles.Path, out string stickyPath))
            {
                string? fallbackId = MechanicIds.IsDelveCrawlerPath(stickyPath) ? MechanicIds.DelveCrawler
                    : MechanicIds.IsIceCavesFossilPath(stickyPath) ? MechanicIds.IceCavesFossil
                    : MechanicIds.IsFatesBossPath(stickyPath) ? MechanicIds.FatesBoss : null;
                if (fallbackId != null)
                {
                    ClearStickyOffscreenTarget();
                    bool enabled = fallbackId switch
                    {
                        MechanicIds.DelveCrawler => _dependencies.Settings.ClickDelveCrawler.Value,
                        MechanicIds.IceCavesFossil => _dependencies.Settings.ClickDelveSpawners.Value,
                        _ => _dependencies.Settings.ClickFates.Value
                    };
                    if (!enabled || TryHandleHigherPriorityWalkTarget(fallbackId))
                        return true;
                    if (!ClickSafetyPolicy.IsWithinSearchRadius(_dependencies.Settings, stickyTarget))
                        return false;
                }
            }

            // Keep the route committed while the target is outside click range.
            if (!ClickSafetyPolicy.IsWithinSearchRadius(_dependencies.Settings, stickyTarget))
                return TryWalkTowardOffscreenTarget(stickyTarget);

            if (_dependencies.StickyTargetHandler.TryClickStickyTargetIfPossible(stickyTarget, windowTopLeft, allLabels, out bool needsApproach))
                return true;

            if (needsApproach)
            {
                _ = TryWalkTowardOffscreenTarget(stickyTarget);
                return true;
            }
            // The label click failed, so fall to fresh resolution instead of re-walking a stale target.
            _ = TryWalkTowardOffscreenTarget();
            return true;
        }

        // Probe fresh competing targets before committing to a fallback. A no-route result rejects
        // that target for this pass; other failures retain the safety gate and consume the tick.
        internal bool TryHandleHigherPriorityWalkTarget(string fallbackId)
        {
            if (!_dependencies.Settings.WalkTowardOffscreenLabels.Value)
                return false;

            HashSet<long> excluded = [];
            for (int i = 0; i < 32; i++)
            {
                Entity? target = _dependencies.TraversalTargetResolver.ResolveHigherPriorityWalkTarget(
                    excluded, MechanicIds.GetFallbackTier(fallbackId),
                    fallbackId == MechanicIds.FatesBoss ? MechanicIds.Fates : null);
                if (target == null)
                    return false;
                long address = DynamicAccess.TryGetDynamicValue(target, DynamicAccessProfiles.Address, out object? rawAddress)
                    && rawAddress != null ? Convert.ToInt64(rawAddress) : 0;
                if (address == 0 || !excluded.Add(address))
                    return true;
                if (TryWalkTowardOffscreenTarget(target))
                    return true;
                if (!_walkRejectedNoRoute)
                    return true;
                ClearStickyOffscreenTarget();
            }
            return true;
        }

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
            // The snapshot only feeds the pathfinding debug overlay section and the copy-all dump, both of which require DebugMode; skip the DLR reads entirely when debug is off.
            if (!_dependencies.Settings.DebugMode.Value)
                return;

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

        internal int GetRemainingOffscreenPathNodeCount()
        {
            IReadOnlyList<PathfindingService.GridPoint> path = _dependencies.PathfindingService.GetLatestGridPath();
            Entity? player = _runtimeSeam.GetPlayer(_dependencies.GameController);
            if (player == null)
                return 0;

            if (!_runtimeSeam.TryGetGridPosition(player, out Vector2 playerGridPosition))
                return 0;

            int nearest = OffscreenPathingMath.FindClosestPathIndexToPlayer(
                path,
                new PathfindingService.GridPoint((int)playerGridPosition.X, (int)playerGridPosition.Y));

            return OffscreenPathingMath.CountRemainingPathNodes(path, nearest);
        }

        private bool TryResolveOffscreenTargetScreenPointFromPath(out Vector2 targetScreen)
        {
            targetScreen = default;

            Entity? player = _runtimeSeam.GetPlayer(_dependencies.GameController);
            if (player == null)
                return false;

            IReadOnlyList<PathfindingService.GridPoint> path = _dependencies.PathfindingService.GetLatestGridPath();
            if (path.Count < 2)
                return false;

            if (!_runtimeSeam.TryGetGridPosition(player, out Vector2 playerGridPosition))
                return false;

            PathfindingService.GridPoint playerGrid = new((int)playerGridPosition.X, (int)playerGridPosition.Y);
            int nearestIndex = OffscreenPathingMath.FindClosestPathIndexToPlayer(path, playerGrid);
            if (nearestIndex < 0)
                return false;

            if (!OffscreenPathingMath.TryGetSmoothedPathDirection(path, playerGrid, nearestIndex, out float deltaX, out float deltaY))
                return false;

            RectangleF window = _runtimeSeam.GetWindowRectangle(_dependencies.GameController);
            Vector2 center = new(window.Width * 0.5f, window.Height * 0.5f);
            float radius = SystemMath.Min(window.Width, window.Height) * 0.30f;
            return OffscreenPathingMath.TryComputeGridDirectionPoint(center, deltaX, deltaY, radius, out targetScreen);
        }

        private bool TryResolveOffscreenTargetScreenPoint(Entity target, out Vector2 targetScreen)
        {
            RectangleF window = _runtimeSeam.GetWindowRectangle(_dependencies.GameController);
            Vector2 center = new(window.Width * 0.5f, window.Height * 0.5f);
            float radius = SystemMath.Min(window.Width, window.Height) * 0.30f;
            TryGetGridDelta(target, out float deltaX, out float deltaY);

            if (target.Type == EntityType.WorldItem
                && OffscreenPathingMath.TryComputeGridDirectionPoint(center, deltaX, deltaY, radius, out targetScreen))
                return true;

            RectangleF client = new(0, 0, window.Width, window.Height);
            if (_runtimeSeam.TryProjectWorldToScreen(_dependencies.GameController, target, out Vector2 projected)
                && OffscreenPathingMath.IsFinite(projected) && OffscreenPathingMath.IsInsideWindow(client, projected)
                && !OffscreenPathingMath.IsNearCorner(projected, client))
            {
                // Target is on screen - but can we actually click there? If the projected position falls in a blocked zone (minimap, right panel, etc.), treat the target as still offscreen so pathfinding keeps walking until it moves into a clickable area.
                if (_dependencies.PointIsInClickableArea(projected, "offscreen"))
                {
                    targetScreen = projected;
                    return true;
                }
            }

            return OffscreenPathingMath.TryComputeGridDirectionPoint(center, deltaX, deltaY, radius, out targetScreen);
        }

        private bool TryResolveOnScreenTargetScreenPoint(Entity target, out Vector2 targetScreen)
        {
            targetScreen = default;
            if (!_runtimeSeam.TryProjectWorldToScreen(_dependencies.GameController, target, out Vector2 projected))
                return false;

            if (!OffscreenPathingMath.IsFinite(projected))
                return false;

            RectangleF window = _runtimeSeam.GetWindowRectangle(_dependencies.GameController);
            RectangleF client = new(0, 0, window.Width, window.Height);
            if (!OffscreenPathingMath.IsInsideWindow(client, projected) || OffscreenPathingMath.IsNearCorner(projected, client))
                return false;

            if (!_dependencies.PointIsInClickableArea(projected, "offscreen"))
                return false;

            targetScreen = projected;
            return true;
        }

        private void TryGetGridDelta(Entity target, out float deltaX, out float deltaY)
        {
            Entity? player = _runtimeSeam.GetPlayer(_dependencies.GameController);
            if (player == null)
            {
                deltaX = 0f;
                deltaY = 0f;
                return;
            }

            if (!_runtimeSeam.TryGetGridPosition(player, out Vector2 playerGridPosition)
                || !_runtimeSeam.TryGetGridPosition(target, out Vector2 targetGridPosition))
            {
                deltaX = 0f;
                deltaY = 0f;
                return;
            }

            deltaX = targetGridPosition.X - playerGridPosition.X;
            deltaY = targetGridPosition.Y - playerGridPosition.Y;
        }

        private bool TryStartTraversal(Entity? preferredTarget, out OffscreenTraversalTargetContext context)
        {
            context = default;

            if (!TryResolveTraversalTarget(preferredTarget, out Entity? target) || target == null)
                return false;

            string targetPath = ReadTraversalTarget(target).Path ?? string.Empty;
            if (preferredTarget == null && _traversalConfirmationGate.ShouldDelay(target, targetPath, out long remainingDelayMs))
            {
                _dependencies.PathfindingService.ClearLatestPath();
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage(
                    "OffscreenPathingAwaitingConfirmation",
                    $"target={targetPath} remainingMs={remainingDelayMs}");
                return false;
            }

            // Keep the gate latched so a confirmed target does not re-enter the delay and wipe the path line every other tick.
            _dependencies.StickyTargetHandler.SetStickyOffscreenTarget(target);
            context = new OffscreenTraversalTargetContext(target, targetPath);
            return true;
        }

        private bool TryBuildTraversalPath(OffscreenTraversalTargetContext context, out bool builtPath)
        {
            Vector2? playerGrid = ReadPlayerGridForNoRouteBackoff();
            // The same chest may be reachable once the player moves to another tile.
            if (playerGrid == _blockedPlayerGrid
                && OffscreenPathingMath.ShouldApplyNoRouteBackoff(
                    context.Target.Address,
                    _blockedTargetAddress,
                    Environment.TickCount64,
                    _blockedAtMs,
                    NoRouteBackoffMs))
            {
                AddPathfindingStage($"Walk: A* no-route backoff active target={context.TargetPath}");
                PublishOffscreenMovementDebug(context.Target, context.TargetPath, false, false, false, default, default, "BlockedNoRoute", PathfindingService.AStarNoRouteFailureReason);
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("OffscreenPathingBlockedNoRoute", $"target={context.TargetPath}");
                _walkRejectedNoRoute = true;
                builtPath = false;
                return false;
            }

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
            {
                _blockedTargetAddress = 0;
                return true;
            }

            PathfindingDebugSnapshot pathfindingSnapshot = _dependencies.PathfindingService.GetDebugSnapshot();
            if (OffscreenPathingMath.ShouldBlockOffscreenTraversalAfterPathBuildFailure(pathfindingSnapshot.LastFailureReason))
            {
                _blockedTargetAddress = context.Target.Address;
                _blockedAtMs = Environment.TickCount64;
                _blockedPlayerGrid = playerGrid;
                _walkRejectedNoRoute = true;
                AddPathfindingStage($"Walk: BLOCKED - A* did not find a route target={context.TargetPath}");
                PublishOffscreenMovementDebug(context.Target, context.TargetPath, builtPath, false, false, default, default, "BlockedNoRoute", pathfindingSnapshot.LastFailureReason);
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("OffscreenPathingBlockedNoRoute", $"target={context.TargetPath}");
                _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Skipping offscreen traversal because A* did not find a route.");
                return false;
            }

            AddPathfindingStage($"Walk: no A* route - directional fallback target={context.TargetPath}");
            _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Pathfinding route not found; trying directional walk click.");
            return true;
        }

        private Vector2? ReadPlayerGridForNoRouteBackoff()
        {
            try
            {
                Entity? player = _runtimeSeam.GetPlayer(_dependencies.GameController);
                return player != null && _runtimeSeam.TryGetGridPosition(player, out Vector2 grid)
                    ? new Vector2(MathF.Floor(grid.X), MathF.Floor(grid.Y))
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private bool TryResolveTraversalClick(
            OffscreenTraversalTargetContext context,
            bool builtPath,
            RectangleF crawlerRect,
            out bool resolvedFromPath,
            out Vector2 targetScreen,
            out Vector2 walkClick)
        {
            walkClick = default;

            resolvedFromPath = false;
            targetScreen = default;
            if (builtPath)
                resolvedFromPath = TryResolveOffscreenTargetScreenPointFromPath(out targetScreen);

            // The path-based point sits at a fixed radius in the path's direction, which overshoots close targets (e.g. a tower already beside the player). Prefer the target's real on-screen projection so the player walks onto it instead of around it.
            bool resolvedOnScreen = false;
            if (resolvedFromPath
                && TryResolveOnScreenTargetScreenPoint(context.Target, out Vector2 onScreenTargetScreen))
            {
                targetScreen = onScreenTargetScreen;
                resolvedFromPath = false;
                resolvedOnScreen = true;
            }

            if (!resolvedFromPath && !resolvedOnScreen)
            {
                if (!TryResolveOffscreenTargetScreenPoint(context.Target, out targetScreen))
                {
                    AddPathfindingStage("Walk: target screen point FAILED");
                    PublishOffscreenMovementDebug(context.Target, context.TargetPath, builtPath, false, false, default, default, "ResolveTargetScreenFailed");
                    _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Failed to resolve target screen point.");
                    return false;
                }
            }

            RectangleF win = _runtimeSeam.GetWindowRectangle(_dependencies.GameController);
            Func<Vector2, string, bool> isClickable = _dependencies.PointIsInClickableArea;
            if (crawlerRect.Width > 0 && crawlerRect.Height > 0)
                isClickable = (point, path) => !LabelClickPointSearch.IsPointInsideRect(point, crawlerRect)
                    && _dependencies.PointIsInClickableArea(point, path);
            if (OffscreenPathingMath.TryResolveDirectionalWalkClickPosition(
                    new RectangleF(0, 0, win.Width, win.Height), targetScreen, context.TargetPath, isClickable, out walkClick))
                return true;

            AddPathfindingStage("Walk: directional click point FAILED");
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
                AddPathfindingStage($"Walk: no walk target resolved (preferred={prefPath})");
                _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] ResolveTraversalTarget: preferred={prefPath} -> no target resolved");
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("ResolveTargetNull",
                    $"preferred={prefPath} | ResolveNearestOffscreenWalkTarget returned null", null);
                ResetTraversalState(resetConfirmation: true, clearStickyTarget: preferredTarget != null, clearLatestPath: true);
                return false;
            }

            CachedTraversalReads reads = ReadTraversalTarget(target);
            string targetPath = reads.Path ?? string.Empty;
            float targetDist = reads.Distance;
            _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] ResolveTraversalTarget: found target path={targetPath} dist={targetDist:F1}");

            bool isValid = reads.IsValid;
            bool isHidden = reads.IsHidden;
            if (!isValid || isHidden || OffscreenPathingMath.IsEntityHiddenByMinimapIcon(target)
                || !OffscreenPathingMath.IsEligibleDelveTraversalTarget(target, targetPath))
            {
                AddPathfindingStage($"Walk: target REJECTED path={targetPath} valid={isValid} hidden={isHidden} minimap={OffscreenPathingMath.IsEntityHiddenByMinimapIcon(target)}");
                _dependencies.DebugLog($"[TryWalkTowardOffscreenTarget] ResolveTraversalTarget: target rejected valid={isValid} hidden={isHidden}");
                _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("ResolveTargetInvalid",
                    $"path={targetPath} dist={targetDist:F1} valid={isValid} hidden={isHidden}", null);
                ResetTraversalState(resetConfirmation: true, clearStickyTarget: true, clearLatestPath: true);
                target = null;
                return false;
            }

            AddPathfindingStage($"Walk: target found path={targetPath} dist={targetDist:F1}");
            _dependencies.ClickDebugPublisher.PublishClickFlowDebugStage("ResolveTargetFound",
                $"path={targetPath} dist={targetDist:F1}", null);
            return true;
        }

        private bool AbortOffscreenPathingForBlocker(string debugMessage, string? debugStage, string? debugDetails)
        {
            AddPathfindingStage($"Walk: aborted - {debugMessage}");
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
