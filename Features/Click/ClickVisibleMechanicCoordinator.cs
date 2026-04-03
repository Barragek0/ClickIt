using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Features.Click
{
    internal readonly record struct VisibleMechanicCoordinatorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        ShrineService ShrineService,
        VisibleMechanicTargetSelector TargetSelector,
        Func<Vector2, string, bool> PointIsInClickableArea,
        Func<Vector2, bool> PerformMechanicClick,
        Func<Vector2, bool, bool> PerformMechanicInteraction,
        Func<Entity?, bool> IsStickyTarget,
        Action ClearStickyOffscreenTarget,
        Action InvalidateShrineCache,
        Action ClearLatestPath,
        Action<string> DebugLog,
        Action<string> HoldDebugTelemetryAfterSuccess,
        Func<bool> ShouldCaptureClickDebug,
        Action<ClickDebugSnapshot> SetLatestClickDebug,
        Func<Vector2, bool> IsInsideWindowInEitherSpace,
        Func<Vector2, string, bool> IsClickableInEitherSpace);

    internal sealed class VisibleMechanicCoordinator(VisibleMechanicCoordinatorDependencies dependencies)
    {
        private const int HiddenFallbackCandidateCacheWindowMs = 150;
        private const int VisibleMechanicCandidateCacheWindowMs = 80;
        private const int GroundLabelEntityAddressCacheWindowMs = 150;

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

            lostShipmentCandidate = _dependencies.TargetSelector.ResolveNextLostShipmentCandidate(labelsOverride);
            settlersOreCandidate = _dependencies.TargetSelector.ResolveNextSettlersOreCandidate();

            cacheState.StoreVisibleCandidates(now, labelCount, lostShipmentCandidate, settlersOreCandidate);
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

            lostShipmentCandidate = _dependencies.TargetSelector.ResolveNextLostShipmentCandidate();
            settlersOreCandidate = _dependencies.TargetSelector.ResolveNextSettlersOreCandidate();

            cacheState.StoreHiddenFallbackCandidates(now, labelCount, lostShipmentCandidate, settlersOreCandidate);
        }

        internal IReadOnlySet<long> CollectGroundLabelEntityAddresses()
        {
            var labels = _dependencies.GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels;
            return cacheState.CollectGroundLabelEntityAddresses(labels, GroundLabelEntityAddressCacheWindowMs);
        }

        public void TryClickShrine(Entity shrine)
        {
            var shrineScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
            Vector2 shrineClickPos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
            if (!_dependencies.PerformMechanicClick(shrineClickPos))
                return;

            if (_dependencies.IsStickyTarget(shrine))
                _dependencies.ClearStickyOffscreenTarget();

            _dependencies.InvalidateShrineCache();
        }

        public void TryClickLostShipment(LostShipmentCandidate candidate)
        {
            _dependencies.DebugLog("[ProcessRegularClick] Clicking Lost Shipment candidate via ItemOnGround position.");
            if (!_dependencies.PerformMechanicClick(candidate.ClickPosition))
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

            bool captureClickDebug = _dependencies.ShouldCaptureClickDebug();
            if (captureClickDebug)
                PublishSettlersCandidateDebug("ClickAttempt", candidate, "Attempting settlers click");

            bool clicked = _dependencies.PerformMechanicInteraction(
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
            _dependencies.SetLatestClickDebug(CreateSettlersClickDebugSnapshot(
                stage: stage,
                mechanicId: candidate.MechanicId,
                entityPath: candidate.EntityPath,
                distance: candidate.Distance,
                worldScreenRaw: candidate.WorldScreenRaw,
                worldScreenAbsolute: candidate.WorldScreenAbsolute,
                resolvedClickPoint: candidate.ClickPosition,
                resolved: true,
                notes: notes));
        }

        private ClickDebugSnapshot CreateSettlersClickDebugSnapshot(
            string stage,
            string mechanicId,
            string entityPath,
            float distance,
            Vector2 worldScreenRaw,
            Vector2 worldScreenAbsolute,
            Vector2 resolvedClickPoint,
            bool resolved,
            string notes)
        {
            return new ClickDebugSnapshot(
                HasData: true,
                Stage: stage,
                MechanicId: mechanicId,
                EntityPath: entityPath,
                Distance: distance,
                WorldScreenRaw: worldScreenRaw,
                WorldScreenAbsolute: worldScreenAbsolute,
                ResolvedClickPoint: resolvedClickPoint,
                Resolved: resolved,
                CenterInWindow: _dependencies.IsInsideWindowInEitherSpace(worldScreenAbsolute),
                CenterClickable: _dependencies.IsClickableInEitherSpace(worldScreenAbsolute, entityPath),
                ResolvedInWindow: resolved && _dependencies.IsInsideWindowInEitherSpace(resolvedClickPoint),
                ResolvedClickable: resolved && _dependencies.IsClickableInEitherSpace(resolvedClickPoint, entityPath),
                Notes: notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64);
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

            if (_dependencies.IsStickyTarget(entity))
                _dependencies.ClearStickyOffscreenTarget();

            if (_dependencies.Settings.WalkTowardOffscreenLabels.Value)
                _dependencies.ClearLatestPath();
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