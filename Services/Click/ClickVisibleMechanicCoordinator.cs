using ClickIt.Definitions;
using ClickIt.Services.Observability;
using ClickIt.Services.Click.Selection;
using ClickIt.Services.Click.Runtime;
using ClickIt.Services.Label.Classification;
using ClickIt.Services.Label.Classification.Policies;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    internal readonly record struct VisibleMechanicCoordinatorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        ShrineService ShrineService,
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
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        Func<string?, bool> IsSettlersMechanicEnabled);

    internal sealed class VisibleMechanicCoordinator(VisibleMechanicCoordinatorDependencies dependencies)
    {
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
                    ClickService.VisibleMechanicCandidateCacheWindowMs,
                    IsLostShipmentCandidateUsable,
                    IsSettlersCandidateUsable,
                    out lostShipmentCandidate,
                    out settlersOreCandidate))
            {
                return;
            }

            lostShipmentCandidate = ResolveNextLostShipmentCandidate(labelsOverride);
            settlersOreCandidate = ResolveNextSettlersOreCandidate();

            cacheState.StoreVisibleCandidates(now, labelCount, lostShipmentCandidate, settlersOreCandidate);
        }

        public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
        {
            int labelCount = _dependencies.GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels?.Count ?? 0;
            long now = Environment.TickCount64;

            if (cacheState.TryGetHiddenFallbackCandidates(
                    now,
                    labelCount,
                    ClickService.HiddenFallbackCandidateCacheWindowMs,
                    out lostShipmentCandidate,
                    out settlersOreCandidate))
            {
                return;
            }

            lostShipmentCandidate = ResolveNextLostShipmentCandidate();
            settlersOreCandidate = ResolveNextSettlersOreCandidate();

            cacheState.StoreHiddenFallbackCandidates(now, labelCount, lostShipmentCandidate, settlersOreCandidate);
        }

        internal IReadOnlySet<long> CollectGroundLabelEntityAddresses()
        {
            var labels = _dependencies.GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels;
            return cacheState.CollectGroundLabelEntityAddresses(labels, ClickService.GroundLabelEntityAddressCacheWindowMs);
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

        private LostShipmentCandidate? ResolveNextLostShipmentCandidate(IReadOnlyList<LabelOnGround>? labelsOverride = null)
        {
            if (!_dependencies.Settings.ClickLostShipmentCrates.Value)
                return null;

            try
            {
                LostShipmentCandidate? best = null;
                RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
                Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                Vector2 cursorAbsolute = ManualCursorSelectionMath.GetCursorAbsolutePosition();

                if (labelsOverride != null)
                {
                    ScanLostShipmentLabelsForBestCandidate(labelsOverride, ref best, cursorAbsolute, windowTopLeft);
                    return best;
                }

                var labels = _dependencies.GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels;
                if (labels == null || labels.Count == 0)
                    return null;

                ScanLostShipmentLabelsForBestCandidate(labels, ref best, cursorAbsolute, windowTopLeft);

                return best;
            }
            catch (Exception ex)
            {
                _dependencies.DebugLog($"[ResolveNextLostShipmentCandidate] Failed to scan hidden labels: {ex.Message}");
                return null;
            }
        }

        private void ScanLostShipmentLabelsForBestCandidate(
            IEnumerable<LabelOnGround> labels,
            ref LostShipmentCandidate? best,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft)
        {
            foreach (LabelOnGround label in labels)
            {
                if (!TryCreateLostShipmentCandidate(label, out LostShipmentCandidate candidate))
                    continue;

                _ = MechanicCandidateResolver.TryPromoteLostShipmentCandidate(ref best, candidate, cursorAbsolute, windowTopLeft);
            }
        }

        private bool TryCreateLostShipmentCandidate(LabelOnGround? label, out LostShipmentCandidate candidate)
        {
            candidate = default;

            Entity? entity = label?.ItemOnGround;
            if (label == null || entity == null)
                return false;

            if (VisibleMechanicSelectionPolicy.ShouldSkipLostShipmentEntity(entity.IsValid, entity.DistancePlayer, _dependencies.Settings.ClickDistance.Value, entity.IsOpened))
                return false;

            string path = entity.Path ?? string.Empty;
            if (!VisibleMechanicSelectionPolicy.IsLostShipmentEntity(path, entity.RenderName))
                return false;

            if (!TryResolveLostShipmentClickPosition(entity, path, out Vector2 clickPos))
                return false;

            candidate = new LostShipmentCandidate(entity, clickPos);
            return true;
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

        private bool TryResolveLostShipmentClickPosition(Entity entity, string path, out Vector2 clickPos)
        {
            var worldScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowArea.X, worldScreenRaw.Y + windowArea.Y);
            return TryResolveNearbyClickablePoint(worldScreenAbsolute, path, out clickPos);
        }

        private SettlersOreCandidate? ResolveNextSettlersOreCandidate()
        {
            if (!_dependencies.Settings.ClickSettlersOre.Value)
                return null;

            bool captureClickDebug = _dependencies.ShouldCaptureClickDebug();
            bool collectDiagnostics = captureClickDebug || _dependencies.Settings.DebugMode?.Value == true;

            try
            {
                SettlersOreCandidate? best = null;
                RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
                Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                Vector2 cursorAbsolute = ManualCursorSelectionMath.GetCursorAbsolutePosition();
                int scanned = 0;
                int prefiltered = 0;
                int mechanicMatched = 0;
                int probeAttempts = 0;
                int probeResolved = 0;
                int labelBacked = 0;
                long labelScanMs = 0;
                long diagnosticsStartMs = collectDiagnostics ? Environment.TickCount64 : 0;
                IReadOnlySet<long>? labelEntityAddresses = CollectGroundLabelEntityAddresses();
                if (collectDiagnostics)
                {
                    labelScanMs = Math.Max(0, Environment.TickCount64 - diagnosticsStartMs);
                    diagnosticsStartMs = Environment.TickCount64;
                }

                EntityQueryService.VisitValidEntities(_dependencies.GameController, entity =>
                {
                    if (collectDiagnostics)
                        scanned++;

                    if (VisibleMechanicSelectionPolicy.ShouldSkipSettlersOreEntity(entity.IsValid, entity.DistancePlayer, _dependencies.Settings.ClickDistance.Value))
                    {
                        if (collectDiagnostics)
                            prefiltered++;
                        return false;
                    }

                    if (!TryBuildSettlersCandidate(
                            entity,
                            windowArea,
                            labelEntityAddresses,
                            captureClickDebug,
                            out SettlersOreCandidate candidate,
                            out bool hadLabel,
                            out bool matchedMechanic,
                            out bool attemptedProbe))
                    {
                        if (collectDiagnostics && matchedMechanic)
                            mechanicMatched++;
                        if (collectDiagnostics && attemptedProbe)
                            probeAttempts++;

                        return false;
                    }

                    if (collectDiagnostics && matchedMechanic)
                        mechanicMatched++;
                    if (collectDiagnostics && attemptedProbe)
                        probeAttempts++;
                    if (collectDiagnostics)
                        probeResolved++;

                    if (collectDiagnostics && hadLabel)
                        labelBacked++;

                    if (MechanicCandidateResolver.TryPromoteSettlersCandidate(ref best, candidate, cursorAbsolute, windowTopLeft))
                    {
                        if (captureClickDebug)
                            PublishSettlersCandidateDebug("CandidateSelected", candidate, "Nearest settlers candidate selected");
                    }

                    return false;
                });

                long entityScanMs = collectDiagnostics
                    ? Math.Max(0, Environment.TickCount64 - diagnosticsStartMs)
                    : 0;

                if (!best.HasValue && collectDiagnostics)
                {
                    _dependencies.DebugLog($"[ResolveNextSettlersOreCandidate] none scanned:{scanned} prefiltered:{prefiltered} mechanicMatched:{mechanicMatched} probeAttempts:{probeAttempts} probeResolved:{probeResolved} labelBacked:{labelBacked} labelScanMs:{labelScanMs} entityScanMs:{entityScanMs}");
                    if (captureClickDebug)
                        PublishNoSettlersCandidateDebug(scanned, prefiltered, mechanicMatched, probeAttempts, probeResolved, labelBacked, labelScanMs, entityScanMs);
                }

                return best;
            }
            catch (Exception ex)
            {
                _dependencies.DebugLog($"[ResolveNextSettlersOreCandidate] Failed to scan entities: {ex.Message}");
                return null;
            }
        }

        private void PublishNoSettlersCandidateDebug(
            int scanned,
            int prefiltered,
            int mechanicMatched,
            int probeAttempts,
            int probeResolved,
            int labelBacked,
            long labelScanMs,
            long entityScanMs)
        {
            _dependencies.SetLatestClickDebug(new ClickDebugSnapshot(
                HasData: true,
                Stage: "NoCandidate",
                MechanicId: string.Empty,
                EntityPath: string.Empty,
                Distance: 0f,
                WorldScreenRaw: default,
                WorldScreenAbsolute: default,
                ResolvedClickPoint: default,
                Resolved: false,
                CenterInWindow: false,
                CenterClickable: false,
                ResolvedInWindow: false,
                ResolvedClickable: false,
                Notes: $"scanned={scanned}, prefiltered={prefiltered}, mechanicMatched={mechanicMatched}, probeAttempts={probeAttempts}, probeResolved={probeResolved}, labelBacked={labelBacked}, labelScanMs={labelScanMs}, entityScanMs={entityScanMs}",
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        }

        private bool TryBuildSettlersCandidate(
            Entity entity,
            RectangleF windowArea,
            IReadOnlySet<long>? labelEntityAddresses,
            bool captureClickDebug,
            out SettlersOreCandidate candidate,
            out bool hasGroundLabel,
            out bool matchedMechanic,
            out bool attemptedProbe)
        {
            candidate = default;
            hasGroundLabel = false;
            matchedMechanic = false;
            attemptedProbe = false;

            if (!TryResolveSettlersMechanic(entity, out string mechanicId, out string path))
                return false;

            matchedMechanic = true;

            hasGroundLabel = labelEntityAddresses != null
                && OffscreenPathingMath.IsBackedByGroundLabel(entity.Address, labelEntityAddresses);

            if (!hasGroundLabel)
                return false;

            var worldScreenRawVec = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            Vector2 worldScreenRaw = new(worldScreenRawVec.X, worldScreenRawVec.Y);
            Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowArea.X, worldScreenRaw.Y + windowArea.Y);

            attemptedProbe = true;
            if (!TryResolveNearbyClickablePoint(worldScreenAbsolute, path, out Vector2 clickPos))
            {
                if (captureClickDebug)
                    PublishSettlersProbeFailedDebug(entity, mechanicId, path, worldScreenRaw, worldScreenAbsolute);
                return false;
            }

            candidate = new SettlersOreCandidate(entity, clickPos, mechanicId, path, worldScreenRaw, worldScreenAbsolute);
            if (captureClickDebug)
                PublishSettlersProbeResolvedDebug(candidate);

            return true;
        }

        private bool TryResolveSettlersMechanic(Entity entity, out string mechanicId, out string path)
        {
            mechanicId = string.Empty;
            path = entity.Path ?? string.Empty;

            if (!MechanicClassifier.TryGetSettlersOreMechanicId(path, out string? resolvedMechanic)
                || string.IsNullOrWhiteSpace(resolvedMechanic))
            {
                return false;
            }

            if (!_dependencies.IsSettlersMechanicEnabled(resolvedMechanic)
                || VisibleMechanicSelectionPolicy.ShouldSkipSettlersOreEntity(entity.IsValid, entity.DistancePlayer, _dependencies.Settings.ClickDistance.Value))
            {
                return false;
            }

            mechanicId = resolvedMechanic;
            return true;
        }

        private void PublishSettlersProbeFailedDebug(Entity entity, string mechanicId, string path, Vector2 worldScreenRaw, Vector2 worldScreenAbsolute)
        {
            _dependencies.SetLatestClickDebug(CreateSettlersClickDebugSnapshot(
                stage: "ProbeFailed",
                mechanicId: mechanicId,
                entityPath: path,
                distance: entity.DistancePlayer,
                worldScreenRaw: worldScreenRaw,
                worldScreenAbsolute: worldScreenAbsolute,
                resolvedClickPoint: default,
                resolved: false,
                notes: "No nearby clickable point resolved"));
        }

        private void PublishSettlersProbeResolvedDebug(SettlersOreCandidate candidate)
        {
            _dependencies.SetLatestClickDebug(CreateSettlersClickDebugSnapshot(
                stage: "ProbeResolved",
                mechanicId: candidate.MechanicId,
                entityPath: candidate.EntityPath,
                distance: candidate.Distance,
                worldScreenRaw: candidate.WorldScreenRaw,
                worldScreenAbsolute: candidate.WorldScreenAbsolute,
                resolvedClickPoint: candidate.ClickPosition,
                resolved: true,
                notes: "Resolved nearby clickable point"));
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

        private bool TryResolveNearbyClickablePoint(Vector2 center, string path, out Vector2 clickPos)
            => ClickableProbeResolver.TryResolveNearbyClickablePoint(
                center,
                path,
                _dependencies.IsInsideWindowInEitherSpace,
                _dependencies.IsClickableInEitherSpace,
                out clickPos);
    }
}