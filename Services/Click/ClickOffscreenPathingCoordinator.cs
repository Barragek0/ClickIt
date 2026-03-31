using ClickIt.Utils;
using ClickIt.Services.Label.Classification;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    internal readonly record struct OffscreenPathingCoordinatorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        LabelFilterService LabelFilterService,
        ShrineService ShrineService,
        PathfindingService PathfindingService,
        Func<long> GetStickyOffscreenTargetAddress,
        Action<long> SetStickyOffscreenTargetAddress,
        Action<string> DebugLog,
        Action<string> HoldDebugTelemetryAfterSuccess,
        Action<string, string> PublishClickFlowDebugStage,
        Func<bool> HasClickableAltars,
        Func<Entity?> ResolveNextShrineCandidate,
        Func<(LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers)> ResolveVisibleMechanicCandidates,
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
        Action InvalidateShrineCache,
        Func<IReadOnlyList<LabelOnGround>?> GetLabelsForOffscreenSelection,
        Action RefreshMechanicPriorityCaches,
        Func<float, string?, MechanicRank> BuildMechanicRank);

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

            if (ClickService.ShouldSkipOffscreenPathfindingForRitual(EntityHelpers.IsRitualActive(_dependencies.GameController)))
            {
                ResetPendingTargetConfirmation();
                ClearStickyOffscreenTarget();
                _dependencies.PathfindingService.ClearLatestPath();
                _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Skipping offscreen pathfinding because a RitualBlocker is active.");
                _dependencies.PublishClickFlowDebugStage("OffscreenPathingBlockedByRitual", "RitualBlocker active");
                return false;
            }

            if (ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable())
            {
                ResetPendingTargetConfirmation();
                ClearStickyOffscreenTarget();
                _dependencies.PathfindingService.ClearLatestPath();
                _dependencies.DebugLog("[TryWalkTowardOffscreenTarget] Skipping offscreen pathfinding because a clickable on-screen mechanic is available.");
                return false;
            }

            Entity? target = preferredTarget ?? ResolveNearestOffscreenWalkTarget();
            if (target == null)
            {
                ResetPendingTargetConfirmation();
                if (preferredTarget != null)
                    ClearStickyOffscreenTarget();

                _dependencies.PathfindingService.ClearLatestPath();
                return false;
            }

            if (!target.IsValid || target.IsHidden || ClickService.IsEntityHiddenByMinimapIcon(target))
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

            if (TryClickStickyTargetIfPossible(stickyTarget, windowTopLeft, allLabels))
                return true;

            _ = TryWalkTowardOffscreenTarget(stickyTarget);
            return true;
        }

        public void SetStickyOffscreenTarget(Entity target)
            => _dependencies.SetStickyOffscreenTargetAddress(target.Address);

        public void ClearStickyOffscreenTarget()
            => _dependencies.SetStickyOffscreenTargetAddress(0);

        private bool ShouldDelayTraversalForPendingTarget(Entity target, string targetPath, out long remainingDelayMs)
        {
            var confirmation = ClickService.EvaluateOffscreenTraversalTargetConfirmation(
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
        {
            target = null;

            long stickyAddress = _dependencies.GetStickyOffscreenTargetAddress();
            if (stickyAddress == 0)
                return false;

            target = FindEntityByAddress(stickyAddress);
            if (target == null || !target.IsValid || target.IsHidden || ClickService.IsEntityHiddenByMinimapIcon(target))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            if (ShrineService.IsShrine(target) && !ShrineService.IsClickableShrineCandidate(target))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            string stickyPath = target.Path ?? string.Empty;
            bool isEldritchAltar = ClickService.IsEldritchAltarPath(stickyPath);
            if (ClickService.ShouldDropStickyTargetForUntargetableEldritchAltar(isEldritchAltar, target.IsTargetable))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            return true;
        }

        public Entity? FindEntityByAddress(long address)
        {
            if (address == 0 || _dependencies.GameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return null;

            foreach (var kv in _dependencies.GameController.EntityListWrapper.ValidEntitiesByType)
            {
                var entities = kv.Value;
                if (entities == null)
                    continue;

                for (int i = 0; i < entities.Count; i++)
                {
                    Entity entity = entities[i];
                    if (entity != null && ClickService.IsSameEntityAddress(address, entity.Address))
                        return entity;
                }
            }

            return null;
        }

        public bool IsStickyTarget(Entity? entity)
            => entity != null && ClickService.IsSameEntityAddress(_dependencies.GetStickyOffscreenTargetAddress(), entity.Address);

        private bool TryClickStickyTargetIfPossible(Entity stickyTarget, Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (ShrineService.IsShrine(stickyTarget))
            {
                var shrineScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(stickyTarget.PosNum);
                Vector2 shrinePos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
                string path = stickyTarget.Path ?? string.Empty;
                if (!_dependencies.IsClickableInEitherSpace(shrinePos, path))
                    return false;

                bool clickedShrine = _dependencies.PerformPathingClick(shrinePos);
                if (clickedShrine)
                {
                    ClearStickyOffscreenTarget();
                    _dependencies.InvalidateShrineCache();
                }

                return clickedShrine;
            }

            LabelOnGround? stickyLabel = ClickService.FindVisibleLabelForEntity(stickyTarget, allLabels);
            if (stickyLabel == null)
                return false;

            if (_dependencies.ShouldSuppressPathfindingLabel(stickyLabel))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            string? mechanicId = _dependencies.GetMechanicIdForLabel(stickyLabel);
            if (string.IsNullOrWhiteSpace(mechanicId))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            (bool resolved, Vector2 clickPos) = _dependencies.TryResolveLabelClickPosition(
                stickyLabel,
                mechanicId,
                windowTopLeft,
                allLabels,
                stickyTarget.Path);
            if (!resolved)
                return false;

            bool clickedLabel = _dependencies.ExecuteStickyLabelInteraction(clickPos, stickyLabel, mechanicId);
            if (clickedLabel)
            {
                string stickyReason = string.IsNullOrWhiteSpace(stickyTarget.Path)
                    ? "Sticky offscreen target click succeeded"
                    : $"Sticky offscreen target click succeeded: {stickyTarget.Path}";
                _dependencies.HoldDebugTelemetryAfterSuccess(stickyReason);
                _dependencies.MarkPendingChestOpenConfirmation(mechanicId, stickyLabel);
                ClearStickyOffscreenTarget();
            }

            return clickedLabel;
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

            _dependencies.PathfindingService.SetLatestOffscreenMovementDebug(new PathfindingService.OffscreenMovementDebugSnapshot(
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

        private Entity? ResolveNearestOffscreenWalkTarget()
        {
            if (_dependencies.GameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return null;

            if (ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable())
            {
                ClearStickyOffscreenTarget();
                return null;
            }

            if (TryResolveStickyOffscreenTarget(out Entity? stickyTarget) && stickyTarget != null)
                return stickyTarget;

            int maxDistance = ClickService.GetOffscreenPathfindingTargetSearchDistance();

            Entity? labelBackedTarget = ResolveNearestOffscreenLabelBackedTarget(maxDistance, out string? labelMechanicId);
            Entity? eldritchAltarTarget = ResolveNearestOffscreenEldritchAltarTarget(maxDistance, out string? eldritchAltarMechanicId);
            Entity? shrineTarget = ResolveNearestOffscreenShrineTarget(maxDistance);
            Entity? areaTransitionTarget = ResolveNearestOffscreenAreaTransitionTarget(maxDistance, out string? areaTransitionMechanicId);

            if (labelBackedTarget == null && eldritchAltarTarget == null && shrineTarget == null && areaTransitionTarget == null)
                return null;

            _dependencies.RefreshMechanicPriorityCaches();

            Entity? best = null;
            string? bestMechanicId = null;
            MechanicRank bestRank = default;
            bool hasBest = false;

            _ = OffscreenCandidateRankingEngine.TryPromote(ref best, ref bestMechanicId, ref bestRank, ref hasBest, labelBackedTarget, labelMechanicId, _dependencies.BuildMechanicRank);
            _ = OffscreenCandidateRankingEngine.TryPromote(ref best, ref bestMechanicId, ref bestRank, ref hasBest, eldritchAltarTarget, eldritchAltarMechanicId, _dependencies.BuildMechanicRank);
            _ = OffscreenCandidateRankingEngine.TryPromote(ref best, ref bestMechanicId, ref bestRank, ref hasBest, shrineTarget, ClickService.ShrineMechanicId, _dependencies.BuildMechanicRank);
            _ = OffscreenCandidateRankingEngine.TryPromote(ref best, ref bestMechanicId, ref bestRank, ref hasBest, areaTransitionTarget, areaTransitionMechanicId, _dependencies.BuildMechanicRank);

            return best;
        }

        private Entity? ResolveNearestOffscreenEldritchAltarTarget(int maxDistance, out string? selectedMechanicId)
        {
            selectedMechanicId = null;

            if ((!_dependencies.Settings.ClickExarchAltars.Value && !_dependencies.Settings.ClickEaterAltars.Value)
                || _dependencies.GameController?.EntityListWrapper?.ValidEntitiesByType == null)
            {
                return null;
            }

            return ResolveNearestOffscreenEntityTarget(
                maxDistance,
                includeEntity: (entity, _) => entity.IsTargetable,
                resolveMechanicId: (_, path) => ClickService.GetEldritchAltarMechanicIdForPath(
                    _dependencies.Settings.ClickExarchAltars.Value,
                    _dependencies.Settings.ClickEaterAltars.Value,
                    path),
                out selectedMechanicId);
        }

        private bool ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable()
        {
            bool prioritizeOnscreen = _dependencies.Settings.PrioritizeOnscreenClickableMechanicsOverPathfinding?.Value == true;
            bool shouldEvaluateOnscreenMechanicChecks = ClickService.ShouldEvaluateOnscreenMechanicChecks(
                prioritizeOnscreen,
                _dependencies.Settings.ClickShrines.Value,
                _dependencies.Settings.ClickLostShipmentCrates.Value,
                _dependencies.Settings.ClickSettlersOre.Value,
                _dependencies.Settings.ClickEaterAltars.Value,
                _dependencies.Settings.ClickExarchAltars.Value);
            if (!shouldEvaluateOnscreenMechanicChecks)
                return false;

            bool hasClickableAltars = _dependencies.HasClickableAltars();
            bool hasClickableShrine = _dependencies.ResolveNextShrineCandidate() != null;
            (LostShipmentCandidate? lostShipmentCandidate, SettlersOreCandidate? settlersOreCandidate) = _dependencies.ResolveVisibleMechanicCandidates();
            bool hasClickableLostShipment = lostShipmentCandidate.HasValue;
            bool hasClickableSettlers = settlersOreCandidate.HasValue;

            bool shouldAvoid = ClickService.ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing(
                prioritizeOnscreen,
                hasClickableAltars,
                hasClickableShrine,
                hasClickableLostShipment,
                hasClickableSettlers);

            if (shouldAvoid)
            {
                _dependencies.PublishClickFlowDebugStage(
                    "OffscreenPathingBlocked",
                    $"onscreen clickable mechanic detected (altar={hasClickableAltars}, shrine={hasClickableShrine}, lost={hasClickableLostShipment}, settlers={hasClickableSettlers})");
            }

            return shouldAvoid;
        }

        private Entity? ResolveNearestOffscreenAreaTransitionTarget(int maxDistance, out string? selectedMechanicId)
        {
            selectedMechanicId = null;

            if ((!_dependencies.Settings.ClickAreaTransitions.Value && !_dependencies.Settings.ClickLabyrinthTrials.Value)
                || _dependencies.GameController?.EntityListWrapper?.ValidEntitiesByType == null)
            {
                return null;
            }

            return ResolveNearestOffscreenEntityTarget(
                maxDistance,
                includeEntity: (_, _) => true,
                resolveMechanicId: (entity, path) => TransitionMechanicClassifier.GetAreaTransitionMechanicId(
                    _dependencies.Settings.ClickAreaTransitions.Value,
                    _dependencies.Settings.ClickLabyrinthTrials.Value,
                    entity.Type,
                    path),
                out selectedMechanicId);
        }

        private Entity? ResolveNearestOffscreenShrineTarget(int maxDistance)
        {
            if (!_dependencies.Settings.ClickShrines.Value || _dependencies.GameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return null;

            return ResolveNearestOffscreenEntityTarget(
                maxDistance,
                includeEntity: (entity, _) => ShrineService.IsClickableShrineCandidate(entity),
                resolveMechanicId: (_, _) => ClickService.ShrineMechanicId,
                out _);
        }

        private Entity? ResolveNearestOffscreenLabelBackedTarget(int maxDistance, out string? selectedMechanicId)
        {
            selectedMechanicId = null;

            IReadOnlyList<LabelOnGround>? labels = _dependencies.GetLabelsForOffscreenSelection();
            if (labels == null || labels.Count == 0)
                return null;

            RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);

            _dependencies.RefreshMechanicPriorityCaches();

            Entity? best = null;
            string? bestMechanicId = null;
            MechanicRank bestRank = default;
            bool hasBestRank = false;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                Entity? entity = label?.ItemOnGround;
                if (label == null || entity == null)
                    continue;
                if (!entity.IsValid || entity.IsHidden || ClickService.IsEntityHiddenByMinimapIcon(entity))
                    continue;
                if (entity.DistancePlayer > maxDistance)
                    continue;
                if (_dependencies.ShouldSuppressPathfindingLabel(label))
                    continue;

                string? mechanicId = _dependencies.GetMechanicIdForLabel(label);
                if (string.IsNullOrWhiteSpace(mechanicId))
                    continue;

                if (!ShouldContinuePathfindingToLabel(label, entity, labels, windowTopLeft))
                    continue;

                MechanicRank rank = _dependencies.BuildMechanicRank(entity.DistancePlayer, mechanicId);
                if (hasBestRank && CandidateRankingEngine.CompareRanks(rank, bestRank) >= 0)
                    continue;

                best = entity;
                bestMechanicId = mechanicId;
                bestRank = rank;
                hasBestRank = true;
            }

            selectedMechanicId = bestMechanicId;
            return best;
        }

        private Entity? ResolveNearestOffscreenEntityTarget(
            int maxDistance,
            Func<Entity, string, bool> includeEntity,
            Func<Entity, string, string?> resolveMechanicId,
            out string? selectedMechanicId)
        {
            selectedMechanicId = null;

            if (_dependencies.GameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return null;

            Entity? best = null;
            float bestDistance = float.MaxValue;
            string? bestMechanicId = null;

            foreach (var kv in _dependencies.GameController.EntityListWrapper.ValidEntitiesByType)
            {
                var entities = kv.Value;
                if (entities == null)
                    continue;

                for (int i = 0; i < entities.Count; i++)
                {
                    Entity entity = entities[i];
                    if (!TryPrepareOffscreenEntityTargetCandidate(entity, maxDistance, out string path))
                        continue;

                    if (!includeEntity(entity, path))
                        continue;

                    string? mechanicId = resolveMechanicId(entity, path);
                    if (string.IsNullOrWhiteSpace(mechanicId))
                        continue;

                    float distance = entity.DistancePlayer;
                    if (distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    best = entity;
                    bestMechanicId = mechanicId;
                }
            }

            selectedMechanicId = bestMechanicId;
            return best;
        }

        private bool TryPrepareOffscreenEntityTargetCandidate(Entity? entity, int maxDistance, out string path)
        {
            path = string.Empty;

            if (entity == null || !entity.IsValid || entity.IsHidden || ClickService.IsEntityHiddenByMinimapIcon(entity))
                return false;
            if (entity.DistancePlayer > maxDistance)
                return false;

            path = entity.Path ?? string.Empty;

            var screenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            Vector2 screen = new(screenRaw.X, screenRaw.Y);
            if (_dependencies.IsClickableInEitherSpace(screen, path))
                return false;

            return true;
        }

        private bool ShouldContinuePathfindingToLabel(
            LabelOnGround label,
            Entity entity,
            IReadOnlyList<LabelOnGround>? allLabels,
            Vector2 windowTopLeft)
        {
            if (!LabelUtils.TryGetLabelRect(label, out RectangleF rect))
                return true;

            string path = entity.Path ?? string.Empty;
            bool labelInWindow = _dependencies.IsInsideWindowInEitherSpace(rect.Center);
            bool labelClickable = _dependencies.IsClickableInEitherSpace(rect.Center, path);

            if (!labelInWindow || !labelClickable)
                return true;

            (bool clickResolvable, _) = _dependencies.TryResolveLabelClickPosition(label, null, windowTopLeft, allLabels, path);
            return ClickService.ShouldContinuePathfindingWhenLabelActionable(labelInWindow, labelClickable, clickResolvable);
        }

    }
}