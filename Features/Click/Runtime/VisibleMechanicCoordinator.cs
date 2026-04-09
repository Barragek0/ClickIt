namespace ClickIt.Features.Click.Runtime
{
    internal readonly record struct VisibleMechanicSelectionSnapshot(
        LostShipmentCandidate? LostShipment,
        SettlersOreCandidate? Settlers)
    {
        internal bool HasLostShipment
            => LostShipment.HasValue;

        internal bool HasSettlers
            => Settlers.HasValue;
    }

    internal readonly record struct VisibleMechanicAvailabilitySnapshot(
        bool HasLostShipment,
        bool HasSettlers);

    internal interface IVisibleMechanicQueryPort
    {
        Entity? ResolveNextShrineCandidate();
        bool HasClickableShrine();

        VisibleMechanicSelectionSnapshot GetVisibleMechanicSelectionSnapshotForLabels(IReadOnlyList<LabelOnGround>? labelsOverride)
        {
            ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers, labelsOverride);
            return new VisibleMechanicSelectionSnapshot(lostShipment, settlers);
        }

        VisibleMechanicSelectionSnapshot GetVisibleMechanicSelectionSnapshot()
        {
            return GetVisibleMechanicSelectionSnapshotForLabels(labelsOverride: null);
        }

        VisibleMechanicSelectionSnapshot GetHiddenFallbackSelectionSnapshot()
        {
            ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
            return new VisibleMechanicSelectionSnapshot(lostShipment, settlers);
        }

        VisibleMechanicAvailabilitySnapshot GetVisibleMechanicAvailabilitySnapshot()
        {
            VisibleMechanicSelectionSnapshot snapshot = GetVisibleMechanicSelectionSnapshot();
            return new VisibleMechanicAvailabilitySnapshot(snapshot.HasLostShipment, snapshot.HasSettlers);
        }

        void ResolveVisibleMechanicCandidates(
            out LostShipmentCandidate? lostShipmentCandidate,
            out SettlersOreCandidate? settlersOreCandidate,
            IReadOnlyList<LabelOnGround>? labelsOverride = null);
        void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate);
    }

    internal interface IVisibleMechanicInteractionPort
    {
        bool TryClickSettlersOre(SettlersOreCandidate candidate);
        bool TryClickLostShipmentInteraction(LostShipmentCandidate candidate);
        bool TryClickShrineInteraction(Entity shrine);

        void HandleSuccessfulMechanicEntityClick(Entity? entity);
        void HandleSuccessfulShrineClick(Entity? shrine);
    }

    internal interface IVisibleMechanicRuntimePort : IVisibleMechanicQueryPort, IVisibleMechanicInteractionPort
    {
    }

    internal readonly record struct VisibleMechanicCoordinatorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        ShrineService ShrineService,
        LostShipmentTargetSelector LostShipmentTargets,
        SettlersOreTargetSelector SettlersOreTargets,
        Func<Vector2, string, bool> PointIsInClickableArea,
        ClickLabelInteractionService LabelInteraction,
        OffscreenStickyTargetHandler StickyTargets,
        PathfindingService PathfindingService,
        Action<string> DebugLog,
        Action<string> HoldDebugTelemetryAfterSuccess,
        ClickDebugPublicationService ClickDebugPublisher);

    internal sealed class VisibleMechanicCoordinator(VisibleMechanicCoordinatorDependencies dependencies) : IVisibleMechanicRuntimePort
    {
        private const int HiddenFallbackCandidateCacheWindowMs = 150;
        private const int VisibleMechanicCandidateCacheWindowMs = 80;

        private readonly record struct SettlersClickPlan(Entity? Entity, bool UseHoldClick, bool CaptureClickDebug);

        private readonly VisibleMechanicCacheState cacheState = new();
        private readonly VisibleMechanicCoordinatorDependencies _dependencies = dependencies;

        public Entity? ResolveNextShrineCandidate()
        {
            if (!_dependencies.Settings.ClickShrines.Value)
                return null;

            RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 cursorAbsolute = ManualCursorSelectionMath.GetCursorAbsolutePosition();
            return _dependencies.ShrineService.GetNearestShrineInRange(
                _dependencies.Settings.ClickDistance.Value,
                isInClickableArea: pos => _dependencies.PointIsInClickableArea(pos, MechanicIds.Shrines),
                cursorDistanceResolver: shrine =>
                {
                    NumVector2 screenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
                    Vector2 shrineScreenAbsolute = new(screenRaw.X + windowTopLeft.X, screenRaw.Y + windowTopLeft.Y);
                    return ManualCursorSelectionMath.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, shrineScreenAbsolute, windowTopLeft);
                });
        }

        public bool HasClickableShrine()
            => ResolveNextShrineCandidate() != null;

        public void ResolveVisibleMechanicCandidates(
            out LostShipmentCandidate? lostShipmentCandidate,
            out SettlersOreCandidate? settlersOreCandidate,
            IReadOnlyList<LabelOnGround>? labelsOverride = null)
        {
            WriteSelectionSnapshot(
                ResolveSelectionSnapshot(labelsOverride, useHiddenFallbackCache: false),
                out lostShipmentCandidate,
                out settlersOreCandidate);
        }

        public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
        {
            WriteSelectionSnapshot(
                ResolveSelectionSnapshot(labelsOverride: null, useHiddenFallbackCache: true),
                out lostShipmentCandidate,
                out settlersOreCandidate);
        }

        public bool TryClickShrineInteraction(Entity shrine)
        {
            NumVector2 shrineScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
            Vector2 shrineClickPos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
            return TryClickDirectMechanic(shrineClickPos, onSuccess: () => TryHandleSuccessfulMechanicAftermath(shrine, invalidateShrineCache: true));
        }

        public bool TryClickLostShipmentInteraction(LostShipmentCandidate candidate)
        {
            _dependencies.DebugLog("[ProcessRegularClick] Clicking Lost Shipment candidate via ItemOnGround position.");
            return TryClickDirectMechanic(candidate.ClickPosition, onSuccess: () => TryHandleSuccessfulMechanicAftermath(candidate.Entity, invalidateShrineCache: false));
        }

        public bool TryClickSettlersOre(SettlersOreCandidate candidate)
        {
            if (!TryBuildSettlersClickPlan(candidate, out SettlersClickPlan plan))
                return false;

            if (!TryExecuteSettlersClick(candidate, plan))
                return false;

            TryHandleSuccessfulMechanicAftermath(plan.Entity, invalidateShrineCache: false);

            return true;
        }

        private void PublishSettlersCandidateDebug(string stage, SettlersOreCandidate candidate, string notes)
        {
            _dependencies.ClickDebugPublisher.PublishSettlersClickDebugSnapshot(
                stage: stage,
                mechanicId: candidate.MechanicId,
                entityPath: candidate.EntityPath,
                distance: candidate.Distance,
                worldScreenRaw: candidate.WorldScreenRaw,
                worldScreenAbsolute: candidate.WorldScreenAbsolute,
                resolvedClickPoint: candidate.ClickPosition,
                resolved: true,
                notes: notes);
        }

        private bool TryBuildSettlersClickPlan(
            SettlersOreCandidate candidate,
            out SettlersClickPlan plan)
        {
            Entity? entity = candidate.Entity;
            plan = default;

            if (!IsSettlersCandidateTargetable(candidate, entity))
                return false;

            _dependencies.DebugLog($"[ProcessRegularClick] Clicking settlers ore candidate ({candidate.MechanicId}) via entity position.");
            bool captureClickDebug = _dependencies.ClickDebugPublisher.ShouldCaptureClickDebug();
            if (captureClickDebug)
                PublishSettlersCandidateDebug("ClickAttempt", candidate, "Attempting settlers click");

            plan = new SettlersClickPlan(
                Entity: entity,
                UseHoldClick: SettlersMechanicPolicy.RequiresHoldClick(candidate.MechanicId),
                CaptureClickDebug: captureClickDebug);

            return true;
        }

        private bool TryExecuteSettlersClick(SettlersOreCandidate candidate, SettlersClickPlan plan)
        {
            bool clicked = _dependencies.LabelInteraction.PerformMechanicInteraction(
                candidate.ClickPosition,
                plan.UseHoldClick);

            if (!clicked)
                return false;

            if (plan.CaptureClickDebug)
                PublishSettlersCandidateDebug("ClickSuccess", candidate, "Settlers click completed");

            return true;
        }

        private bool TryClickDirectMechanic(Vector2 clickPosition, Action onSuccess)
        {
            if (!_dependencies.LabelInteraction.PerformMechanicClick(clickPosition))
                return false;

            onSuccess();
            return true;
        }

        private bool IsSettlersCandidateTargetable(SettlersOreCandidate candidate, Entity? entity)
        {
            if (entity == null || entity.IsTargetable)
                return true;

            _dependencies.DebugLog($"[ProcessRegularClick] Skipping settlers ore candidate ({candidate.MechanicId}) because entity is not targetable.");
            return false;
        }

        private int GetVisibleMechanicLabelCount(IReadOnlyList<LabelOnGround>? labelsOverride)
            => labelsOverride?.Count
                ?? _dependencies.GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels?.Count
                ?? 0;

        private VisibleMechanicSelectionSnapshot ResolveSelectionSnapshot(
            IReadOnlyList<LabelOnGround>? labelsOverride,
            bool useHiddenFallbackCache)
        {
            int labelCount = GetVisibleMechanicLabelCount(labelsOverride);
            long now = Environment.TickCount64;

            if (TryResolveCachedCandidates(now, labelCount, useHiddenFallbackCache, out LostShipmentCandidate? cachedLostShipment, out SettlersOreCandidate? cachedSettlers))
                return new VisibleMechanicSelectionSnapshot(cachedLostShipment, cachedSettlers);

            return ResolveFreshSelectionSnapshot(now, labelCount, labelsOverride, useHiddenFallbackCache);
        }

        private VisibleMechanicSelectionSnapshot ResolveFreshSelectionSnapshot(
            long now,
            int labelCount,
            IReadOnlyList<LabelOnGround>? labelsOverride,
            bool useHiddenFallbackCache)
        {
            LostShipmentCandidate? lostShipmentCandidate = ResolveLostShipmentCandidate(useHiddenFallbackCache, labelsOverride);
            SettlersOreCandidate? settlersOreCandidate = _dependencies.SettlersOreTargets.ResolveNextSettlersOreCandidate();

            if (useHiddenFallbackCache)
                cacheState.StoreHiddenFallbackCandidates(now, labelCount, lostShipmentCandidate, settlersOreCandidate);
            else
                cacheState.StoreVisibleCandidates(now, labelCount, lostShipmentCandidate, settlersOreCandidate);

            return new VisibleMechanicSelectionSnapshot(lostShipmentCandidate, settlersOreCandidate);
        }

        private static void WriteSelectionSnapshot(
            VisibleMechanicSelectionSnapshot snapshot,
            out LostShipmentCandidate? lostShipmentCandidate,
            out SettlersOreCandidate? settlersOreCandidate)
        {
            lostShipmentCandidate = snapshot.LostShipment;
            settlersOreCandidate = snapshot.Settlers;
        }

        private bool TryResolveCachedCandidates(
            long now,
            int labelCount,
            bool useHiddenFallbackCache,
            out LostShipmentCandidate? lostShipmentCandidate,
            out SettlersOreCandidate? settlersOreCandidate)
        {
            if (useHiddenFallbackCache)
            {
                return cacheState.TryGetHiddenFallbackCandidates(
                    now,
                    labelCount,
                    HiddenFallbackCandidateCacheWindowMs,
                    out lostShipmentCandidate,
                    out settlersOreCandidate);
            }

            return cacheState.TryGetVisibleCandidates(
                now,
                labelCount,
                VisibleMechanicCandidateCacheWindowMs,
                IsLostShipmentCandidateUsable,
                IsSettlersCandidateUsable,
                out lostShipmentCandidate,
                out settlersOreCandidate);
        }

        private LostShipmentCandidate? ResolveLostShipmentCandidate(bool useHiddenFallbackCache, IReadOnlyList<LabelOnGround>? labelsOverride)
            => useHiddenFallbackCache
                ? _dependencies.LostShipmentTargets.ResolveNextLostShipmentCandidate()
                : _dependencies.LostShipmentTargets.ResolveNextLostShipmentCandidate(labelsOverride);

        /**
        Keep this thin runtime wrapper so normal click success still reads from
        the real Entity owner at runtime. The internal overload preserves a
        bounded seam for aftermath tests without fabricating brittle ExileCore
        Entity state just to prove telemetry, sticky-target cleanup, or path
        clearing behavior.
         */
        public void HandleSuccessfulMechanicEntityClick(Entity? entity)
        {
            TryHandleSuccessfulMechanicAftermath(entity, invalidateShrineCache: false);
        }

        internal void HandleSuccessfulMechanicEntityClick(string? entityPath, bool isStickyTarget)
        {
            HandleSuccessfulMechanicAftermath(entityPath, isStickyTarget, invalidateShrineCache: false);
        }

        public void HandleSuccessfulShrineClick(Entity? shrine)
        {
            TryHandleSuccessfulMechanicAftermath(shrine, invalidateShrineCache: true);
        }

        private bool TryHandleSuccessfulMechanicAftermath(Entity? entity, bool invalidateShrineCache)
        {
            if (entity == null)
                return false;

            HandleSuccessfulMechanicAftermath(
                entity.Path,
                _dependencies.StickyTargets.IsStickyTarget(entity),
                invalidateShrineCache);
            return true;
        }

        private void HandleSuccessfulMechanicAftermath(
            string? entityPath,
            bool isStickyTarget,
            bool invalidateShrineCache)
        {
            ApplySuccessfulMechanicAftermath(new SuccessfulInteractionAftermath(
                Reason: BuildSuccessfulMechanicClickReason(entityPath),
                ShouldClearStickyTarget: isStickyTarget,
                ShouldClearPath: _dependencies.Settings.WalkTowardOffscreenLabels.Value,
                ShouldInvalidateShrineCache: invalidateShrineCache));
        }

        private static string BuildSuccessfulMechanicClickReason(string? entityPath)
        {
            string resolvedEntityPath = entityPath ?? string.Empty;
            return string.IsNullOrWhiteSpace(resolvedEntityPath)
                ? "Successful mechanic click"
                : $"Successful mechanic click: {resolvedEntityPath}";
        }

        private void ApplySuccessfulMechanicAftermath(SuccessfulInteractionAftermath aftermath)
            => SuccessfulInteractionAftermathApplier.Apply(
                aftermath,
                _dependencies.HoldDebugTelemetryAfterSuccess,
                clearStickyTarget: _dependencies.StickyTargets.ClearStickyOffscreenTarget,
                clearPath: _dependencies.PathfindingService.ClearLatestPath,
                invalidateShrineCache: () => _dependencies.ShrineService.InvalidateCache());

        private bool IsLostShipmentCandidateUsable(LostShipmentCandidate? candidate)
        {
            if (!candidate.HasValue)
                return true;

            Entity entity = candidate.Value.Entity;
            return entity != null
                && entity.IsValid
                && !entity.IsOpened
                && entity.DistancePlayer <= _dependencies.Settings.ClickDistance.Value;
        }

        private bool IsSettlersCandidateUsable(SettlersOreCandidate? candidate)
        {
            if (!candidate.HasValue)
                return true;

            Entity entity = candidate.Value.Entity;
            return entity != null
                && entity.IsValid
                && entity.DistancePlayer <= _dependencies.Settings.ClickDistance.Value;
        }

    }
}