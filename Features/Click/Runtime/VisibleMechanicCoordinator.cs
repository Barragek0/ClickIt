namespace ClickIt.Features.Click.Runtime
{
    internal interface IVisibleMechanicSelectionSource
    {
        Entity? ResolveNextShrineCandidate();
        bool HasClickableShrine();
        void ResolveVisibleMechanicCandidates(
            out LostShipmentCandidate? lostShipmentCandidate,
            out SettlersOreCandidate? settlersOreCandidate,
            IReadOnlyList<LabelOnGround>? labelsOverride = null);
        void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate);
        (LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers) GetVisibleMechanicCandidates();
        (bool HasLostShipment, bool HasSettlers) GetVisibleMechanicAvailability();
    }

    internal interface IVisibleMechanicManualInteractionPort : IVisibleMechanicSelectionSource
    {
        bool TryClickSettlersOre(SettlersOreCandidate candidate);
        void TryClickLostShipment(LostShipmentCandidate candidate);
        void TryClickShrine(Entity shrine);
        void HandleSuccessfulMechanicEntityClick(Entity? entity);
        void HandleSuccessfulShrineClick(Entity? shrine);
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

    internal sealed class VisibleMechanicCoordinator(VisibleMechanicCoordinatorDependencies dependencies) : IVisibleMechanicManualInteractionPort
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
            int labelCount = labelsOverride?.Count
                ?? _dependencies.GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels?.Count
                ?? 0;

            long now = Environment.TickCount64;

            if (cacheState.TryGetVisibleCandidates(
                    now,
                    labelCount,
                    VisibleMechanicCandidateCacheWindowMs,
                    IsLostShipmentCandidateUsable,
                    IsSettlersCandidateUsable,
                    out lostShipmentCandidate,
                    out settlersOreCandidate))
            {
                return;
            }

            lostShipmentCandidate = _dependencies.LostShipmentTargets.ResolveNextLostShipmentCandidate(labelsOverride);
            settlersOreCandidate = _dependencies.SettlersOreTargets.ResolveNextSettlersOreCandidate();

            cacheState.StoreVisibleCandidates(now, labelCount, lostShipmentCandidate, settlersOreCandidate);
        }

        public (LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers) GetVisibleMechanicCandidates()
        {
            ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
            return (lostShipment, settlers);
        }

        public (bool HasLostShipment, bool HasSettlers) GetVisibleMechanicAvailability()
        {
            (LostShipmentCandidate? lostShipment, SettlersOreCandidate? settlers) = GetVisibleMechanicCandidates();
            return (lostShipment.HasValue, settlers.HasValue);
        }

        public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
        {
            int labelCount = _dependencies.GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels?.Count ?? 0;
            long now = Environment.TickCount64;

            if (cacheState.TryGetHiddenFallbackCandidates(
                    now,
                    labelCount,
                    HiddenFallbackCandidateCacheWindowMs,
                    out lostShipmentCandidate,
                    out settlersOreCandidate))
            {
                return;
            }

            lostShipmentCandidate = _dependencies.LostShipmentTargets.ResolveNextLostShipmentCandidate();
            settlersOreCandidate = _dependencies.SettlersOreTargets.ResolveNextSettlersOreCandidate();

            cacheState.StoreHiddenFallbackCandidates(now, labelCount, lostShipmentCandidate, settlersOreCandidate);
        }

        public void TryClickShrine(Entity shrine)
        {
            var shrineScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
            Vector2 shrineClickPos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
            if (!_dependencies.LabelInteraction.PerformMechanicClick(shrineClickPos))
                return;

            HandleSuccessfulShrineClick(shrine);
        }

        public void TryClickLostShipment(LostShipmentCandidate candidate)
        {
            _dependencies.DebugLog("[ProcessRegularClick] Clicking Lost Shipment candidate via ItemOnGround position.");
            if (!_dependencies.LabelInteraction.PerformMechanicClick(candidate.ClickPosition))
                return;

            HandleSuccessfulMechanicEntityClick(candidate.Entity);
        }

        public bool TryClickSettlersOre(SettlersOreCandidate candidate)
        {
            Entity entity = candidate.Entity;
            if (entity != null && !entity.IsTargetable)
            {
                _dependencies.DebugLog($"[ProcessRegularClick] Skipping settlers ore candidate ({candidate.MechanicId}) because entity is not targetable.");
                return false;
            }

            _dependencies.DebugLog($"[ProcessRegularClick] Clicking settlers ore candidate ({candidate.MechanicId}) via entity position.");

            bool captureClickDebug = _dependencies.ClickDebugPublisher.ShouldCaptureClickDebug();
            if (captureClickDebug)
                PublishSettlersCandidateDebug("ClickAttempt", candidate, "Attempting settlers click");

            bool clicked = _dependencies.LabelInteraction.PerformMechanicInteraction(
                candidate.ClickPosition,
                SettlersMechanicPolicy.RequiresHoldClick(candidate.MechanicId));

            if (!clicked)
                return false;

            if (captureClickDebug)
                PublishSettlersCandidateDebug("ClickSuccess", candidate, "Settlers click completed");

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

        public void HandleSuccessfulMechanicEntityClick(Entity? entity)
        {
            if (entity == null)
                return;

            string entityPath = entity.Path ?? string.Empty;
            string reason = string.IsNullOrWhiteSpace(entityPath)
                ? "Successful mechanic click"
                : $"Successful mechanic click: {entityPath}";
            _dependencies.HoldDebugTelemetryAfterSuccess(reason);

            if (_dependencies.StickyTargets.IsStickyTarget(entity))
                _dependencies.StickyTargets.ClearStickyOffscreenTarget();

            if (_dependencies.Settings.WalkTowardOffscreenLabels.Value)
                _dependencies.PathfindingService.ClearLatestPath();
        }

        public void HandleSuccessfulShrineClick(Entity? shrine)
        {
            HandleSuccessfulMechanicEntityClick(shrine);
            _dependencies.ShrineService.InvalidateCache();
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