using System.Collections;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ClickIt.Definitions;
using ClickIt.Utils;
using System.Windows.Forms;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ExileCore.PoEMemory.Components;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        internal const int MovementSkillRecastDelayMs = 450;
        internal const int MovementSkillKeyTapDelayMs = 30;
        private const int MovementSkillDefaultPostCastClickBlockMs = 120;
        internal const int MovementSkillShieldChargePostCastClickBlockMs = 100;
        private const int MovementSkillLeapSlamPostCastClickBlockMs = 230;
        private const int MovementSkillWhirlingBladesPostCastClickBlockMs = 170;
        private const int MovementSkillBlinkArrowPostCastClickBlockMs = 260;
        private const int MovementSkillChargedDashPostCastClickBlockMs = 300;
        private const int MovementSkillLightningWarpPostCastClickBlockMs = 320;
        private const int MovementSkillConsecratedPathPostCastClickBlockMs = 240;
        private const int MovementSkillChainHookPostCastClickBlockMs = 220;
        private const int MovementSkillDefaultStatusPollExtraMs = 900;
        private const int MovementSkillExtendedStatusPollExtraMs = 1300;
        internal const int PostChestLootSettleDefaultInitialDelayMs = 500;
        internal const int PostChestLootSettleDefaultPollIntervalMs = 100;
        internal const int PostChestLootSettleDefaultQuietWindowMs = 500;
        internal const float ManualCursorTargetSnapDistancePx = 34f;
        internal const float ManualCursorGroundProjectionSnapDistancePx = 44f;

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

        internal bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? allLabels)
        {
            // Stable ClickService facade entry point; implementation lives in the label-selection coordinator.
            return LabelSelection.TryClickManualUiHoverLabel(allLabels);
        }

        internal IEnumerator ProcessRegularClick()
        {
            // Stable ClickService facade entry point; orchestration lives in the regular-click coordinator.
            return RegularClick.Run();
        }

        internal static bool ShouldAttemptManualCursorAltarClick(bool isAltarLabel, bool hasClickableAltars)
        {
            return isAltarLabel && hasClickableAltars;
        }

        internal static bool ShouldUseManualGroundProjectionForCandidate(bool hasBackingEntity, bool isWorldItem)
        {
            return hasBackingEntity && !isWorldItem;
        }

        internal static bool ShouldTreatManualCursorAsHoveringCandidate(bool cursorInsideLabelRect, bool cursorNearGroundProjection)
        {
            return cursorInsideLabelRect || cursorNearGroundProjection;
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
            => CoordinateSpace.DistanceSquaredInEitherSpace(cursorAbsolute, candidatePoint, windowTopLeft);

        internal static float GetManualCursorLabelHitScore(RectangleF rect, Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            Vector2 center = rect.Center;
            return GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, center, windowTopLeft);
        }

        internal static Vector2 GetCursorAbsolutePosition()
        {
            var cursor = Mouse.GetCursorPosition();
            return new Vector2(cursor.X, cursor.Y);
        }

        internal static float GetCursorDistanceSquaredToPoint(Vector2 point, Vector2 cursorAbsolute, Vector2 windowTopLeft)
            => GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, point, windowTopLeft);

        private float? TryGetCursorDistanceSquaredToEntity(Entity? entity, Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            if (entity == null || !entity.IsValid)
                return null;

            try
            {
                var worldScreenRaw = gameController.Game.IngameState.Camera.WorldToScreen(entity.PosNum);
                Vector2 worldScreenAbsolute = new(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);
                return GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, worldScreenAbsolute, windowTopLeft);
            }
            catch
            {
                return null;
            }
        }

        internal static float? TryGetCursorDistanceSquaredToLabel(LabelOnGround? label, Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            if (!LabelUtils.TryGetLabelRect(label, out RectangleF rect))
                return null;

            return GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, rect.Center, windowTopLeft);
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

        internal static bool IsEssenceLabel(LabelOnGround lbl)
        {
            if (lbl == null || lbl.Label == null)
                return false;

            return LabelUtils.HasEssenceImprisonmentText(lbl);
        }

        internal static int GetGroundLabelSearchLimit(int totalVisibleLabels)
            => Math.Max(0, totalVisibleLabels);

        internal static LabelOnGround? FindLabelByAddress(IReadOnlyList<LabelOnGround> labels, long address)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label?.Label != null && label.Label.Address == address)
                    return label;
            }

            return null;
        }

        internal static int IndexOfLabelReference(IReadOnlyList<LabelOnGround> labels, LabelOnGround target, int start, int endExclusive)
        {
            for (int i = start; i < endExclusive; i++)
            {
                if (ReferenceEquals(labels[i], target))
                    return i;
            }

            return -1;
        }

        internal static bool IsLeverClickSuppressedByCooldown(ulong lastLeverKey, long lastLeverClickTimestampMs, ulong currentLeverKey, long now, int cooldownMs)
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

        internal static bool IsLeverLabel(LabelOnGround? label)
        {
            string? path = label?.ItemOnGround?.Path;
            return !string.IsNullOrWhiteSpace(path)
                && path.Contains("Switch_Once", StringComparison.OrdinalIgnoreCase);
        }

        internal static ulong GetLeverIdentityKey(LabelOnGround label)
        {
            ulong itemAddress = unchecked((ulong)(label.ItemOnGround?.Address ?? 0));
            if (itemAddress != 0)
                return itemAddress;

            ulong elementAddress = unchecked((ulong)(label.Label?.Address ?? 0));
            if (elementAddress != 0)
                return elementAddress;

            return 0;
        }

        internal static bool IsAltarLabel(LabelOnGround label)
        {
            var item = label.ItemOnGround;
            string path = item.Path ?? string.Empty;
            return path.Contains("CleansingFireAltar") || path.Contains("TangleAltar");
        }

        internal static LabelOnGround? FindPendingChestLabel(IReadOnlyList<LabelOnGround>? allLabels, long itemAddress, long labelAddress)
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

        internal static void SeedKnownGroundItemAddresses(HashSet<long> knownAddresses, IReadOnlySet<long>? snapshot)
        {
            knownAddresses.Clear();
            _ = MergeNewGroundItemAddresses(knownAddresses, snapshot);
        }

        internal static bool MergeNewGroundItemAddresses(HashSet<long> knownAddresses, IReadOnlySet<long>? snapshot)
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

        internal static bool IsMechanicEligibleForNearbyChestLootSettlementBypass(string? mechanicId)
        {
            return !string.IsNullOrWhiteSpace(mechanicId);
        }

        internal static bool IsWithinNearbyChestLootSettlementBypassDistance(Vector2 sourceGridPos, Vector2 entityGridPos, int maxDistance)
        {
            if (maxDistance < 0)
                return false;

            float maxDistanceSq = maxDistance * maxDistance;
            float distanceSq = CoordinateSpace.DistanceSquared(sourceGridPos, entityGridPos);
            return distanceSq <= maxDistanceSq;
        }

        internal static bool TryGetEntityGridPosition(Entity? entity, out Vector2 gridPos)
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

        internal static ChestLootSettlementTiming ResolvePostChestLootSettlementTimingSettings(
            string? mechanicId,
            in ChestLootSettlementTimingOptions options)
        {
            if (string.Equals(mechanicId, MechanicIds.BasicChests, StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeChestLootSettlementTiming(options.Basic);
            }

            if (string.Equals(mechanicId, MechanicIds.LeagueChests, StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeChestLootSettlementTiming(options.League);
            }

            return new ChestLootSettlementTiming(
                PostChestLootSettleDefaultInitialDelayMs,
                PostChestLootSettleDefaultPollIntervalMs,
                PostChestLootSettleDefaultQuietWindowMs);
        }

        private static ChestLootSettlementTiming NormalizeChestLootSettlementTiming(in ChestLootSettlementTiming timing)
            => new(
                Math.Max(0, timing.InitialDelayMs),
                Math.Max(1, timing.PollIntervalMs),
                Math.Max(0, timing.QuietWindowMs));

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

        internal static bool ShouldDropStickyTargetForUntargetableEldritchAltar(bool isEldritchAltar, bool isTargetable)
        {
            return isEldritchAltar && !isTargetable;
        }

        internal static bool IsSameEntityAddress(long leftAddress, long rightAddress)
        {
            return leftAddress != 0 && leftAddress == rightAddress;
        }

        internal static bool IsEntityHiddenByMinimapIcon(Entity entity)
        {
            MinimapIcon? minimapIcon = entity.GetComponent<MinimapIcon>();
            return minimapIcon != null && minimapIcon.IsHide;
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

        private bool ShouldSuppressPathfindingLabel(LabelOnGround label)
        {
            return ShouldSuppressPathfindingLabelCore(
                LabelSelection.ShouldSuppressLeverClick(label),
                ShouldSuppressInactiveUltimatumLabel(label));
        }

        internal static bool ShouldSuppressPathfindingLabelCore(bool suppressLeverClick, bool suppressInactiveUltimatum)
        {
            return suppressLeverClick || suppressInactiveUltimatum;
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

        internal static bool ShouldForceUiHoverVerificationForLabel(LabelOnGround? label)
        {
            Entity? item = label?.ItemOnGround;
            if (item == null || item.Type != ExileCore.Shared.Enums.EntityType.WorldItem)
                return false;

            return InputHandler.ShouldForceUiHoverVerificationForWorldItem(item.Path, item.RenderName);
        }

        internal static int GetOffscreenPathfindingTargetSearchDistance()
        {
            return 50000;
        }

        internal static (bool ShouldDelay, long NextAddress, string NextPath, long NextFirstSeenTimestampMs, long RemainingDelayMs)
            EvaluateOffscreenTraversalTargetConfirmation(
                long targetAddress,
                string? targetPath,
                long pendingAddress,
                string? pendingPath,
                long pendingFirstSeenTimestampMs,
                long now,
                int confirmationWindowMs)
        {
            string normalizedPath = targetPath ?? string.Empty;

            if (confirmationWindowMs <= 0)
            {
                return (false, targetAddress, normalizedPath, now, 0);
            }

            bool isSameTarget = IsSameOffscreenTraversalTarget(targetAddress, normalizedPath, pendingAddress, pendingPath);
            if (!isSameTarget)
            {
                return (true, targetAddress, normalizedPath, now, confirmationWindowMs);
            }

            long firstSeen = pendingFirstSeenTimestampMs > 0 ? pendingFirstSeenTimestampMs : now;
            long elapsed = Math.Max(0, now - firstSeen);
            if (elapsed >= confirmationWindowMs)
            {
                return (false, targetAddress, normalizedPath, firstSeen, 0);
            }

            return (true, targetAddress, normalizedPath, firstSeen, confirmationWindowMs - elapsed);
        }

        internal static bool IsSameOffscreenTraversalTarget(long leftAddress, string? leftPath, long rightAddress, string? rightPath)
        {
            if (leftAddress != 0 && rightAddress != 0)
                return leftAddress == rightAddress;

            return string.Equals(leftPath ?? string.Empty, rightPath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        internal static LabelOnGround? FindVisibleLabelForEntity(Entity entity, IReadOnlyList<LabelOnGround>? labels)
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
