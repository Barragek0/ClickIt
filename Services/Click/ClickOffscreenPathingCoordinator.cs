using ClickIt.Utils;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private sealed class OffscreenPathingCoordinator(ClickService owner)
        {
            public bool TryWalkTowardOffscreenTarget(Entity? preferredTarget = null)
            {
                if (!owner.settings.WalkTowardOffscreenLabels.Value)
                    return false;

                if (ShouldSkipOffscreenPathfindingForRitual(EntityHelpers.IsRitualActive(owner.gameController)))
                {
                    ClearStickyOffscreenTarget();
                    owner.pathfindingService.ClearLatestPath();
                    owner.DebugLog(() => "[TryWalkTowardOffscreenTarget] Skipping offscreen pathfinding because a RitualBlocker is active.");
                    owner.PublishClickFlowDebugStage("OffscreenPathingBlockedByRitual", "RitualBlocker active");
                    return false;
                }

                if (ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable())
                {
                    ClearStickyOffscreenTarget();
                    owner.pathfindingService.ClearLatestPath();
                    owner.DebugLog(() => "[TryWalkTowardOffscreenTarget] Skipping offscreen pathfinding because a clickable on-screen mechanic is available.");
                    return false;
                }

                Entity? target = preferredTarget ?? ResolveNearestOffscreenWalkTarget();
                if (target == null)
                {
                    if (preferredTarget != null)
                    {
                        ClearStickyOffscreenTarget();
                    }

                    owner.pathfindingService.ClearLatestPath();
                    return false;
                }

                if (!target.IsValid || target.IsHidden || IsEntityHiddenByMinimapIcon(target))
                {
                    ClearStickyOffscreenTarget();
                    owner.pathfindingService.ClearLatestPath();
                    return false;
                }

                SetStickyOffscreenTarget(target);

                string targetPath = target.Path ?? string.Empty;
                bool builtPath = owner.pathfindingService.TryBuildPathToTarget(owner.gameController, target, owner.settings.OffscreenPathfindingSearchBudget.Value);
                if (!builtPath)
                {
                    owner.DebugLog(() => "[TryWalkTowardOffscreenTarget] Pathfinding route not found; trying directional walk click.");
                }

                Vector2 targetScreen = default;
                bool resolvedFromPath = builtPath && owner.TryResolveOffscreenTargetScreenPointFromPath(out targetScreen);
                if (!resolvedFromPath && !owner.TryResolveOffscreenTargetScreenPoint(target, out targetScreen))
                {
                    PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, resolvedClickPoint: false, targetScreen, clickScreen: default, stage: "ResolveTargetScreenFailed");
                    owner.DebugLog(() => "[TryWalkTowardOffscreenTarget] Failed to resolve target screen point.");
                    return false;
                }

                if (!TryResolveDirectionalWalkClickPosition(targetScreen, targetPath, out Vector2 walkClick))
                {
                    PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, resolvedClickPoint: false, targetScreen, clickScreen: default, stage: "ResolveClickPointFailed");
                    owner.DebugLog(() => "[TryWalkTowardOffscreenTarget] Failed to resolve directional click point.");
                    return false;
                }

                string movementSkillDebug;
                if (owner.MovementSkills.TryUseMovementSkillForOffscreenPathing(targetPath, targetScreen, builtPath, out Vector2 movementSkillCastPoint, out movementSkillDebug))
                {
                    PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, resolvedClickPoint: true, targetScreen, movementSkillCastPoint, stage: "MovementSkillUsed", movementSkillDebug);
                    owner.DebugLog(() => $"[TryWalkTowardOffscreenTarget] Used movement skill toward offscreen target: {targetPath}");
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(movementSkillDebug))
                {
                    owner.DebugLog(() => $"[TryWalkTowardOffscreenTarget] Movement skill not used: {movementSkillDebug}");
                }

                PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, resolvedClickPoint: true, targetScreen, walkClick, stage: "BeforeClick", movementSkillDebug);

                bool clicked = owner.PerformLabelClick(walkClick, null, owner.gameController);
                if (clicked)
                {
                    PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, resolvedClickPoint: true, targetScreen, walkClick, stage: "Clicked", movementSkillDebug);
                    _ = owner.pathfindingService.TryBuildPathToTarget(owner.gameController, target, owner.settings.OffscreenPathfindingSearchBudget.Value);
                    owner.DebugLog(() => $"[TryWalkTowardOffscreenTarget] Walking toward offscreen target: {targetPath}");
                }
                else
                {
                    PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, resolvedClickPoint: true, targetScreen, walkClick, stage: "ClickRejected", movementSkillDebug);
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
            {
                owner._stickyOffscreenTargetAddress = target.Address;
            }

            public void ClearStickyOffscreenTarget()
            {
                owner._stickyOffscreenTargetAddress = 0;
            }

            public bool TryResolveStickyOffscreenTarget(out Entity? target)
            {
                target = null;

                if (owner._stickyOffscreenTargetAddress == 0)
                    return false;

                target = FindEntityByAddress(owner._stickyOffscreenTargetAddress);
                if (target == null || !target.IsValid || target.IsHidden || IsEntityHiddenByMinimapIcon(target))
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
                bool isEldritchAltar = IsEldritchAltarPath(stickyPath);
                if (ShouldDropStickyTargetForUntargetableEldritchAltar(isEldritchAltar, target.IsTargetable))
                {
                    ClearStickyOffscreenTarget();
                    return false;
                }

                return true;
            }

            public Entity? FindEntityByAddress(long address)
            {
                if (address == 0 || owner.gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                    return null;

                foreach (var kv in owner.gameController.EntityListWrapper.ValidEntitiesByType)
                {
                    var entities = kv.Value;
                    if (entities == null)
                        continue;

                    for (int i = 0; i < entities.Count; i++)
                    {
                        Entity entity = entities[i];
                        if (entity != null && IsSameEntityAddress(address, entity.Address))
                            return entity;
                    }
                }

                return null;
            }

            public bool IsStickyTarget(Entity? entity)
            {
                return entity != null && IsSameEntityAddress(owner._stickyOffscreenTargetAddress, entity.Address);
            }

            private bool TryClickStickyTargetIfPossible(Entity stickyTarget, Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
            {
                if (ShrineService.IsShrine(stickyTarget))
                {
                    var shrineScreenRaw = owner.gameController.Game.IngameState.Camera.WorldToScreen(stickyTarget.PosNum);
                    Vector2 shrinePos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
                    string path = stickyTarget.Path ?? string.Empty;
                    if (!owner.IsClickableInEitherSpace(shrinePos, path))
                        return false;

                    bool clickedShrine = owner.PerformLabelClick(shrinePos, null, owner.gameController);
                    if (clickedShrine)
                    {
                        ClearStickyOffscreenTarget();
                        owner.shrineService.InvalidateCache();
                    }

                    return clickedShrine;
                }

                LabelOnGround? stickyLabel = FindVisibleLabelForEntity(stickyTarget, allLabels);
                if (stickyLabel == null)
                    return false;

                if (owner.ShouldSuppressPathfindingLabel(stickyLabel))
                {
                    ClearStickyOffscreenTarget();
                    return false;
                }

                string? mechanicId = owner.labelFilterService.GetMechanicIdForLabel(stickyLabel);
                if (string.IsNullOrWhiteSpace(mechanicId))
                {
                    ClearStickyOffscreenTarget();
                    return false;
                }

                if (!owner.TryResolveLabelClickPosition(
                    stickyLabel,
                    mechanicId,
                    windowTopLeft,
                    allLabels,
                    out Vector2 clickPos,
                    explicitPath: stickyTarget.Path))
                {
                    return false;
                }

                bool clickedLabel = ShouldUseHoldClickForSettlersMechanic(mechanicId)
                    ? owner.PerformLabelHoldClick(clickPos, stickyLabel.Label, owner.gameController, holdDurationMs: 0, ShouldForceUiHoverVerificationForLabel(stickyLabel))
                    : owner.PerformLabelClick(clickPos, stickyLabel.Label, owner.gameController, ShouldForceUiHoverVerificationForLabel(stickyLabel));
                if (clickedLabel)
                {
                    owner.ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, stickyLabel);
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
                var player = owner.gameController.Player;
                Vector2 playerGrid = player != null
                    ? new Vector2(player.GridPosNum.X, player.GridPosNum.Y)
                    : default;
                Vector2 targetGrid = new(target.GridPosNum.X, target.GridPosNum.Y);
                RectangleF win = owner.gameController.Window.GetWindowRectangleTimeCache;
                Vector2 center = new(win.X + (win.Width * 0.5f), win.Y + (win.Height * 0.5f));

                owner.pathfindingService.SetLatestOffscreenMovementDebug(new PathfindingService.OffscreenMovementDebugSnapshot(
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
                clickPos = default;

                RectangleF win = owner.gameController.Window.GetWindowRectangleTimeCache;
                if (win.Width <= 0 || win.Height <= 0)
                    return false;

                float insetX = Math.Max(28f, win.Width * 0.10f);
                float insetY = Math.Max(28f, win.Height * 0.10f);
                float safeLeft = win.Left + insetX;
                float safeRight = win.Right - insetX;
                float safeTop = win.Top + insetY;
                float safeBottom = win.Bottom - insetY;

                Vector2 center = new(win.X + (win.Width * 0.5f), win.Y + (win.Height * 0.5f));
                Vector2 direction = targetScreen - center;
                float lenSq = (direction.X * direction.X) + (direction.Y * direction.Y);
                if (lenSq < 1f)
                    return false;

                for (float t = 1.05f; t >= 0.30f; t -= 0.1f)
                {
                    Vector2 candidate = center + (direction * t);
                    if (!IsInsideWindow(win, candidate))
                        continue;
                    if (candidate.X < safeLeft || candidate.X > safeRight || candidate.Y < safeTop || candidate.Y > safeBottom)
                        continue;
                    if (!owner.pointIsInClickableArea(candidate, targetPath))
                        continue;

                    clickPos = candidate;
                    return true;
                }

                Vector2 clamped = new(
                    Math.Clamp(targetScreen.X, safeLeft, safeRight),
                    Math.Clamp(targetScreen.Y, safeTop, safeBottom));

                if (owner.pointIsInClickableArea(clamped, targetPath))
                {
                    clickPos = clamped;
                    return true;
                }

                return false;
            }

            private Entity? ResolveNearestOffscreenWalkTarget()
            {
                if (owner.gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                    return null;

                if (ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable())
                {
                    ClearStickyOffscreenTarget();
                    return null;
                }

                if (TryResolveStickyOffscreenTarget(out Entity? stickyTarget) && stickyTarget != null)
                    return stickyTarget;

                int maxDistance = GetOffscreenPathfindingTargetSearchDistance();

                Entity? labelBackedTarget = ResolveNearestOffscreenLabelBackedTarget(maxDistance, out string? labelMechanicId);
                Entity? eldritchAltarTarget = ResolveNearestOffscreenEldritchAltarTarget(maxDistance, out string? eldritchAltarMechanicId);
                Entity? shrineTarget = ResolveNearestOffscreenShrineTarget(maxDistance);
                Entity? areaTransitionTarget = ResolveNearestOffscreenAreaTransitionTarget(maxDistance, out string? areaTransitionMechanicId);

                if (labelBackedTarget == null && eldritchAltarTarget == null && shrineTarget == null && areaTransitionTarget == null)
                    return null;

                owner.RefreshMechanicPriorityCaches();

                Entity? best = null;
                string? bestMechanicId = null;
                MechanicRank bestRank = default;
                bool hasBest = false;

                owner.PromoteOffscreenTargetCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, labelBackedTarget, labelMechanicId);
                owner.PromoteOffscreenTargetCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, eldritchAltarTarget, eldritchAltarMechanicId);
                owner.PromoteOffscreenTargetCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, shrineTarget, ShrineMechanicId);
                owner.PromoteOffscreenTargetCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, areaTransitionTarget, areaTransitionMechanicId);

                return best;
            }

            private Entity? ResolveNearestOffscreenEldritchAltarTarget(int maxDistance, out string? selectedMechanicId)
            {
                selectedMechanicId = null;

                if ((!owner.settings.ClickExarchAltars.Value && !owner.settings.ClickEaterAltars.Value)
                    || owner.gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                {
                    return null;
                }

                return ResolveNearestOffscreenEntityTarget(
                    maxDistance,
                    includeEntity: (entity, _) => entity.IsTargetable,
                    resolveMechanicId: (_, path) => GetEldritchAltarMechanicIdForPath(
                        owner.settings.ClickExarchAltars.Value,
                        owner.settings.ClickEaterAltars.Value,
                        path),
                    out selectedMechanicId);
            }

            private bool ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable()
            {
                bool prioritizeOnscreen = owner.settings.PrioritizeOnscreenClickableMechanicsOverPathfinding?.Value == true;
                bool shouldEvaluateOnscreenMechanicChecks = ShouldEvaluateOnscreenMechanicChecks(
                    prioritizeOnscreen,
                    owner.settings.ClickShrines.Value,
                    owner.settings.ClickLostShipmentCrates.Value,
                    owner.settings.ClickSettlersOre.Value,
                    owner.settings.ClickEaterAltars.Value,
                    owner.settings.ClickExarchAltars.Value);
                if (!shouldEvaluateOnscreenMechanicChecks)
                    return false;

                bool hasClickableAltars = owner.HasClickableAltars();
                bool hasClickableShrine = owner.VisibleMechanics.ResolveNextShrineCandidate() != null;
                owner.VisibleMechanics.ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate);
                bool hasClickableLostShipment = lostShipmentCandidate.HasValue;
                bool hasClickableSettlers = settlersOreCandidate.HasValue;

                bool shouldAvoid = ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing(
                    prioritizeOnscreen,
                    hasClickableAltars,
                    hasClickableShrine,
                    hasClickableLostShipment,
                    hasClickableSettlers);

                if (shouldAvoid)
                {
                    owner.PublishClickFlowDebugStage(
                        "OffscreenPathingBlocked",
                        $"onscreen clickable mechanic detected (altar={hasClickableAltars}, shrine={hasClickableShrine}, lost={hasClickableLostShipment}, settlers={hasClickableSettlers})");
                }

                return shouldAvoid;
            }

            private Entity? ResolveNearestOffscreenAreaTransitionTarget(int maxDistance, out string? selectedMechanicId)
            {
                selectedMechanicId = null;

                if ((!owner.settings.ClickAreaTransitions.Value && !owner.settings.ClickLabyrinthTrials.Value)
                    || owner.gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                {
                    return null;
                }

                return ResolveNearestOffscreenEntityTarget(
                    maxDistance,
                    includeEntity: (_, _) => true,
                    resolveMechanicId: (entity, path) => GetAreaTransitionMechanicIdForPath(
                        owner.settings.ClickAreaTransitions.Value,
                        owner.settings.ClickLabyrinthTrials.Value,
                        entity.Type,
                        path),
                    out selectedMechanicId);
            }

            private Entity? ResolveNearestOffscreenShrineTarget(int maxDistance)
            {
                if (!owner.settings.ClickShrines.Value || owner.gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                    return null;

                return ResolveNearestOffscreenEntityTarget(
                    maxDistance,
                    includeEntity: (entity, _) => ShrineService.IsClickableShrineCandidate(entity),
                    resolveMechanicId: (_, _) => ShrineMechanicId,
                    out _);
            }

            private Entity? ResolveNearestOffscreenLabelBackedTarget(int maxDistance, out string? selectedMechanicId)
            {
                selectedMechanicId = null;

                var labels = owner.GetLabelsForOffscreenSelection();
                if (labels == null || labels.Count == 0)
                    return null;

                RectangleF windowArea = owner.gameController.Window.GetWindowRectangleTimeCache;
                Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);

                owner.RefreshMechanicPriorityCaches();

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
                    if (!entity.IsValid || entity.IsHidden || IsEntityHiddenByMinimapIcon(entity))
                        continue;
                    if (entity.DistancePlayer > maxDistance)
                        continue;
                    if (owner.ShouldSuppressPathfindingLabel(label))
                        continue;

                    string? mechanicId = owner.labelFilterService.GetMechanicIdForLabel(label);
                    if (string.IsNullOrWhiteSpace(mechanicId))
                        continue;

                    if (!ShouldContinuePathfindingToLabel(label, entity, labels, windowTopLeft))
                        continue;

                    MechanicRank rank = owner.BuildMechanicRankWithSharedEngine(entity.DistancePlayer, mechanicId);
                    if (hasBestRank && CompareMechanicRanks(rank, bestRank) >= 0)
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

                if (owner.gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                    return null;

                Entity? best = null;
                float bestDistance = float.MaxValue;
                string? bestMechanicId = null;

                foreach (var kv in owner.gameController.EntityListWrapper.ValidEntitiesByType)
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

                        float d = entity.DistancePlayer;
                        if (d >= bestDistance)
                            continue;

                        bestDistance = d;
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

                if (entity == null || !entity.IsValid || entity.IsHidden || IsEntityHiddenByMinimapIcon(entity))
                    return false;
                if (entity.DistancePlayer > maxDistance)
                    return false;

                path = entity.Path ?? string.Empty;

                var screenRaw = owner.gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
                Vector2 screen = new(screenRaw.X, screenRaw.Y);
                if (owner.IsClickableInEitherSpace(screen, path))
                    return false;

                return true;
            }

            private bool ShouldContinuePathfindingToLabel(
                LabelOnGround label,
                Entity entity,
                IReadOnlyList<LabelOnGround>? allLabels,
                Vector2 windowTopLeft)
            {
                if (!TryGetLabelRect(label, out RectangleF rect))
                    return true;

                string path = entity.Path ?? string.Empty;
                bool labelInWindow = owner.IsInsideWindowInEitherSpace(rect.Center);
                bool labelClickable = owner.IsClickableInEitherSpace(rect.Center, path);

                if (!labelInWindow || !labelClickable)
                    return true;

                bool clickPointResolvable = allLabels != null
                    && owner.inputHandler.TryCalculateClickPosition(
                        label,
                        windowTopLeft,
                        allLabels,
                        point => owner.IsClickableInEitherSpace(point, path),
                        out _);

                return ShouldContinuePathfindingWhenLabelActionable(labelInWindow, labelClickable, clickPointResolvable);
            }
        }
    }
}