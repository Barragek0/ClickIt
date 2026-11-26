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
        PathfindingLabelSuppressionEvaluator PathfindingLabelSuppression,
        Action<string>? DebugLog = null,
        Func<LabelOnGround, IReadOnlyList<LabelOnGround>?, bool>? IsLabelFullyOverlapped = null,
        Func<Entity, bool>? ShouldApproachAltar = null,
        Func<RectangleF, bool>? IsBlightLabelRegionClickable = null);

    internal sealed class OffscreenTraversalTargetResolver(OffscreenTraversalTargetResolverDependencies dependencies)
    {
        private readonly OffscreenTraversalTargetResolverDependencies _dependencies = dependencies;

        // Offscreen-walkable structures (eldritch altars, area transitions, shrines) are retained by the shared EntityEventHub with ONE subscription and ONE path read per event. The resolution re-reads each retained entity's fresh dynamic state (distance/targetable/clickable) because that changes in place and does not fire events. The offscreen walk-target scan runs every click tick whenever no label is clickable and walks all enabled entity categories (altars/shrines/transitions) with per-entity DLR reads — the dominant click-processing cost in that state. Cache the resolution for a short window; entities stream in/out within 250ms and the sticky-target gate bounds walk decisions.
        private Entity? _cachedWalkTarget;
        private long _cachedWalkTargetAtMs;
        private const long OffscreenTargetCacheWindowMs = 250;
        private IReadOnlySet<long>? _crawlerFallbackExcluded;
        private int _fallbackTier;

        internal Entity? ResolveHigherPriorityWalkTarget(IReadOnlySet<long> excluded, int fallbackTier)
        {
            _crawlerFallbackExcluded = excluded;
            _fallbackTier = fallbackTier;
            try { return ResolveNearestOffscreenWalkTargetCore(); }
            finally { _crawlerFallbackExcluded = null; }
        }

        // Retains any entity that could be an offscreen walk target: an eldritch altar (path marker), an area transition (type or path marker), or a shrine (path marker). One path read + type read per EntityAdded keeps the event handler cheap. The per-category resolution still applies its own targetable/clickable filters on top of this coarse retained set.
        internal static bool IsOffscreenWalkableStructure(Entity entity)
        {
            if (entity == null)
                return false;
            string path = DynamicAccess.TryReadString(entity, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            if (EntityEventHub.IsShrineEntity(path, entity))
                return true;
            if (EntityEventHub.IsOffscreenStructurePath(path))
                return true;
            try { return entity.Type == EntityType.AreaTransition; }
            catch { return false; }
        }

        private void EnsureStructureSubscription()
        {
            EntityEventHub.Instance.EnsureSubscribed(_dependencies.GameController);
            if (EntityEventHub.Instance.OffscreenStructures.Count == 0 && Environment.TickCount64 - _lastStructureReseedMs >= StructureReseedIntervalMs)
            {
                _lastStructureReseedMs = Environment.TickCount64;
                EntityEventHub.Instance.Reseed();
            }
        }

        private const long StructureReseedIntervalMs = 2000;
        private long _lastStructureReseedMs;

        internal Entity? ResolveNearestOffscreenWalkTarget()
        {
            long now = Environment.TickCount64;
            if (now - _cachedWalkTargetAtMs < OffscreenTargetCacheWindowMs)
            {
                if (_cachedWalkTarget == null)
                    return _cachedWalkTarget;
                if (DynamicAccess.TryReadString(_cachedWalkTarget, DynamicAccessProfiles.Path, out string cachedPath)
                    && !MechanicIds.IsIceCavesFossilPath(cachedPath)
                    && !MechanicIds.IsDelveCrawlerPath(cachedPath)
                    && OffscreenPathingMath.IsEligibleDelveTraversalTarget(_cachedWalkTarget, cachedPath)
                    && (!MechanicIds.IsDelveRewardChestPath(cachedPath)
                        || MechanicClassifier.IsUnopenedTargetableDelveChest(_cachedWalkTarget)))
                    return _cachedWalkTarget;
            }

            Entity? resolved = ResolveNearestOffscreenWalkTargetCore();
            _cachedWalkTarget = resolved;
            _cachedWalkTargetAtMs = now;
            return resolved;
        }

        private Entity? ResolveNearestOffscreenWalkTargetCore()
        {
            int maxDistance = OffscreenPathingMath.OffscreenPathfindingTargetSearchDistance;

            (Entity? labelBackedTarget, string? labelMechanicId) = ResolveNearestOffscreenLabelBackedTarget(maxDistance);
            (Entity? eldritchAltarTarget, string? eldritchAltarMechanicId) = ResolveNearestOffscreenEldritchAltarTarget(maxDistance);
            Entity? shrineTarget = ResolveNearestOffscreenShrineTarget(maxDistance);
            (Entity? areaTransitionTarget, string? areaTransitionMechanicId) = ResolveNearestOffscreenAreaTransitionTarget(maxDistance);
            (Entity? delveTarget, string? delveMechanicId) = ResolveNearestOffscreenDelveTarget(maxDistance);

            _dependencies.DebugLog?.Invoke(string.Format("[TraversalResolver] ResolveNearestOffscreenWalkTarget: label={0} altar={1} shrine={2} transition={3}",
                    DescribePath(labelBackedTarget), DescribePath(eldritchAltarTarget),
                    shrineTarget != null ? "yes" : "null", DescribePath(areaTransitionTarget)));

            if (labelBackedTarget == null && eldritchAltarTarget == null && shrineTarget == null && areaTransitionTarget == null && delveTarget == null)
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
            _ = MechanicCandidateResolver.TryPromoteOffscreenCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, delveTarget, delveMechanicId, (distance, mechanicId) => BuildMechanicRank(distance, mechanicId, mechanicPriorityContext));

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
                resolveMechanicId: (entity, path) => MechanicClassifier.GetAreaTransitionMechanicId(
                    _dependencies.Settings.ClickAreaTransitions.Value,
                    _dependencies.Settings.ClickLabyrinthTrials.Value,
                    entity.Type,
                    path));
        }

        private (Entity? Target, string? MechanicId) ResolveNearestOffscreenDelveTarget(int maxDistance)
        {
            if (!_dependencies.Settings.ClickDelveSpawners.Value
                && !_dependencies.Settings.ClickLeagueChests.Value)
                return (null, null);

            return ResolveNearestOffscreenEntityTarget(
                maxDistance,
                includeEntity: static (_, path) => MechanicIds.IsDelveOffscreenWalkTargetPath(path),
                resolveMechanicId: (entity, path) => MechanicIds.IsTangledFossilInitiatorPath(path)
                    ? (_dependencies.Settings.ClickDelveSpawners.Value ? MechanicIds.DelveEncounterInitiators : null)
                    : path.Contains("/DelveChests/", StringComparison.OrdinalIgnoreCase) ? ResolveDelveChestMechanic(entity, path)
                    : _dependencies.Settings.ClickDelveSpawners.Value ? MechanicIds.DelveAzuriteVeins : null);
        }

        private string? ResolveDelveChestMechanic(Entity entity, string path)
        {
            ClickItSettings settings = _dependencies.Settings;
            string name = DynamicAccess.TryReadString(entity, DynamicAccessProfiles.RenderName, out string renderName)
                ? renderName : string.Empty;
            return MechanicClassifier.GetChestMechanicIdFromConfiguredRules(
                settings.ClickBasicChests.Value, settings.ClickLeagueChests.Value, settings.ClickLeagueChestsOther.Value,
                ClickSettingsFactory.BuildEnabledLeagueChestSpecificIds(settings, settings.ClickLeagueChests.Value),
                EntityType.Chest, path, name);
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
            IReadOnlyList<LabelOnGround>? labels = _dependencies.VisibleLabelSnapshots.GetCachedLabels();
            if (labels == null || labels.Count == 0)
            {
                _dependencies.DebugLog?.Invoke("[TraversalResolver] ResolveNearestOffscreenLabelBackedTarget: no cached labels");
                return (null, null);
            }

            _dependencies.DebugLog?.Invoke($"[TraversalResolver] ResolveNearestOffscreenLabelBackedTarget: scanning {labels.Count} cached labels");

            RectangleF windowArea;
            try
            {
                windowArea = GameWindowRectResolver.Resolve(_dependencies.GameController);
            }
            catch
            {
                windowArea = default;
            }
            _dependencies.MechanicPriorityContextProvider.Refresh();
            MechanicPriorityContext mechanicPriorityContext = _dependencies.MechanicPriorityContextProvider.CreateContext();

            int rejectedNoEntity = 0;
            int rejectedDistance = 0;
            int rejectedSuppressed = 0;
            int rejectedNoMechanic = 0;
            int rejectedShouldContinue = 0;
            int rejectedFullyOverlapped = 0;
            Entity? best = null;
            string? bestMechanicId = null;
            MechanicRank bestRank = default;
            bool hasBestRank = false;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                DynamicAccess.TryGetLabelItemOnGround(label, out Entity? entity);
                if (label == null || entity == null)
                {
                    rejectedNoEntity++;
                    continue;
                }
                if (_crawlerFallbackExcluded != null && TryGetAddress(entity, out long labelAddress) && _crawlerFallbackExcluded.Contains(labelAddress))
                    continue;
                if (!TryGetOffscreenCandidateState(entity, out float distance, out _))
                {
                    rejectedDistance++;
                    continue;
                }
                if (DynamicAccess.TryReadString(entity, DynamicAccessProfiles.Path, out string labelPath)
                    && (!OffscreenPathingMath.IsEligibleDelveTraversalTarget(entity, labelPath)
                        || (MechanicIds.IsDelveRewardChestPath(labelPath)
                            && !MechanicClassifier.IsUnopenedTargetableDelveChest(entity))))
                    continue;
                if (distance > maxDistance)
                {
                    rejectedDistance++;
                    continue;
                }
                if (_dependencies.PathfindingLabelSuppression.ShouldSuppressPathfindingLabel(label))
                {
                    rejectedSuppressed++;
                    continue;
                }

                if (_crawlerFallbackExcluded == null && _dependencies.IsLabelFullyOverlapped?.Invoke(label, labels) == true)
                {
                    rejectedFullyOverlapped++;
                    continue;
                }

                string? mechanicId = _dependencies.LabelInteractionPort.GetMechanicIdForLabel(label);
                if (string.IsNullOrWhiteSpace(mechanicId))
                {
                    rejectedNoMechanic++;
                    continue;
                }
                if (!_dependencies.LabelInteraction.CanSelectLabel(label, mechanicId))
                    continue;
                if ((MechanicClassifier.IsLockedDelveChest(entity) && !MechanicClassifier.HasVisibleDelveCompletionLabel(labels))
                    || (_crawlerFallbackExcluded != null && (MechanicIds.GetFallbackTier(mechanicId) >= _fallbackTier
                        || MechanicClassifier.IsLockedStrongbox(entity))))
                    continue;

                if (!ShouldContinuePathfindingToLabel(label, entity, labels, windowArea, mechanicId))
                {
                    rejectedShouldContinue++;
                    continue;
                }

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

            _dependencies.DebugLog?.Invoke(string.Format("[TraversalResolver] Label scan done: total={0} noEntity={1} dist={2} supp={3} noMech={4} skipCont={5} overlapped={6} best={7}",
                    labels.Count, rejectedNoEntity, rejectedDistance, rejectedSuppressed,
                    rejectedNoMechanic, rejectedShouldContinue, rejectedFullyOverlapped,
                    DescribePath(best)));

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

            void Consider(Entity entity)
            {
                if (_crawlerFallbackExcluded != null && TryGetAddress(entity, out long address) && _crawlerFallbackExcluded.Contains(address))
                    return;
                if (!TryPrepareOffscreenEntityTargetCandidate(entity, maxDistance, out string path))
                    return;

                if (MechanicClassifier.IsLockedDelveChest(entity)
                    && !MechanicClassifier.HasVisibleDelveCompletionLabel(_dependencies.VisibleLabelSnapshots.GetCachedLabels()))
                    return;

                if (_crawlerFallbackExcluded != null && MechanicIds.IsDelveOffscreenWalkTargetPath(path)
                    && !MechanicIds.IsTangledFossilInitiatorPath(path)
                    && (!MechanicIds.IsDelveRewardChestPath(path)
                        || ResolveDelveChestMechanic(entity, path) == null
                        || !DynamicAccess.TryReadBool(entity, DynamicAccessProfiles.IsTargetable, out bool targetable) || !targetable
                        || (MechanicClassifier.IsLockedDelveChest(entity)
                            && !MechanicClassifier.HasVisibleDelveCompletionLabel(_dependencies.VisibleLabelSnapshots.GetCachedLabels()))))
                    return;

                if (!includeEntity(entity, path))
                    return;

                string? mechanicId = resolveMechanicId(entity, path);
                if (string.IsNullOrWhiteSpace(mechanicId))
                    return;

                if (!DynamicAccess.TryReadFloat(entity, DynamicAccessProfiles.DistancePlayer, out float distance))
                    return;

                if (distance >= bestDistance)
                    return;

                bestDistance = distance;
                best = entity;
                bestMechanicId = mechanicId;
            }

            EnsureStructureSubscription();
            List<Entity> structures = EntityEventHub.Instance.OffscreenStructures.Snapshot();
            if (structures.Count > 0)
            {
                // Event-maintained retained set: only the fixed offscreen structures, not every entity in the area. Far-away structures survive stream-out (the retained cache), so offscreen targets are found without the full valid-entity walk.
                for (int i = 0; i < structures.Count; i++)
                    Consider(structures[i]);
            }
            else
            {
                // Discovery fallback (only when nothing is retained yet): a stale/unseeded retained cache or structures that appeared after the last reseed.
                EntityQueryService.VisitValidEntities(_dependencies.GameController, entity =>
                {
                    Consider(entity);
                    return false;
                });
            }

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

            if (!OffscreenPathingMath.IsEligibleDelveTraversalTarget(entity, path))
                return false;

            if (MechanicIds.IsDelveRewardChestPath(path)
                && !MechanicClassifier.IsUnopenedTargetableDelveChest(entity))
                return false;

            if (!TryProjectEntityScreenPosition(entity, out Vector2 screen))
                return false;

            if (ClickSafetyPolicy.IsWithinSearchRadius(_dependencies.Settings, entity)
                && _dependencies.IsClickableInEitherSpace(screen, path)
                && !CanApproachUnlabelledDelveTarget(entity, path, _dependencies.VisibleLabelSnapshots.GetCachedLabels()))
                return false;

            return true;
        }

        internal static bool CanApproachUnlabelledDelveTarget(Entity entity, string path, IReadOnlyList<LabelOnGround>? labels)
        {
            bool rewardChest = MechanicIds.IsDelveRewardChestPath(path);
            if ((!rewardChest && !MechanicIds.IsTangledFossilInitiatorPath(path))
                || labels == null
                || !DynamicAccess.TryReadEntityAddress(entity, out long address) || address == 0
                || !OffscreenPathingMath.IsEligibleDelveTraversalTarget(entity, path)
                || (rewardChest && !MechanicClassifier.IsUnopenedTargetableDelveChest(entity)))
                return false;

            for (int i = 0; i < labels.Count; i++)
            {
                if (!DynamicAccess.TryGetLabelItemOnGround(labels[i], out Entity? item)
                    || !DynamicAccess.TryReadEntityAddress(item, out long itemAddress) || itemAddress == 0)
                    return false;
                if (itemAddress == address)
                    return false;
            }

            return true;
        }

        private bool ShouldContinuePathfindingToLabel(
            LabelOnGround label,
            Entity entity,
            IReadOnlyList<LabelOnGround>? allLabels,
            RectangleF windowArea,
            string mechanicId)
        {
            if (!ClickSafetyPolicy.IsWithinSearchRadius(_dependencies.Settings, entity))
                return true;

            if (mechanicId is MechanicIds.AltarsEaterOfWorlds or MechanicIds.AltarsSearingExarch
                && _dependencies.ShouldApproachAltar?.Invoke(entity) == true)
                return true;

            if (!LabelGeometry.TryGetLabelRect(label, out RectangleF rect))
                return true;

            string path = DynamicAccess.TryReadString(entity, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;

            // Blight menus need the full label on screen; other labels just need a clickable part.
            bool blightRequiresFullLabel = path.Contains(Constants.BlightPump, StringComparison.OrdinalIgnoreCase)
                || path.Contains(Constants.BlightFoundation, StringComparison.OrdinalIgnoreCase);

            bool labelInWindow = blightRequiresFullLabel && rect.Left >= 0 && rect.Top >= 0
                && rect.Right <= windowArea.Width && rect.Bottom <= windowArea.Height;
            bool labelClickable = labelInWindow && (_dependencies.IsBlightLabelRegionClickable?.Invoke(rect)
                ?? _dependencies.IsClickableInEitherSpace(rect.Center, path));

            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            (bool clickResolvable, _) = _dependencies.LabelInteraction.TryResolveLabelClickPositionResult(label, null, windowTopLeft, allLabels, path);
            return OffscreenPathingMath.ShouldContinuePathfindingForLabel(
                blightRequiresFullLabel, labelInWindow, labelClickable, clickResolvable);
        }

        private static MechanicRank BuildMechanicRank(float distance, string? mechanicId, MechanicPriorityContext mechanicPriorityContext)
            => CandidateRankingEngine.BuildRank(distance, mechanicId, mechanicPriorityContext);

        private static string DescribePath(Entity? entity)
            => entity == null ? "null" : DynamicAccess.TryReadString(entity, DynamicAccessProfiles.Path, out string path) ? path : "?";

        private static bool TryGetAddress(Entity entity, out long address)
        {
            address = 0;
            if (!DynamicAccess.TryGetDynamicValue(entity, DynamicAccessProfiles.Address, out object? value) || value == null)
                return false;
            address = Convert.ToInt64(value);
            return address != 0;
        }

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
            => EntityScreenProjection.TryProjectEntityScreen(_dependencies.GameController, entity, out screenPosition);
    }
}
