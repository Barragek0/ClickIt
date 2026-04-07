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
        VisibleMechanicSelectionSnapshot GetVisibleMechanicSelectionSnapshot()
        {
            ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
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
                    var screenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
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
            int labelCount = GetVisibleMechanicLabelCount(labelsOverride);
            long now = Environment.TickCount64;

            if (TryResolveCachedCandidates(now, labelCount, useHiddenFallbackCache: false, out lostShipmentCandidate, out settlersOreCandidate))
            {
                return;
            }

            ResolveFreshCandidates(now, labelCount, labelsOverride, useHiddenFallbackCache: false, out lostShipmentCandidate, out settlersOreCandidate);
        }

        public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
        {
            int labelCount = GetVisibleMechanicLabelCount(labelsOverride: null);
            long now = Environment.TickCount64;

            if (TryResolveCachedCandidates(now, labelCount, useHiddenFallbackCache: true, out lostShipmentCandidate, out settlersOreCandidate))
            {
                return;
            }

            ResolveFreshCandidates(now, labelCount, labelsOverride: null, useHiddenFallbackCache: true, out lostShipmentCandidate, out settlersOreCandidate);
        }

        public bool TryClickShrineInteraction(Entity shrine)
        {
            var shrineScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
            Vector2 shrineClickPos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
            return TryExecuteMechanicClick(shrineClickPos, () => HandleSuccessfulShrineClick(shrine));
        }

        public bool TryClickLostShipmentInteraction(LostShipmentCandidate candidate)
        {
            _dependencies.DebugLog("[ProcessRegularClick] Clicking Lost Shipment candidate via ItemOnGround position.");
            return TryExecuteMechanicClick(candidate.ClickPosition, () => HandleSuccessfulMechanicEntityClick(candidate.Entity));
        }

        public bool TryClickSettlersOre(SettlersOreCandidate candidate)
        {
            if (!TryPrepareSettlersClick(candidate, out Entity? entity, out bool captureClickDebug))
                return false;

            if (!TryExecuteSettlersClick(candidate, captureClickDebug))
                return false;

            HandleSuccessfulMechanicEntityClick(entity);

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

        private bool TryPrepareSettlersClick(
            SettlersOreCandidate candidate,
            out Entity? entity,
            out bool captureClickDebug)
        {
            entity = candidate.Entity;
            captureClickDebug = false;

            if (!IsSettlersCandidateTargetable(candidate, entity))
                return false;

            _dependencies.DebugLog($"[ProcessRegularClick] Clicking settlers ore candidate ({candidate.MechanicId}) via entity position.");
            captureClickDebug = _dependencies.ClickDebugPublisher.ShouldCaptureClickDebug();
            if (captureClickDebug)
                PublishSettlersCandidateDebug("ClickAttempt", candidate, "Attempting settlers click");

            return true;
        }

        private bool TryExecuteSettlersClick(SettlersOreCandidate candidate, bool captureClickDebug)
        {
            bool clicked = _dependencies.LabelInteraction.PerformMechanicInteraction(
                candidate.ClickPosition,
                SettlersMechanicPolicy.RequiresHoldClick(candidate.MechanicId));

            if (!clicked)
                return false;

            if (captureClickDebug)
                PublishSettlersCandidateDebug("ClickSuccess", candidate, "Settlers click completed");

            return true;
        }

        private bool TryExecuteMechanicClick(Vector2 clickPosition, Action onSuccess)
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

        private void ResolveFreshCandidates(
            long now,
            int labelCount,
            IReadOnlyList<LabelOnGround>? labelsOverride,
            bool useHiddenFallbackCache,
            out LostShipmentCandidate? lostShipmentCandidate,
            out SettlersOreCandidate? settlersOreCandidate)
        {
            lostShipmentCandidate = ResolveLostShipmentCandidate(useHiddenFallbackCache, labelsOverride);
            settlersOreCandidate = _dependencies.SettlersOreTargets.ResolveNextSettlersOreCandidate();

            if (useHiddenFallbackCache)
            {
                cacheState.StoreHiddenFallbackCandidates(now, labelCount, lostShipmentCandidate, settlersOreCandidate);
                return;
            }

            cacheState.StoreVisibleCandidates(now, labelCount, lostShipmentCandidate, settlersOreCandidate);
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
            if (entity == null)
                return;

            HandleSuccessfulMechanicEntityClick(entity.Path, _dependencies.StickyTargets.IsStickyTarget(entity));
        }

        internal void HandleSuccessfulMechanicEntityClick(string? entityPath, bool isStickyTarget)
        {
            ApplySuccessfulMechanicAftermath(BuildSuccessfulMechanicClickReason(entityPath), isStickyTarget);
        }

        public void HandleSuccessfulShrineClick(Entity? shrine)
        {
            HandleSuccessfulMechanicEntityClick(shrine);
            _dependencies.ShrineService.InvalidateCache();
        }

        private static string BuildSuccessfulMechanicClickReason(string? entityPath)
        {
            string resolvedEntityPath = entityPath ?? string.Empty;
            return string.IsNullOrWhiteSpace(resolvedEntityPath)
                ? "Successful mechanic click"
                : $"Successful mechanic click: {resolvedEntityPath}";
        }

        private void ApplySuccessfulMechanicAftermath(string reason, bool isStickyTarget)
        {
            _dependencies.HoldDebugTelemetryAfterSuccess(reason);

            if (isStickyTarget)
                _dependencies.StickyTargets.ClearStickyOffscreenTarget();

            if (_dependencies.Settings.WalkTowardOffscreenLabels.Value)
                _dependencies.PathfindingService.ClearLatestPath();
        }

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