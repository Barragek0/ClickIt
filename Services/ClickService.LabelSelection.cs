using System.Collections;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ClickIt.Utils;
using ClickIt.Definitions;
using Microsoft.CSharp.RuntimeBinder;
using System.Windows.Forms;
using PathConstants = ClickIt.Definitions.Constants;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private const string ShrineMechanicId = MechanicIds.Shrines;
        private const string LostShipmentMechanicId = MechanicIds.LostShipment;
        private const string LostShipmentPathMarker = "Metadata/Chests/LostShipmentCrate";
        private const string LostShipmentLoosePathMarker = "LostShipment";
        private const string LostGoodsRenderNameMarker = "Lost Goods";
        private const string LostShipmentRenderNameMarker = "Lost Shipment";
        private const string VerisiumMechanicId = MechanicIds.SettlersVerisium;
        private const string VerisiumBossSubAreaTransitionPathMarker = MechanicIds.VerisiumBossSubAreaTransitionPathMarker;
        private const string AreaTransitionsMechanicId = MechanicIds.AreaTransitions;
        private const string LabyrinthTrialsMechanicId = MechanicIds.LabyrinthTrials;
        private const int MovementSkillRecastDelayMs = 450;
        private const int MovementSkillKeyTapDelayMs = 30;
        private const int MovementSkillDefaultPostCastClickBlockMs = 120;
        private const int MovementSkillShieldChargePostCastClickBlockMs = 100;
        private const int MovementSkillLeapSlamPostCastClickBlockMs = 230;
        private const int MovementSkillWhirlingBladesPostCastClickBlockMs = 170;
        private const int MovementSkillBlinkArrowPostCastClickBlockMs = 260;
        private const int MovementSkillChargedDashPostCastClickBlockMs = 300;
        private const int MovementSkillLightningWarpPostCastClickBlockMs = 320;
        private const int MovementSkillConsecratedPathPostCastClickBlockMs = 240;
        private const int MovementSkillChainHookPostCastClickBlockMs = 220;
        private const int MovementSkillDefaultStatusPollExtraMs = 900;
        private const int MovementSkillExtendedStatusPollExtraMs = 1300;

        private static readonly string[] MovementSkillInternalNameMarkers =
        [
            "QuickDashGem",
            "Dash",
            "dash",
            "FlameDash",
            "flame_dash",
            "FrostblinkSkillGem",
            "Frostblink",
            "frostblink",
            "LeapSlam",
            "leap_slam",
            "ShieldCharge",
            "shield_charge",
            "WhirlingBlades",
            "whirling_blades",
            "BlinkArrow",
            "blink_arrow",
            "MirrorArrow",
            "mirror_arrow",
            "CorpseWarp",
            "Bodyswap",
            "bodyswap",
            "LightningWarp",
            "lightning_warp",
            "ChargedDashGem",
            "ChargedDash",
            "charged_dash",
            "HolyPathGem",
            "ConsecratedPath",
            "consecrated_path",
            "PhaseRun",
            "phase_run",
            "ChainStrikeGem",
            "ChainHook",
            "chain_hook",
            "WitheringStepGem",
            "WitheringStep",
            "withering_step",
            "SmokeBomb",
            "SmokeMine",
            "smoke_mine",
            "AmbushSkillGem",
            "Ambush",
            "ambush_player",
            "QuickStepGem",
            "slow_dodge"
        ];

        private static readonly int[] LabelSearchCaps = [1, 5, 25, 100];
        private IReadOnlyList<string>? _cachedMechanicPriorityOrder;
        private IReadOnlyCollection<string>? _cachedMechanicIgnoreDistanceIds;
        private IReadOnlyDictionary<string, int>? _cachedMechanicIgnoreDistanceWithinById;
        private IReadOnlyDictionary<string, int> _cachedMechanicPriorityIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlySet<string> _cachedMechanicIgnoreDistanceSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyDictionary<string, int> _cachedMechanicIgnoreDistanceWithinMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private readonly struct LostShipmentCandidate(Entity entity, Vector2 clickPosition)
        {
            public Entity Entity { get; } = entity;
            public Vector2 ClickPosition { get; } = clickPosition;
            public float Distance { get; } = entity.DistancePlayer;
        }

        private readonly struct SettlersOreCandidate(
            Entity entity,
            Vector2 clickPosition,
            string mechanicId,
            string entityPath,
            Vector2 worldScreenRaw,
            Vector2 worldScreenAbsolute)
        {
            public Entity Entity { get; } = entity;
            public Vector2 ClickPosition { get; } = clickPosition;
            public string MechanicId { get; } = mechanicId;
            public string EntityPath { get; } = entityPath;
            public Vector2 WorldScreenRaw { get; } = worldScreenRaw;
            public Vector2 WorldScreenAbsolute { get; } = worldScreenAbsolute;
            public float Distance { get; } = entity.DistancePlayer;
        }

        public IEnumerator ProcessRegularClick()
        {
            PublishClickFlowDebugStage("TickStart", "ProcessRegularClick entered");

            if (HasClickableAltars())
            {
                PublishClickFlowDebugStage("AltarBranch", "Clickable altar detected; regular label click path skipped");
                yield return ProcessAltarClicking();
                yield break;
            }

            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);

            // Keep regular clicking alive even if Ultimatum UI shape differs on a given ExileAPI/runtime build.
            try
            {
                if (TryHandleUltimatumPanelUi(windowTopLeft))
                    yield break;
            }
            catch (Exception ex)
            {
                DebugLog(() => $"[ProcessRegularClick] Ultimatum UI handler failed, continuing regular click path: {ex.Message}");
            }

            if (TryGetMovementSkillPostCastBlockState(Environment.TickCount64, out string movementSkillBlockReason))
            {
                DebugLog(() => $"[ProcessRegularClick] Skipping click attempt while movement skill is still executing ({movementSkillBlockReason}).");
                PublishClickFlowDebugStage("MovementBlocked", movementSkillBlockReason);
                yield break;
            }

            var nextShrine = ResolveNextShrineCandidate();
            LostShipmentCandidate? lostShipmentCandidate = ResolveNextLostShipmentCandidate();
            SettlersOreCandidate? settlersOreCandidate = ResolveNextSettlersOreCandidate();
            RefreshMechanicPriorityCaches();

            if (!groundItemsVisible())
            {
                PublishClickFlowDebugStage("GroundItemsHidden", "Ground item labels hidden; evaluating non-label fallbacks");

                if (settlersOreCandidate.HasValue
                    && ShouldPreferSettlersOreOverVisibleCandidates(
                        settlersOreCandidate.Value.Distance,
                        settlersOreCandidate.Value.MechanicId,
                        labelDistance: null,
                        labelMechanicId: null,
                        shrineDistance: nextShrine?.DistancePlayer,
                        lostShipmentDistance: lostShipmentCandidate.HasValue ? lostShipmentCandidate.Value.Distance : null,
                        _cachedMechanicPriorityIndexMap,
                        _cachedMechanicIgnoreDistanceSet,
                        _cachedMechanicIgnoreDistanceWithinMap,
                        settings.MechanicPriorityDistancePenalty.Value))
                {
                    PublishClickFlowDebugStage("HiddenSettlersFallback", "Using hidden settlers candidate", settlersOreCandidate.Value.MechanicId);
                    TryClickSettlersOre(settlersOreCandidate.Value);
                    yield break;
                }

                if (lostShipmentCandidate.HasValue
                    && ShouldPreferLostShipmentOverVisibleCandidates(
                        lostShipmentCandidate.Value.Distance,
                        labelDistance: null,
                        labelMechanicId: null,
                        shrineDistance: nextShrine?.DistancePlayer,
                        _cachedMechanicPriorityIndexMap,
                        _cachedMechanicIgnoreDistanceSet,
                        _cachedMechanicIgnoreDistanceWithinMap,
                        settings.MechanicPriorityDistancePenalty.Value))
                {
                    TryClickLostShipment(lostShipmentCandidate.Value);
                    yield break;
                }

                if (nextShrine != null && ShouldClickShrineWhenGroundItemsHidden(nextShrine))
                {
                    TryClickShrine(nextShrine);
                    // Do not start offscreen pathfinding while actively attempting an on-screen shrine interaction.
                    yield break;
                }

                if (settings.WalkTowardOffscreenLabels.Value)
                {
                    TryWalkTowardOffscreenTarget();
                }

                DebugLog(() => "[ProcessRegularClick] Ground items not visible, breaking");
                PublishClickFlowDebugStage("GroundItemsHiddenExit", "No clickable hidden fallback selected");
                yield break;
            }

            var allLabels = GetLabelsForRegularSelection();
            PublishClickFlowDebugStage("LabelSource", BuildLabelSourceDebugSummary(allLabels));
            LabelOnGround? nextLabel = ResolveNextLabelCandidate(allLabels);

            string? nextLabelMechanicId = nextLabel != null
                ? labelFilterService.GetMechanicIdForLabel(nextLabel)
                : null;

            if (lostShipmentCandidate.HasValue
                && ShouldPreferLostShipmentOverVisibleCandidates(
                    lostShipmentCandidate.Value.Distance,
                    nextLabel?.ItemOnGround?.DistancePlayer,
                    nextLabelMechanicId,
                    nextShrine?.DistancePlayer,
                    _cachedMechanicPriorityIndexMap,
                    _cachedMechanicIgnoreDistanceSet,
                    _cachedMechanicIgnoreDistanceWithinMap,
                    settings.MechanicPriorityDistancePenalty.Value))
            {
                TryClickLostShipment(lostShipmentCandidate.Value);
                yield break;
            }

            bool useShrine = ShouldPreferShrineOverLabel(nextLabel, nextShrine);
            if (useShrine && nextShrine != null)
            {
                TryClickShrine(nextShrine);

                yield break;
            }

            if (nextLabel == null)
            {
                labelFilterService.LogSelectionDiagnostics(allLabels, 0, allLabels?.Count ?? 0);
                PublishClickFlowDebugStage("NoLabelCandidate", BuildNoLabelDebugSummary(allLabels));

                if (settings.WalkTowardOffscreenLabels.Value && TryHandleStickyOffscreenTarget(windowTopLeft, allLabels))
                {
                    yield break;
                }

                if (settings.WalkTowardOffscreenLabels.Value)
                {
                    TryWalkTowardOffscreenTarget();
                }

                DebugLog(() => "[ProcessRegularClick] No label to click found, breaking");
                PublishClickFlowDebugStage("NoLabelExit", "No label click attempted");
                yield break;
            }

            if (ShouldSkipOrHandleSpecialLabel(nextLabel, windowTopLeft))
            {
                PublishClickFlowDebugStage("SpecialLabelHandled", "Special label handling consumed click tick", nextLabelMechanicId);
                yield break;
            }

            if (!inputHandler.TryCalculateClickPosition(
                nextLabel,
                windowTopLeft,
                allLabels,
                point => IsClickableInEitherSpace(point, nextLabel.ItemOnGround?.Path ?? string.Empty),
                out Vector2 clickPos))
            {
                DebugLog(() => "[ProcessRegularClick] Skipping label: no clickable point inside label bounds.");
                PublishClickFlowDebugStage("ClickPointResolveFailed", "TryCalculateClickPosition returned false", nextLabelMechanicId);

                bool shouldContinueEntityPathing = ShouldPathfindToEntityAfterClickPointResolveFailure(
                    settings.WalkTowardOffscreenLabels.Value,
                    nextLabel.ItemOnGround != null,
                    nextLabelMechanicId);
                if (shouldContinueEntityPathing)
                {
                    PublishClickFlowDebugStage("EntityPathingFallback", "Label visible but unresolved click point; continuing pathing", nextLabelMechanicId);
                    _ = TryWalkTowardOffscreenTarget(nextLabel.ItemOnGround);
                }

                yield break;
            }

            PublishClickFlowDebugStage("ClickPointResolved", $"Resolved click point ({clickPos.X:0.0},{clickPos.Y:0.0})", nextLabelMechanicId);

            bool isSettlersMechanic = IsSettlersMechanicId(nextLabelMechanicId);
            PublishLabelClickDebug(
                stage: "LabelCandidate",
                mechanicId: nextLabelMechanicId,
                label: nextLabel,
                resolvedClickPos: clickPos,
                resolved: true,
                notes: "Settlers label candidate selected from ItemsOnGroundLabelsVisible");

            bool clicked = ShouldUseHoldClickForSettlersMechanic(nextLabelMechanicId)
                ? PerformLabelHoldClick(clickPos, nextLabel.Label, gameController, holdDurationMs: 0)
                : PerformLabelClick(clickPos, nextLabel.Label, gameController);

            PublishLabelClickDebug(
                stage: clicked ? "ClickSuccess" : "ClickFailed",
                mechanicId: nextLabelMechanicId,
                label: nextLabel,
                resolvedClickPos: clickPos,
                resolved: clicked,
                notes: clicked ? "Settlers click completed via label pipeline" : "Settlers click attempt failed via label pipeline");

            PublishClickFlowDebugStage(clicked ? "ClickExecuted" : "ClickRejected", clicked ? "Input click executed" : "Input click rejected", nextLabelMechanicId);

            if (clicked)
            {
                if (IsStickyTarget(nextLabel.ItemOnGround))
                {
                    ClearStickyOffscreenTarget();
                }

                MarkLeverClicked(nextLabel);
                if (settings.WalkTowardOffscreenLabels.Value)
                {
                    pathfindingService.ClearLatestPath();
                }
            }

            if (inputHandler.TriggerToggleItems())
            {
                int blockMs = inputHandler.GetToggleItemsPostClickBlockMs();
                if (blockMs > 0)
                {
                    yield return new WaitTime(blockMs);
                }
            }
        }

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
            SharpDX.Vector2 shrineClickPos = new SharpDX.Vector2(shrineScreenRaw.X, shrineScreenRaw.Y);
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
                    {
                        best = candidate;
                    }
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
                            continue;
                        }

                        bool resolvedInWindow = IsInsideWindowInEitherSpace(clickPos);
                        bool resolvedClickable = IsClickableInEitherSpace(clickPos, path);

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

                        var candidate = new SettlersOreCandidate(entity, clickPos, settlersMechanicId, path, worldScreenRaw, worldScreenAbsolute);
                        if (!best.HasValue || candidate.Distance < best.Value.Distance)
                        {
                            best = candidate;
                            SetLatestClickDebug(new ClickDebugSnapshot(
                                HasData: true,
                                Stage: "CandidateSelected",
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
                                Notes: "Nearest settlers candidate selected",
                                Sequence: 0,
                                TimestampMs: Environment.TickCount64));
                        }
                    }
                }

                if (!best.HasValue)
                {
                    DebugLog(() => $"[ResolveNextSettlersOreCandidate] none scanned:{scanned} matched:{matchedPath} labelBacked:{labelBacked}");
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

            // Hidden-label targets can have an unclickable center; probe nearby points conservatively.
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

            bool clicked = PerformLabelClick(candidate.ClickPosition, null, gameController);
            if (clicked)
            {
                if (IsStickyTarget(candidate.Entity))
                {
                    ClearStickyOffscreenTarget();
                }

                if (settings.WalkTowardOffscreenLabels.Value)
                {
                    pathfindingService.ClearLatestPath();
                }
            }
        }

        private void TryClickSettlersOre(SettlersOreCandidate candidate)
        {
            DebugLog(() => $"[ProcessRegularClick] Clicking settlers ore candidate ({candidate.MechanicId}) via entity position.");

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

            bool clicked = ShouldUseHoldClickForSettlersMechanic(candidate.MechanicId)
                ? PerformLabelHoldClick(candidate.ClickPosition, null, gameController, holdDurationMs: 0)
                : PerformLabelClick(candidate.ClickPosition, null, gameController);
            if (clicked)
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

                if (IsStickyTarget(candidate.Entity))
                    ClearStickyOffscreenTarget();

                if (settings.WalkTowardOffscreenLabels.Value)
                    pathfindingService.ClearLatestPath();
            }
        }

        internal static bool IsLostShipmentPath(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && (path.Contains(LostShipmentPathMarker, StringComparison.OrdinalIgnoreCase)
                    || path.Contains(LostShipmentLoosePathMarker, StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsVerisiumPath(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.Contains(PathConstants.Verisium, StringComparison.OrdinalIgnoreCase)
                && !path.Contains(VerisiumBossSubAreaTransitionPathMarker, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldUseHoldClickForSettlersMechanic(string? mechanicId)
        {
            return string.Equals(mechanicId, VerisiumMechanicId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLostShipmentEntity(string? path, string? renderName)
        {
            if (IsLostShipmentPath(path))
                return true;

            if (string.IsNullOrWhiteSpace(renderName))
                return false;

            return renderName.Contains(LostGoodsRenderNameMarker, StringComparison.OrdinalIgnoreCase)
                || renderName.Contains(LostShipmentRenderNameMarker, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldSkipLostShipmentEntity(bool isValid, float distance, int clickDistance, bool isOpened)
        {
            return !isValid || isOpened || distance > clickDistance;
        }

        internal static bool ShouldSkipSettlersOreEntity(bool isValid, float distance, int clickDistance)
        {
            return !isValid || distance > clickDistance;
        }

        internal static bool ShouldSkipVerisiumEntity(bool isValid, float distance, int clickDistance)
        {
            return ShouldSkipSettlersOreEntity(isValid, distance, clickDistance);
        }

        internal static bool ShouldPreferLostShipmentOverVisibleCandidates(
            float lostShipmentDistance,
            float? labelDistance,
            string? labelMechanicId,
            float? shrineDistance,
            IReadOnlyDictionary<string, int> priorityIndexMap,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
            int priorityDistancePenalty)
        {
            var lostShipmentRank = BuildMechanicRank(
                lostShipmentDistance,
                LostShipmentMechanicId,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);

            if (labelDistance.HasValue)
            {
                var labelRank = BuildMechanicRank(
                    labelDistance.Value,
                    labelMechanicId,
                    priorityIndexMap,
                    ignoreDistanceSet,
                    ignoreDistanceWithinByMechanicId,
                    priorityDistancePenalty);

                if (CompareMechanicRanks(lostShipmentRank, labelRank) >= 0)
                    return false;
            }

            if (shrineDistance.HasValue)
            {
                var shrineRank = BuildMechanicRank(
                    shrineDistance.Value,
                    ShrineMechanicId,
                    priorityIndexMap,
                    ignoreDistanceSet,
                    ignoreDistanceWithinByMechanicId,
                    priorityDistancePenalty);

                if (CompareMechanicRanks(lostShipmentRank, shrineRank) >= 0)
                    return false;
            }

            return true;
        }

        internal static bool ShouldPreferSettlersOreOverVisibleCandidates(
            float settlersOreDistance,
            string settlersOreMechanicId,
            float? labelDistance,
            string? labelMechanicId,
            float? shrineDistance,
            float? lostShipmentDistance,
            IReadOnlyDictionary<string, int> priorityIndexMap,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
            int priorityDistancePenalty)
        {
            var settlersOreRank = BuildMechanicRank(
                settlersOreDistance,
                settlersOreMechanicId,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);

            if (labelDistance.HasValue)
            {
                var labelRank = BuildMechanicRank(
                    labelDistance.Value,
                    labelMechanicId,
                    priorityIndexMap,
                    ignoreDistanceSet,
                    ignoreDistanceWithinByMechanicId,
                    priorityDistancePenalty);

                if (CompareMechanicRanks(settlersOreRank, labelRank) >= 0)
                    return false;
            }

            if (shrineDistance.HasValue)
            {
                var shrineRank = BuildMechanicRank(
                    shrineDistance.Value,
                    ShrineMechanicId,
                    priorityIndexMap,
                    ignoreDistanceSet,
                    ignoreDistanceWithinByMechanicId,
                    priorityDistancePenalty);

                if (CompareMechanicRanks(settlersOreRank, shrineRank) >= 0)
                    return false;
            }

            if (lostShipmentDistance.HasValue)
            {
                var lostShipmentRank = BuildMechanicRank(
                    lostShipmentDistance.Value,
                    LostShipmentMechanicId,
                    priorityIndexMap,
                    ignoreDistanceSet,
                    ignoreDistanceWithinByMechanicId,
                    priorityDistancePenalty);

                if (CompareMechanicRanks(settlersOreRank, lostShipmentRank) >= 0)
                    return false;
            }

            return true;
        }

        internal static bool ShouldPreferVerisiumOverVisibleCandidates(
            float verisiumDistance,
            float? labelDistance,
            string? labelMechanicId,
            float? shrineDistance,
            float? lostShipmentDistance,
            IReadOnlyDictionary<string, int> priorityIndexMap,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
            int priorityDistancePenalty)
        {
            return ShouldPreferSettlersOreOverVisibleCandidates(
                verisiumDistance,
                VerisiumMechanicId,
                labelDistance,
                labelMechanicId,
                shrineDistance,
                lostShipmentDistance,
                priorityIndexMap,
                ignoreDistanceSet,
                ignoreDistanceWithinByMechanicId,
                priorityDistancePenalty);
        }

        private bool HasStickyOffscreenTarget()
        {
            return _stickyOffscreenTargetAddress != 0;
        }

        private bool ShouldPreferShrineOverLabel(LabelOnGround? label, Entity? shrine)
        {
            if (shrine == null)
                return false;
            if (label == null)
                return true;

            string? labelMechanicId = labelFilterService.GetMechanicIdForLabel(label);
            if (string.IsNullOrWhiteSpace(labelMechanicId))
                return true;

            RefreshMechanicPriorityCaches();

            float labelDistance = label.ItemOnGround?.DistancePlayer ?? float.MaxValue;
            float shrineDistance = shrine.DistancePlayer;

            var labelRank = BuildMechanicRank(labelDistance, labelMechanicId);
            var shrineRank = BuildMechanicRank(shrineDistance, ShrineMechanicId);

            return CompareMechanicRanks(shrineRank, labelRank) < 0;
        }

        private readonly struct MechanicRank(bool ignored, int priorityIndex, float weightedDistance, float rawDistance)
        {
            public bool Ignored { get; } = ignored;
            public int PriorityIndex { get; } = priorityIndex;
            public float WeightedDistance { get; } = weightedDistance;
            public float RawDistance { get; } = rawDistance;
        }

        private MechanicRank BuildMechanicRank(float distance, string? mechanicId)
        {
            return BuildMechanicRank(
                distance,
                mechanicId,
                _cachedMechanicPriorityIndexMap,
                _cachedMechanicIgnoreDistanceSet,
                _cachedMechanicIgnoreDistanceWithinMap,
                settings.MechanicPriorityDistancePenalty.Value);
        }

        private static MechanicRank BuildMechanicRank(
            float distance,
            string? mechanicId,
            IReadOnlyDictionary<string, int> priorityIndexMap,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
            int priorityDistancePenalty)
        {
            int priorityIndex = int.MaxValue;
            if (!string.IsNullOrWhiteSpace(mechanicId)
                && priorityIndexMap.TryGetValue(mechanicId, out int index))
            {
                priorityIndex = index;
            }

            bool ignored = IsIgnoreDistanceActiveForMechanic(mechanicId, distance, ignoreDistanceSet, ignoreDistanceWithinByMechanicId);
            float weightedDistance = distance + (priorityIndex == int.MaxValue ? float.MaxValue : priorityIndex * Math.Max(0, priorityDistancePenalty));
            return new MechanicRank(ignored, priorityIndex, weightedDistance, distance);
        }

        private static bool IsIgnoreDistanceActiveForMechanic(
            string? mechanicId,
            float distance,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId)
        {
            if (string.IsNullOrWhiteSpace(mechanicId))
                return false;
            if (!ignoreDistanceSet.Contains(mechanicId))
                return false;

            int maxDistance = ignoreDistanceWithinByMechanicId.TryGetValue(mechanicId, out int configured)
                ? configured
                : 100;
            return distance <= maxDistance;
        }

        internal static bool ShouldPreferShrineOverLabelForOffscreen(
            float shrineDistance,
            float labelDistance,
            string? labelMechanicId,
            IReadOnlyDictionary<string, int> priorityIndexMap,
            IReadOnlySet<string> ignoreDistanceSet,
            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId,
            int priorityDistancePenalty)
        {
            var labelRank = BuildMechanicRank(labelDistance, labelMechanicId, priorityIndexMap, ignoreDistanceSet, ignoreDistanceWithinByMechanicId, priorityDistancePenalty);
            var shrineRank = BuildMechanicRank(shrineDistance, ShrineMechanicId, priorityIndexMap, ignoreDistanceSet, ignoreDistanceWithinByMechanicId, priorityDistancePenalty);
            return CompareMechanicRanks(shrineRank, labelRank) < 0;
        }

        private static int CompareMechanicRanks(MechanicRank left, MechanicRank right)
        {
            if (left.Ignored && right.Ignored)
            {
                int priorityCompare = left.PriorityIndex.CompareTo(right.PriorityIndex);
                if (priorityCompare != 0)
                    return priorityCompare;
                return left.RawDistance.CompareTo(right.RawDistance);
            }

            if (left.Ignored != right.Ignored)
            {
                return left.Ignored
                    ? (left.PriorityIndex <= right.PriorityIndex ? -1 : 1)
                    : (right.PriorityIndex <= left.PriorityIndex ? 1 : -1);
            }

            int weightedCompare = left.WeightedDistance.CompareTo(right.WeightedDistance);
            if (weightedCompare != 0)
                return weightedCompare;

            int distanceCompare = left.RawDistance.CompareTo(right.RawDistance);
            if (distanceCompare != 0)
                return distanceCompare;

            return left.PriorityIndex.CompareTo(right.PriorityIndex);
        }

        private void RefreshMechanicPriorityCaches()
        {
            IReadOnlyList<string> priorityOrder = settings.GetMechanicPriorityOrder();
            if (!ReferenceEquals(_cachedMechanicPriorityOrder, priorityOrder))
            {
                _cachedMechanicPriorityOrder = priorityOrder;

                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < priorityOrder.Count; i++)
                {
                    string id = priorityOrder[i] ?? string.Empty;
                    if (id.Length == 0 || map.ContainsKey(id))
                        continue;
                    map[id] = i;
                }

                _cachedMechanicPriorityIndexMap = map;
            }

            IReadOnlyCollection<string> ignoreDistanceIds = settings.GetMechanicPriorityIgnoreDistanceIds();
            if (!ReferenceEquals(_cachedMechanicIgnoreDistanceIds, ignoreDistanceIds))
            {
                _cachedMechanicIgnoreDistanceIds = ignoreDistanceIds;
                _cachedMechanicIgnoreDistanceSet = new HashSet<string>(ignoreDistanceIds, StringComparer.OrdinalIgnoreCase);
            }

            IReadOnlyDictionary<string, int> ignoreDistanceWithinByMechanicId = settings.GetMechanicPriorityIgnoreDistanceWithinById();
            if (!ReferenceEquals(_cachedMechanicIgnoreDistanceWithinById, ignoreDistanceWithinByMechanicId))
            {
                _cachedMechanicIgnoreDistanceWithinById = ignoreDistanceWithinByMechanicId;
                _cachedMechanicIgnoreDistanceWithinMap = new Dictionary<string, int>(ignoreDistanceWithinByMechanicId, StringComparer.OrdinalIgnoreCase);
            }
        }

        private int GetMechanicPriorityIndex(string? mechanicId)
        {
            if (string.IsNullOrWhiteSpace(mechanicId))
                return int.MaxValue;

            return _cachedMechanicPriorityIndexMap.TryGetValue(mechanicId, out int index) ? index : int.MaxValue;
        }

        private LabelOnGround? ResolveNextLabelCandidate(IReadOnlyList<LabelOnGround>? allLabels)
        {
            LabelOnGround? nextLabel = FindNextLabelToClick(allLabels);
            return PreferUiHoverEssenceLabel(nextLabel, allLabels);
        }

        private LabelOnGround? PreferUiHoverEssenceLabel(LabelOnGround? nextLabel, IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (allLabels == null)
                return nextLabel;

            var uiHover = gameController?.IngameState?.UIHoverElement;
            if (uiHover == null)
                return nextLabel;

            LabelOnGround? hovered = FindLabelByAddress(allLabels, uiHover.Address);
            if (hovered == null)
                return nextLabel;

            bool hoveredIsEssence = IsEssenceLabel(hovered);
            bool nextIsEssence = nextLabel != null && IsEssenceLabel(nextLabel);
            bool hoveredHasOverlappingEssence = hoveredIsEssence && HasOverlappingEssenceLabel(hovered, allLabels);
            bool hoveredDiffersFromNext = !ReferenceEquals(hovered, nextLabel);

            if (ShouldPreferHoveredEssenceLabel(hoveredIsEssence, hoveredHasOverlappingEssence, nextIsEssence, hoveredDiffersFromNext))
            {
                DebugLog(() => "[ProcessRegularClick] UIHover-first: switching target to UIHover label");
                return hovered;
            }

            return nextLabel;
        }

        internal static bool ShouldPreferHoveredEssenceLabel(
            bool hoveredIsEssence,
            bool hoveredHasOverlappingEssence,
            bool nextIsEssence,
            bool hoveredDiffersFromNext)
        {
            if (!hoveredIsEssence)
                return false;

            if (!hoveredDiffersFromNext)
                return false;

            if (hoveredHasOverlappingEssence)
                return true;

            return nextIsEssence;
        }

        private static bool HasOverlappingEssenceLabel(LabelOnGround hoveredEssence, IReadOnlyList<LabelOnGround> allLabels)
        {
            if (!TryGetLabelRect(hoveredEssence, out RectangleF hoveredRect))
                return false;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround? candidate = allLabels[i];
                if (candidate == null || ReferenceEquals(candidate, hoveredEssence) || !IsEssenceLabel(candidate))
                    continue;

                if (!TryGetLabelRect(candidate, out RectangleF candidateRect))
                    continue;

                if (hoveredRect.Intersects(candidateRect))
                    return true;
            }

            return false;
        }

        private static bool TryGetLabelRect(LabelOnGround? label, out RectangleF rect)
        {
            rect = default;
            Element? element = label?.Label;
            if (element == null || !element.IsValid)
                return false;

            object? maybeRect = element.GetClientRect();
            if (maybeRect is not RectangleF r)
                return false;

            if (r.Width <= 0 || r.Height <= 0)
                return false;

            rect = r;
            return true;
        }

        private bool ShouldSkipOrHandleSpecialLabel(LabelOnGround nextLabel, Vector2 windowTopLeft)
        {
            if (IsAltarLabel(nextLabel))
            {
                DebugLog(() => "[ProcessRegularClick] Item is an altar, breaking");
                return true;
            }

            if (TryCorruptEssence(nextLabel, windowTopLeft))
                return true;

            if (!settings.IsInitialUltimatumClickEnabled() || !IsUltimatumLabel(nextLabel))
                return false;

            if (TryClickPreferredUltimatumModifier(nextLabel, windowTopLeft))
                return true;

            DebugLog(() => "[ProcessRegularClick] Ultimatum label detected but no preferred modifier matched; skipping generic label click");
            return true;
        }

        private static bool IsEssenceLabel(LabelOnGround lbl)
        {
            if (lbl == null || lbl.Label == null)
                return false;

            return LabelUtils.HasEssenceImprisonmentText(lbl);
        }

        private LabelOnGround? FindNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (allLabels == null || allLabels.Count == 0)
                return null;

            foreach (int cap in LabelSearchCaps)
            {
                int limit = Math.Min(cap, allLabels.Count);
                LabelOnGround? candidate = FindLabelInRange(allLabels, 0, limit);
                if (candidate != null)
                    return candidate;
            }

            // Fallback to full scan (rare).
            return FindLabelInRange(allLabels, 0, allLabels.Count);
        }

        private static LabelOnGround? FindLabelByAddress(IReadOnlyList<LabelOnGround> labels, long address)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label?.Label != null && label.Label.Address == address)
                    return label;
            }

            return null;
        }

        private LabelOnGround? FindLabelInRange(IReadOnlyList<LabelOnGround> allLabels, int start, int endExclusive)
        {
            int currentStart = start;
            int examined = 0;
            int leverSuppressed = 0;
            int ultimatumSuppressed = 0;
            int overlappedSuppressed = 0;
            int indexMisses = 0;

            while (currentStart < endExclusive)
            {
                LabelOnGround? label = labelFilterService.GetNextLabelToClick(allLabels, currentStart, endExclusive - currentStart);
                if (label == null)
                {
                    string noLabelSummary = BuildLabelRangeRejectionDebugSummary(allLabels, start, endExclusive, examined);
                    PublishClickFlowDebugStage("FindLabelNull", noLabelSummary);
                    if (examined > 0)
                    {
                        DebugLog(() =>
                            $"[LabelSelectDiag] range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} im:{indexMisses}");
                    }
                    return null;
                }

                examined++;

                bool suppressLever = ShouldSuppressLeverClick(label);
                bool suppressUltimatum = ShouldSuppressInactiveUltimatumLabel(label);
                bool fullyOverlapped = inputHandler.IsLabelFullyOverlapped(label, allLabels);

                if (suppressLever)
                    leverSuppressed++;
                if (suppressUltimatum)
                    ultimatumSuppressed++;

                if (fullyOverlapped)
                    overlappedSuppressed++;

                if (!suppressLever
                    && !suppressUltimatum
                    && !fullyOverlapped)
                {
                    PublishClickFlowDebugStage("FindLabelMatch", $"range:{start}-{endExclusive} examined:{examined}");
                    return label;
                }

                if (fullyOverlapped)
                {
                    DebugLog(() => "[ProcessRegularClick] Skipping fully-overlapped label");
                }

                int idx = IndexOfLabelReference(allLabels, label, currentStart, endExclusive);
                if (idx < 0)
                {
                    indexMisses++;
                    PublishClickFlowDebugStage("FindLabelIndexMiss", $"range:{start}-{endExclusive} examined:{examined} misses:{indexMisses}");
                    DebugLog(() =>
                        $"[LabelSelectDiag] index-miss range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} im:{indexMisses}");
                    return null;
                }

                currentStart = idx + 1;
            }

            if (examined > 0)
            {
                PublishClickFlowDebugStage("FindLabelExhausted", $"range:{start}-{endExclusive} examined:{examined}");
                DebugLog(() =>
                    $"[LabelSelectDiag] exhausted range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} im:{indexMisses}");
            }

            return null;
        }

        private static int IndexOfLabelReference(IReadOnlyList<LabelOnGround> labels, LabelOnGround target, int start, int endExclusive)
        {
            for (int i = start; i < endExclusive; i++)
            {
                if (ReferenceEquals(labels[i], target))
                    return i;
            }

            return -1;
        }

        private bool ShouldSuppressLeverClick(LabelOnGround label)
        {
            if (!settings.LazyMode.Value)
                return false;
            if (!IsLeverLabel(label))
                return false;

            int cooldownMs = settings.LazyModeLeverReclickDelay?.Value ?? 1200;
            ulong currentLeverKey = GetLeverIdentityKey(label);
            long now = Environment.TickCount64;

            return IsLeverClickSuppressedByCooldown(_lastLeverKey, _lastLeverClickTimestampMs, currentLeverKey, now, cooldownMs);
        }

        private static bool IsLeverClickSuppressedByCooldown(ulong lastLeverKey, long lastLeverClickTimestampMs, ulong currentLeverKey, long now, int cooldownMs)
        {
            if (cooldownMs <= 0)
                return false;
            if (currentLeverKey == 0 || lastLeverKey == 0)
                return false;
            if (currentLeverKey != lastLeverKey)
                return false;
            if (lastLeverClickTimestampMs <= 0)
                return false;

            long elapsed = now - lastLeverClickTimestampMs;
            return elapsed >= 0 && elapsed < cooldownMs;
        }

        private void MarkLeverClicked(LabelOnGround label)
        {
            if (!settings.LazyMode.Value)
                return;
            if (!IsLeverLabel(label))
                return;

            ulong key = GetLeverIdentityKey(label);
            if (key == 0)
                return;

            _lastLeverKey = key;
            _lastLeverClickTimestampMs = Environment.TickCount64;
        }

        private static bool IsLeverLabel(LabelOnGround? label)
        {
            string? path = label?.ItemOnGround?.Path;
            return !string.IsNullOrWhiteSpace(path)
                && path.Contains("Switch_Once", StringComparison.OrdinalIgnoreCase);
        }

        private static ulong GetLeverIdentityKey(LabelOnGround label)
        {
            ulong itemAddress = unchecked((ulong)(label.ItemOnGround?.Address ?? 0));
            if (itemAddress != 0)
                return itemAddress;

            ulong elementAddress = unchecked((ulong)(label.Label?.Address ?? 0));
            if (elementAddress != 0)
                return elementAddress;

            return 0;
        }

        private static bool IsAltarLabel(LabelOnGround label)
        {
            var item = label.ItemOnGround;
            string path = item.Path ?? string.Empty;
            return path.Contains("CleansingFireAltar") || path.Contains("TangleAltar");
        }

        private bool TryCorruptEssence(LabelOnGround label, Vector2 windowTopLeft)
        {
            if (settings.ClickEssences && labelFilterService.ShouldCorruptEssence(label))
            {
                Vector2? corruptionPos = LabelFilterService.GetCorruptionClickPosition(label, windowTopLeft);
                if (corruptionPos.HasValue)
                {
                    if (!EnsureCursorInsideGameWindowForClick("[TryCorruptEssence] Skipping corruption click - cursor outside PoE window"))
                        return false;

                    DebugLog(() => $"[ProcessRegularClick] Corruption click at {corruptionPos.Value}");
                    PerformLockedClick(corruptionPos.Value, null, gameController);
                    performanceMonitor.RecordClickInterval();
                    return true;
                }
            }

            return false;
        }

        private bool PerformLabelClick(Vector2 clickPos, Element? expectedElement, GameController? controller)
        {
            if (!EnsureCursorInsideGameWindowForClick("[PerformLabelClick] Skipping label click - cursor outside PoE window"))
                return false;

            PerformLockedClick(clickPos, expectedElement, controller);

            performanceMonitor.RecordClickInterval();
            return true;
        }

        private bool PerformLabelHoldClick(Vector2 clickPos, Element? expectedElement, GameController? controller, int holdDurationMs)
        {
            if (!EnsureCursorInsideGameWindowForClick("[PerformLabelHoldClick] Skipping hold click - cursor outside PoE window"))
                return false;

            PerformLockedHoldClick(clickPos, holdDurationMs, expectedElement, controller);

            performanceMonitor.RecordClickInterval();
            return true;
        }

        private bool TryWalkTowardOffscreenTarget(Entity? preferredTarget = null)
        {
            if (!settings.WalkTowardOffscreenLabels.Value)
                return false;

            Entity? target = preferredTarget ?? ResolveNearestOffscreenWalkTarget();
            if (target == null)
            {
                if (preferredTarget != null)
                {
                    ClearStickyOffscreenTarget();
                }

                pathfindingService.ClearLatestPath();
                return false;
            }

            SetStickyOffscreenTarget(target);

            string targetPath = target.Path ?? string.Empty;
            bool builtPath = pathfindingService.TryBuildPathToTarget(gameController, target, settings.OffscreenPathfindingSearchBudget.Value);
            if (!builtPath)
            {
                DebugLog(() => "[TryWalkTowardOffscreenTarget] Pathfinding route not found; trying directional walk click.");
            }

            Vector2 targetScreen = default;
            bool resolvedFromPath = builtPath && TryResolveOffscreenTargetScreenPointFromPath(out targetScreen);
            if (!resolvedFromPath && !TryResolveOffscreenTargetScreenPoint(target, out targetScreen))
            {
                PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, resolvedClickPoint: false, targetScreen, clickScreen: default, stage: "ResolveTargetScreenFailed");
                DebugLog(() => "[TryWalkTowardOffscreenTarget] Failed to resolve target screen point.");
                return false;
            }

            if (!TryResolveDirectionalWalkClickPosition(targetScreen, targetPath, out Vector2 walkClick))
            {
                PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, resolvedClickPoint: false, targetScreen, clickScreen: default, stage: "ResolveClickPointFailed");
                DebugLog(() => "[TryWalkTowardOffscreenTarget] Failed to resolve directional click point.");
                return false;
            }

            string movementSkillDebug;
            if (TryUseMovementSkillForOffscreenPathing(targetPath, targetScreen, builtPath, out Vector2 movementSkillCastPoint, out movementSkillDebug))
            {
                PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, resolvedClickPoint: true, targetScreen, movementSkillCastPoint, stage: "MovementSkillUsed", movementSkillDebug);
                DebugLog(() => $"[TryWalkTowardOffscreenTarget] Used movement skill toward offscreen target: {targetPath}");
                return true;
            }

            if (!string.IsNullOrWhiteSpace(movementSkillDebug))
            {
                DebugLog(() => $"[TryWalkTowardOffscreenTarget] Movement skill not used: {movementSkillDebug}");
            }

            PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, resolvedClickPoint: true, targetScreen, walkClick, stage: "BeforeClick", movementSkillDebug);

            bool clicked = PerformLabelClick(walkClick, null, gameController);
            if (clicked)
            {
                PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, resolvedClickPoint: true, targetScreen, walkClick, stage: "Clicked", movementSkillDebug);
                _ = pathfindingService.TryBuildPathToTarget(gameController, target, settings.OffscreenPathfindingSearchBudget.Value);
                DebugLog(() => $"[TryWalkTowardOffscreenTarget] Walking toward offscreen target: {targetPath}");
            }
            else
            {
                PublishOffscreenMovementDebug(target, targetPath, builtPath, resolvedFromPath, resolvedClickPoint: true, targetScreen, walkClick, stage: "ClickRejected", movementSkillDebug);
            }

            return clicked;
        }

        private bool TryUseMovementSkillForOffscreenPathing(string targetPath, Vector2 targetScreen, bool builtPath, out Vector2 castPoint, out string debugReason)
        {
            castPoint = default;
            debugReason = string.Empty;

            int remainingNodes = GetRemainingOffscreenPathNodeCount();
            int minimumNodes = Math.Max(1, settings.OffscreenMovementSkillMinPathSubsectionLength?.Value ?? 8);
            long now = Environment.TickCount64;
            bool movementSkillsEnabled = settings.UseMovementSkillsForOffscreenPathfinding?.Value == true;

            if (!movementSkillsEnabled)
            {
                debugReason = "Skipped: setting disabled (Use Movement Skills for Offscreen Pathfinding = false).";
                return false;
            }

            if (!builtPath)
            {
                debugReason = "Skipped: no fresh path available (movement skill requires successful path build).";
                return false;
            }

            if (remainingNodes < minimumNodes)
            {
                debugReason = $"Skipped: remaining path nodes {remainingNodes} below minimum {minimumNodes}.";
                return false;
            }

            if (_lastMovementSkillUseTimestampMs > 0 && MovementSkillRecastDelayMs > 0)
            {
                long elapsed = now - _lastMovementSkillUseTimestampMs;
                if (elapsed < MovementSkillRecastDelayMs)
                {
                    debugReason = $"Skipped: local recast delay active ({elapsed}ms elapsed, need {MovementSkillRecastDelayMs}ms).";
                    return false;
                }
            }

            if (!ShouldAttemptMovementSkill(
                movementSkillsEnabled,
                builtPath,
                remainingPathNodes: remainingNodes,
                minPathNodes: minimumNodes,
                now,
                lastSkillUseTimestampMs: _lastMovementSkillUseTimestampMs,
                recastDelayMs: MovementSkillRecastDelayMs))
            {
                debugReason = "Skipped: movement skill gate returned false.";
                return false;
            }

            if (!TryResolveMovementSkillCastPosition(targetScreen, targetPath, out castPoint))
            {
                debugReason = "Skipped: unable to resolve safe/clickable movement-skill cast point.";
                return false;
            }

            if (!TryFindReadyMovementSkillKey(out Keys boundKey, out string movementSkillName, out object? movementSkillEntry, out string skillSearchDebug))
            {
                debugReason = $"Skipped: no ready movement skill key found. {skillSearchDebug}";
                return false;
            }

            if (!EnsureCursorInsideGameWindowForClick("[TryUseMovementSkillForOffscreenPathing] Skipping cast - cursor outside PoE window"))
            {
                debugReason = "Skipped: cursor outside game window safety check failed.";
                return false;
            }

            if (!Mouse.DisableNativeInput)
            {
                Input.SetCursorPos(castPoint);
                Thread.Sleep(10);
            }

            Keyboard.KeyPress(boundKey, MovementSkillKeyTapDelayMs);
            _lastMovementSkillUseTimestampMs = now;
            int postCastClickBlockMs = ResolveMovementSkillPostCastClickBlockMsForCast(movementSkillName);
            _movementSkillPostCastClickBlockUntilTimestampMs = postCastClickBlockMs > 0
                ? now + postCastClickBlockMs
                : 0;
            int statusPollWindowMs = ResolveMovementSkillStatusPollWindowMs(postCastClickBlockMs, movementSkillName);
            _movementSkillStatusPollUntilTimestampMs = statusPollWindowMs > 0
                ? now + statusPollWindowMs
                : 0;
            _lastUsedMovementSkillEntry = statusPollWindowMs > 0
                ? movementSkillEntry
                : null;
            performanceMonitor.RecordClickInterval();
            DebugLog(() => $"[TryUseMovementSkillForOffscreenPathing] Cast movement skill '{movementSkillName}' with key '{boundKey}'");
            debugReason = $"Used movement skill '{movementSkillName}' with key '{boundKey}' (remainingNodes={remainingNodes}, minNodes={minimumNodes}, postCastClickBlockMs={postCastClickBlockMs}, statusPollWindowMs={statusPollWindowMs}).";
            return true;
        }

        private int ResolveMovementSkillPostCastClickBlockMsForCast(string? movementSkillInternalName)
        {
            int resolved = ResolveMovementSkillPostCastClickBlockMs(movementSkillInternalName);
            if (!IsShieldChargeMovementSkill(movementSkillInternalName))
                return resolved;

            return Math.Max(0, settings.OffscreenShieldChargePostCastClickDelayMs?.Value ?? MovementSkillShieldChargePostCastClickBlockMs);
        }

        private bool TryGetMovementSkillPostCastBlockState(long now, out string reason)
        {
            reason = string.Empty;

            if (IsMovementSkillPostCastClickBlocked(now, _movementSkillPostCastClickBlockUntilTimestampMs, out long remainingMs))
            {
                reason = $"timing window active ({remainingMs}ms remaining)";
                return true;
            }

            if (_movementSkillStatusPollUntilTimestampMs <= 0 || now > _movementSkillStatusPollUntilTimestampMs)
            {
                _movementSkillStatusPollUntilTimestampMs = 0;
                _lastUsedMovementSkillEntry = null;
                return false;
            }

            if (!TryResolveMovementSkillRuntimeState(_lastUsedMovementSkillEntry, out bool isUsing, out bool? allowedToCast, out bool? canBeUsed))
                return false;

            if (isUsing)
            {
                reason = "Skill.IsUsing=true";
                return true;
            }

            if (allowedToCast.HasValue && !allowedToCast.Value)
            {
                reason = "Skill.AllowedToCast=false";
                return true;
            }

            if (canBeUsed.HasValue && !canBeUsed.Value)
            {
                reason = "Skill.CanBeUsed=false";
                return true;
            }

            _movementSkillStatusPollUntilTimestampMs = 0;
            _lastUsedMovementSkillEntry = null;
            return false;
        }

        private int GetRemainingOffscreenPathNodeCount()
        {
            var path = pathfindingService.GetLatestGridPath();
            var player = gameController.Player;
            if (path == null || player == null)
                return 0;

            int nearestIndex = FindClosestPathIndexToPlayer(path, new PathfindingService.GridPoint((int)player.GridPosNum.X, (int)player.GridPosNum.Y));
            return CountRemainingPathNodes(path, nearestIndex);
        }

        internal static int CountRemainingPathNodes(IReadOnlyList<PathfindingService.GridPoint>? path, int nearestIndex)
        {
            if (path == null || path.Count == 0 || nearestIndex < 0)
                return 0;

            int clampedIndex = Math.Min(path.Count - 1, nearestIndex);
            int remaining = path.Count - (clampedIndex + 1);
            return Math.Max(0, remaining);
        }

        internal static bool ShouldAttemptMovementSkill(
            bool movementSkillsEnabled,
            bool builtPath,
            int remainingPathNodes,
            int minPathNodes,
            long now,
            long lastSkillUseTimestampMs,
            int recastDelayMs)
        {
            if (!movementSkillsEnabled || !builtPath)
                return false;

            if (remainingPathNodes < Math.Max(1, minPathNodes))
                return false;

            if (lastSkillUseTimestampMs <= 0 || recastDelayMs <= 0)
                return true;

            long elapsed = now - lastSkillUseTimestampMs;
            return elapsed >= recastDelayMs;
        }

        internal static bool IsMovementSkillPostCastClickBlocked(long now, long blockUntilTimestampMs, out long remainingMs)
        {
            remainingMs = 0;
            if (blockUntilTimestampMs <= 0)
                return false;

            long remaining = blockUntilTimestampMs - now;
            if (remaining <= 0)
                return false;

            remainingMs = remaining;
            return true;
        }

        internal static int ResolveMovementSkillStatusPollWindowMs(int postCastClickBlockMs, string? movementSkillInternalName)
        {
            if (postCastClickBlockMs <= 0 || string.IsNullOrWhiteSpace(movementSkillInternalName))
                return 0;

            MovementSkillTimingProfile profile = ResolveMovementSkillTimingProfile(movementSkillInternalName);
            if (profile.DisableStatusPoll)
                return 0;

            return postCastClickBlockMs + profile.StatusPollExtraMs;
        }

        private static bool TryResolveMovementSkillRuntimeState(object? entry, out bool isUsing, out bool? allowedToCast, out bool? canBeUsed)
        {
            isUsing = false;
            allowedToCast = null;
            canBeUsed = null;

            if (entry == null)
                return false;

            object skillObject = ResolveSkillObject(entry);

            bool foundAny = false;

            if (TryReadBoolSkillMember(skillObject, entry, out bool usingValue, s => s.IsUsing))
            {
                isUsing = usingValue;
                foundAny = true;
            }

            if (TryReadBoolSkillMember(skillObject, entry, out bool allowedValue, s => s.AllowedToCast))
            {
                allowedToCast = allowedValue;
                foundAny = true;
            }

            if (TryReadBoolSkillMember(skillObject, entry, out bool canUseValue, s => s.CanBeUsed))
            {
                canBeUsed = canUseValue;
                foundAny = true;
            }

            return foundAny;
        }

        private static object ResolveSkillObject(object entry)
        {
            if (TryGetDynamicValue(entry, s => s.Skill, out object? skill) && skill != null)
                return skill;

            if (TryGetDynamicValue(entry, s => s.ActorSkill, out object? actorSkill) && actorSkill != null)
                return actorSkill;

            return entry;
        }

        private static bool TryReadBoolSkillMember(object skillObject, object entry, out bool value, Func<dynamic, object?> accessor)
        {
            value = false;

            if (TryReadBool(accessor, skillObject, out value))
                return true;

            return TryReadBool(accessor, entry, out value);
        }

        private static bool TryReadBool(Func<dynamic, object?> accessor, object? source, out bool value)
        {
            return DynamicAccess.TryReadBool(source, accessor, out value);
        }

        internal static int ResolveMovementSkillPostCastClickBlockMs(string? movementSkillInternalName)
        {
            if (string.IsNullOrWhiteSpace(movementSkillInternalName))
                return 0;

            MovementSkillTimingProfile profile = ResolveMovementSkillTimingProfile(movementSkillInternalName);
            return profile.PostCastClickBlockMs;
        }

        private readonly record struct MovementSkillTimingProfile(int PostCastClickBlockMs, int StatusPollExtraMs, bool DisableStatusPoll);

        private static readonly (string Marker, MovementSkillTimingProfile Profile)[] MovementSkillTimingProfiles =
        [
            ("Frostblink", new MovementSkillTimingProfile(0, 0, true)),
            ("QuickDashGem", new MovementSkillTimingProfile(0, 0, true)),
            ("Dash", new MovementSkillTimingProfile(0, 0, true)),
            ("FlameDash", new MovementSkillTimingProfile(0, 0, true)),
            ("flame_dash", new MovementSkillTimingProfile(0, 0, true)),
            ("WitheringStep", new MovementSkillTimingProfile(0, 0, true)),
            ("withering_step", new MovementSkillTimingProfile(0, 0, true)),
            ("PhaseRun", new MovementSkillTimingProfile(0, 0, true)),
            ("phase_run", new MovementSkillTimingProfile(0, 0, true)),
            ("Ambush", new MovementSkillTimingProfile(0, 0, true)),
            ("ambush_player", new MovementSkillTimingProfile(0, 0, true)),
            ("ShieldCharge", new MovementSkillTimingProfile(MovementSkillShieldChargePostCastClickBlockMs, 0, true)),
            ("shield_charge", new MovementSkillTimingProfile(MovementSkillShieldChargePostCastClickBlockMs, 0, true)),
            ("LeapSlam", new MovementSkillTimingProfile(MovementSkillLeapSlamPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("leap_slam", new MovementSkillTimingProfile(MovementSkillLeapSlamPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("WhirlingBlades", new MovementSkillTimingProfile(MovementSkillWhirlingBladesPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("whirling_blades", new MovementSkillTimingProfile(MovementSkillWhirlingBladesPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("BlinkArrow", new MovementSkillTimingProfile(MovementSkillBlinkArrowPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("blink_arrow", new MovementSkillTimingProfile(MovementSkillBlinkArrowPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("MirrorArrow", new MovementSkillTimingProfile(MovementSkillBlinkArrowPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("mirror_arrow", new MovementSkillTimingProfile(MovementSkillBlinkArrowPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("ChargedDash", new MovementSkillTimingProfile(MovementSkillChargedDashPostCastClickBlockMs, MovementSkillExtendedStatusPollExtraMs, false)),
            ("charged_dash", new MovementSkillTimingProfile(MovementSkillChargedDashPostCastClickBlockMs, MovementSkillExtendedStatusPollExtraMs, false)),
            ("LightningWarp", new MovementSkillTimingProfile(MovementSkillLightningWarpPostCastClickBlockMs, MovementSkillExtendedStatusPollExtraMs, false)),
            ("lightning_warp", new MovementSkillTimingProfile(MovementSkillLightningWarpPostCastClickBlockMs, MovementSkillExtendedStatusPollExtraMs, false)),
            ("ConsecratedPath", new MovementSkillTimingProfile(MovementSkillConsecratedPathPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("consecrated_path", new MovementSkillTimingProfile(MovementSkillConsecratedPathPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("ChainHook", new MovementSkillTimingProfile(MovementSkillChainHookPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false)),
            ("chain_hook", new MovementSkillTimingProfile(MovementSkillChainHookPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false))
        ];

        private static MovementSkillTimingProfile ResolveMovementSkillTimingProfile(string? movementSkillInternalName)
        {
            if (string.IsNullOrWhiteSpace(movementSkillInternalName))
                return new MovementSkillTimingProfile(0, 0, true);

            string normalized = movementSkillInternalName.Trim();
            for (int i = 0; i < MovementSkillTimingProfiles.Length; i++)
            {
                (string marker, MovementSkillTimingProfile profile) = MovementSkillTimingProfiles[i];
                if (ContainsSkillMarker(normalized, marker))
                    return profile;
            }

            return new MovementSkillTimingProfile(MovementSkillDefaultPostCastClickBlockMs, MovementSkillDefaultStatusPollExtraMs, false);
        }

        private static bool ContainsSkillMarker(string skillName, string marker)
        {
            return skillName.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsShieldChargeMovementSkill(string? movementSkillInternalName)
        {
            if (string.IsNullOrWhiteSpace(movementSkillInternalName))
                return false;

            return ContainsSkillMarker(movementSkillInternalName, "ShieldCharge")
                || ContainsSkillMarker(movementSkillInternalName, "shield_charge");
        }

        private bool TryResolveMovementSkillCastPosition(Vector2 targetScreen, string targetPath, out Vector2 castPoint)
        {
            castPoint = default;

            RectangleF win = gameController.Window.GetWindowRectangleTimeCache;
            if (win.Width <= 0 || win.Height <= 0)
                return false;

            float insetX = Math.Max(24f, win.Width * 0.12f);
            float insetY = Math.Max(24f, win.Height * 0.12f);
            float safeLeft = win.Left + insetX;
            float safeRight = win.Right - insetX;
            float safeTop = win.Top + insetY;
            float safeBottom = win.Bottom - insetY;

            Vector2 center = new(win.X + (win.Width * 0.5f), win.Y + (win.Height * 0.5f));
            Vector2 direction = targetScreen - center;
            float lenSq = (direction.X * direction.X) + (direction.Y * direction.Y);
            if (lenSq < 1f)
                return false;

            for (float t = 1.65f; t >= 0.70f; t -= 0.1f)
            {
                Vector2 candidate = center + (direction * t);
                if (!IsInsideWindow(win, candidate))
                    continue;
                if (candidate.X < safeLeft || candidate.X > safeRight || candidate.Y < safeTop || candidate.Y > safeBottom)
                    continue;
                if (!pointIsInClickableArea(candidate, targetPath))
                    continue;

                castPoint = candidate;
                return true;
            }

            Vector2 clamped = new(
                Math.Clamp(targetScreen.X, safeLeft, safeRight),
                Math.Clamp(targetScreen.Y, safeTop, safeBottom));

            if (pointIsInClickableArea(clamped, targetPath))
            {
                castPoint = clamped;
                return true;
            }

            return false;
        }

        private bool TryFindReadyMovementSkillKey(out Keys boundKey, out string movementSkillName, out object? matchedSkillEntry, out string diagnostic)
        {
            boundKey = Keys.None;
            movementSkillName = string.Empty;
            matchedSkillEntry = null;
            diagnostic = string.Empty;

            if (!TryGetSkillBarEntries(out IReadOnlyList<object?> skillEntries))
            {
                diagnostic = "SkillBar/Skills collection unavailable.";
                return false;
            }

            if (skillEntries.Count == 0)
            {
                diagnostic = "SkillBar contains zero entries.";
                return false;
            }

            int nullEntries = 0;
            int nonMovementEntries = 0;
            int cooldownEntries = 0;
            int missingKeyEntries = 0;
            int unsupportedKeyEntries = 0;

            for (int i = 0; i < skillEntries.Count; i++)
            {
                object? entry = skillEntries[i];
                if (entry == null)
                {
                    nullEntries++;
                    continue;
                }

                if (!TryResolveMovementSkillInternalName(entry, out string internalName))
                {
                    nonMovementEntries++;
                    continue;
                }

                if (IsSkillEntryOnCooldown(entry))
                {
                    cooldownEntries++;
                    continue;
                }

                if (!TryResolveSkillKeyText(entry, out string keyText))
                {
                    missingKeyEntries++;
                    continue;
                }

                if (!TryMapKeyTextToKeys(keyText, out Keys parsedKey))
                {
                    unsupportedKeyEntries++;
                    continue;
                }

                boundKey = parsedKey;
                movementSkillName = internalName;
                matchedSkillEntry = entry;
                diagnostic = $"Matched movement skill '{internalName}' on key text '{keyText}'.";
                return true;
            }

            diagnostic = $"entries={skillEntries.Count}, null={nullEntries}, nonMovement={nonMovementEntries}, onCooldown={cooldownEntries}, missingKeyText={missingKeyEntries}, unsupportedOrMouseKey={unsupportedKeyEntries}";
            return false;
        }

        private bool TryGetSkillBarEntries(out IReadOnlyList<object?> entries)
        {
            entries = [];

            object? skillBar = gameController?.IngameState?.IngameUi?.SkillBar;
            if (skillBar == null)
                return false;

            if (!TryGetDynamicValue(skillBar, s => s.Skills, out object? skillsCollection))
                return false;

            if (skillsCollection is not IEnumerable enumerable)
                return false;

            var list = new List<object?>();
            foreach (object? entry in enumerable)
            {
                list.Add(entry);
            }

            entries = list;
            return entries.Count > 0;
        }

        private bool TryResolveMovementSkillInternalName(object entry, out string internalName)
        {
            internalName = string.Empty;

            object skillObject = ResolveSkillObject(entry);

            if (TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.InternalName)
                || TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.Name)
                || TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.Id)
                || TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.SkillId)
                || TryResolveMovementSkillNameCandidate(skillObject, entry, out internalName, s => s.MetadataId))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveMovementSkillNameCandidate(object skillObject, object entry, out string internalName, Func<dynamic, object?> accessor)
        {
            internalName = string.Empty;

            if (!TryReadString(accessor, skillObject, out string candidate)
                && !TryReadString(accessor, entry, out candidate))
            {
                return false;
            }

            if (!IsMovementSkillInternalName(candidate))
                return false;

            internalName = candidate;
            return true;
        }

        internal static bool IsMovementSkillInternalName(string? skillInternalName)
        {
            if (string.IsNullOrWhiteSpace(skillInternalName))
                return false;

            string normalized = skillInternalName.Trim();
            for (int i = 0; i < MovementSkillInternalNameMarkers.Length; i++)
            {
                if (normalized.IndexOf(MovementSkillInternalNameMarkers[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool IsSkillEntryOnCooldown(object entry)
        {
            object skillObject = ResolveSkillObject(entry);
            return TryReadBoolSkillMember(skillObject, entry, out bool onCooldown, s => s.IsOnCooldown)
                ? onCooldown
                : TryReadBoolSkillMember(skillObject, entry, out onCooldown, s => s.OnCooldown)
                    ? onCooldown
                    : TryReadBoolSkillMember(skillObject, entry, out onCooldown, s => s.HasCooldown) && onCooldown;
        }

        private static bool TryResolveSkillKeyText(object entry, out string keyText)
        {
            keyText = string.Empty;

            object skillObject = ResolveSkillObject(entry);

            if (TryReadStringSkillMember(skillObject, entry, out keyText, s => s.KeyText)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.SkillBarText)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.Key)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.Bind)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.Hotkey)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.InputText)
                || TryReadStringSkillMember(skillObject, entry, out keyText, s => s.SlotText))
            {
                return true;
            }

            if (TryResolveSkillKeyTextFromKnownChildPath(entry, out string childPathText))
            {
                keyText = childPathText;
                return true;
            }

            return false;
        }

        // SkillBar entries can expose keybind text only via the UI tree:
        // Skills[i].Child(0).Child(0).Child(0).Child(1).Text
        private static bool TryResolveSkillKeyTextFromKnownChildPath(object entry, out string keyText)
        {
            keyText = string.Empty;

            object? node = entry;
            int[] childIndices = [0, 0, 0, 1];
            for (int i = 0; i < childIndices.Length; i++)
            {
                if (!TryGetChildNode(node, childIndices[i], out node) || node == null)
                    return false;
            }

            return TryReadNodeText(node, out keyText);
        }

        private static bool TryGetChildNode(object? node, int index, out object? child)
        {
            child = null;
            if (node == null || index < 0)
                return false;

            if (node is Element element)
            {
                child = element.GetChildAtIndex(index);
                return child != null;
            }

            if (TryGetDynamicValue(node, n => n.Child(index), out object? dynamicChild) && dynamicChild != null)
            {
                child = dynamicChild;
                return true;
            }

            if (TryGetDynamicValue(node, n => n.Children, out object? childrenObj) && childrenObj is IList list && index < list.Count)
            {
                child = list[index];
                return child != null;
            }

            return false;
        }

        private static bool TryReadNodeText(object node, out string text)
        {
            text = string.Empty;
            if (node == null)
                return false;

            if (node is Element element)
            {
                string value = element.GetText(256) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    text = value.Trim();
                    return true;
                }
            }

            return TryReadString(n => n.Text, node, out text)
                || TryReadString(n => n.Label, node, out text)
                || TryReadString(n => n.KeyText, node, out text);
        }

        internal static bool TryMapKeyTextToKeys(string? keyText, out Keys key)
        {
            key = Keys.None;
            if (string.IsNullOrWhiteSpace(keyText))
                return false;

            string normalized = keyText.Trim().ToUpperInvariant();
            string[] modifierSplit = normalized.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (modifierSplit.Length > 0)
                normalized = modifierSplit[modifierSplit.Length - 1];

            normalized = normalized.Replace(" ", string.Empty);

            if (normalized is "LMB" or "RMB" or "MMB" or "MOUSE4" or "MOUSE5")
                return false;

            if (normalized.Length == 1)
            {
                char c = normalized[0];
                if (c >= 'A' && c <= 'Z')
                {
                    key = Keys.A + (c - 'A');
                    return true;
                }

                if (c >= '0' && c <= '9')
                {
                    key = Keys.D0 + (c - '0');
                    return true;
                }
            }

            if (normalized.StartsWith("F", StringComparison.Ordinal) && int.TryParse(normalized[1..], out int fNum) && fNum >= 1 && fNum <= 24)
            {
                key = Keys.F1 + (fNum - 1);
                return true;
            }

            return Enum.TryParse(normalized, ignoreCase: true, out key);
        }

        private static bool TryReadStringSkillMember(object skillObject, object entry, out string value, Func<dynamic, object?> accessor)
        {
            value = string.Empty;

            if (TryReadString(accessor, entry, out value))
                return true;

            return TryReadString(accessor, skillObject, out value);
        }

        private static bool TryReadString(Func<dynamic, object?> accessor, object? source, out string value)
        {
            return DynamicAccess.TryReadString(source, accessor, out value);
        }

        private static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
        {
            return DynamicAccess.TryGetDynamicValue(source, accessor, out value);
        }

        private bool TryHandleStickyOffscreenTarget(Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (!TryResolveStickyOffscreenTarget(out Entity? stickyTarget) || stickyTarget == null)
                return false;

            if (TryClickStickyTargetIfPossible(stickyTarget, windowTopLeft, allLabels))
                return true;

            _ = TryWalkTowardOffscreenTarget(stickyTarget);
            return true;
        }

        private bool TryClickStickyTargetIfPossible(Entity stickyTarget, Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (ShrineService.IsShrine(stickyTarget))
            {
                var shrineScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(stickyTarget.PosNum);
                Vector2 shrinePos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
                string path = stickyTarget.Path ?? string.Empty;
                if (!IsClickableInEitherSpace(shrinePos, path))
                    return false;

                bool clickedShrine = PerformLabelClick(shrinePos, null, gameController);
                if (clickedShrine)
                {
                    ClearStickyOffscreenTarget();
                    shrineService.InvalidateCache();
                }

                return clickedShrine;
            }

            LabelOnGround? stickyLabel = FindVisibleLabelForEntity(stickyTarget, allLabels);
            if (stickyLabel == null)
                return false;

            if (ShouldSuppressPathfindingLabel(stickyLabel))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            string? mechanicId = labelFilterService.GetMechanicIdForLabel(stickyLabel);
            if (string.IsNullOrWhiteSpace(mechanicId))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            if (!inputHandler.TryCalculateClickPosition(
                stickyLabel,
                windowTopLeft,
                allLabels,
                point => IsClickableInEitherSpace(point, stickyTarget.Path ?? string.Empty),
                out Vector2 clickPos))
            {
                return false;
            }

            bool clickedLabel = ShouldUseHoldClickForSettlersMechanic(mechanicId)
                ? PerformLabelHoldClick(clickPos, stickyLabel.Label, gameController, holdDurationMs: 0)
                : PerformLabelClick(clickPos, stickyLabel.Label, gameController);
            if (clickedLabel)
            {
                ClearStickyOffscreenTarget();
            }

            return clickedLabel;
        }

        private void SetStickyOffscreenTarget(Entity target)
        {
            _stickyOffscreenTargetAddress = target.Address;
        }

        private void ClearStickyOffscreenTarget()
        {
            _stickyOffscreenTargetAddress = 0;
        }

        private bool TryResolveStickyOffscreenTarget(out Entity? target)
        {
            target = null;

            if (_stickyOffscreenTargetAddress == 0)
                return false;

            target = FindEntityByAddress(_stickyOffscreenTargetAddress);
            if (target == null || !target.IsValid || target.IsHidden)
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            if (ShrineService.IsShrine(target) && !ShrineService.IsClickableShrineCandidate(target))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            return true;
        }

        private Entity? FindEntityByAddress(long address)
        {
            if (address == 0 || gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return null;

            foreach (var kv in gameController.EntityListWrapper.ValidEntitiesByType)
            {
                var entities = kv.Value;
                if (entities == null)
                    continue;

                for (int i = 0; i < entities.Count; i++)
                {
                    Entity entity = entities[i];
                    if (entity != null && IsSameEntityAddress(address, entity.Address))
                        return entity;
                }
            }

            return null;
        }

        private bool IsStickyTarget(Entity? entity)
        {
            return entity != null && IsSameEntityAddress(_stickyOffscreenTargetAddress, entity.Address);
        }

        internal static bool IsSameEntityAddress(long leftAddress, long rightAddress)
        {
            return leftAddress != 0 && leftAddress == rightAddress;
        }

        private void PublishOffscreenMovementDebug(
            Entity target,
            string targetPath,
            bool builtPath,
            bool resolvedFromPath,
            bool resolvedClickPoint,
            Vector2 targetScreen,
            Vector2 clickScreen,
            string stage,
            string movementSkillDebug = "")
        {
            var player = gameController.Player;
            Vector2 playerGrid = player != null
                ? new Vector2(player.GridPosNum.X, player.GridPosNum.Y)
                : default;
            Vector2 targetGrid = new(target.GridPosNum.X, target.GridPosNum.Y);
            RectangleF win = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 center = new(win.X + (win.Width * 0.5f), win.Y + (win.Height * 0.5f));

            pathfindingService.SetLatestOffscreenMovementDebug(new PathfindingService.OffscreenMovementDebugSnapshot(
                HasData: true,
                Stage: stage,
                TargetPath: targetPath,
                BuiltPath: builtPath,
                ResolvedFromPath: resolvedFromPath,
                ResolvedClickPoint: resolvedClickPoint,
                WindowCenter: center,
                TargetScreen: targetScreen,
                ClickScreen: clickScreen,
                PlayerGrid: playerGrid,
                TargetGrid: targetGrid,
                MovementSkillDebug: movementSkillDebug ?? string.Empty,
                TimestampMs: Environment.TickCount64));
        }

        private bool TryResolveDirectionalWalkClickPosition(Vector2 targetScreen, string targetPath, out Vector2 clickPos)
        {
            clickPos = default;

            RectangleF win = gameController.Window.GetWindowRectangleTimeCache;
            if (win.Width <= 0 || win.Height <= 0)
                return false;

            float insetX = Math.Max(28f, win.Width * 0.10f);
            float insetY = Math.Max(28f, win.Height * 0.10f);
            float safeLeft = win.Left + insetX;
            float safeRight = win.Right - insetX;
            float safeTop = win.Top + insetY;
            float safeBottom = win.Bottom - insetY;

            Vector2 center = new(win.X + (win.Width * 0.5f), win.Y + (win.Height * 0.5f));
            Vector2 direction = targetScreen - center;
            float lenSq = (direction.X * direction.X) + (direction.Y * direction.Y);
            if (lenSq < 1f)
                return false;

            for (float t = 1.05f; t >= 0.30f; t -= 0.1f)
            {
                Vector2 candidate = center + (direction * t);
                if (!IsInsideWindow(win, candidate))
                    continue;
                if (candidate.X < safeLeft || candidate.X > safeRight || candidate.Y < safeTop || candidate.Y > safeBottom)
                    continue;
                if (!pointIsInClickableArea(candidate, targetPath))
                    continue;

                clickPos = candidate;
                return true;
            }

            Vector2 clamped = new(
                Math.Clamp(targetScreen.X, safeLeft, safeRight),
                Math.Clamp(targetScreen.Y, safeTop, safeBottom));

            if (pointIsInClickableArea(clamped, targetPath))
            {
                clickPos = clamped;
                return true;
            }

            return false;
        }

        private bool TryResolveOffscreenTargetScreenPointFromPath(out Vector2 targetScreen)
        {
            targetScreen = default;

            var player = gameController.Player;
            if (player == null)
                return false;

            var path = pathfindingService.GetLatestGridPath();
            if (path == null || path.Count < 2)
                return false;

            var playerGrid = new PathfindingService.GridPoint((int)player.GridPosNum.X, (int)player.GridPosNum.Y);
            int nearestIndex = FindClosestPathIndexToPlayer(path, playerGrid);
            if (nearestIndex < 0)
                return false;

            // This avoids left-right zig-zag when A* alternates between horizontal/vertical steps.
            if (!TryGetSmoothedPathDirection(path, playerGrid, nearestIndex, out float deltaX, out float deltaY))
                return false;

            RectangleF win = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 center = new(win.X + (win.Width * 0.5f), win.Y + (win.Height * 0.5f));
            float directionRadius = Math.Min(win.Width, win.Height) * 0.30f;

            return TryComputeGridDirectionPoint(center, deltaX, deltaY, directionRadius, out targetScreen);
        }

        internal static bool TryGetSmoothedPathDirection(
            IReadOnlyList<PathfindingService.GridPoint> path,
            PathfindingService.GridPoint playerGrid,
            int nearestIndex,
            out float deltaX,
            out float deltaY)
        {
            deltaX = 0f;
            deltaY = 0f;

            if (path == null || path.Count < 2 || nearestIndex < 0)
                return false;

            int startIndex = Math.Min(path.Count - 1, nearestIndex + 1);
            int endIndex = Math.Min(path.Count - 1, nearestIndex + 8);
            if (endIndex < startIndex)
                return false;

            float weightedDx = 0f;
            float weightedDy = 0f;
            float totalWeight = 0f;

            for (int i = startIndex; i <= endIndex; i++)
            {
                var node = path[i];
                float dx = node.X - playerGrid.X;
                float dy = node.Y - playerGrid.Y;

                if (Math.Abs(dx) + Math.Abs(dy) < 0.001f)
                    continue;

                float weight = (i - startIndex) + 1f;
                weightedDx += dx * weight;
                weightedDy += dy * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
                return false;

            deltaX = weightedDx / totalWeight;
            deltaY = weightedDy / totalWeight;
            return Math.Abs(deltaX) + Math.Abs(deltaY) >= 0.001f;
        }

        private bool TryResolveOffscreenTargetScreenPoint(Entity target, out Vector2 targetScreen)
        {
            targetScreen = default;

            RectangleF win = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 center = new(win.X + (win.Width * 0.5f), win.Y + (win.Height * 0.5f));
            float directionRadius = Math.Min(win.Width, win.Height) * 0.30f;

            if (target.Type == ExileCore.Shared.Enums.EntityType.WorldItem)
            {
                if (TryComputeGridDirectionPoint(center, GetGridDeltaX(target), GetGridDeltaY(target), directionRadius, out targetScreen))
                    return true;
            }

            var targetScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(target.PosNum);
            Vector2 projected = new(targetScreenRaw.X, targetScreenRaw.Y);

            if (IsFinite(projected) && !IsNearCorner(projected, win))
            {
                targetScreen = projected;
                return true;
            }

            // Fallback for any unstable projection: derive direction from grid delta.
            return TryComputeGridDirectionPoint(center, GetGridDeltaX(target), GetGridDeltaY(target), directionRadius, out targetScreen);
        }

        private float GetGridDeltaX(Entity target)
        {
            var player = gameController.Player;
            if (player == null)
                return 0f;

            return target.GridPosNum.X - player.GridPosNum.X;
        }

        private float GetGridDeltaY(Entity target)
        {
            var player = gameController.Player;
            if (player == null)
                return 0f;

            return target.GridPosNum.Y - player.GridPosNum.Y;
        }

        internal static bool TryComputeGridDirectionPoint(Vector2 center, float deltaGridX, float deltaGridY, float radius, out Vector2 point)
        {
            Vector2 dir = new(deltaGridX - deltaGridY, -(deltaGridX + deltaGridY) * 0.65f);
            float lenSq = (dir.X * dir.X) + (dir.Y * dir.Y);
            if (lenSq < 0.001f || radius <= 0f)
            {
                point = default;
                return false;
            }

            float invLen = 1f / MathF.Sqrt(lenSq);
            Vector2 norm = new(dir.X * invLen, dir.Y * invLen);
            point = center + (norm * radius);
            return true;
        }

        internal static int FindClosestPathIndexToPlayer(IReadOnlyList<PathfindingService.GridPoint> path, PathfindingService.GridPoint playerGrid)
        {
            if (path == null || path.Count == 0)
                return -1;

            int bestIndex = -1;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                int dx = path[i].X - playerGrid.X;
                int dy = path[i].Y - playerGrid.Y;
                int manhattan = Math.Abs(dx) + Math.Abs(dy);
                if (manhattan < bestDistance)
                {
                    bestDistance = manhattan;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static bool IsFinite(Vector2 p)
        {
            return !float.IsNaN(p.X) && !float.IsInfinity(p.X) && !float.IsNaN(p.Y) && !float.IsInfinity(p.Y);
        }

        private static bool IsNearCorner(Vector2 p, RectangleF win)
        {
            float marginX = win.Width * 0.05f;
            float marginY = win.Height * 0.05f;

            bool nearLeft = p.X <= win.Left + marginX;
            bool nearRight = p.X >= win.Right - marginX;
            bool nearTop = p.Y <= win.Top + marginY;
            bool nearBottom = p.Y >= win.Bottom - marginY;

            return (nearLeft || nearRight) && (nearTop || nearBottom);
        }

        private static bool IsInsideWindow(RectangleF win, Vector2 p)
        {
            return p.X >= win.Left && p.X <= win.Right && p.Y >= win.Top && p.Y <= win.Bottom;
        }

        private Entity? ResolveNearestOffscreenWalkTarget()
        {
            if (gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return null;

            if (TryResolveStickyOffscreenTarget(out Entity? stickyTarget) && stickyTarget != null)
                return stickyTarget;

            int maxDistance = GetOffscreenPathfindingTargetSearchDistance();

            Entity? labelBackedTarget = ResolveNearestOffscreenLabelBackedTarget(maxDistance, out string? labelMechanicId);
            Entity? shrineTarget = ResolveNearestOffscreenShrineTarget(maxDistance);
            Entity? areaTransitionTarget = ResolveNearestOffscreenAreaTransitionTarget(maxDistance, out string? areaTransitionMechanicId);

            if (labelBackedTarget == null && shrineTarget == null && areaTransitionTarget == null)
                return null;

            RefreshMechanicPriorityCaches();

            Entity? best = null;
            string? bestMechanicId = null;
            MechanicRank bestRank = default;
            bool hasBest = false;

            PromoteOffscreenTargetCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, labelBackedTarget, labelMechanicId);
            PromoteOffscreenTargetCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, shrineTarget, ShrineMechanicId);
            PromoteOffscreenTargetCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, areaTransitionTarget, areaTransitionMechanicId);

            return best;
        }

        private static bool IsSettlersMechanicId(string? mechanicId)
        {
            return !string.IsNullOrWhiteSpace(mechanicId)
                && mechanicId.StartsWith("settlers-", StringComparison.OrdinalIgnoreCase);
        }

        private string BuildNoLabelDebugSummary(IReadOnlyList<LabelOnGround>? allLabels)
        {
            int labelCount = allLabels?.Count ?? 0;
            string sourceSummary = BuildLabelSourceDebugSummary(allLabels);
            if (labelCount <= 0)
                return $"{sourceSummary} | selection:r:0-0 t:0";

            var summary = labelFilterService.GetSelectionDebugSummary(allLabels, 0, labelCount);
            return $"{sourceSummary} | selection:{summary.ToCompactString()}";
        }

        private string BuildLabelRangeRejectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int start, int endExclusive, int examined)
        {
            int maxCount = Math.Max(0, endExclusive - start);
            var summary = labelFilterService.GetSelectionDebugSummary(allLabels, start, maxCount);
            return $"range:{start}-{endExclusive} examined:{examined} | {summary.ToCompactString()}";
        }

        private string BuildLabelSourceDebugSummary(IReadOnlyList<LabelOnGround>? cachedLabelSnapshot)
        {
            int cachedCount = cachedLabelSnapshot?.Count ?? 0;
            int visibleCount = 0;
            try
            {
                visibleCount = gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible?.Count ?? 0;
            }
            catch
            {
                visibleCount = 0;
            }

            bool groundVisible = groundItemsVisible();
            return $"visible:{visibleCount} cached:{cachedCount} groundVisible:{groundVisible}";
        }

        private void PublishClickFlowDebugStage(string stage, string notes, string? mechanicId = null)
        {
            SetLatestClickDebug(new ClickDebugSnapshot(
                HasData: true,
                Stage: stage,
                MechanicId: mechanicId ?? string.Empty,
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
                Notes: notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        }

        private void PublishLabelClickDebug(
            string stage,
            string? mechanicId,
            LabelOnGround label,
            Vector2 resolvedClickPos,
            bool resolved,
            string notes)
        {
            Entity? entity = label?.ItemOnGround;
            if (entity == null)
                return;

            string entityPath = entity.Path ?? string.Empty;
            var worldScreenRawVec = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            Vector2 worldScreenRaw = new(worldScreenRawVec.X, worldScreenRawVec.Y);

            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 worldScreenAbsolute = worldScreenRaw + windowTopLeft;

            bool centerInWindow = IsInsideWindowInEitherSpace(worldScreenAbsolute);
            bool centerClickable = IsClickableInEitherSpace(worldScreenAbsolute, entityPath);
            bool resolvedInWindow = IsInsideWindowInEitherSpace(resolvedClickPos);
            bool resolvedClickable = IsClickableInEitherSpace(resolvedClickPos, entityPath);

            SetLatestClickDebug(new ClickDebugSnapshot(
                HasData: true,
                Stage: stage,
                MechanicId: mechanicId ?? string.Empty,
                EntityPath: entityPath,
                Distance: entity.DistancePlayer,
                WorldScreenRaw: worldScreenRaw,
                WorldScreenAbsolute: worldScreenAbsolute,
                ResolvedClickPoint: resolvedClickPos,
                Resolved: resolved,
                CenterInWindow: centerInWindow,
                CenterClickable: centerClickable,
                ResolvedInWindow: resolvedInWindow,
                ResolvedClickable: resolvedClickable,
                Notes: notes,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));
        }

        private Entity? ResolveNearestOffscreenSettlersOreTarget(int maxDistance, out string? selectedMechanicId)
        {
            selectedMechanicId = null;

            if (!settings.ClickSettlersOre.Value || gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return null;

            Entity? best = null;
            float bestDistance = float.MaxValue;
            var labelEntityAddresses = CollectGroundLabelEntityAddresses();

            foreach (var kv in gameController.EntityListWrapper.ValidEntitiesByType)
            {
                var entities = kv.Value;
                if (entities == null)
                    continue;

                for (int i = 0; i < entities.Count; i++)
                {
                    Entity entity = entities[i];
                    if (entity == null || !entity.IsValid || entity.IsHidden)
                        continue;
                    if (entity.DistancePlayer > maxDistance)
                        continue;
                    if (!IsBackedByGroundLabel(entity.Address, labelEntityAddresses))
                        continue;

                    string path = entity.Path ?? string.Empty;
                    if (!LabelFilterService.TryGetSettlersOreMechanicId(path, out string? mechanicId) || string.IsNullOrWhiteSpace(mechanicId))
                        continue;

                    var screenRaw = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
                    Vector2 screen = new(screenRaw.X, screenRaw.Y);
                    if (IsInsideWindowInEitherSpace(screen))
                        continue;

                    if (IsClickableInEitherSpace(screen, path))
                        continue;

                    float d = entity.DistancePlayer;
                    if (d >= bestDistance)
                        continue;

                    bestDistance = d;
                    selectedMechanicId = mechanicId;
                    best = entity;
                }
            }

            return best;
        }

        private HashSet<long> CollectGroundLabelEntityAddresses()
        {
            var addresses = new HashSet<long>();

            try
            {
                var labels = gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels;
                if (labels == null || labels.Count == 0)
                    return addresses;

                for (int i = 0; i < labels.Count; i++)
                {
                    long address = labels[i]?.ItemOnGround?.Address ?? 0;
                    if (address != 0)
                        addresses.Add(address);
                }
            }
            catch
            {
            }

            return addresses;
        }

        internal static bool IsBackedByGroundLabel(long entityAddress, IReadOnlySet<long>? labelEntityAddresses)
        {
            return entityAddress != 0
                && labelEntityAddresses != null
                && labelEntityAddresses.Contains(entityAddress);
        }

        private bool IsInsideWindowInEitherSpace(Vector2 point)
        {
            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            return IsInsideWindowInEitherSpace(point, windowArea);
        }

        internal static bool IsInsideWindowInEitherSpace(Vector2 point, RectangleF windowArea)
        {
            bool inClientSpace = point.X >= 0f
                && point.Y >= 0f
                && point.X <= windowArea.Width
                && point.Y <= windowArea.Height;

            bool inScreenSpace = point.X >= windowArea.Left
                && point.Y >= windowArea.Top
                && point.X <= windowArea.Right
                && point.Y <= windowArea.Bottom;

            return inClientSpace || inScreenSpace;
        }

        private void PromoteOffscreenTargetCandidate(
            ref Entity? best,
            ref string? bestMechanicId,
            ref MechanicRank bestRank,
            ref bool hasBest,
            Entity? candidate,
            string? mechanicId)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(mechanicId))
                return;

            MechanicRank rank = BuildMechanicRank(candidate.DistancePlayer, mechanicId);
            if (!hasBest || CompareMechanicRanks(rank, bestRank) < 0)
            {
                best = candidate;
                bestMechanicId = mechanicId;
                bestRank = rank;
                hasBest = true;
            }
        }

        private Entity? ResolveNearestOffscreenAreaTransitionTarget(int maxDistance, out string? selectedMechanicId)
        {
            selectedMechanicId = null;

            if ((!settings.ClickAreaTransitions.Value && !settings.ClickLabyrinthTrials.Value)
                || gameController?.EntityListWrapper?.ValidEntitiesByType == null)
            {
                return null;
            }

            Entity? best = null;
            string? bestMechanicId = null;
            float bestDistance = float.MaxValue;

            foreach (var kv in gameController.EntityListWrapper.ValidEntitiesByType)
            {
                var entities = kv.Value;
                if (entities == null)
                    continue;

                for (int i = 0; i < entities.Count; i++)
                {
                    Entity entity = entities[i];
                    if (entity == null || !entity.IsValid || entity.IsHidden)
                        continue;
                    if (entity.DistancePlayer > maxDistance)
                        continue;

                    string path = entity.Path ?? string.Empty;
                    string? mechanicId = GetAreaTransitionMechanicIdForPath(
                        settings.ClickAreaTransitions.Value,
                        settings.ClickLabyrinthTrials.Value,
                        entity.Type,
                        path);
                    if (string.IsNullOrWhiteSpace(mechanicId))
                        continue;

                    var screenRaw = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
                    Vector2 screen = new(screenRaw.X, screenRaw.Y);
                    if (IsClickableInEitherSpace(screen, path))
                        continue;

                    float d = entity.DistancePlayer;
                    if (d >= bestDistance)
                        continue;

                    bestDistance = d;
                    best = entity;
                    bestMechanicId = mechanicId;
                }
            }

            selectedMechanicId = bestMechanicId;
            return best;
        }

        internal static string? GetAreaTransitionMechanicIdForPath(
            bool clickAreaTransitions,
            bool clickLabyrinthTrials,
            ExileCore.Shared.Enums.EntityType type,
            string path)
        {
            bool isAreaTransition = type == ExileCore.Shared.Enums.EntityType.AreaTransition
                || path.Contains("AreaTransition", StringComparison.OrdinalIgnoreCase);
            if (!isAreaTransition)
                return null;

            if (IsLabyrinthTrialTransitionPath(path))
                return clickLabyrinthTrials ? LabyrinthTrialsMechanicId : null;

            return clickAreaTransitions ? AreaTransitionsMechanicId : null;
        }

        private static bool IsLabyrinthTrialTransitionPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return path.Contains("LabyrinthTrial", StringComparison.OrdinalIgnoreCase)
                || path.Contains("Labyrinth/Trial", StringComparison.OrdinalIgnoreCase)
                || path.Contains("TrialPortal", StringComparison.OrdinalIgnoreCase);
        }

        private Entity? ResolveNearestOffscreenShrineTarget(int maxDistance)
        {
            if (!settings.ClickShrines.Value || gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return null;

            Entity? best = null;
            float bestDistance = float.MaxValue;

            foreach (var kv in gameController.EntityListWrapper.ValidEntitiesByType)
            {
                var entities = kv.Value;
                if (entities == null)
                    continue;

                for (int i = 0; i < entities.Count; i++)
                {
                    Entity entity = entities[i];
                    if (!ShrineService.IsClickableShrineCandidate(entity))
                        continue;
                    if (entity.DistancePlayer > maxDistance)
                        continue;

                    var screenRaw = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
                    Vector2 screen = new(screenRaw.X, screenRaw.Y);
                    string path = entity.Path ?? string.Empty;
                    if (IsClickableInEitherSpace(screen, path))
                        continue;

                    float d = entity.DistancePlayer;
                    if (d >= bestDistance)
                        continue;

                    bestDistance = d;
                    best = entity;
                }
            }

            return best;
        }

        private Entity? ResolveNearestOffscreenLabelBackedTarget(int maxDistance)
        {
            return ResolveNearestOffscreenLabelBackedTarget(maxDistance, out _);
        }

        private Entity? ResolveNearestOffscreenLabelBackedTarget(int maxDistance, out string? selectedMechanicId)
        {
            selectedMechanicId = null;

            var labels = GetLabelsForOffscreenSelection();
            if (labels == null || labels.Count == 0)
                return null;

            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);

            RefreshMechanicPriorityCaches();

            Entity? best = null;
            string? bestMechanicId = null;
            MechanicRank bestRank = default;
            bool hasBestRank = false;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                Entity? entity = label?.ItemOnGround;
                if (label == null || entity == null)
                    continue;
                if (!entity.IsValid || entity.IsHidden)
                    continue;
                if (entity.DistancePlayer > maxDistance)
                    continue;
                if (ShouldSuppressPathfindingLabel(label))
                    continue;

                string? mechanicId = labelFilterService.GetMechanicIdForLabel(label);
                if (string.IsNullOrWhiteSpace(mechanicId))
                    continue;

                if (!ShouldContinuePathfindingToLabel(label, entity, labels, windowTopLeft))
                    continue;

                var rank = BuildMechanicRank(entity.DistancePlayer, mechanicId);
                if (hasBestRank && CompareMechanicRanks(rank, bestRank) >= 0)
                    continue;

                best = entity;
                bestMechanicId = mechanicId;
                bestRank = rank;
                hasBestRank = true;
            }

            selectedMechanicId = bestMechanicId;
            return best;
        }

        private bool ShouldSuppressPathfindingLabel(LabelOnGround label)
        {
            return ShouldSuppressPathfindingLabelCore(
                ShouldSuppressLeverClick(label),
                ShouldSuppressInactiveUltimatumLabel(label));
        }

        internal static bool ShouldSuppressPathfindingLabelCore(bool suppressLeverClick, bool suppressInactiveUltimatum)
        {
            return suppressLeverClick || suppressInactiveUltimatum;
        }

        private bool ShouldContinuePathfindingToLabel(
            LabelOnGround label,
            Entity entity,
            IReadOnlyList<LabelOnGround>? allLabels,
            Vector2 windowTopLeft)
        {
            if (!TryGetLabelRect(label, out RectangleF rect))
                return true;

            string path = entity.Path ?? string.Empty;
            bool labelInWindow = IsInsideWindowInEitherSpace(rect.Center);
            bool labelClickable = IsClickableInEitherSpace(rect.Center, path);

            if (!labelInWindow || !labelClickable)
                return true;

            bool clickPointResolvable = allLabels != null
                && inputHandler.TryCalculateClickPosition(
                    label,
                    windowTopLeft,
                    allLabels,
                    point => IsClickableInEitherSpace(point, path),
                    out _);

            return ShouldContinuePathfindingWhenLabelActionable(labelInWindow, labelClickable, clickPointResolvable);
        }

        internal static bool ShouldContinuePathfindingWhenLabelClickable(bool labelClickable)
        {
            return !labelClickable;
        }

        internal static bool ShouldContinuePathfindingWhenLabelActionable(bool labelInWindow, bool labelClickable, bool clickPointResolvable)
        {
            return !(labelInWindow && labelClickable && clickPointResolvable);
        }

        internal static bool ShouldPathfindToEntityAfterClickPointResolveFailure(
            bool walkTowardOffscreenLabelsEnabled,
            bool hasEntity,
            string? mechanicId)
        {
            if (!walkTowardOffscreenLabelsEnabled || !hasEntity || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            return true;
        }

        internal static bool ShouldAllowSettlersCandidateWithoutGroundLabel(bool hasGroundLabel, bool isVerisiumPath)
        {
            return hasGroundLabel || isVerisiumPath;
        }

        private static int GetOffscreenPathfindingTargetSearchDistance()
        {
            return 50000;
        }

        private static LabelOnGround? FindVisibleLabelForEntity(Entity entity, IReadOnlyList<LabelOnGround>? labels)
        {
            if (entity == null || labels == null || labels.Count == 0)
                return null;

            long entityAddress = entity.Address;
            if (entityAddress == 0)
                return null;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label?.ItemOnGround == null)
                    continue;

                if (label.ItemOnGround.Address == entityAddress)
                    return label;
            }

            return null;
        }

        private IReadOnlyList<LabelOnGround>? GetLabelsForOffscreenSelection()
        {
            try
            {
                var raw = gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
                if (raw != null && raw.Count > 0)
                    return [.. raw];
            }
            catch
            {
            }

            return cachedLabels?.Value;
        }

        private IReadOnlyList<LabelOnGround>? GetLabelsForRegularSelection()
        {
            try
            {
                var raw = gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
                if (raw != null && raw.Count > 0)
                    return [.. raw];
            }
            catch
            {
            }

            return cachedLabels?.Value;
        }

    }
}
