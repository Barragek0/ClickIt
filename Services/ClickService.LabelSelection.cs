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
                    if (TryClickSettlersOre(settlersOreCandidate.Value))
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
                if (TryClickSettlersOre(settlersOreCandidate.Value))
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
                    if (TryClickSettlersOre(settlersOreCandidate.Value))
                    {
                        PublishClickFlowDebugStage("SettlersEntityFallbackSuccess", "Settlers entity click succeeded after label resolve failure", settlersOreCandidate.Value.MechanicId);
                        yield break;
                    }
                }

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
            bool hasClickableAltars)
        {
            return walkTowardOffscreenLabelsEnabled
                && hasBackingEntity
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

        private bool PerformLabelClick(Vector2 clickPos, Element? expectedElement, GameController? controller, bool forceUiHoverVerification = false)
        {
            if (!EnsureCursorInsideGameWindowForClick("[PerformLabelClick] Skipping label click - cursor outside PoE window"))
                return false;

            PerformLockedClick(clickPos, expectedElement, controller, forceUiHoverVerification);

            performanceMonitor.RecordClickInterval();
            return true;
        }

        private bool PerformLabelHoldClick(Vector2 clickPos, Element? expectedElement, GameController? controller, int holdDurationMs, bool forceUiHoverVerification = false)
        {
            if (!EnsureCursorInsideGameWindowForClick("[PerformLabelHoldClick] Skipping hold click - cursor outside PoE window"))
                return false;

            PerformLockedHoldClick(clickPos, holdDurationMs, expectedElement, controller, forceUiHoverVerification);

            performanceMonitor.RecordClickInterval();
            return true;
        }

        private bool TryWalkTowardOffscreenTarget(Entity? preferredTarget = null)
        {
            if (!settings.WalkTowardOffscreenLabels.Value)
                return false;

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
                    if (!entity.IsTargetable)
                        continue;

                    string path = entity.Path ?? string.Empty;
                    string? mechanicId = GetEldritchAltarMechanicIdForPath(
                        settings.ClickExarchAltars.Value,
                        settings.ClickEaterAltars.Value,
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

        private bool ShouldAvoidOffscreenPathfindingBecauseOnscreenMechanicIsClickable()
        {
            bool prioritizeOnscreen = settings.PrioritizeOnscreenClickableMechanicsOverPathfinding?.Value == true;
            if (!prioritizeOnscreen)
                return false;

            bool hasClickableAltars = HasClickableAltars();
            bool hasClickableShrine = ResolveNextShrineCandidate() != null;
            bool hasClickableLostShipment = ResolveNextLostShipmentCandidate().HasValue;
            bool hasClickableSettlers = ResolveNextSettlersOreCandidate().HasValue;

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
                    if (!IsSettlersMechanicEnabled(mechanicId))
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
