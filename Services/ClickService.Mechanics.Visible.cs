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
            Vector2 shrineClickPos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
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

        private LostShipmentCandidate? ResolveNextLostShipmentCandidate()
        {
            if (!settings.ClickLostShipmentCrates.Value)
                return null;

            try
            {
                var labels = gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels;
                if (labels == null || labels.Count == 0)
                    return null;

                LostShipmentCandidate? best = null;
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

        private bool TryResolveLostShipmentClickPosition(Entity entity, string path, out Vector2 clickPos)
        {
            var worldScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowArea.X, worldScreenRaw.Y + windowArea.Y);
            return TryResolveNearbyClickablePoint(worldScreenAbsolute, path, out clickPos);
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

                        if (!TryBuildSettlersCandidate(entity, labelEntityAddresses, captureClickDebug, out SettlersOreCandidate candidate, out bool hadLabel))
                            continue;

                        matchedPath++;
                        if (hadLabel)
                            labelBacked++;

                        if (!best.HasValue || candidate.Distance < best.Value.Distance)
                        {
                            best = candidate;
                            if (captureClickDebug)
                                PublishSettlersCandidateDebug("CandidateSelected", candidate, "Nearest settlers candidate selected");
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

        private bool TryBuildSettlersCandidate(
            Entity entity,
            IReadOnlySet<long> labelEntityAddresses,
            bool captureClickDebug,
            out SettlersOreCandidate candidate,
            out bool hasGroundLabel)
        {
            candidate = default;
            hasGroundLabel = false;

            string path = entity.Path ?? string.Empty;
            if (!LabelFilterService.TryGetSettlersOreMechanicId(path, out string? mechanicId)
                || string.IsNullOrWhiteSpace(mechanicId)
                || !IsSettlersMechanicEnabled(mechanicId)
                || ShouldSkipSettlersOreEntity(entity.IsValid, entity.DistancePlayer, settings.ClickDistance.Value))
            {
                return false;
            }

            hasGroundLabel = IsBackedByGroundLabel(entity.Address, labelEntityAddresses);

            var worldScreenRawVec = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            Vector2 worldScreenRaw = new(worldScreenRawVec.X, worldScreenRawVec.Y);
            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowArea.X, worldScreenRaw.Y + windowArea.Y);

            bool centerInWindow = IsInsideWindowInEitherSpace(worldScreenAbsolute);
            bool centerClickable = IsClickableInEitherSpace(worldScreenAbsolute, path);

            if (!TryResolveNearbyClickablePoint(worldScreenAbsolute, path, out Vector2 clickPos))
            {
                if (captureClickDebug)
                {
                    SetLatestClickDebug(new ClickDebugSnapshot(
                        HasData: true,
                        Stage: "ProbeFailed",
                        MechanicId: mechanicId,
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

                return false;
            }

            bool resolvedInWindow = IsInsideWindowInEitherSpace(clickPos);
            bool resolvedClickable = IsClickableInEitherSpace(clickPos, path);

            candidate = new SettlersOreCandidate(entity, clickPos, mechanicId, path, worldScreenRaw, worldScreenAbsolute);

            if (captureClickDebug)
            {
                SetLatestClickDebug(new ClickDebugSnapshot(
                    HasData: true,
                    Stage: "ProbeResolved",
                    MechanicId: mechanicId,
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

            return true;
        }

        private void PublishSettlersCandidateDebug(string stage, SettlersOreCandidate candidate, string notes)
        {
            SetLatestClickDebug(new ClickDebugSnapshot(
                HasData: true,
                Stage: stage,
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
                Notes: notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        }

        private bool TryResolveNearbyClickablePoint(Vector2 center, string path, out Vector2 clickPos)
        {
            for (int i = 0; i < NearbyClickProbeOffsets.Length; i++)
            {
                Vector2 candidate = center + NearbyClickProbeOffsets[i];
                if (!IsInsideWindowInEitherSpace(candidate))
                    continue;
                if (!IsClickableInEitherSpace(candidate, path))
                    continue;

                clickPos = candidate;
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

            if (IsStickyTarget(candidate.Entity))
                ClearStickyOffscreenTarget();

            if (settings.WalkTowardOffscreenLabels.Value)
                pathfindingService.ClearLatestPath();
        }

        private bool TryClickSettlersOre(SettlersOreCandidate candidate)
        {
            var entity = candidate.Entity;
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

            if (IsStickyTarget(entity))
                ClearStickyOffscreenTarget();

            if (settings.WalkTowardOffscreenLabels.Value)
                pathfindingService.ClearLatestPath();

            return true;
        }
    }
}