using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private sealed class VisibleMechanicCoordinator(ClickService owner)
        {
            public Entity? ResolveNextShrineCandidate()
            {
                if (!owner.settings.ClickShrines.Value)
                    return null;

                RectangleF windowArea = owner.gameController.Window.GetWindowRectangleTimeCache;
                Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                Vector2 cursorAbsolute = GetCursorAbsolutePosition();
                return owner.shrineService.GetNearestShrineInRange(
                    owner.settings.ClickDistance.Value,
                    isInClickableArea: pos => owner.pointIsInClickableArea(pos, ShrineMechanicId),
                    cursorDistanceResolver: shrine =>
                    {
                        var screenRaw = owner.gameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
                        Vector2 shrineScreenAbsolute = new(screenRaw.X + windowTopLeft.X, screenRaw.Y + windowTopLeft.Y);
                        return GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, shrineScreenAbsolute, windowTopLeft);
                    });
            }

            public void ResolveVisibleMechanicCandidates(
                out LostShipmentCandidate? lostShipmentCandidate,
                out SettlersOreCandidate? settlersOreCandidate,
                IReadOnlyList<LabelOnGround>? labelsOverride = null)
            {
                int labelCount = labelsOverride?.Count
                    ?? owner.gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels?.Count
                    ?? 0;

                long now = Environment.TickCount64;

                if (owner._visibleMechanicCandidateCacheHasValue
                    && ShouldReuseTimedLabelCountCache(
                        now,
                        owner._visibleMechanicCandidateCacheTimestampMs,
                        owner._visibleMechanicCandidateLabelCount,
                        labelCount,
                        VisibleMechanicCandidateCacheWindowMs)
                    && IsLostShipmentCandidateUsable(owner._visibleMechanicCachedLostShipmentCandidate)
                    && IsSettlersCandidateUsable(owner._visibleMechanicCachedSettlersCandidate))
                {
                    lostShipmentCandidate = owner._visibleMechanicCachedLostShipmentCandidate;
                    settlersOreCandidate = owner._visibleMechanicCachedSettlersCandidate;
                    return;
                }

                lostShipmentCandidate = ResolveNextLostShipmentCandidate(labelsOverride);
                settlersOreCandidate = ResolveNextSettlersOreCandidate();

                owner._visibleMechanicCachedLostShipmentCandidate = lostShipmentCandidate;
                owner._visibleMechanicCachedSettlersCandidate = settlersOreCandidate;
                owner._visibleMechanicCandidateCacheTimestampMs = now;
                owner._visibleMechanicCandidateLabelCount = labelCount;
                owner._visibleMechanicCandidateCacheHasValue = true;
            }

            public void ResolveHiddenFallbackCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate)
            {
                int labelCount = owner.gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels?.Count ?? 0;
                long now = Environment.TickCount64;

                if (owner._hiddenFallbackCandidateCacheHasValue
                    && ShouldReuseTimedLabelCountCache(
                        now,
                        owner._hiddenFallbackCandidateCacheTimestampMs,
                        owner._hiddenFallbackCandidateLabelCount,
                        labelCount,
                        HiddenFallbackCandidateCacheWindowMs))
                {
                    lostShipmentCandidate = owner._hiddenFallbackCachedLostShipmentCandidate;
                    settlersOreCandidate = owner._hiddenFallbackCachedSettlersCandidate;
                    return;
                }

                lostShipmentCandidate = ResolveNextLostShipmentCandidate();
                settlersOreCandidate = ResolveNextSettlersOreCandidate();

                owner._hiddenFallbackCachedLostShipmentCandidate = lostShipmentCandidate;
                owner._hiddenFallbackCachedSettlersCandidate = settlersOreCandidate;
                owner._hiddenFallbackCandidateCacheTimestampMs = now;
                owner._hiddenFallbackCandidateLabelCount = labelCount;
                owner._hiddenFallbackCandidateCacheHasValue = true;
            }

            public void TryClickShrine(Entity shrine)
            {
                var shrineScreenRaw = owner.gameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
                Vector2 shrineClickPos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
                if (!owner.PerformLabelClick(shrineClickPos, null, owner.gameController))
                    return;

                if (owner.OffscreenPathing.IsStickyTarget(shrine))
                    owner.OffscreenPathing.ClearStickyOffscreenTarget();

                owner.shrineService.InvalidateCache();
            }

            public void TryClickLostShipment(LostShipmentCandidate candidate)
            {
                owner.DebugLog(() => "[ProcessRegularClick] Clicking Lost Shipment candidate via ItemOnGround position.");
                if (!owner.PerformLabelClick(candidate.ClickPosition, null, owner.gameController))
                    return;

                HandleSuccessfulMechanicEntityClick(candidate.Entity);
            }

            public bool TryClickSettlersOre(SettlersOreCandidate candidate)
            {
                Entity entity = candidate.Entity;
                if (entity != null && !entity.IsTargetable)
                {
                    owner.DebugLog(() => $"[ProcessRegularClick] Skipping settlers ore candidate ({candidate.MechanicId}) because entity is not targetable.");
                    return false;
                }

                owner.DebugLog(() => $"[ProcessRegularClick] Clicking settlers ore candidate ({candidate.MechanicId}) via entity position.");

                bool captureClickDebug = owner.ShouldCaptureClickDebug();
                if (captureClickDebug)
                    PublishSettlersCandidateDebug("ClickAttempt", candidate, "Attempting settlers click");

                bool clicked = ShouldUseHoldClickForSettlersMechanic(candidate.MechanicId)
                    ? owner.PerformLabelHoldClick(candidate.ClickPosition, null, owner.gameController, holdDurationMs: 0)
                    : owner.PerformLabelClick(candidate.ClickPosition, null, owner.gameController);

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

                if (owner.OffscreenPathing.IsStickyTarget(entity))
                    owner.OffscreenPathing.ClearStickyOffscreenTarget();

                if (owner.settings.WalkTowardOffscreenLabels.Value)
                    owner.pathfindingService.ClearLatestPath();
            }

            private LostShipmentCandidate? ResolveNextLostShipmentCandidate(IReadOnlyList<LabelOnGround>? labelsOverride = null)
            {
                if (!owner.settings.ClickLostShipmentCrates.Value)
                    return null;

                try
                {
                    LostShipmentCandidate? best = null;
                    RectangleF windowArea = owner.gameController.Window.GetWindowRectangleTimeCache;
                    Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                    Vector2 cursorAbsolute = GetCursorAbsolutePosition();

                    if (labelsOverride != null)
                    {
                        ScanLostShipmentLabelsForBestCandidate(labelsOverride, ref best, cursorAbsolute, windowTopLeft);
                        return best;
                    }

                    var labels = owner.gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels;
                    if (labels == null || labels.Count == 0)
                        return null;

                    ScanLostShipmentLabelsForBestCandidate(labels, ref best, cursorAbsolute, windowTopLeft);

                    return best;
                }
                catch (Exception ex)
                {
                    owner.DebugLog(() => $"[ResolveNextLostShipmentCandidate] Failed to scan hidden labels: {ex.Message}");
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

                    if (!best.HasValue
                        || candidate.Distance < best.Value.Distance
                        || (ArePlayerDistancesEquivalent(candidate.Distance, best.Value.Distance)
                            && IsFirstCandidateCloserToCursor(candidate.ClickPosition, best.Value.ClickPosition, cursorAbsolute, windowTopLeft)))
                    {
                        best = candidate;
                    }
                }
            }

            private bool TryCreateLostShipmentCandidate(LabelOnGround? label, out LostShipmentCandidate candidate)
            {
                candidate = default;

                Entity? entity = label?.ItemOnGround;
                if (label == null || entity == null)
                    return false;

                if (ShouldSkipLostShipmentEntity(entity.IsValid, entity.DistancePlayer, owner.settings.ClickDistance.Value, entity.IsOpened))
                    return false;

                string path = entity.Path ?? string.Empty;
                if (!IsLostShipmentEntity(path, entity.RenderName))
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
                    && entity.DistancePlayer <= owner.settings.ClickDistance.Value;
            }

            private bool IsSettlersCandidateUsable(SettlersOreCandidate? candidate)
            {
                if (!candidate.HasValue)
                    return true;

                Entity entity = candidate.Value.Entity;
                return entity != null
                    && entity.IsValid
                    && entity.DistancePlayer <= owner.settings.ClickDistance.Value;
            }

            private bool TryResolveLostShipmentClickPosition(Entity entity, string path, out Vector2 clickPos)
            {
                var worldScreenRaw = owner.gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
                RectangleF windowArea = owner.gameController.Window.GetWindowRectangleTimeCache;
                Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowArea.X, worldScreenRaw.Y + windowArea.Y);
                return TryResolveNearbyClickablePoint(worldScreenAbsolute, path, out clickPos);
            }

            private SettlersOreCandidate? ResolveNextSettlersOreCandidate()
            {
                if (!owner.settings.ClickSettlersOre.Value || owner.gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                    return null;

                bool captureClickDebug = owner.ShouldCaptureClickDebug();
                bool collectDiagnostics = captureClickDebug || owner.settings.DebugMode?.Value == true;

                try
                {
                    SettlersOreCandidate? best = null;
                    RectangleF windowArea = owner.gameController.Window.GetWindowRectangleTimeCache;
                    Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                    Vector2 cursorAbsolute = GetCursorAbsolutePosition();
                    int scanned = 0;
                    int prefiltered = 0;
                    int mechanicMatched = 0;
                    int probeAttempts = 0;
                    int probeResolved = 0;
                    int labelBacked = 0;
                    long labelScanMs = 0;
                    long diagnosticsStartMs = collectDiagnostics ? Environment.TickCount64 : 0;
                    IReadOnlySet<long>? labelEntityAddresses = ShouldScanSettlersGroundLabelAddresses(captureClickDebug)
                        ? owner.CollectGroundLabelEntityAddresses()
                        : null;
                    if (collectDiagnostics)
                    {
                        labelScanMs = Math.Max(0, Environment.TickCount64 - diagnosticsStartMs);
                        diagnosticsStartMs = Environment.TickCount64;
                    }

                    foreach (var kv in owner.gameController.EntityListWrapper.ValidEntitiesByType)
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

                            if (ShouldSkipSettlersEntityBeforeMechanicResolution(entity.IsValid, entity.IsHidden, entity.DistancePlayer, owner.settings.ClickDistance.Value))
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

                            if (!best.HasValue
                                || candidate.Distance < best.Value.Distance
                                || (ArePlayerDistancesEquivalent(candidate.Distance, best.Value.Distance)
                                    && IsFirstCandidateCloserToCursor(candidate.ClickPosition, best.Value.ClickPosition, cursorAbsolute, windowTopLeft)))
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
                        owner.DebugLog(() => $"[ResolveNextSettlersOreCandidate] none scanned:{scanned} prefiltered:{prefiltered} mechanicMatched:{mechanicMatched} probeAttempts:{probeAttempts} probeResolved:{probeResolved} labelBacked:{labelBacked} labelScanMs:{labelScanMs} entityScanMs:{entityScanMs}");
                        if (captureClickDebug)
                            PublishNoSettlersCandidateDebug(scanned, prefiltered, mechanicMatched, probeAttempts, probeResolved, labelBacked, labelScanMs, entityScanMs);
                    }

                    return best;
                }
                catch (Exception ex)
                {
                    owner.DebugLog(() => $"[ResolveNextSettlersOreCandidate] Failed to scan entities: {ex.Message}");
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
                owner.SetLatestClickDebug(new ClickDebugSnapshot(
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

                if (!ShouldAcceptSettlersCandidate(hasGroundLabel))
                    return false;

                var worldScreenRawVec = owner.gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
                Vector2 worldScreenRaw = new(worldScreenRawVec.X, worldScreenRawVec.Y);
                RectangleF windowArea = owner.gameController.Window.GetWindowRectangleTimeCache;
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

                if (!LabelFilterService.TryGetSettlersOreMechanicId(path, out string? resolvedMechanic)
                    || string.IsNullOrWhiteSpace(resolvedMechanic))
                {
                    return false;
                }

                if (!owner.IsSettlersMechanicEnabled(resolvedMechanic)
                    || ShouldSkipSettlersOreEntity(entity.IsValid, entity.DistancePlayer, owner.settings.ClickDistance.Value))
                {
                    return false;
                }

                mechanicId = resolvedMechanic;
                return true;
            }

            private void PublishSettlersProbeFailedDebug(Entity entity, string mechanicId, string path, Vector2 worldScreenRaw, Vector2 worldScreenAbsolute)
            {
                owner.SetLatestClickDebug(CreateSettlersClickDebugSnapshot(
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
                owner.SetLatestClickDebug(CreateSettlersClickDebugSnapshot(
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
                owner.SetLatestClickDebug(CreateSettlersClickDebugSnapshot(
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
                    CenterInWindow: owner.IsInsideWindowInEitherSpace(worldScreenAbsolute),
                    CenterClickable: owner.IsClickableInEitherSpace(worldScreenAbsolute, entityPath),
                    ResolvedInWindow: resolved && owner.IsInsideWindowInEitherSpace(resolvedClickPoint),
                    ResolvedClickable: resolved && owner.IsClickableInEitherSpace(resolvedClickPoint, entityPath),
                    Notes: notes,
                    Sequence: 0,
                    TimestampMs: Environment.TickCount64);
            }

            private bool TryResolveNearbyClickablePoint(Vector2 center, string path, out Vector2 clickPos)
            {
                for (int i = 0; i < NearbyClickProbeOffsets.Length; i++)
                {
                    Vector2 probe = center + NearbyClickProbeOffsets[i];
                    if (!owner.IsInsideWindowInEitherSpace(probe))
                        continue;
                    if (!owner.IsClickableInEitherSpace(probe, path))
                        continue;

                    clickPos = probe;
                    return true;
                }

                clickPos = default;
                return false;
            }
        }
    }
}