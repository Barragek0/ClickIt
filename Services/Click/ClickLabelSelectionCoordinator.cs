using ClickIt.Definitions;
using ExileCore;
using ClickIt.Utils;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using System.Windows.Forms;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    internal readonly record struct LabelSelectionCoordinatorDependencies(
        ClickItSettings Settings,
        GameController GameController,
        LabelFilterService LabelFilterService,
        InputHandler InputHandler,
        Func<bool> HasClickableAltars,
        Func<Vector2, Vector2, bool> TryClickManualCursorPreferredAltarOption,
        Func<LabelOnGround, Vector2, bool> TryCorruptEssence,
        Func<LabelOnGround, Vector2, bool> TryClickPreferredUltimatumModifier,
        Func<LabelOnGround, string?, Vector2, IReadOnlyList<LabelOnGround>?, (bool Success, Vector2 ClickPos)> TryResolveLabelClickPosition,
        Func<Vector2, bool, bool> PerformManualCursorInteraction,
        Action<string?, LabelOnGround> MarkPendingChestOpenConfirmation,
        Action ClearLatestPath,
        Action<string> DebugLog,
        Func<Entity?> ResolveNextShrineCandidate,
        Func<(LostShipmentCandidate? LostShipment, SettlersOreCandidate? Settlers)> ResolveVisibleMechanicCandidates,
        Action<Entity?> HandleSuccessfulMechanicEntityClick,
        Action InvalidateShrineCache,
        Func<Entity, Vector2, Vector2, float?> TryGetCursorDistanceSquaredToEntity,
        Action RefreshMechanicPriorityCaches,
        Func<MechanicPriorityContext> CreateMechanicPriorityContext,
        Func<bool> ShouldCaptureClickDebug,
        Func<IReadOnlyList<LabelOnGround>?, int, int, int, string> BuildLabelRangeRejectionDebugSummary,
        Action<string, string> PublishClickFlowDebugStage,
        Func<ulong> GetLastLeverKey,
        Action<ulong> SetLastLeverKey,
        Func<long> GetLastLeverClickTimestampMs,
        Action<long> SetLastLeverClickTimestampMs);

    internal sealed class LabelSelectionCoordinator(LabelSelectionCoordinatorDependencies dependencies)
    {
        private readonly LabelSelectionCoordinatorDependencies _dependencies = dependencies;

        public bool ShouldPreferShrineOverLabel(LabelOnGround? label, Entity? shrine)
        {
            if (shrine == null)
                return false;
            if (label == null)
                return true;

            string? labelMechanicId = _dependencies.LabelFilterService.GetMechanicIdForLabel(label);
            if (string.IsNullOrWhiteSpace(labelMechanicId))
                return true;

            _dependencies.RefreshMechanicPriorityCaches();
            MechanicPriorityContext mechanicPriorityContext = _dependencies.CreateMechanicPriorityContext();

            float labelDistance = label.ItemOnGround?.DistancePlayer ?? float.MaxValue;
            float shrineDistance = shrine.DistancePlayer;
            RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 cursorAbsolute = ClickService.GetCursorAbsolutePosition();
            return ClickService.ShouldPreferShrineOverLabelForOffscreen(
                ClickService.CreateMechanicCandidateSignal(
                    MechanicIds.Shrines,
                    shrineDistance,
                    _dependencies.TryGetCursorDistanceSquaredToEntity(shrine, cursorAbsolute, windowTopLeft)),
                ClickService.CreateMechanicCandidateSignal(
                    labelMechanicId,
                    labelDistance,
                    ClickService.TryGetCursorDistanceSquaredToLabel(label, cursorAbsolute, windowTopLeft)),
                mechanicPriorityContext);
        }

        public LabelOnGround? ResolveNextLabelCandidate(IReadOnlyList<LabelOnGround>? allLabels)
        {
            LabelOnGround? nextLabel = FindNextLabelToClick(allLabels);
            return PreferUiHoverEssenceLabel(nextLabel, allLabels);
        }

        public bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (_dependencies.GameController.Window == null)
                return false;

            RectangleF windowArea = _dependencies.GameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            var cursor = Mouse.GetCursorPosition();
            Vector2 cursorAbsolute = new(cursor.X, cursor.Y);

            if (_dependencies.TryClickManualCursorPreferredAltarOption(cursorAbsolute, windowTopLeft))
                return true;

            if (TryResolveManualCursorLabelCandidate(allLabels, cursorAbsolute, windowTopLeft, out LabelOnGround? hoveredLabel, out string? mechanicId))
            {
                if (ClickService.ShouldAttemptManualCursorAltarClick(ClickService.IsAltarLabel(hoveredLabel), _dependencies.HasClickableAltars()))
                    return _dependencies.TryClickManualCursorPreferredAltarOption(cursorAbsolute, windowTopLeft);

                if (_dependencies.TryCorruptEssence(hoveredLabel, windowTopLeft))
                    return true;

                if (_dependencies.Settings.IsInitialUltimatumClickEnabled() && ClickService.IsUltimatumLabel(hoveredLabel))
                    return _dependencies.TryClickPreferredUltimatumModifier(hoveredLabel, windowTopLeft);

                (bool resolved, Vector2 clickPos) = _dependencies.TryResolveLabelClickPosition(hoveredLabel, mechanicId, windowTopLeft, allLabels);
                if (!resolved)
                    return false;

                bool clicked = _dependencies.PerformManualCursorInteraction(clickPos, ClickService.ShouldUseHoldClickForSettlersMechanic(mechanicId));
                if (!clicked)
                    return false;

                _dependencies.MarkPendingChestOpenConfirmation(mechanicId, hoveredLabel);
                MarkLeverClicked(hoveredLabel);
                if (_dependencies.Settings.WalkTowardOffscreenLabels.Value)
                    _dependencies.ClearLatestPath();

                return true;
            }

            return TryClickManualCursorVisibleMechanic(cursorAbsolute, windowTopLeft);
        }

        public bool ShouldSkipOrHandleSpecialLabel(LabelOnGround nextLabel, Vector2 windowTopLeft)
        {
            if (ClickService.IsAltarLabel(nextLabel))
            {
                bool shouldContinuePathing = ClickService.ShouldContinuePathingForSpecialAltarLabel(
                    _dependencies.Settings.WalkTowardOffscreenLabels.Value,
                    nextLabel.ItemOnGround != null,
                    nextLabel.ItemOnGround?.IsHidden == true,
                    _dependencies.HasClickableAltars());
                if (shouldContinuePathing)
                {
                    _dependencies.DebugLog("[ProcessRegularClick] Item is an altar and altar choices are not fully clickable yet; continuing pathing");
                    return true;
                }

                _dependencies.DebugLog("[ProcessRegularClick] Item is an altar, breaking");
                return true;
            }

            if (_dependencies.TryCorruptEssence(nextLabel, windowTopLeft))
                return true;

            if (!_dependencies.Settings.IsInitialUltimatumClickEnabled() || !ClickService.IsUltimatumLabel(nextLabel))
                return false;

            if (_dependencies.TryClickPreferredUltimatumModifier(nextLabel, windowTopLeft))
                return true;

            _dependencies.DebugLog("[ProcessRegularClick] Ultimatum label detected but no preferred modifier matched; skipping generic label click");
            return true;
        }

        public bool ShouldSuppressLeverClick(LabelOnGround label)
        {
            if (!_dependencies.Settings.LazyMode.Value)
                return false;
            if (!ClickService.IsLeverLabel(label))
                return false;

            int cooldownMs = _dependencies.Settings.LazyModeLeverReclickDelay?.Value ?? 1200;
            ulong currentLeverKey = ClickService.GetLeverIdentityKey(label);
            long now = Environment.TickCount64;

            return ClickService.IsLeverClickSuppressedByCooldown(
                _dependencies.GetLastLeverKey(),
                _dependencies.GetLastLeverClickTimestampMs(),
                currentLeverKey,
                now,
                cooldownMs);
        }

        public void MarkLeverClicked(LabelOnGround label)
        {
            if (!_dependencies.Settings.LazyMode.Value)
                return;
            if (!ClickService.IsLeverLabel(label))
                return;

            ulong key = ClickService.GetLeverIdentityKey(label);
            if (key == 0)
                return;

            _dependencies.SetLastLeverKey(key);
            _dependencies.SetLastLeverClickTimestampMs(Environment.TickCount64);
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
                    || ClickService.ShouldSuppressInactiveUltimatumLabel(candidate)
                    || _dependencies.InputHandler.IsLabelFullyOverlapped(candidate, allLabels))
                {
                    continue;
                }

                string? mechanicId = _dependencies.LabelFilterService.GetMechanicIdForLabel(candidate);
                if (string.IsNullOrWhiteSpace(mechanicId))
                    continue;

                Entity? candidateEntity = candidate.ItemOnGround;
                bool shouldUseGroundProjection = ClickService.ShouldUseManualGroundProjectionForCandidate(
                    hasBackingEntity: candidateEntity != null,
                    isWorldItem: candidateEntity?.Type == ExileCore.Shared.Enums.EntityType.WorldItem);
                Vector2 projectedGroundPoint = default;

                bool hasLabelRect = LabelUtils.TryGetLabelRect(candidate, out RectangleF rect);
                bool cursorInsideLabelRect = hasLabelRect && ClickService.IsPointInsideRectInEitherSpace(rect, cursorAbsolute, windowTopLeft);
                bool cursorNearGroundProjection = shouldUseGroundProjection
                    && TryGetGroundProjectionPoint(candidateEntity, windowTopLeft, out projectedGroundPoint)
                    && ClickService.IsWithinManualCursorMatchDistanceInEitherSpace(cursorAbsolute, projectedGroundPoint, windowTopLeft, ClickService.ManualCursorGroundProjectionSnapDistancePx);

                if (!ClickService.ShouldTreatManualCursorAsHoveringCandidate(cursorInsideLabelRect, cursorNearGroundProjection))
                    continue;

                float score = float.MaxValue;
                if (cursorInsideLabelRect)
                {
                    score = ClickService.GetManualCursorLabelHitScore(rect, cursorAbsolute, windowTopLeft);
                }

                if (cursorNearGroundProjection)
                {
                    float objectScore = ClickService.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, projectedGroundPoint, windowTopLeft);
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

        private bool TryClickManualCursorVisibleMechanic(Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            int selectedType = 0;
            float bestDistanceSq = float.MaxValue;
            Vector2 selectedClickPos = default;
            Entity? selectedEntity = null;
            string? selectedSettlersMechanicId = null;

            Entity? shrine = _dependencies.ResolveNextShrineCandidate();
            if (shrine != null)
            {
                var shrineScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
                Vector2 shrineClickPos = new(shrineScreenRaw.X, shrineScreenRaw.Y);
                if (ClickService.IsWithinManualCursorMatchDistanceInEitherSpace(cursorAbsolute, shrineClickPos, windowTopLeft, ClickService.ManualCursorTargetSnapDistancePx))
                {
                    float d2 = ClickService.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, shrineClickPos, windowTopLeft);
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

            (LostShipmentCandidate? lostShipment, SettlersOreCandidate? settlers) = _dependencies.ResolveVisibleMechanicCandidates();
            if (lostShipment.HasValue)
            {
                LostShipmentCandidate candidate = lostShipment.Value;
                if (ClickService.IsWithinManualCursorMatchDistanceInEitherSpace(cursorAbsolute, candidate.ClickPosition, windowTopLeft, ClickService.ManualCursorTargetSnapDistancePx))
                {
                    float d2 = ClickService.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, candidate.ClickPosition, windowTopLeft);
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
                if (ClickService.IsWithinManualCursorMatchDistanceInEitherSpace(cursorAbsolute, candidate.ClickPosition, windowTopLeft, ClickService.ManualCursorTargetSnapDistancePx))
                {
                    float d2 = ClickService.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, candidate.ClickPosition, windowTopLeft);
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

            bool clicked = _dependencies.PerformManualCursorInteraction(
                selectedClickPos,
                selectedType == 3 && ClickService.ShouldUseHoldClickForSettlersMechanic(selectedSettlersMechanicId));

            if (!clicked)
                return false;

            if (selectedType == 1)
                _dependencies.InvalidateShrineCache();

            _dependencies.HandleSuccessfulMechanicEntityClick(selectedEntity);
            return true;
        }

        private bool TryGetGroundProjectionPoint(Entity? item, Vector2 windowTopLeft, out Vector2 projectedPoint)
        {
            projectedPoint = default;
            if (item == null || !item.IsValid)
                return false;

            try
            {
                var worldScreenRaw = _dependencies.GameController.Game.IngameState.Camera.WorldToScreen(item.PosNum);
                projectedPoint = new Vector2(worldScreenRaw.X + windowTopLeft.X, worldScreenRaw.Y + windowTopLeft.Y);
                return float.IsFinite(projectedPoint.X) && float.IsFinite(projectedPoint.Y);
            }
            catch
            {
                return false;
            }
        }

        private LabelOnGround? PreferUiHoverEssenceLabel(LabelOnGround? nextLabel, IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (allLabels == null)
                return nextLabel;

            var uiHover = _dependencies.GameController?.IngameState?.UIHoverElement;
            if (uiHover == null)
                return nextLabel;

            LabelOnGround? hovered = ClickService.FindLabelByAddress(allLabels, uiHover.Address);
            if (hovered == null)
                return nextLabel;

            bool hoveredIsEssence = ClickService.IsEssenceLabel(hovered);
            bool nextIsEssence = nextLabel != null && ClickService.IsEssenceLabel(nextLabel);
            bool hoveredHasOverlappingEssence = hoveredIsEssence && HasOverlappingEssenceLabel(hovered, allLabels);
            bool hoveredDiffersFromNext = !ReferenceEquals(hovered, nextLabel);

            if (ClickService.ShouldPreferHoveredEssenceLabel(hoveredIsEssence, hoveredHasOverlappingEssence, nextIsEssence, hoveredDiffersFromNext))
            {
                _dependencies.DebugLog("[ProcessRegularClick] UIHover-first: switching target to UIHover label");
                return hovered;
            }

            return nextLabel;
        }

        private static bool HasOverlappingEssenceLabel(LabelOnGround hoveredEssence, IReadOnlyList<LabelOnGround> allLabels)
        {
            if (!LabelUtils.TryGetLabelRect(hoveredEssence, out RectangleF hoveredRect))
                return false;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround? candidate = allLabels[i];
                if (candidate == null || ReferenceEquals(candidate, hoveredEssence) || !ClickService.IsEssenceLabel(candidate))
                    continue;

                if (!LabelUtils.TryGetLabelRect(candidate, out RectangleF candidateRect))
                    continue;

                if (hoveredRect.Intersects(candidateRect))
                    return true;
            }

            return false;
        }

        private LabelOnGround? FindNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels)
        {
            if (allLabels == null || allLabels.Count == 0)
                return null;

            int searchLimit = ClickService.GetGroundLabelSearchLimit(allLabels.Count);
            return FindLabelInRange(allLabels, 0, searchLimit);
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
                LabelOnGround? label = _dependencies.LabelFilterService.GetNextLabelToClick(allLabels, currentStart, endExclusive - currentStart);
                if (label == null)
                {
                    if (_dependencies.ShouldCaptureClickDebug())
                    {
                        string noLabelSummary = _dependencies.BuildLabelRangeRejectionDebugSummary(allLabels, start, endExclusive, examined);
                        _dependencies.PublishClickFlowDebugStage("FindLabelNull", noLabelSummary);
                    }
                    if (examined > 0)
                    {
                        _dependencies.DebugLog($"[LabelSelectDiag] range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} im:{indexMisses}");
                    }
                    return null;
                }

                examined++;

                bool suppressLever = ShouldSuppressLeverClick(label);
                bool suppressUltimatum = ClickService.ShouldSuppressInactiveUltimatumLabel(label);
                bool fullyOverlapped = _dependencies.InputHandler.IsLabelFullyOverlapped(label, allLabels);

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
                    _dependencies.PublishClickFlowDebugStage("FindLabelMatch", $"range:{start}-{endExclusive} examined:{examined}");
                    return label;
                }

                if (fullyOverlapped)
                    _dependencies.DebugLog("[ProcessRegularClick] Skipping fully-overlapped label");

                int idx = ClickService.IndexOfLabelReference(allLabels, label, currentStart, endExclusive);
                if (idx < 0)
                {
                    indexMisses++;
                    _dependencies.PublishClickFlowDebugStage("FindLabelIndexMiss", $"range:{start}-{endExclusive} examined:{examined} misses:{indexMisses}");
                    _dependencies.DebugLog($"[LabelSelectDiag] index-miss range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} im:{indexMisses}");
                    return null;
                }

                currentStart = idx + 1;
            }

            if (examined > 0)
            {
                _dependencies.PublishClickFlowDebugStage("FindLabelExhausted", $"range:{start}-{endExclusive} examined:{examined}");
                _dependencies.DebugLog($"[LabelSelectDiag] exhausted range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} im:{indexMisses}");
            }

            return null;
        }
    }
}