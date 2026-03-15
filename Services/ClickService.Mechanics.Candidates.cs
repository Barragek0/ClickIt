using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        internal static bool ShouldClickShrineWhenGroundItemsHidden(Entity? shrine)
        {
            return shrine != null;
        }

        internal static bool ShouldResolveShrineCandidate(bool hasStickyOffscreenTarget)
        {
            return !hasStickyOffscreenTarget;
        }

        private void TryClickShrine(Entity shrine)
        {
            var shrineScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
            Vector2 shrineClickPos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
            bool shrineClicked = PerformLabelClick(shrineClickPos, null, gameController);
            if (shrineClicked)
            {
                if (IsStickyTarget(shrine))
                {
                    ClearStickyOffscreenTarget();
                }

                shrineService.InvalidateCache();
            }
        }

        private Entity? ResolveNextShrineCandidate()
        {
            if (!settings.ClickShrines.Value)
                return null;

            return shrineService.GetNearestShrineInRange(settings.ClickDistance.Value, pos => pointIsInClickableArea(pos, ShrineMechanicId));
        }

        private LostShipmentCandidate? ResolveNextLostShipmentCandidate()
        {
            if (!settings.ClickLostShipmentCrates.Value)
                return null;

            try
            {
                var allLabels = gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels;
                if (allLabels == null || allLabels.Count == 0)
                    return null;

                LostShipmentCandidate? best = null;
                for (int i = 0; i < allLabels.Count; i++)
                {
                    LabelOnGround? label = allLabels[i];
                    Entity? entity = label?.ItemOnGround;
                    if (label == null || entity == null)
                        continue;
                    if (ShouldSkipLostShipmentEntity(entity.IsValid, entity.DistancePlayer, settings.ClickDistance.Value, entity.IsOpened))
                        continue;

                    string path = entity.Path ?? string.Empty;
                    string renderName = entity.RenderName ?? string.Empty;
                    if (!IsLostShipmentEntity(path, renderName))
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

        private bool TryResolveLostShipmentClickPosition(Entity entity, string path, out Vector2 clickPos)
        {
            clickPos = default;

            var worldScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 worldScreen = new(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);

            return TryResolveNearbyClickablePoint(worldScreen, path, out clickPos);
        }

        private SettlersOreCandidate? ResolveNextSettlersOreCandidate()
        {
            if (!settings.ClickSettlersOre.Value || gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return null;

            bool captureClickDebug = ShouldCaptureClickDebug();

            try
            {
                SettlersOreCandidate? best = null;
                int scanned = 0;
                int matchedPath = 0;
                int labelBacked = 0;
                var labelEntityAddresses = CollectGroundLabelEntityAddresses();

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

                        scanned++;
                        string path = entity.Path ?? string.Empty;
                        if (!LabelFilterService.TryGetSettlersOreMechanicId(path, out string? settlersMechanicId) || string.IsNullOrWhiteSpace(settlersMechanicId))
                            continue;
                        if (!IsSettlersMechanicEnabled(settlersMechanicId))
                            continue;

                        matchedPath++;
                        if (ShouldSkipSettlersOreEntity(entity.IsValid, entity.DistancePlayer, settings.ClickDistance.Value))
                            continue;

                        bool hasGroundLabel = IsBackedByGroundLabel(entity.Address, labelEntityAddresses);
                        bool isVerisiumPath = IsVerisiumPath(path);
                        if (!ShouldAllowSettlersCandidateWithoutGroundLabel(hasGroundLabel, isVerisiumPath))
                            continue;

                        if (hasGroundLabel)
                            labelBacked++;

                        var worldScreenRawVec = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
                        Vector2 worldScreenRaw = new(worldScreenRawVec.X, worldScreenRawVec.Y);
                        RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
                        Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                        Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);

                        bool centerInWindow = IsInsideWindowInEitherSpace(worldScreenAbsolute);
                        bool centerClickable = IsClickableInEitherSpace(worldScreenAbsolute, path);

                        if (!TryResolveNearbyClickablePoint(worldScreenAbsolute, path, out Vector2 clickPos))
                        {
                            if (captureClickDebug)
                            {
                                SetLatestClickDebug(new ClickDebugSnapshot(
                                    HasData: true,
                                    Stage: "ProbeFailed",
                                    MechanicId: settlersMechanicId,
                                    EntityPath: path,
                                    Distance: entity.DistancePlayer,
                                    WorldScreenRaw: worldScreenRaw,
                                    WorldScreenAbsolute: worldScreenAbsolute,
                                    ResolvedClickPoint: default,
                                    Resolved: false,
                                    CenterInWindow: centerInWindow,
                                    CenterClickable: centerClickable,
                                    ResolvedInWindow: false,
                                    ResolvedClickable: false,
                                    Notes: "No nearby clickable point resolved",
                                    Sequence: 0,
                                    TimestampMs: Environment.TickCount64));
                            }
                            continue;
                        }

                        bool resolvedInWindow = IsInsideWindowInEitherSpace(clickPos);
                        bool resolvedClickable = IsClickableInEitherSpace(clickPos, path);

                        if (captureClickDebug)
                        {
                            SetLatestClickDebug(new ClickDebugSnapshot(
                                HasData: true,
                                Stage: "ProbeResolved",
                                MechanicId: settlersMechanicId,
                                EntityPath: path,
                                Distance: entity.DistancePlayer,
                                WorldScreenRaw: worldScreenRaw,
                                WorldScreenAbsolute: worldScreenAbsolute,
                                ResolvedClickPoint: clickPos,
                                Resolved: true,
                                CenterInWindow: centerInWindow,
                                CenterClickable: centerClickable,
                                ResolvedInWindow: resolvedInWindow,
                                ResolvedClickable: resolvedClickable,
                                Notes: "Resolved nearby clickable point",
                                Sequence: 0,
                                TimestampMs: Environment.TickCount64));
                        }

                        var candidate = new SettlersOreCandidate(entity, clickPos, settlersMechanicId, path, worldScreenRaw, worldScreenAbsolute);
                        if (!best.HasValue || candidate.Distance < best.Value.Distance)
                        {
                            best = candidate;
                            if (captureClickDebug)
                            {
                                SetLatestClickDebug(new ClickDebugSnapshot(
                                    HasData: true,
                                    Stage: "CandidateSelected",
                                    MechanicId: settlersMechanicId,
                                    EntityPath: path,
                                    Distance: candidate.Distance,
                                    WorldScreenRaw: worldScreenRaw,
                                    WorldScreenAbsolute: worldScreenAbsolute,
                                    ResolvedClickPoint: clickPos,
                                    Resolved: true,
                                    CenterInWindow: centerInWindow,
                                    CenterClickable: centerClickable,
                                    ResolvedInWindow: resolvedInWindow,
                                    ResolvedClickable: resolvedClickable,
                                    Notes: "Nearest settlers candidate selected",
                                    Sequence: 0,
                                    TimestampMs: Environment.TickCount64));
                            }
                        }
                    }
                }

                if (!best.HasValue)
                {
                    DebugLog(() => $"[ResolveNextSettlersOreCandidate] none scanned:{scanned} matched:{matchedPath} labelBacked:{labelBacked}");
                    if (captureClickDebug)
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
                            Notes: $"scanned={scanned}, matchedPath={matchedPath}, labelBacked={labelBacked}",
                            Sequence: 0,
                            TimestampMs: Environment.TickCount64));
                    }
                }

                return best;
            }
            catch (Exception ex)
            {
                DebugLog(() => $"[ResolveNextSettlersOreCandidate] Failed to scan entities: {ex.Message}");
                return null;
            }
        }

        private bool TryResolveNearbyClickablePoint(Vector2 center, string path, out Vector2 clickPos)
        {
            clickPos = default;

            ReadOnlySpan<Vector2> offsets =
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

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 candidate = center + offsets[i];
                if (!IsInsideWindowInEitherSpace(candidate))
                    continue;
                if (!IsClickableInEitherSpace(candidate, path))
                    continue;

                clickPos = candidate;
                return true;
            }

            return false;
        }

        private void TryClickLostShipment(LostShipmentCandidate candidate)
        {
            DebugLog(() => "[ProcessRegularClick] Clicking Lost Shipment candidate via ItemOnGround position.");

            if (!PerformLabelClick(candidate.ClickPosition, null, gameController))
                return;

            if (IsStickyTarget(candidate.Entity))
            {
                ClearStickyOffscreenTarget();
            }

            if (settings.WalkTowardOffscreenLabels.Value)
            {
                pathfindingService.ClearLatestPath();
            }
        }

        private void TryClickSettlersOre(SettlersOreCandidate candidate)
        {
            DebugLog(() => $"[ProcessRegularClick] Clicking settlers ore candidate ({candidate.MechanicId}) via entity position.");

            bool captureClickDebug = ShouldCaptureClickDebug();

            if (captureClickDebug)
            {
                SetLatestClickDebug(new ClickDebugSnapshot(
                    HasData: true,
                    Stage: "ClickAttempt",
                    MechanicId: candidate.MechanicId,
                    EntityPath: candidate.EntityPath,
                    Distance: candidate.Distance,
                    WorldScreenRaw: candidate.WorldScreenRaw,
                    WorldScreenAbsolute: candidate.WorldScreenAbsolute,
                    ResolvedClickPoint: candidate.ClickPosition,
                    Resolved: true,
                    CenterInWindow: IsInsideWindowInEitherSpace(candidate.WorldScreenAbsolute),
                    CenterClickable: IsClickableInEitherSpace(candidate.WorldScreenAbsolute, candidate.EntityPath),
                    ResolvedInWindow: IsInsideWindowInEitherSpace(candidate.ClickPosition),
                    ResolvedClickable: IsClickableInEitherSpace(candidate.ClickPosition, candidate.EntityPath),
                    Notes: "Attempting settlers click",
                    Sequence: 0,
                    TimestampMs: Environment.TickCount64));
            }

            bool clicked = ShouldUseHoldClickForSettlersMechanic(candidate.MechanicId)
                ? PerformLabelHoldClick(candidate.ClickPosition, null, gameController, holdDurationMs: 0)
                : PerformLabelClick(candidate.ClickPosition, null, gameController);

            if (!clicked)
                return;

            if (captureClickDebug)
            {
                SetLatestClickDebug(new ClickDebugSnapshot(
                    HasData: true,
                    Stage: "ClickSuccess",
                    MechanicId: candidate.MechanicId,
                    EntityPath: candidate.EntityPath,
                    Distance: candidate.Distance,
                    WorldScreenRaw: candidate.WorldScreenRaw,
                    WorldScreenAbsolute: candidate.WorldScreenAbsolute,
                    ResolvedClickPoint: candidate.ClickPosition,
                    Resolved: true,
                    CenterInWindow: IsInsideWindowInEitherSpace(candidate.WorldScreenAbsolute),
                    CenterClickable: IsClickableInEitherSpace(candidate.WorldScreenAbsolute, candidate.EntityPath),
                    ResolvedInWindow: IsInsideWindowInEitherSpace(candidate.ClickPosition),
                    ResolvedClickable: IsClickableInEitherSpace(candidate.ClickPosition, candidate.EntityPath),
                    Notes: "Settlers click completed",
                    Sequence: 0,
                    TimestampMs: Environment.TickCount64));
            }

            if (IsStickyTarget(candidate.Entity))
                ClearStickyOffscreenTarget();

            if (settings.WalkTowardOffscreenLabels.Value)
                pathfindingService.ClearLatestPath();
        }
    }
}