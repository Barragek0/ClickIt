using System.Collections;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ClickIt.Definitions;
using ClickIt.Utils;
using Microsoft.CSharp.RuntimeBinder;
using System.Windows.Forms;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ExileCore.PoEMemory.Components;

namespace ClickIt.Services
{
    public partial class ClickService
    {
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
        private const int PostChestLootSettleDefaultInitialDelayMs = 500;
        private const int PostChestLootSettleDefaultPollIntervalMs = 100;
        private const int PostChestLootSettleDefaultQuietWindowMs = 500;
        private const float ManualCursorTargetSnapDistancePx = 34f;
        private const float ManualCursorGroundProjectionSnapDistancePx = 44f;

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

        [ThreadStatic]
        private static HashSet<long>? _threadGroundLabelEntityAddresses;

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

            long now = Environment.TickCount64;
            bool isPostChestLootSettleBlocking = IsPostChestLootSettlementBlocking(now, out string chestLootSettleReason);

            var allLabels = GetLabelsForRegularSelection();
            if (TryHandlePendingChestOpenConfirmation(windowTopLeft, allLabels))
            {
                yield break;
            }

            var nextShrine = ResolveNextShrineCandidate();
            LostShipmentCandidate? lostShipmentCandidate;
            SettlersOreCandidate? settlersOreCandidate;
            RefreshMechanicPriorityCaches();

            if (!groundItemsVisible())
            {
                ResolveHiddenFallbackCandidates(out lostShipmentCandidate, out settlersOreCandidate);
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
                    if (isPostChestLootSettleBlocking
                        && !ShouldAllowMechanicInteractionDuringPostChestLootSettlement(settlersOreCandidate.Value.MechanicId, settlersOreCandidate.Value.Entity, out string bypassDecisionSettlersHidden))
                    {
                        PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{chestLootSettleReason} | nearby-bypass:{bypassDecisionSettlersHidden}", settlersOreCandidate.Value.MechanicId);
                    }
                    else if (TryClickSettlersOre(settlersOreCandidate.Value))
                    {
                        PublishClickFlowDebugStage("HiddenSettlersFallback", "Using hidden settlers candidate", settlersOreCandidate.Value.MechanicId);
                        yield break;
                    }

                    PublishClickFlowDebugStage("HiddenSettlersFallbackSkipped", "Hidden settlers candidate was not targetable/valid at click time", settlersOreCandidate.Value.MechanicId);
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
                    if (isPostChestLootSettleBlocking
                        && !ShouldAllowMechanicInteractionDuringPostChestLootSettlement(MechanicIds.LostShipment, lostShipmentCandidate.Value.Entity, out string bypassDecisionLostShipmentHidden))
                    {
                        PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{chestLootSettleReason} | nearby-bypass:{bypassDecisionLostShipmentHidden}", MechanicIds.LostShipment);
                    }
                    else
                    {
                        TryClickLostShipment(lostShipmentCandidate.Value);
                        yield break;
                    }
                }

                if (nextShrine != null && ShouldClickShrineWhenGroundItemsHidden(nextShrine))
                {
                    if (isPostChestLootSettleBlocking
                        && !ShouldAllowMechanicInteractionDuringPostChestLootSettlement(MechanicIds.Shrines, nextShrine, out string bypassDecisionShrineHidden))
                    {
                        PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{chestLootSettleReason} | nearby-bypass:{bypassDecisionShrineHidden}", MechanicIds.Shrines);
                    }
                    else
                    {
                        TryClickShrine(nextShrine);
                        // Do not start offscreen pathfinding while actively attempting an on-screen shrine interaction.
                        yield break;
                    }
                }

                if (isPostChestLootSettleBlocking)
                {
                    DebugLog(() => $"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                    PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason);
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

            ResolveVisibleMechanicCandidates(out lostShipmentCandidate, out settlersOreCandidate, allLabels);

            if (ShouldCaptureClickDebug())
            {
                PublishClickFlowDebugStage("LabelSource", BuildLabelSourceDebugSummary(allLabels));
            }
            LabelOnGround? nextLabel = ResolveNextLabelCandidate(allLabels);

            string? nextLabelMechanicId = nextLabel != null
                ? labelFilterService.GetMechanicIdForLabel(nextLabel)
                : null;
            nextLabelMechanicId = ResolveLabelMechanicIdForVisibleCandidateComparison(
                nextLabelMechanicId,
                hasLabel: nextLabel != null,
                isWorldItemLabel: nextLabel?.ItemOnGround?.Type == ExileCore.Shared.Enums.EntityType.WorldItem,
                clickItemsEnabled: settings.ClickItems.Value);

            if (settlersOreCandidate.HasValue
                && ShouldPreferSettlersOreOverVisibleCandidates(
                    settlersOreCandidate.Value.Distance,
                    settlersOreCandidate.Value.MechanicId,
                    nextLabel?.ItemOnGround?.DistancePlayer,
                    nextLabelMechanicId,
                    nextShrine?.DistancePlayer,
                    lostShipmentCandidate.HasValue ? lostShipmentCandidate.Value.Distance : null,
                    _cachedMechanicPriorityIndexMap,
                    _cachedMechanicIgnoreDistanceSet,
                    _cachedMechanicIgnoreDistanceWithinMap,
                    settings.MechanicPriorityDistancePenalty.Value))
            {
                if (isPostChestLootSettleBlocking
                    && !ShouldAllowMechanicInteractionDuringPostChestLootSettlement(settlersOreCandidate.Value.MechanicId, settlersOreCandidate.Value.Entity, out string bypassDecisionSettlersVisible))
                {
                    PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{chestLootSettleReason} | nearby-bypass:{bypassDecisionSettlersVisible}", settlersOreCandidate.Value.MechanicId);
                }
                else if (TryClickSettlersOre(settlersOreCandidate.Value))
                    yield break;
            }

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
                if (isPostChestLootSettleBlocking
                    && !ShouldAllowMechanicInteractionDuringPostChestLootSettlement(MechanicIds.LostShipment, lostShipmentCandidate.Value.Entity, out string bypassDecisionLostShipmentVisible))
                {
                    PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{chestLootSettleReason} | nearby-bypass:{bypassDecisionLostShipmentVisible}", MechanicIds.LostShipment);
                }
                else
                {
                    TryClickLostShipment(lostShipmentCandidate.Value);
                    yield break;
                }
            }

            bool useShrine = ShouldPreferShrineOverLabel(nextLabel, nextShrine);
            if (useShrine && nextShrine != null)
            {
                if (isPostChestLootSettleBlocking
                    && !ShouldAllowMechanicInteractionDuringPostChestLootSettlement(MechanicIds.Shrines, nextShrine, out string bypassDecisionShrineVisible))
                {
                    PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{chestLootSettleReason} | nearby-bypass:{bypassDecisionShrineVisible}", MechanicIds.Shrines);
                }
                else
                {
                    TryClickShrine(nextShrine);

                    yield break;
                }
            }

            if (nextLabel == null)
            {
                if (isPostChestLootSettleBlocking)
                {
                    DebugLog(() => $"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                    PublishClickFlowDebugStage("PostChestLootSettleBlocked", chestLootSettleReason);
                    yield break;
                }

                labelFilterService.LogSelectionDiagnostics(allLabels, 0, allLabels?.Count ?? 0);
                if (ShouldCaptureClickDebug())
                {
                    PublishClickFlowDebugStage("NoLabelCandidate", BuildNoLabelDebugSummary(allLabels));
                }

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

            if (isPostChestLootSettleBlocking
                && !ShouldAllowMechanicInteractionDuringPostChestLootSettlement(nextLabelMechanicId, nextLabel.ItemOnGround, out string bypassDecisionLabel))
            {
                DebugLog(() => $"[ProcessRegularClick] Skipping click attempt while {chestLootSettleReason}.");
                PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{chestLootSettleReason} | nearby-bypass:{bypassDecisionLabel}", nextLabelMechanicId);
                yield break;
            }

            if (ShouldSkipOrHandleSpecialLabel(nextLabel, windowTopLeft))
            {
                PublishClickFlowDebugStage("SpecialLabelHandled", "Special label handling consumed click tick", nextLabelMechanicId);
                yield break;
            }

            if (!TryResolveLabelClickPosition(
                nextLabel,
                nextLabelMechanicId,
                windowTopLeft,
                allLabels,
                out Vector2 clickPos))
            {
                DebugLog(() => "[ProcessRegularClick] Skipping label: no clickable point inside label bounds.");
                PublishClickFlowDebugStage("ClickPointResolveFailed", "TryCalculateClickPosition returned false", nextLabelMechanicId);

                if (settlersOreCandidate.HasValue
                    && ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(nextLabelMechanicId, settlersOreCandidate.Value.MechanicId))
                {
                    PublishClickFlowDebugStage("SettlersEntityFallbackAttempt", "Label unresolved; attempting settlers entity click", settlersOreCandidate.Value.MechanicId);
                    if (isPostChestLootSettleBlocking
                        && !ShouldAllowMechanicInteractionDuringPostChestLootSettlement(settlersOreCandidate.Value.MechanicId, settlersOreCandidate.Value.Entity, out string bypassDecisionSettlersFallback))
                    {
                        PublishClickFlowDebugStage("PostChestLootSettleBlocked", $"{chestLootSettleReason} | nearby-bypass:{bypassDecisionSettlersFallback}", settlersOreCandidate.Value.MechanicId);
                    }
                    else if (TryClickSettlersOre(settlersOreCandidate.Value))
                    {
                        PublishClickFlowDebugStage("SettlersEntityFallbackSuccess", "Settlers entity click succeeded after label resolve failure", settlersOreCandidate.Value.MechanicId);
                        yield break;
                    }
                }

                bool shouldContinueEntityPathing = ShouldPathfindToEntityAfterClickPointResolveFailure(
                    settings.WalkTowardOffscreenLabels.Value,
                    nextLabel.ItemOnGround != null,
                    nextLabel.ItemOnGround?.IsHidden == true,
                    nextLabelMechanicId);
                if (shouldContinueEntityPathing)
                {
                    PublishClickFlowDebugStage("EntityPathingFallback", "Label visible but unresolved click point; continuing pathing", nextLabelMechanicId);
                    _ = TryWalkTowardOffscreenTarget(nextLabel.ItemOnGround);
                }

                yield break;
            }

            PublishClickFlowDebugStage("ClickPointResolved", $"Resolved click point ({clickPos.X:0.0},{clickPos.Y:0.0})", nextLabelMechanicId);

            PublishLabelClickDebug(
                stage: "LabelCandidate",
                mechanicId: nextLabelMechanicId,
                label: nextLabel,
                resolvedClickPos: clickPos,
                resolved: true,
                notes: "Settlers label candidate selected from ItemsOnGroundLabelsVisible");

            bool forceUiHoverVerification = ShouldForceUiHoverVerificationForLabel(nextLabel);

            bool clicked = ShouldUseHoldClickForSettlersMechanic(nextLabelMechanicId)
                ? PerformLabelHoldClick(clickPos, nextLabel.Label, gameController, holdDurationMs: 0, forceUiHoverVerification)
                : PerformLabelClick(clickPos, nextLabel.Label, gameController, forceUiHoverVerification);

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

                MarkPendingChestOpenConfirmation(nextLabelMechanicId, nextLabel);
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
            return ShouldPreferShrineOverLabelForOffscreen(
                shrineDistance,
                labelDistance,
                labelMechanicId,
                _cachedMechanicPriorityIndexMap,
                _cachedMechanicIgnoreDistanceSet,
                _cachedMechanicIgnoreDistanceWithinMap,
                settings.MechanicPriorityDistancePenalty.Value);
        }

        private LabelOnGround? ResolveNextLabelCandidate(IReadOnlyList<LabelOnGround>? allLabels)
        {
            LabelOnGround? nextLabel = FindNextLabelToClick(allLabels);
            return PreferUiHoverEssenceLabel(nextLabel, allLabels);
        }

        public bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (gameController?.Window == null)
                return false;

            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            var cursor = Mouse.GetCursorPosition();
            Vector2 cursorAbsolute = new(cursor.X, cursor.Y);

            if (TryClickManualCursorPreferredAltarOption(cursorAbsolute, windowTopLeft))
                return true;

            if (TryResolveManualCursorLabelCandidate(allLabels, cursorAbsolute, windowTopLeft, out LabelOnGround? hoveredLabel, out string? mechanicId))
            {
                if (ShouldAttemptManualCursorAltarClick(IsAltarLabel(hoveredLabel), HasClickableAltars()))
                    return TryClickManualCursorPreferredAltarOption(cursorAbsolute, windowTopLeft);

                if (TryCorruptEssence(hoveredLabel, windowTopLeft))
                    return true;

                if (settings.IsInitialUltimatumClickEnabled() && IsUltimatumLabel(hoveredLabel))
                    return TryClickPreferredUltimatumModifier(hoveredLabel, windowTopLeft);

                if (!TryResolveLabelClickPosition(
                    hoveredLabel,
                    mechanicId,
                    windowTopLeft,
                    allLabels,
                    out Vector2 clickPos))
                {
                    return false;
                }

                bool clicked = ShouldUseHoldClickForSettlersMechanic(mechanicId)
                    ? PerformLabelHoldClick(clickPos, null, gameController, holdDurationMs: 0, forceUiHoverVerification: false, allowWhenHotkeyInactive: true, avoidCursorMove: true)
                    : PerformLabelClick(clickPos, null, gameController, forceUiHoverVerification: false, allowWhenHotkeyInactive: true, avoidCursorMove: true);

                if (!clicked)
                    return false;

                MarkPendingChestOpenConfirmation(mechanicId, hoveredLabel);
                MarkLeverClicked(hoveredLabel);
                if (settings.WalkTowardOffscreenLabels.Value)
                {
                    pathfindingService.ClearLatestPath();
                }

                return true;
            }

            return TryClickManualCursorVisibleMechanic(cursorAbsolute, windowTopLeft);
        }

        internal static bool ShouldAttemptManualCursorAltarClick(bool isAltarLabel, bool hasClickableAltars)
        {
            return isAltarLabel && hasClickableAltars;
        }

        private bool TryResolveManualCursorLabelCandidate(
            IReadOnlyList<LabelOnGround>? allLabels,
            Vector2 cursorAbsolute,
            Vector2 windowTopLeft,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LabelOnGround? selectedLabel,
            out string? selectedMechanicId)
        {
            selectedLabel = null;
            selectedMechanicId = null;

            if (allLabels == null || allLabels.Count == 0)
                return false;

            float bestScore = float.MaxValue;
            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround? candidate = allLabels[i];
                if (candidate == null)
                    continue;

                if (ShouldSuppressLeverClick(candidate)
                    || ShouldSuppressInactiveUltimatumLabel(candidate)
                    || inputHandler.IsLabelFullyOverlapped(candidate, allLabels))
                {
                    continue;
                }

                string? mechanicId = labelFilterService.GetMechanicIdForLabel(candidate);
                if (string.IsNullOrWhiteSpace(mechanicId))
                    continue;

                Entity? candidateEntity = candidate.ItemOnGround;
                bool shouldUseGroundProjection = ShouldUseManualGroundProjectionForCandidate(
                    hasBackingEntity: candidateEntity != null,
                    isWorldItem: candidateEntity?.Type == ExileCore.Shared.Enums.EntityType.WorldItem);
                Vector2 projectedGroundPoint = default;

                bool hasLabelRect = TryGetLabelRect(candidate, out RectangleF rect);
                bool cursorInsideLabelRect = hasLabelRect && IsPointInsideRectInEitherSpace(rect, cursorAbsolute, windowTopLeft);
                bool cursorNearGroundProjection = shouldUseGroundProjection
                    && TryGetGroundProjectionPoint(candidateEntity, windowTopLeft, out projectedGroundPoint)
                    && IsWithinManualCursorMatchDistanceInEitherSpace(cursorAbsolute, projectedGroundPoint, windowTopLeft, ManualCursorGroundProjectionSnapDistancePx);

                if (!ShouldTreatManualCursorAsHoveringCandidate(cursorInsideLabelRect, cursorNearGroundProjection))
                    continue;

                float score = float.MaxValue;
                if (cursorInsideLabelRect)
                {
                    score = GetManualCursorLabelHitScore(rect, cursorAbsolute, windowTopLeft);
                }

                if (cursorNearGroundProjection)
                {
                    float objectScore = GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, projectedGroundPoint, windowTopLeft);
                    score = Math.Min(score, objectScore);
                }

                if (score >= bestScore)
                    continue;

                bestScore = score;
                selectedLabel = candidate;
                selectedMechanicId = mechanicId;
            }

            return selectedLabel != null && !string.IsNullOrWhiteSpace(selectedMechanicId);
        }

        internal static bool ShouldUseManualGroundProjectionForCandidate(bool hasBackingEntity, bool isWorldItem)
        {
            return hasBackingEntity && !isWorldItem;
        }

        internal static bool ShouldTreatManualCursorAsHoveringCandidate(bool cursorInsideLabelRect, bool cursorNearGroundProjection)
        {
            return cursorInsideLabelRect || cursorNearGroundProjection;
        }

        private bool TryGetGroundProjectionPoint(Entity? item, Vector2 windowTopLeft, out Vector2 projectedPoint)
        {
            projectedPoint = default;
            if (item == null || !item.IsValid)
                return false;

            try
            {
                var worldScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(item.PosNum);
                projectedPoint = new Vector2(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);
                return float.IsFinite(projectedPoint.X) && float.IsFinite(projectedPoint.Y);
            }
            catch
            {
                return false;
            }
        }

        private bool TryClickManualCursorVisibleMechanic(Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            int selectedType = 0;
            float bestDistanceSq = float.MaxValue;
            Vector2 selectedClickPos = default;
            Entity? selectedEntity = null;
            string? selectedSettlersMechanicId = null;

            Entity? shrine = ResolveNextShrineCandidate();
            if (shrine != null)
            {
                var shrineScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
                Vector2 shrineClickPos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
                if (IsWithinManualCursorMatchDistanceInEitherSpace(cursorAbsolute, shrineClickPos, windowTopLeft, ManualCursorTargetSnapDistancePx))
                {
                    float d2 = GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, shrineClickPos, windowTopLeft);
                    if (d2 < bestDistanceSq)
                    {
                        selectedType = 1;
                        bestDistanceSq = d2;
                        selectedClickPos = shrineClickPos;
                        selectedEntity = shrine;
                        selectedSettlersMechanicId = null;
                    }
                }
            }

            ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
            if (lostShipment.HasValue)
            {
                LostShipmentCandidate candidate = lostShipment.Value;
                if (IsWithinManualCursorMatchDistanceInEitherSpace(cursorAbsolute, candidate.ClickPosition, windowTopLeft, ManualCursorTargetSnapDistancePx))
                {
                    float d2 = GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, candidate.ClickPosition, windowTopLeft);
                    if (d2 < bestDistanceSq)
                    {
                        selectedType = 2;
                        bestDistanceSq = d2;
                        selectedClickPos = candidate.ClickPosition;
                        selectedEntity = candidate.Entity;
                        selectedSettlersMechanicId = null;
                    }
                }
            }

            if (settlers.HasValue)
            {
                SettlersOreCandidate candidate = settlers.Value;
                if (IsWithinManualCursorMatchDistanceInEitherSpace(cursorAbsolute, candidate.ClickPosition, windowTopLeft, ManualCursorTargetSnapDistancePx))
                {
                    float d2 = GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, candidate.ClickPosition, windowTopLeft);
                    if (d2 < bestDistanceSq)
                    {
                        selectedType = 3;
                        bestDistanceSq = d2;
                        selectedClickPos = candidate.ClickPosition;
                        selectedEntity = candidate.Entity;
                        selectedSettlersMechanicId = candidate.MechanicId;
                    }
                }
            }

            if (selectedType == 0)
                return false;

            bool clicked = selectedType == 3 && ShouldUseHoldClickForSettlersMechanic(selectedSettlersMechanicId)
                ? PerformLabelHoldClick(selectedClickPos, null, gameController, holdDurationMs: 0, forceUiHoverVerification: false, allowWhenHotkeyInactive: true, avoidCursorMove: true)
                : PerformLabelClick(selectedClickPos, null, gameController, forceUiHoverVerification: false, allowWhenHotkeyInactive: true, avoidCursorMove: true);

            if (!clicked)
                return false;

            if (selectedType == 1)
            {
                shrineService.InvalidateCache();
            }

            HandleSuccessfulMechanicEntityClick(selectedEntity);
            return true;
        }

        internal static bool IsPointInsideRectInEitherSpace(RectangleF rect, Vector2 absolutePoint, Vector2 windowTopLeft)
        {
            if (rect.Contains(absolutePoint.X, absolutePoint.Y))
                return true;

            Vector2 clientPoint = absolutePoint - windowTopLeft;
            return rect.Contains(clientPoint.X, clientPoint.Y);
        }

        internal static bool IsWithinManualCursorMatchDistanceInEitherSpace(
            Vector2 cursorAbsolute,
            Vector2 candidatePoint,
            Vector2 windowTopLeft,
            float maxDistancePx)
        {
            if (maxDistancePx <= 0f)
                return false;

            float maxDistanceSq = maxDistancePx * maxDistancePx;
            float distanceSq = GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, candidatePoint, windowTopLeft);
            return distanceSq <= maxDistanceSq;
        }

        internal static float GetManualCursorDistanceSquaredInEitherSpace(Vector2 cursorAbsolute, Vector2 candidatePoint, Vector2 windowTopLeft)
        {
            float absoluteDistanceSq = GetDistanceSquared(cursorAbsolute, candidatePoint);
            Vector2 cursorClient = cursorAbsolute - windowTopLeft;
            float clientDistanceSq = GetDistanceSquared(cursorClient, candidatePoint);
            return Math.Min(absoluteDistanceSq, clientDistanceSq);
        }

        private static float GetManualCursorLabelHitScore(RectangleF rect, Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            Vector2 center = rect.Center;
            return GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, center, windowTopLeft);
        }

        private static float GetDistanceSquared(Vector2 a, Vector2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (dx * dx) + (dy * dy);
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
                bool shouldContinuePathing = ShouldContinuePathingForSpecialAltarLabel(
                    settings.WalkTowardOffscreenLabels.Value,
                    nextLabel.ItemOnGround != null,
                    nextLabel.ItemOnGround?.IsHidden == true,
                    HasClickableAltars());
                if (shouldContinuePathing)
                {
                    _ = TryWalkTowardOffscreenTarget(nextLabel.ItemOnGround);
                    DebugLog(() => "[ProcessRegularClick] Item is an altar and altar choices are not fully clickable yet; continuing pathing");
                    return true;
                }

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

        internal static bool ShouldContinuePathingForSpecialAltarLabel(
            bool walkTowardOffscreenLabelsEnabled,
            bool hasBackingEntity,
            bool isBackingEntityHidden,
            bool hasClickableAltars)
        {
            return walkTowardOffscreenLabelsEnabled
                && hasBackingEntity
                && !isBackingEntityHidden
                && !hasClickableAltars;
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

            int searchLimit = GetGroundLabelSearchLimit(allLabels.Count);
            return FindLabelInRange(allLabels, 0, searchLimit);
        }

        internal static int GetGroundLabelSearchLimit(int totalVisibleLabels)
            => Math.Max(0, totalVisibleLabels);

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
                    if (ShouldCaptureClickDebug())
                    {
                        string noLabelSummary = BuildLabelRangeRejectionDebugSummary(allLabels, start, endExclusive, examined);
                        PublishClickFlowDebugStage("FindLabelNull", noLabelSummary);
                    }
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

        private void StartPostChestLootSettlementWatch(string? mechanicId)
        {
            if (!ShouldWaitForChestLootSettlement(
                mechanicId,
                settings.PauseAfterOpeningBasicChests?.Value == true,
                settings.PauseAfterOpeningLeagueChests?.Value == true))
            {
                return;
            }

            ResolvePostChestLootSettlementTimingSettings(
                mechanicId,
                settings.PauseAfterOpeningBasicChestsInitialDelayMs?.Value ?? PostChestLootSettleDefaultInitialDelayMs,
                settings.PauseAfterOpeningBasicChestsPollIntervalMs?.Value ?? PostChestLootSettleDefaultPollIntervalMs,
                settings.PauseAfterOpeningBasicChestsQuietWindowMs?.Value ?? PostChestLootSettleDefaultQuietWindowMs,
                settings.PauseAfterOpeningLeagueChestsInitialDelayMs?.Value ?? PostChestLootSettleDefaultInitialDelayMs,
                settings.PauseAfterOpeningLeagueChestsPollIntervalMs?.Value ?? PostChestLootSettleDefaultPollIntervalMs,
                settings.PauseAfterOpeningLeagueChestsQuietWindowMs?.Value ?? PostChestLootSettleDefaultQuietWindowMs,
                out int initialDelayMs,
                out int pollIntervalMs,
                out int quietWindowMs);

            long now = Environment.TickCount64;
            bool hadSourceGrid = _postChestInteractionSourceGridValid;
            Vector2 sourceGrid = _postChestInteractionSourceGrid;
            ClearPendingChestOpenConfirmation();
            ClearPostChestLootSettlementWatch();
            _postChestLootSettleWatcherActive = true;
            _postChestLootSettleInitialDelayUntilTimestampMs = now + initialDelayMs;
            _postChestLootSettleNextPollTimestampMs = _postChestLootSettleInitialDelayUntilTimestampMs;
            _postChestLootSettleLastNewItemTimestampMs = _postChestLootSettleInitialDelayUntilTimestampMs;
            _postChestLootSettlePollIntervalMs = pollIntervalMs;
            _postChestLootSettleQuietWindowMs = quietWindowMs;
            _postChestInteractionSourceGridValid = hadSourceGrid;
            _postChestInteractionSourceGrid = sourceGrid;
            SeedKnownGroundItemAddresses(_postChestLootSettleKnownGroundItemAddresses, CollectGroundLabelEntityAddresses());
        }

        private bool TryHandlePendingChestOpenConfirmation(Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (!_pendingChestOpenConfirmationActive)
                return false;

            LabelOnGround? pendingChestLabel = FindPendingChestLabel(allLabels, _pendingChestOpenItemAddress, _pendingChestOpenLabelAddress);
            bool chestLabelVisible = pendingChestLabel != null;
            if (ShouldStartChestLootSettlementAfterClick(_pendingChestOpenConfirmationActive, chestLabelVisible))
            {
                PublishClickFlowDebugStage("PostChestOpenDetected", "Chest label disappeared; starting loot settle watch", _pendingChestOpenMechanicId);
                StartPostChestLootSettlementWatch(_pendingChestOpenMechanicId);
                return true;
            }

            if (!ShouldContinueChestOpenRetries(_pendingChestOpenConfirmationActive, chestLabelVisible) || pendingChestLabel == null)
                return false;

            if (!TryResolveLabelClickPosition(
                pendingChestLabel,
                _pendingChestOpenMechanicId,
                windowTopLeft,
                allLabels,
                out Vector2 clickPos))
            {
                PublishClickFlowDebugStage("PostChestReclickResolveFailed", "Pending chest label visible but click point could not be resolved", _pendingChestOpenMechanicId);
                return true;
            }

            bool clicked = PerformLabelClick(clickPos, pendingChestLabel.Label, gameController, ShouldForceUiHoverVerificationForLabel(pendingChestLabel));
            PublishClickFlowDebugStage(
                clicked ? "PostChestReclick" : "PostChestReclickRejected",
                clicked ? "Chest label still visible; reattempted chest click" : "Chest label still visible; chest reclick was rejected",
                _pendingChestOpenMechanicId);
            return true;
        }

        private void MarkPendingChestOpenConfirmation(string? mechanicId, LabelOnGround? chestLabel)
        {
            if (!ShouldWaitForChestLootSettlement(
                mechanicId,
                settings.PauseAfterOpeningBasicChests?.Value == true,
                settings.PauseAfterOpeningLeagueChests?.Value == true))
            {
                return;
            }

            ClearPendingChestOpenConfirmation();
            _pendingChestOpenConfirmationActive = true;
            _pendingChestOpenMechanicId = mechanicId;
            _pendingChestOpenItemAddress = chestLabel?.ItemOnGround?.Address ?? 0;
            _pendingChestOpenLabelAddress = chestLabel?.Label?.Address ?? 0;
            _postChestInteractionSourceGridValid = TryGetEntityGridPosition(chestLabel?.ItemOnGround, out _postChestInteractionSourceGrid);
        }

        private void ClearPendingChestOpenConfirmation()
        {
            _pendingChestOpenConfirmationActive = false;
            _pendingChestOpenMechanicId = null;
            _pendingChestOpenItemAddress = 0;
            _pendingChestOpenLabelAddress = 0;
        }

        private static LabelOnGround? FindPendingChestLabel(IReadOnlyList<LabelOnGround>? allLabels, long itemAddress, long labelAddress)
        {
            if (allLabels == null || allLabels.Count == 0)
                return null;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround? label = allLabels[i];
                if (label == null)
                    continue;

                long currentItemAddress = label.ItemOnGround?.Address ?? 0;
                long currentLabelAddress = label.Label?.Address ?? 0;
                if ((itemAddress != 0 && currentItemAddress == itemAddress)
                    || (labelAddress != 0 && currentLabelAddress == labelAddress))
                {
                    return label;
                }
            }

            return null;
        }

        private bool IsPostChestLootSettlementBlocking(long now, out string reason)
        {
            reason = string.Empty;
            if (!_postChestLootSettleWatcherActive)
                return false;

            if (now < _postChestLootSettleInitialDelayUntilTimestampMs)
            {
                long initialDelayRemainingMs = _postChestLootSettleInitialDelayUntilTimestampMs - now;
                reason = $"waiting {initialDelayRemainingMs}ms before monitoring chest drops";
                return true;
            }

            if (now >= _postChestLootSettleNextPollTimestampMs)
            {
                bool hasNewGroundItems = MergeNewGroundItemAddresses(
                    _postChestLootSettleKnownGroundItemAddresses,
                    CollectGroundLabelEntityAddresses());
                if (hasNewGroundItems)
                {
                    _postChestLootSettleLastNewItemTimestampMs = now;
                }

                _postChestLootSettleNextPollTimestampMs = now + Math.Max(1, _postChestLootSettlePollIntervalMs);
            }

            if (IsChestLootSettlementQuietPeriodElapsed(
                now,
                _postChestLootSettleLastNewItemTimestampMs,
                _postChestLootSettleQuietWindowMs,
                out long quietWindowRemainingMs))
            {
                ClearPostChestLootSettlementWatch();
                return false;
            }

            reason = $"waiting for chest loot to settle ({quietWindowRemainingMs}ms quiet window remaining)";
            return true;
        }

        private void ClearPostChestLootSettlementWatch()
        {
            _postChestLootSettleWatcherActive = false;
            _postChestLootSettleInitialDelayUntilTimestampMs = 0;
            _postChestLootSettleNextPollTimestampMs = 0;
            _postChestLootSettleLastNewItemTimestampMs = 0;
            _postChestLootSettlePollIntervalMs = 0;
            _postChestLootSettleQuietWindowMs = 0;
            _postChestInteractionSourceGridValid = false;
            _postChestInteractionSourceGrid = default;
            _postChestLootSettleKnownGroundItemAddresses.Clear();
        }

        private static void SeedKnownGroundItemAddresses(HashSet<long> knownAddresses, IReadOnlySet<long>? snapshot)
        {
            knownAddresses.Clear();
            _ = MergeNewGroundItemAddresses(knownAddresses, snapshot);
        }

        private static bool MergeNewGroundItemAddresses(HashSet<long> knownAddresses, IReadOnlySet<long>? snapshot)
        {
            if (snapshot == null || snapshot.Count == 0)
                return false;

            bool addedAny = false;
            foreach (long address in snapshot)
            {
                if (address == 0)
                    continue;
                if (knownAddresses.Add(address))
                    addedAny = true;
            }

            return addedAny;
        }

        private bool ShouldAllowMechanicInteractionDuringPostChestLootSettlement(string? mechanicId, Entity? entity)
            => ShouldAllowMechanicInteractionDuringPostChestLootSettlement(mechanicId, entity, out _);

        private bool ShouldAllowMechanicInteractionDuringPostChestLootSettlement(string? mechanicId, Entity? entity, out string decision)
        {
            decision = string.Empty;
            if (!_postChestLootSettleWatcherActive)
            {
                decision = "watcher-inactive";
                return false;
            }
            if (settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle?.Value != true)
            {
                decision = "setting-disabled";
                return false;
            }
            if (!_postChestInteractionSourceGridValid)
            {
                decision = "source-grid-unavailable";
                return false;
            }
            if (!IsMechanicEligibleForNearbyChestLootSettlementBypass(mechanicId))
            {
                decision = "mechanic-not-eligible";
                return false;
            }
            if (!TryGetEntityGridPosition(entity, out Vector2 entityGridPos))
            {
                decision = "candidate-grid-unavailable";
                return false;
            }

            int maxDistance = Math.Max(0, settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettleDistance?.Value ?? 10);
            float distanceSq = GetDistanceSquared(_postChestInteractionSourceGrid, entityGridPos);
            float distance = MathF.Sqrt(distanceSq);
            bool allowed = IsWithinNearbyChestLootSettlementBypassDistance(_postChestInteractionSourceGrid, entityGridPos, maxDistance);
            decision = $"{(allowed ? "allowed" : "blocked")}; mechanic:{mechanicId ?? "unknown"}; dist:{distance:0.0}; max:{maxDistance}; source:({_postChestInteractionSourceGrid.X:0.0},{_postChestInteractionSourceGrid.Y:0.0}); candidate:({entityGridPos.X:0.0},{entityGridPos.Y:0.0})";
            return allowed;
        }

        internal static bool IsMechanicEligibleForNearbyChestLootSettlementBypass(string? mechanicId)
        {
            return !string.IsNullOrWhiteSpace(mechanicId);
        }

        internal static bool IsWithinNearbyChestLootSettlementBypassDistance(Vector2 sourceGridPos, Vector2 entityGridPos, int maxDistance)
        {
            if (maxDistance < 0)
                return false;

            float maxDistanceSq = maxDistance * maxDistance;
            float distanceSq = GetDistanceSquared(sourceGridPos, entityGridPos);
            return distanceSq <= maxDistanceSq;
        }

        private static bool TryGetEntityGridPosition(Entity? entity, out Vector2 gridPos)
        {
            gridPos = default;
            if (entity == null || !entity.IsValid)
                return false;

            var grid = entity.GridPosNum;
            gridPos = new Vector2(grid.X, grid.Y);
            return true;
        }

        internal static bool ShouldWaitForChestLootSettlement(
            string? mechanicId,
            bool waitAfterOpeningBasicChests,
            bool waitAfterOpeningLeagueChests)
        {
            if (string.Equals(mechanicId, MechanicIds.BasicChests, StringComparison.OrdinalIgnoreCase))
                return waitAfterOpeningBasicChests;

            if (string.Equals(mechanicId, MechanicIds.LeagueChests, StringComparison.OrdinalIgnoreCase))
                return waitAfterOpeningLeagueChests;

            return false;
        }

        internal static void ResolvePostChestLootSettlementTimingSettings(
            string? mechanicId,
            int basicInitialDelayMs,
            int basicPollIntervalMs,
            int basicQuietWindowMs,
            int leagueInitialDelayMs,
            int leaguePollIntervalMs,
            int leagueQuietWindowMs,
            out int initialDelayMs,
            out int pollIntervalMs,
            out int quietWindowMs)
        {
            if (string.Equals(mechanicId, MechanicIds.BasicChests, StringComparison.OrdinalIgnoreCase))
            {
                initialDelayMs = Math.Max(0, basicInitialDelayMs);
                pollIntervalMs = Math.Max(1, basicPollIntervalMs);
                quietWindowMs = Math.Max(0, basicQuietWindowMs);
                return;
            }

            if (string.Equals(mechanicId, MechanicIds.LeagueChests, StringComparison.OrdinalIgnoreCase))
            {
                initialDelayMs = Math.Max(0, leagueInitialDelayMs);
                pollIntervalMs = Math.Max(1, leaguePollIntervalMs);
                quietWindowMs = Math.Max(0, leagueQuietWindowMs);
                return;
            }

            initialDelayMs = PostChestLootSettleDefaultInitialDelayMs;
            pollIntervalMs = PostChestLootSettleDefaultPollIntervalMs;
            quietWindowMs = PostChestLootSettleDefaultQuietWindowMs;
        }

        internal static bool ShouldContinueChestOpenRetries(bool pendingChestOpenConfirmationActive, bool chestLabelVisible)
            => pendingChestOpenConfirmationActive && chestLabelVisible;

        internal static bool ShouldStartChestLootSettlementAfterClick(bool pendingChestOpenConfirmationActive, bool chestLabelVisible)
            => pendingChestOpenConfirmationActive && !chestLabelVisible;

        internal static bool IsChestLootSettlementQuietPeriodElapsed(
            long now,
            long lastNewGroundItemTimestampMs,
            int quietWindowMs,
            out long remainingMs)
        {
            if (quietWindowMs <= 0)
            {
                remainingMs = 0;
                return true;
            }

            if (lastNewGroundItemTimestampMs <= 0)
            {
                remainingMs = quietWindowMs;
                return false;
            }

            long elapsed = Math.Max(0, now - lastNewGroundItemTimestampMs);
            if (elapsed >= quietWindowMs)
            {
                remainingMs = 0;
                return true;
            }

            remainingMs = quietWindowMs - elapsed;
            return false;
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

        private bool PerformLabelClick(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
            if (!EnsureCursorInsideGameWindowForClick("[PerformLabelClick] Skipping label click - cursor outside PoE window"))
                return false;

            PerformLockedClick(clickPos, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);

            performanceMonitor.RecordClickInterval();
            return true;
        }

        private bool PerformLabelHoldClick(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            int holdDurationMs,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
            if (!EnsureCursorInsideGameWindowForClick("[PerformLabelHoldClick] Skipping hold click - cursor outside PoE window"))
                return false;

            PerformLockedHoldClick(clickPos, holdDurationMs, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);

            performanceMonitor.RecordClickInterval();
            return true;
        }

        private bool TryWalkTowardOffscreenTarget(Entity? preferredTarget = null)
        {
            if (!settings.WalkTowardOffscreenLabels.Value)
                return false;

            if (ShouldSkipOffscreenPathfindingForRitual(EntityHelpers.IsRitualActive(gameController)))
            {
                ClearStickyOffscreenTarget();
                pathfindingService.ClearLatestPath();
                DebugLog(() => "[TryWalkTowardOffscreenTarget] Skipping offscreen pathfinding because a RitualBlocker is active.");
                PublishClickFlowDebugStage("OffscreenPathingBlockedByRitual", "RitualBlocker active");
                return false;
            }

            if (ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable())
            {
                ClearStickyOffscreenTarget();
                pathfindingService.ClearLatestPath();
                DebugLog(() => "[TryWalkTowardOffscreenTarget] Skipping offscreen pathfinding because a clickable on-screen mechanic is available.");
                return false;
            }

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

            if (!target.IsValid || target.IsHidden || IsEntityHiddenByMinimapIcon(target))
            {
                ClearStickyOffscreenTarget();
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

            if (!TryResolveLabelClickPosition(
                stickyLabel,
                mechanicId,
                windowTopLeft,
                allLabels,
                out Vector2 clickPos,
                explicitPath: stickyTarget.Path))
            {
                return false;
            }

            bool clickedLabel = ShouldUseHoldClickForSettlersMechanic(mechanicId)
                ? PerformLabelHoldClick(clickPos, stickyLabel.Label, gameController, holdDurationMs: 0, ShouldForceUiHoverVerificationForLabel(stickyLabel))
                : PerformLabelClick(clickPos, stickyLabel.Label, gameController, ShouldForceUiHoverVerificationForLabel(stickyLabel));
            if (clickedLabel)
            {
                MarkPendingChestOpenConfirmation(mechanicId, stickyLabel);
                ClearStickyOffscreenTarget();
            }

            return clickedLabel;
        }

        private void SetStickyOffscreenTarget(Entity target)
        {
            _stickyOffscreenTargetAddress = target.Address;
        }

        private bool TryResolveLabelClickPosition(
            LabelOnGround label,
            string? mechanicId,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels,
            out Vector2 clickPos,
            string? explicitPath = null)
        {
            string path = explicitPath ?? label.ItemOnGround?.Path ?? string.Empty;

            if (inputHandler.TryCalculateClickPosition(
                label,
                windowTopLeft,
                allLabels,
                point => IsClickableInEitherSpace(point, path),
                out clickPos))
            {
                return true;
            }

            // Settlers labels can remain clickable while the backing world entity projection is off-screen.
            // In that case, relax area validation and let UIHover verification guard the final click.
            if (!ShouldRetryLabelClickPointWithoutClickableArea(mechanicId))
                return false;

            if (!ShouldAllowSettlersRelaxedClickPointFallback(label.ItemOnGround != null, IsItemWorldProjectionInWindow(label.ItemOnGround, windowTopLeft)))
                return false;

            return inputHandler.TryCalculateClickPosition(
                label,
                windowTopLeft,
                allLabels,
                isClickableArea: null,
                out clickPos);
        }

        internal static bool ShouldRetryLabelClickPointWithoutClickableArea(string? mechanicId)
        {
            return IsSettlersMechanicId(mechanicId);
        }

        internal static bool ShouldAllowSettlersRelaxedClickPointFallback(bool hasBackingEntity, bool worldProjectionInWindow)
        {
            if (!hasBackingEntity)
                return false;

            return !worldProjectionInWindow;
        }

        private bool IsItemWorldProjectionInWindow(Entity? item, Vector2 windowTopLeft)
        {
            if (item == null)
                return false;

            var worldScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(item.PosNum);
            Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);
            return IsInsideWindowInEitherSpace(worldScreenAbsolute);
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
            if (target == null || !target.IsValid || target.IsHidden || IsEntityHiddenByMinimapIcon(target))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            if (ShrineService.IsShrine(target) && !ShrineService.IsClickableShrineCandidate(target))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            string stickyPath = target.Path ?? string.Empty;
            bool isEldritchAltar = IsEldritchAltarPath(stickyPath);
            if (ShouldDropStickyTargetForUntargetableEldritchAltar(isEldritchAltar, target.IsTargetable))
            {
                ClearStickyOffscreenTarget();
                return false;
            }

            return true;
        }

        internal static bool ShouldDropStickyTargetForUntargetableEldritchAltar(bool isEldritchAltar, bool isTargetable)
        {
            return isEldritchAltar && !isTargetable;
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

        private static bool IsEntityHiddenByMinimapIcon(Entity entity)
        {
            MinimapIcon? minimapIcon = entity.GetComponent<MinimapIcon>();
            return minimapIcon != null && minimapIcon.IsHide;
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

        private Entity? ResolveNearestOffscreenWalkTarget()
        {
            if (gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return null;

            if (ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable())
            {
                ClearStickyOffscreenTarget();
                return null;
            }

            if (TryResolveStickyOffscreenTarget(out Entity? stickyTarget) && stickyTarget != null)
                return stickyTarget;

            int maxDistance = GetOffscreenPathfindingTargetSearchDistance();

            Entity? labelBackedTarget = ResolveNearestOffscreenLabelBackedTarget(maxDistance, out string? labelMechanicId);
            Entity? eldritchAltarTarget = ResolveNearestOffscreenEldritchAltarTarget(maxDistance, out string? eldritchAltarMechanicId);
            Entity? shrineTarget = ResolveNearestOffscreenShrineTarget(maxDistance);
            Entity? areaTransitionTarget = ResolveNearestOffscreenAreaTransitionTarget(maxDistance, out string? areaTransitionMechanicId);

            if (labelBackedTarget == null && eldritchAltarTarget == null && shrineTarget == null && areaTransitionTarget == null)
                return null;

            RefreshMechanicPriorityCaches();

            Entity? best = null;
            string? bestMechanicId = null;
            MechanicRank bestRank = default;
            bool hasBest = false;

            PromoteOffscreenTargetCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, labelBackedTarget, labelMechanicId);
            PromoteOffscreenTargetCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, eldritchAltarTarget, eldritchAltarMechanicId);
            PromoteOffscreenTargetCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, shrineTarget, ShrineMechanicId);
            PromoteOffscreenTargetCandidate(ref best, ref bestMechanicId, ref bestRank, ref hasBest, areaTransitionTarget, areaTransitionMechanicId);

            return best;
        }

        private Entity? ResolveNearestOffscreenEldritchAltarTarget(int maxDistance, out string? selectedMechanicId)
        {
            selectedMechanicId = null;

            if ((!settings.ClickExarchAltars.Value && !settings.ClickEaterAltars.Value)
                || gameController?.EntityListWrapper?.ValidEntitiesByType == null)
            {
                return null;
            }

            return ResolveNearestOffscreenEntityTarget(
                maxDistance,
                includeEntity: (entity, _) => entity.IsTargetable,
                resolveMechanicId: (_, path) => GetEldritchAltarMechanicIdForPath(
                    settings.ClickExarchAltars.Value,
                    settings.ClickEaterAltars.Value,
                    path),
                out selectedMechanicId);
        }

        private bool ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable()
        {
            bool prioritizeOnscreen = settings.PrioritizeOnscreenClickableMechanicsOverPathfinding?.Value == true;
            bool shouldEvaluateOnscreenMechanicChecks = ShouldEvaluateOnscreenMechanicChecks(
                prioritizeOnscreen,
                settings.ClickShrines.Value,
                settings.ClickLostShipmentCrates.Value,
                settings.ClickSettlersOre.Value,
                settings.ClickEaterAltars.Value,
                settings.ClickExarchAltars.Value);
            if (!shouldEvaluateOnscreenMechanicChecks)
                return false;

            bool hasClickableAltars = HasClickableAltars();
            bool hasClickableShrine = ResolveNextShrineCandidate() != null;
            ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipmentCandidate, out SettlersOreCandidate? settlersOreCandidate);
            bool hasClickableLostShipment = lostShipmentCandidate.HasValue;
            bool hasClickableSettlers = settlersOreCandidate.HasValue;

            bool shouldAvoid = ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing(
                prioritizeOnscreen,
                hasClickableAltars,
                hasClickableShrine,
                hasClickableLostShipment,
                hasClickableSettlers);

            if (shouldAvoid)
            {
                PublishClickFlowDebugStage(
                    "OffscreenPathingBlocked",
                    $"onscreen clickable mechanic detected (altar={hasClickableAltars}, shrine={hasClickableShrine}, lost={hasClickableLostShipment}, settlers={hasClickableSettlers})");
            }

            return shouldAvoid;
        }

        internal static bool ShouldPrioritizeOnscreenMechanicsOverOffscreenPathing(
            bool prioritizeOnscreenClickableMechanics,
            bool hasClickableAltar,
            bool hasClickableShrine,
            bool hasClickableLostShipment,
            bool hasClickableSettlersOre)
        {
            return prioritizeOnscreenClickableMechanics
                && (hasClickableAltar
                    || hasClickableShrine
                    || hasClickableLostShipment
                    || hasClickableSettlersOre);
        }

        internal static bool ShouldEvaluateOnscreenMechanicChecks(
            bool prioritizeOnscreenClickableMechanics,
            bool clickShrinesEnabled,
            bool clickLostShipmentEnabled,
            bool clickSettlersOreEnabled,
            bool clickEaterAltarsEnabled,
            bool clickExarchAltarsEnabled)
        {
            if (!prioritizeOnscreenClickableMechanics)
                return false;

            return clickShrinesEnabled
                || clickLostShipmentEnabled
                || clickSettlersOreEnabled
                || clickEaterAltarsEnabled
                || clickExarchAltarsEnabled;
        }

        internal static bool ShouldSkipOffscreenPathfindingForRitual(bool ritualActive)
            => ritualActive;

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
            if (!ShouldCaptureClickDebug())
                return;

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
            if (!ShouldCaptureClickDebug())
                return;

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

        private IReadOnlySet<long> CollectGroundLabelEntityAddresses()
        {
            try
            {
                var labels = gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels;
                int labelCount = labels?.Count ?? 0;
                if (labels == null || labelCount == 0)
                {
                    _cachedGroundLabelEntityAddresses.Clear();
                    _cachedGroundLabelEntityLabelCount = 0;
                    _cachedGroundLabelEntityAddressesTimestampMs = Environment.TickCount64;
                    return _cachedGroundLabelEntityAddresses;
                }

                long now = Environment.TickCount64;
                if (ShouldReuseTimedLabelCountCache(
                        now,
                        _cachedGroundLabelEntityAddressesTimestampMs,
                        _cachedGroundLabelEntityLabelCount,
                        labelCount,
                        GroundLabelEntityAddressCacheWindowMs))
                {
                    return _cachedGroundLabelEntityAddresses;
                }

                _cachedGroundLabelEntityAddresses.Clear();
                _cachedGroundLabelEntityAddresses.EnsureCapacity(labelCount);

                for (int i = 0; i < labelCount; i++)
                {
                    long address = labels[i]?.ItemOnGround?.Address ?? 0;
                    if (address != 0)
                        _cachedGroundLabelEntityAddresses.Add(address);
                }

                _cachedGroundLabelEntityAddressesTimestampMs = now;
                _cachedGroundLabelEntityLabelCount = labelCount;
            }
            catch
            {
            }

            return _cachedGroundLabelEntityAddresses;
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
            if (candidate == null || !candidate.IsValid || candidate.IsHidden || IsEntityHiddenByMinimapIcon(candidate) || string.IsNullOrWhiteSpace(mechanicId))
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

            return ResolveNearestOffscreenEntityTarget(
                maxDistance,
                includeEntity: (_, _) => true,
                resolveMechanicId: (entity, path) => GetAreaTransitionMechanicIdForPath(
                    settings.ClickAreaTransitions.Value,
                    settings.ClickLabyrinthTrials.Value,
                    entity.Type,
                    path),
                out selectedMechanicId);
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

        internal static string? GetEldritchAltarMechanicIdForPath(bool clickExarchAltars, bool clickEaterAltars, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (clickExarchAltars && path.Contains(global::ClickIt.Definitions.Constants.CleansingFireAltar, StringComparison.OrdinalIgnoreCase))
                return MechanicIds.AltarsSearingExarch;

            if (clickEaterAltars && path.Contains(global::ClickIt.Definitions.Constants.TangleAltar, StringComparison.OrdinalIgnoreCase))
                return MechanicIds.AltarsEaterOfWorlds;

            return null;
        }

        internal static bool IsEldritchAltarPath(string path)
        {
            return !string.IsNullOrWhiteSpace(GetEldritchAltarMechanicIdForPath(
                clickExarchAltars: true,
                clickEaterAltars: true,
                path));
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

            return ResolveNearestOffscreenEntityTarget(
                maxDistance,
                includeEntity: (entity, _) => ShrineService.IsClickableShrineCandidate(entity),
                resolveMechanicId: (_, _) => ShrineMechanicId,
                out _);
        }

        // Compatibility seam for reflection-based tests that assert this dedicated helper exists.
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
                if (!entity.IsValid || entity.IsHidden || IsEntityHiddenByMinimapIcon(entity))
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

        private Entity? ResolveNearestOffscreenEntityTarget(
            int maxDistance,
            Func<Entity, string, bool> includeEntity,
            Func<Entity, string, string?> resolveMechanicId,
            out string? selectedMechanicId)
        {
            selectedMechanicId = null;

            if (gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return null;

            Entity? best = null;
            float bestDistance = float.MaxValue;
            string? bestMechanicId = null;

            foreach (var kv in gameController.EntityListWrapper.ValidEntitiesByType)
            {
                var entities = kv.Value;
                if (entities == null)
                    continue;

                for (int i = 0; i < entities.Count; i++)
                {
                    Entity entity = entities[i];
                    if (!TryPrepareOffscreenEntityTargetCandidate(entity, maxDistance, out string path))
                        continue;

                    if (!includeEntity(entity, path))
                        continue;

                    string? mechanicId = resolveMechanicId(entity, path);
                    if (string.IsNullOrWhiteSpace(mechanicId))
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

        private bool TryPrepareOffscreenEntityTargetCandidate(Entity? entity, int maxDistance, out string path)
        {
            path = string.Empty;

            if (entity == null || !entity.IsValid || entity.IsHidden || IsEntityHiddenByMinimapIcon(entity))
                return false;
            if (entity.DistancePlayer > maxDistance)
                return false;

            path = entity.Path ?? string.Empty;

            var screenRaw = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
            Vector2 screen = new(screenRaw.X, screenRaw.Y);
            if (IsClickableInEitherSpace(screen, path))
                return false;

            return true;
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
            bool isEntityHidden,
            string? mechanicId)
        {
            if (!walkTowardOffscreenLabelsEnabled || !hasEntity || isEntityHidden || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            return true;
        }

        internal static string? ResolveLabelMechanicIdForVisibleCandidateComparison(
            string? resolvedMechanicId,
            bool hasLabel,
            bool isWorldItemLabel,
            bool clickItemsEnabled)
        {
            if (!string.IsNullOrWhiteSpace(resolvedMechanicId))
                return resolvedMechanicId;

            if (hasLabel && isWorldItemLabel && clickItemsEnabled)
                return MechanicIds.Items;

            return resolvedMechanicId;
        }

        internal static bool ShouldFallbackToSettlersEntityClickAfterLabelResolveFailure(
            string? labelMechanicId,
            string? settlersCandidateMechanicId)
        {
            if (!IsSettlersMechanicId(labelMechanicId) || !IsSettlersMechanicId(settlersCandidateMechanicId))
                return false;

            if (string.IsNullOrWhiteSpace(labelMechanicId) || string.IsNullOrWhiteSpace(settlersCandidateMechanicId))
                return false;

            return string.Equals(labelMechanicId, settlersCandidateMechanicId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldForceUiHoverVerificationForLabel(LabelOnGround? label)
        {
            Entity? item = label?.ItemOnGround;
            if (item == null || item.Type != ExileCore.Shared.Enums.EntityType.WorldItem)
                return false;

            return InputHandler.ShouldForceUiHoverVerificationForWorldItem(item.Path, item.RenderName);
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
            => GetVisibleOrCachedLabels();

        private IReadOnlyList<LabelOnGround>? GetLabelsForRegularSelection()
            => GetVisibleOrCachedLabels();

        private IReadOnlyList<LabelOnGround>? GetVisibleOrCachedLabels()
        {
            try
            {
                var raw = gameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
                var visible = ResolveVisibleLabelsWithoutForcedCopy(raw);
                if (visible != null)
                    return visible;
            }
            catch
            {
            }

            return cachedLabels?.Value;
        }

        internal static IReadOnlyList<LabelOnGround>? ResolveVisibleLabelsWithoutForcedCopy(object? rawVisibleLabels)
        {
            if (rawVisibleLabels is IReadOnlyList<LabelOnGround> visibleList)
            {
                return visibleList.Count > 0 ? visibleList : null;
            }

            if (rawVisibleLabels is IEnumerable<LabelOnGround> visibleEnumerable)
            {
                List<LabelOnGround> snapshot = [.. visibleEnumerable];
                return snapshot.Count > 0 ? snapshot : null;
            }

            return null;
        }

    }
}
