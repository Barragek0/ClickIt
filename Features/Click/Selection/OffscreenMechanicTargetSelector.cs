using ClickIt.Features.Click.Ranking;
using ClickIt.Features.Click.Runtime;
using ClickIt.Features.Labels.Classification;
using ClickIt.Shared;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct OffscreenMechanicTargetSelectorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        ShrineService ShrineService,
        Func<bool> HasClickableAltars,
        Func<bool> HasClickableShrine,
        Func<(bool HasClickableLostShipment, bool HasClickableSettlers)> GetVisibleMechanicAvailability,
        Action<string, string> PublishClickFlowDebugStage,
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        Func<Vector2, bool> IsInsideWindowInEitherSpace,
        Func<LabelOnGround, bool> ShouldSuppressPathfindingLabel,
        Func<LabelOnGround, string?> GetMechanicIdForLabel,
        Func<LabelOnGround, string?, Vector2, IReadOnlyList<LabelOnGround>?, string?, (bool Success, Vector2 ClickPos)> TryResolveLabelClickPosition,
        Func<IReadOnlyList<LabelOnGround>?> GetLabelsForOffscreenSelection,
        Action RefreshMechanicPriorityCaches,
        Func<float, string?, MechanicRank> BuildMechanicRank);

    internal sealed class OffscreenMechanicTargetSelector(OffscreenMechanicTargetSelectorDependencies dependencies)
    {
        private readonly OffscreenMechanicTargetSelectorDependencies _dependencies = dependencies;

        internal bool ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable()
        {
            bool prioritizeOnscreen = _dependencies.Settings.PrioritizeOnscreenClickableMechanicsOverPathfinding?.Value == true;
            bool shouldEvaluateOnscreenMechanicChecks = OffscreenPathingMath.ShouldEvaluateOnscreenMechanicChecks(
                prioritizeOnscreen,
                _dependencies.Settings.ClickShrines.Value,
                _dependencies.Settings.ClickLostShipmentCrates.Value,
                _dependencies.Settings.ClickSettlersOre.Value,
                _dependencies.Settings.ClickEaterAltars.Value,
                _dependencies.Settings.ClickExarchAltars.Value);
            if (!shouldEvaluateOnscreenMechanicChecks)
                return false;

            bool hasClickableAltars = _dependencies.HasClickableAltars();
            bool hasClickableShrine = _dependencies.HasClickableShrine();
            (bool hasClickableLostShipment, bool hasClickableSettlers) = _dependencies.GetVisibleMechanicAvailability();

            bool shouldAvoid = OffscreenPathingMath.ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing(
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

        internal Entity? ResolveNearestOffscreenWalkTarget()
        {
            int maxDistance = OffscreenPathingMath.OffscreenPathfindingTargetSearchDistance;

            (Entity? labelBackedTarget, string? labelMechanicId) = ResolveNearestOffscreenLabelBackedTarget(maxDistance);
            (Entity? eldritchAltarTarget, string? eldritchAltarMechanicId) = ResolveNearestOffscreenEldritchAltarTarget(maxDistance);
            Entity? shrineTarget = ResolveNearestOffscreenShrineTarget(maxDistance);
            (Entity? areaTransitionTarget, string? areaTransitionMechanicId) = ResolveNearestOffscreenAreaTransitionTarget(maxDistance);

            if (labelBackedTarget == null && eldritchAltarTarget == null && shrineTarget == null && areaTransitionTarget == null)
                return null;

            _dependencies.RefreshMechanicPriorityCaches();

            Entity? best = null;
            string? bestMechanicId = null;
            MechanicRank bestRank = default;
            bool hasBest = false;

            _ = MechanicCandidateResolver.TryPromoteOffscreenCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, labelBackedTarget, labelMechanicId, _dependencies.BuildMechanicRank);
            _ = MechanicCandidateResolver.TryPromoteOffscreenCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, eldritchAltarTarget, eldritchAltarMechanicId, _dependencies.BuildMechanicRank);
            _ = MechanicCandidateResolver.TryPromoteOffscreenCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, shrineTarget, MechanicIds.Shrines, _dependencies.BuildMechanicRank);
            _ = MechanicCandidateResolver.TryPromoteOffscreenCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, areaTransitionTarget, areaTransitionMechanicId, _dependencies.BuildMechanicRank);

            return best;
        }

        private (Entity? Target, string? MechanicId) ResolveNearestOffscreenEldritchAltarTarget(int maxDistance)
        {
            if (!_dependencies.Settings.ClickExarchAltars.Value && !_dependencies.Settings.ClickEaterAltars.Value)
                return (null, null);

            return ResolveNearestOffscreenEntityTarget(
                maxDistance,
                includeEntity: (entity, _) => entity.IsTargetable,
                resolveMechanicId: (_, path) => OffscreenPathingMath.GetEldritchAltarMechanicIdForPath(
                    _dependencies.Settings.ClickExarchAltars.Value,
                    _dependencies.Settings.ClickEaterAltars.Value,
                    path));
        }

        private (Entity? Target, string? MechanicId) ResolveNearestOffscreenAreaTransitionTarget(int maxDistance)
        {
            if (!_dependencies.Settings.ClickAreaTransitions.Value && !_dependencies.Settings.ClickLabyrinthTrials.Value)
                return (null, null);

            return ResolveNearestOffscreenEntityTarget(
                maxDistance,
                includeEntity: (_, _) => true,
                resolveMechanicId: (entity, path) => TransitionMechanicClassifier.GetAreaTransitionMechanicId(
                    _dependencies.Settings.ClickAreaTransitions.Value,
                    _dependencies.Settings.ClickLabyrinthTrials.Value,
                    entity.Type,
                    path));
        }

        private Entity? ResolveNearestOffscreenShrineTarget(int maxDistance)
        {
            if (!_dependencies.Settings.ClickShrines.Value)
                return null;

            return ResolveNearestOffscreenEntityTarget(
                maxDistance,
                includeEntity: (entity, _) => ShrineService.IsClickableShrineCandidate(entity),
                resolveMechanicId: (_, _) => MechanicIds.Shrines).Target;
        }

        private (Entity? Target, string? MechanicId) ResolveNearestOffscreenLabelBackedTarget(int maxDistance)
        {
            IReadOnlyList<LabelOnGround>? labels = _dependencies.GetLabelsForOffscreenSelection();
            if (labels == null || labels.Count == 0)
                return (null, null);

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
                if (!entity.IsValid || entity.IsHidden || OffscreenPathingMath.IsEntityHiddenByMinimapIcon(entity))
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
                _ = OffscreenTargetRanker.TryPromoteRankedCandidate(
                    ref best,
                    ref bestMechanicId,
                    ref bestRank,
                    ref hasBestRank,
                    entity,
                    mechanicId,
                    rank);
            }

            return (best, bestMechanicId);
        }

        private (Entity? Target, string? MechanicId) ResolveNearestOffscreenEntityTarget(
            int maxDistance,
            Func<Entity, string, bool> includeEntity,
            Func<Entity, string, string?> resolveMechanicId)
        {
            Entity? best = null;
            float bestDistance = float.MaxValue;
            string? bestMechanicId = null;

            EntityQueryService.VisitValidEntities(_dependencies.GameController, entity =>
            {
                if (!TryPrepareOffscreenEntityTargetCandidate(entity, maxDistance, out string path))
                    return false;

                if (!includeEntity(entity, path))
                    return false;

                string? mechanicId = resolveMechanicId(entity, path);
                if (string.IsNullOrWhiteSpace(mechanicId))
                    return false;

                float distance = entity.DistancePlayer;
                if (distance >= bestDistance)
                    return false;

                bestDistance = distance;
                best = entity;
                bestMechanicId = mechanicId;
                return false;
            });

            return (best, bestMechanicId);
        }

        private bool TryPrepareOffscreenEntityTargetCandidate(Entity? entity, int maxDistance, out string path)
        {
            path = string.Empty;

            if (entity == null || !entity.IsValid || entity.IsHidden || OffscreenPathingMath.IsEntityHiddenByMinimapIcon(entity))
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
            return OffscreenPathingMath.ShouldContinuePathfindingWhenLabelActionable(labelInWindow, labelClickable, clickResolvable);
        }
    }
}