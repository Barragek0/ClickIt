using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private static readonly Vector2[] NearbyClickProbeOffsets =
        [
            new Vector2(0f, 0f),
            new Vector2(12f, 0f),
            new Vector2(-12f, 0f),
            new Vector2(0f, 12f),
            new Vector2(0f, -12f),
            new Vector2(24f, 0f),
            new Vector2(-24f, 0f),
            new Vector2(0f, 24f),
            new Vector2(0f, -24f)
        ];

        internal static bool ShouldClickShrineWhenGroundItemsHidden(Entity? shrine) => shrine != null;

        internal static bool ShouldResolveShrineCandidate(bool hasStickyOffscreenTarget) => !hasStickyOffscreenTarget;

        private void TryClickShrine(Entity shrine)
        {
            var shrineScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
            Vector2 shrineClickPos = new Vector2(shrineScreenRaw.X, shrineScreenRaw.Y);
            if (!PerformLabelClick(shrineClickPos, null, gameController))
                return;

            if (IsStickyTarget(shrine))
                ClearStickyOffscreenTarget();

            shrineService.InvalidateCache();
        }

        private Entity? ResolveNextShrineCandidate()
        {
            if (!settings.ClickShrines.Value)
                return null;

            return shrineService.GetNearestShrineInRange(settings.ClickDistance.Value, pos => pointIsInClickableArea(pos, ShrineMechanicId));
        }

        private LostShipmentCandidate? ResolveNextLostShipmentCandidate(IReadOnlyList<LabelOnGround>? labelsOverride = null)
        {
            if (!settings.ClickLostShipmentCrates.Value)
                return null;

            try
            {
                LostShipmentCandidate? best = null;

                if (labelsOverride != null)
                {
                    for (int i = 0; i < labelsOverride.Count; i++)
                    {
                        LabelOnGround? label = labelsOverride[i];
                        Entity? entity = label?.ItemOnGround;
                        if (label == null || entity == null)
                            continue;
                        if (ShouldSkipLostShipmentEntity(entity.IsValid, entity.DistancePlayer, settings.ClickDistance.Value, entity.IsOpened))
                            continue;

                        string path = entity.Path ?? string.Empty;
                        if (!IsLostShipmentEntity(path, entity.RenderName))
                            continue;

                        if (!TryResolveLostShipmentClickPosition(entity, path, out Vector2 clickPos))
                            continue;

                        var candidate = new LostShipmentCandidate(entity, clickPos);
                        if (!best.HasValue || candidate.Distance < best.Value.Distance)
                            best = candidate;
                    }

                    return best;
                }

                var labels = gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels;
                if (labels == null || labels.Count == 0)
                    return null;

                for (int i = 0; i < labels.Count; i++)
                {
                    LabelOnGround? label = labels[i];
                    Entity? entity = label?.ItemOnGround;
                    if (label == null || entity == null)
                        continue;
                    if (ShouldSkipLostShipmentEntity(entity.IsValid, entity.DistancePlayer, settings.ClickDistance.Value, entity.IsOpened))
                        continue;

                    string path = entity.Path ?? string.Empty;
                    if (!IsLostShipmentEntity(path, entity.RenderName))
                        continue;

                    if (!TryResolveLostShipmentClickPosition(entity, path, out Vector2 clickPos))
                        continue;

                    var candidate = new LostShipmentCandidate(entity, clickPos);
                    if (!best.HasValue || candidate.Distance < best.Value.Distance)
                        best = candidate;
                }

                return best;
            }
            catch (Exception ex)
            {
                DebugLog(() => $"[ResolveNextLostShipmentCandidate] Failed to scan hidden labels: {ex.Message}");
                return null;
            }
        }

        private void ResolveVisibleMechanicCandidates(
            out LostShipmentCandidate? lostShipmentCandidate,
            out SettlersOreCandidate? settlersOreCandidate,
            IReadOnlyList<LabelOnGround>? labelsOverride = null)
        {
            int labelCount = labelsOverride?.Count
                ?? gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels?.Count
                ?? 0;

            long now = Environment.TickCount64;

            if (_visibleMechanicCandidateCacheHasValue
                && ShouldReuseTimedLabelCountCache(
                    now,
                    _visibleMechanicCandidateCacheTimestampMs,
                    _visibleMechanicCandidateLabelCount,
                    labelCount,
                    VisibleMechanicCandidateCacheWindowMs)
                && IsLostShipmentCandidateUsable(_visibleMechanicCachedLostShipmentCandidate)
                && IsSettlersCandidateUsable(_visibleMechanicCachedSettlersCandidate))
            {
                lostShipmentCandidate = _visibleMechanicCachedLostShipmentCandidate;
                settlersOreCandidate = _visibleMechanicCachedSettlersCandidate;
                return;
            }

            lostShipmentCandidate = ResolveNextLostShipmentCandidate(labelsOverride);
            settlersOreCandidate = ResolveNextSettlersOreCandidate();

            _visibleMechanicCachedLostShipmentCandidate = lostShipmentCandidate;
            _visibleMechanicCachedSettlersCandidate = settlersOreCandidate;
            _visibleMechanicCandidateCacheTimestampMs = now;
            _visibleMechanicCandidateLabelCount = labelCount;
            _visibleMechanicCandidateCacheHasValue = true;
        }

        private bool IsLostShipmentCandidateUsable(LostShipmentCandidate? candidate)
        {
            if (!candidate.HasValue)
                return true;

            Entity entity = candidate.Value.Entity;
            return entity != null
                && entity.IsValid
                && !entity.IsOpened
                && entity.DistancePlayer <= settings.ClickDistance.Value;
        }

        private bool IsSettlersCandidateUsable(SettlersOreCandidate? candidate)
        {
            if (!candidate.HasValue)
                return true;

            Entity entity = candidate.Value.Entity;
            return entity != null
                && entity.IsValid
                && entity.DistancePlayer <= settings.ClickDistance.Value;
        }

        private bool TryResolveLostShipmentClickPosition(Entity entity, string path, out Vector2 clickPos)
        {
            var worldScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 worldScreenAbsolute = new Vector2(worldScreenRaw.X + windowArea.X, worldScreenRaw.Y + windowArea.Y);
            return TryResolveNearbyClickablePoint(worldScreenAbsolute, path, out clickPos);
        }

        private void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
        {
            int labelCount = gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels?.Count ?? 0;
            long now = Environment.TickCount64;

            if (_hiddenFallbackCandidateCacheHasValue
                && ShouldReuseTimedLabelCountCache(
                    now,
                    _hiddenFallbackCandidateCacheTimestampMs,
                    _hiddenFallbackCandidateLabelCount,
                    labelCount,
                    HiddenFallbackCandidateCacheWindowMs))
            {
                lostShipmentCandidate = _hiddenFallbackCachedLostShipmentCandidate;
                settlersOreCandidate = _hiddenFallbackCachedSettlersCandidate;
                return;
            }

            lostShipmentCandidate = ResolveNextLostShipmentCandidate();
            settlersOreCandidate = ResolveNextSettlersOreCandidate();

            _hiddenFallbackCachedLostShipmentCandidate = lostShipmentCandidate;
            _hiddenFallbackCachedSettlersCandidate = settlersOreCandidate;
            _hiddenFallbackCandidateCacheTimestampMs = now;
            _hiddenFallbackCandidateLabelCount = labelCount;
            _hiddenFallbackCandidateCacheHasValue = true;
        }

        internal static bool ShouldReuseTimedLabelCountCache(long now, long cachedAtMs, int cachedLabelCount, int currentLabelCount, int cacheWindowMs)
        {
            if (cachedAtMs <= 0 || cacheWindowMs <= 0)
                return false;

            if (cachedLabelCount != currentLabelCount)
                return false;

            long age = now - cachedAtMs;
            return age >= 0 && age <= cacheWindowMs;
        }

        private SettlersOreCandidate? ResolveNextSettlersOreCandidate()
        {
            if (!settings.ClickSettlersOre.Value || gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return null;

            bool captureClickDebug = ShouldCaptureClickDebug();
            bool collectDiagnostics = captureClickDebug || settings.DebugMode?.Value == true;

            try
            {
                SettlersOreCandidate? best = null;
                int scanned = 0;
                int prefiltered = 0;
                int mechanicMatched = 0;
                int probeAttempts = 0;
                int probeResolved = 0;
                int labelBacked = 0;
                long labelScanMs = 0;
                long diagnosticsStartMs = collectDiagnostics ? Environment.TickCount64 : 0;
                IReadOnlySet<long>? labelEntityAddresses = ShouldScanSettlersGroundLabelAddresses(captureClickDebug)
                    ? CollectGroundLabelEntityAddresses()
                    : null;
                if (collectDiagnostics)
                {
                    labelScanMs = Math.Max(0, Environment.TickCount64 - diagnosticsStartMs);
                    diagnosticsStartMs = Environment.TickCount64;
                }

                foreach (var kv in gameController.EntityListWrapper.ValidEntitiesByType)
                {
                    var entities = kv.Value;
                    if (entities == null)
                        continue;

                    for (int i = 0; i < entities.Count; i++)
                    {
                        Entity entity = entities[i];
                        if (entity == null)
                            continue;

                        if (collectDiagnostics)
                            scanned++;

                        if (ShouldSkipSettlersEntityBeforeMechanicResolution(entity.IsValid, entity.IsHidden, entity.DistancePlayer, settings.ClickDistance.Value))
                        {
                            if (collectDiagnostics)
                                prefiltered++;
                            continue;
                        }

                        if (!TryBuildSettlersCandidate(
                                entity,
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

                            continue;
                        }

                        if (collectDiagnostics && matchedMechanic)
                            mechanicMatched++;
                        if (collectDiagnostics && attemptedProbe)
                            probeAttempts++;
                        if (collectDiagnostics)
                            probeResolved++;

                        if (collectDiagnostics && hadLabel)
                            labelBacked++;

                        if (!best.HasValue || candidate.Distance < best.Value.Distance)
                        {
                            best = candidate;
                            if (captureClickDebug)
                                PublishSettlersCandidateDebug("CandidateSelected", candidate, "Nearest settlers candidate selected");
                        }
                    }
                }

                long entityScanMs = collectDiagnostics
                    ? Math.Max(0, Environment.TickCount64 - diagnosticsStartMs)
                    : 0;

                if (!best.HasValue && collectDiagnostics)
                {
                    DebugLog(() => $"[ResolveNextSettlersOreCandidate] none scanned:{scanned} prefiltered:{prefiltered} mechanicMatched:{mechanicMatched} probeAttempts:{probeAttempts} probeResolved:{probeResolved} labelBacked:{labelBacked} labelScanMs:{labelScanMs} entityScanMs:{entityScanMs}");
                    if (captureClickDebug)
                        PublishNoSettlersCandidateDebug(scanned, prefiltered, mechanicMatched, probeAttempts, probeResolved, labelBacked, labelScanMs, entityScanMs);
                }

                return best;
            }
            catch (Exception ex)
            {
                DebugLog(() => $"[ResolveNextSettlersOreCandidate] Failed to scan entities: {ex.Message}");
                return null;
            }
        }

        internal static bool ShouldScanSettlersGroundLabelAddresses(bool captureClickDebug)
            => captureClickDebug;

        internal static bool ShouldSkipSettlersEntityBeforeMechanicResolution(bool isValid, bool isHidden, float distance, int clickDistance)
            => ShouldSkipSettlersOreEntity(isValid, distance, clickDistance);

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
            SetLatestClickDebug(new ClickDebugSnapshot(
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
                && IsBackedByGroundLabel(entity.Address, labelEntityAddresses);

            var worldScreenRawVec = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            Vector2 worldScreenRaw = new Vector2(worldScreenRawVec.X, worldScreenRawVec.Y);
            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 worldScreenAbsolute = new Vector2(worldScreenRaw.X + windowArea.X, worldScreenRaw.Y + windowArea.Y);

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

            if (!LabelFilterService.TryGetSettlersOreMechanicId(path, out string? resolvedMechanic)
                || string.IsNullOrWhiteSpace(resolvedMechanic))
            {
                return false;
            }

            if (!IsSettlersMechanicEnabled(resolvedMechanic)
                || ShouldSkipSettlersOreEntity(entity.IsValid, entity.DistancePlayer, settings.ClickDistance.Value))
            {
                return false;
            }

            mechanicId = resolvedMechanic;
            return true;
        }

        private void PublishSettlersProbeFailedDebug(Entity entity, string mechanicId, string path, Vector2 worldScreenRaw, Vector2 worldScreenAbsolute)
        {
            SetLatestClickDebug(CreateSettlersClickDebugSnapshot(
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
            SetLatestClickDebug(CreateSettlersClickDebugSnapshot(
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
            SetLatestClickDebug(CreateSettlersClickDebugSnapshot(
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
                CenterInWindow: IsInsideWindowInEitherSpace(worldScreenAbsolute),
                CenterClickable: IsClickableInEitherSpace(worldScreenAbsolute, entityPath),
                ResolvedInWindow: resolved && IsInsideWindowInEitherSpace(resolvedClickPoint),
                ResolvedClickable: resolved && IsClickableInEitherSpace(resolvedClickPoint, entityPath),
                Notes: notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64);
        }

        private bool TryResolveNearbyClickablePoint(Vector2 center, string path, out Vector2 clickPos)
        {
            for (int i = 0; i < NearbyClickProbeOffsets.Length; i++)
            {
                Vector2 probe = center + NearbyClickProbeOffsets[i];
                if (!IsInsideWindowInEitherSpace(probe))
                    continue;
                if (!IsClickableInEitherSpace(probe, path))
                    continue;

                clickPos = probe;
                return true;
            }

            clickPos = default;
            return false;
        }

        private void TryClickLostShipment(LostShipmentCandidate candidate)
        {
            DebugLog(() => "[ProcessRegularClick] Clicking Lost Shipment candidate via ItemOnGround position.");
            if (!PerformLabelClick(candidate.ClickPosition, null, gameController))
                return;

            HandleSuccessfulMechanicEntityClick(candidate.Entity);
        }

        private bool TryClickSettlersOre(SettlersOreCandidate candidate)
        {
            Entity entity = candidate.Entity;
            if (entity != null && !entity.IsTargetable)
            {
                DebugLog(() => $"[ProcessRegularClick] Skipping settlers ore candidate ({candidate.MechanicId}) because entity is not targetable.");
                return false;
            }

            DebugLog(() => $"[ProcessRegularClick] Clicking settlers ore candidate ({candidate.MechanicId}) via entity position.");

            bool captureClickDebug = ShouldCaptureClickDebug();
            if (captureClickDebug)
                PublishSettlersCandidateDebug("ClickAttempt", candidate, "Attempting settlers click");

            bool clicked = ShouldUseHoldClickForSettlersMechanic(candidate.MechanicId)
                ? PerformLabelHoldClick(candidate.ClickPosition, null, gameController, holdDurationMs: 0)
                : PerformLabelClick(candidate.ClickPosition, null, gameController);

            if (!clicked)
                return false;

            if (captureClickDebug)
                PublishSettlersCandidateDebug("ClickSuccess", candidate, "Settlers click completed");

            HandleSuccessfulMechanicEntityClick(entity);

            return true;
        }

        private void HandleSuccessfulMechanicEntityClick(Entity? entity)
        {
            if (entity == null)
                return;

            if (IsStickyTarget(entity))
                ClearStickyOffscreenTarget();

            if (settings.WalkTowardOffscreenLabels.Value)
                pathfindingService.ClearLatestPath();
        }
    }
}