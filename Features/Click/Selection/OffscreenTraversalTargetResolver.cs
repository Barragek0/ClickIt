namespace ClickIt.Features.Click.Selection
{
    internal readonly record struct OffscreenTraversalTargetResolverDependencies(
        ClickItSettings Settings,
        GameController GameController,
        MechanicPriorityContextProvider MechanicPriorityContextProvider,
        ClickLabelInteractionService LabelInteraction,
        ILabelInteractionPort LabelInteractionPort,
        VisibleLabelSnapshotProvider VisibleLabelSnapshots,
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        Func<Vector2, bool> IsInsideWindowInEitherSpace,
        PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression);

    internal sealed class OffscreenTraversalTargetResolver(OffscreenTraversalTargetResolverDependencies dependencies)
    {
        private readonly OffscreenTraversalTargetResolverDependencies _dependencies = dependencies;

        internal Entity? ResolveNearestOffscreenWalkTarget()
        {
            int maxDistance = OffscreenPathingMath.OffscreenPathfindingTargetSearchDistance;

            (Entity? labelBackedTarget, string? labelMechanicId) = ResolveNearestOffscreenLabelBackedTarget(maxDistance);
            (Entity? eldritchAltarTarget, string? eldritchAltarMechanicId) = ResolveNearestOffscreenEldritchAltarTarget(maxDistance);
            Entity? shrineTarget = ResolveNearestOffscreenShrineTarget(maxDistance);
            (Entity? areaTransitionTarget, string? areaTransitionMechanicId) = ResolveNearestOffscreenAreaTransitionTarget(maxDistance);

            if (labelBackedTarget == null && eldritchAltarTarget == null && shrineTarget == null && areaTransitionTarget == null)
                return null;

            _dependencies.MechanicPriorityContextProvider.Refresh();
            MechanicPriorityContext mechanicPriorityContext = _dependencies.MechanicPriorityContextProvider.CreateContext();

            Entity? best = null;
            string? bestMechanicId = null;
            MechanicRank bestRank = default;
            bool hasBest = false;

            _ = MechanicCandidateResolver.TryPromoteOffscreenCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, labelBackedTarget, labelMechanicId, (distance, mechanicId) => BuildMechanicRank(distance, mechanicId, mechanicPriorityContext));
            _ = MechanicCandidateResolver.TryPromoteOffscreenCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, eldritchAltarTarget, eldritchAltarMechanicId, (distance, mechanicId) => BuildMechanicRank(distance, mechanicId, mechanicPriorityContext));
            _ = MechanicCandidateResolver.TryPromoteOffscreenCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, shrineTarget, MechanicIds.Shrines, (distance, mechanicId) => BuildMechanicRank(distance, mechanicId, mechanicPriorityContext));
            _ = MechanicCandidateResolver.TryPromoteOffscreenCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, areaTransitionTarget, areaTransitionMechanicId, (distance, mechanicId) => BuildMechanicRank(distance, mechanicId, mechanicPriorityContext));

            return best;
        }

        private (Entity? Target, string? MechanicId) ResolveNearestOffscreenEldritchAltarTarget(int maxDistance)
        {
            if (!_dependencies.Settings.ClickExarchAltars.Value && !_dependencies.Settings.ClickEaterAltars.Value)
                return (null, null);

            return ResolveNearestOffscreenEntityTarget(
                maxDistance,
                includeEntity: static (entity, _) => DynamicAccess.TryReadBool(entity, DynamicAccessProfiles.IsTargetable, out bool isTargetable) && isTargetable,
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
            IReadOnlyList<LabelOnGround>? labels = _dependencies.VisibleLabelSnapshots.GetVisibleOrCachedLabels();
            if (labels == null || labels.Count == 0)
                return (null, null);

            RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);

            _dependencies.MechanicPriorityContextProvider.Refresh();
            MechanicPriorityContext mechanicPriorityContext = _dependencies.MechanicPriorityContextProvider.CreateContext();

            Entity? best = null;
            string? bestMechanicId = null;
            MechanicRank bestRank = default;
            bool hasBestRank = false;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                Entity? entity = DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                    ? rawItem as Entity
                    : null;
                if (label == null || entity == null)
                    continue;
                if (!TryGetOffscreenCandidateState(entity, out float distance, out _))
                    continue;
                if (distance > maxDistance)
                    continue;
                if (_dependencies.PathfindingLabelSuppression.ShouldSuppressPathfindingLabel(label))
                    continue;

                string? mechanicId = _dependencies.LabelInteractionPort.GetMechanicIdForLabel(label);
                if (string.IsNullOrWhiteSpace(mechanicId))
                    continue;

                if (!ShouldContinuePathfindingToLabel(label, entity, labels, windowTopLeft))
                    continue;

                MechanicRank rank = BuildMechanicRank(distance, mechanicId, mechanicPriorityContext);
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

                if (!DynamicAccess.TryReadFloat(entity, DynamicAccessProfiles.DistancePlayer, out float distance))
                    return false;

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

            if (entity == null
                || !TryGetOffscreenCandidateState(entity, out float distance, out path)
                || OffscreenPathingMath.IsEntityHiddenByMinimapIcon(entity))
            {
                return false;
            }

            if (distance > maxDistance)
                return false;

            if (!TryProjectEntityScreenPosition(entity, out Vector2 screen))
                return false;

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
            if (!LabelGeometry.TryGetLabelRect(label, out RectangleF rect))
                return true;

            string path = DynamicAccess.TryReadString(entity, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            bool labelInWindow = _dependencies.IsInsideWindowInEitherSpace(rect.Center);
            bool labelClickable = _dependencies.IsClickableInEitherSpace(rect.Center, path);

            if (!labelInWindow || !labelClickable)
                return true;

            (bool clickResolvable, _) = _dependencies.LabelInteraction.TryResolveLabelClickPositionResult(label, null, windowTopLeft, allLabels, path);
            return OffscreenPathingMath.ShouldContinuePathfindingWhenLabelActionable(labelInWindow, labelClickable, clickResolvable);
        }

        private static MechanicRank BuildMechanicRank(float distance, string? mechanicId, MechanicPriorityContext mechanicPriorityContext)
            => CandidateRankingEngine.BuildRank(distance, mechanicId, mechanicPriorityContext);

        private static bool TryGetOffscreenCandidateState(Entity entity, out float distance, out string path)
        {
            distance = 0;
            path = string.Empty;

            if (!DynamicAccess.TryReadBool(entity, DynamicAccessProfiles.IsValid, out bool isValid)
                || !isValid
                || !DynamicAccess.TryReadBool(entity, DynamicAccessProfiles.IsHidden, out bool isHidden)
                || isHidden
                || !DynamicAccess.TryReadFloat(entity, DynamicAccessProfiles.DistancePlayer, out distance))
            {
                return false;
            }

            path = DynamicAccess.TryReadString(entity, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            return true;
        }

        private bool TryProjectEntityScreenPosition(Entity entity, out Vector2 screenPosition)
        {
            screenPosition = default;

            if (!DynamicAccess.TryGetDynamicValue(entity, DynamicAccessProfiles.PosNum, out object? rawPosition)
                || rawPosition is not System.Numerics.Vector3 position
                || !DynamicAccess.TryGetDynamicValue(_dependencies.GameController, DynamicAccessProfiles.Game, out object? rawGame)
                || !DynamicAccess.TryGetDynamicValue(rawGame, DynamicAccessProfiles.IngameState, out object? rawIngameState)
                || !DynamicAccess.TryGetDynamicValue(rawIngameState, DynamicAccessProfiles.Camera, out object? rawCamera)
                || !DynamicAccess.TryProjectWorldToScreen(rawCamera, position, out object? rawProjected)
                || !DynamicAccess.TryReadFloat(rawProjected, DynamicAccessProfiles.X, out float projectedX)
                || !DynamicAccess.TryReadFloat(rawProjected, DynamicAccessProfiles.Y, out float projectedY))
            {
                return false;
            }

            screenPosition = new(projectedX, projectedY);
            return true;
        }
    }
}