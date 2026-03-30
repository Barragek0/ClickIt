using ClickIt.Definitions;
using ClickIt.Utils;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using System.Windows.Forms;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private sealed class LabelSelectionCoordinator(ClickService owner)
        {
            public bool ShouldPreferShrineOverLabel(LabelOnGround? label, Entity? shrine)
            {
                if (shrine == null)
                    return false;
                if (label == null)
                    return true;

                string? labelMechanicId = owner.labelFilterService.GetMechanicIdForLabel(label);
                if (string.IsNullOrWhiteSpace(labelMechanicId))
                    return true;

                owner.RefreshMechanicPriorityCaches();
                MechanicPriorityContext mechanicPriorityContext = owner.CreateMechanicPriorityContext();

                float labelDistance = label.ItemOnGround?.DistancePlayer ?? float.MaxValue;
                float shrineDistance = shrine.DistancePlayer;
                RectangleF windowArea = owner.gameController.Window.GetWindowRectangleTimeCache;
                Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                Vector2 cursorAbsolute = GetCursorAbsolutePosition();
                return ShouldPreferShrineOverLabelForOffscreen(
                    CreateMechanicCandidateSignal(
                        ShrineMechanicId,
                        shrineDistance,
                        owner.TryGetCursorDistanceSquaredToEntity(shrine, cursorAbsolute, windowTopLeft)),
                    CreateMechanicCandidateSignal(
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
                if (owner.gameController?.Window == null)
                    return false;

                RectangleF windowArea = owner.gameController.Window.GetWindowRectangleTimeCache;
                Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
                var cursor = Mouse.GetCursorPosition();
                Vector2 cursorAbsolute = new(cursor.X, cursor.Y);

                if (owner.TryClickManualCursorPreferredAltarOption(cursorAbsolute, windowTopLeft))
                    return true;

                if (TryResolveManualCursorLabelCandidate(allLabels, cursorAbsolute, windowTopLeft, out LabelOnGround? hoveredLabel, out string? mechanicId))
                {
                    if (ShouldAttemptManualCursorAltarClick(IsAltarLabel(hoveredLabel), owner.HasClickableAltars()))
                        return owner.TryClickManualCursorPreferredAltarOption(cursorAbsolute, windowTopLeft);

                    if (owner.TryCorruptEssence(hoveredLabel, windowTopLeft))
                        return true;

                    if (owner.settings.IsInitialUltimatumClickEnabled() && IsUltimatumLabel(hoveredLabel))
                        return owner.TryClickPreferredUltimatumModifier(hoveredLabel, windowTopLeft);

                    if (!owner.TryResolveLabelClickPosition(
                        hoveredLabel,
                        mechanicId,
                        windowTopLeft,
                        allLabels,
                        out Vector2 clickPos))
                    {
                        return false;
                    }

                    bool clicked = ShouldUseHoldClickForSettlersMechanic(mechanicId)
                        ? owner.PerformLabelHoldClick(clickPos, null, owner.gameController, holdDurationMs: 0, forceUiHoverVerification: false, allowWhenHotkeyInactive: true, avoidCursorMove: true)
                        : owner.PerformLabelClick(clickPos, null, owner.gameController, forceUiHoverVerification: false, allowWhenHotkeyInactive: true, avoidCursorMove: true);

                    if (!clicked)
                        return false;

                    owner.ChestLootSettlement.MarkPendingChestOpenConfirmation(mechanicId, hoveredLabel);
                    MarkLeverClicked(hoveredLabel);
                    if (owner.settings.WalkTowardOffscreenLabels.Value)
                    {
                        owner.pathfindingService.ClearLatestPath();
                    }

                    return true;
                }

                return TryClickManualCursorVisibleMechanic(cursorAbsolute, windowTopLeft);
            }

            public bool ShouldSkipOrHandleSpecialLabel(LabelOnGround nextLabel, Vector2 windowTopLeft)
            {
                if (IsAltarLabel(nextLabel))
                {
                    bool shouldContinuePathing = ShouldContinuePathingForSpecialAltarLabel(
                        owner.settings.WalkTowardOffscreenLabels.Value,
                        nextLabel.ItemOnGround != null,
                        nextLabel.ItemOnGround?.IsHidden == true,
                        owner.HasClickableAltars());
                    if (shouldContinuePathing)
                    {
                        _ = owner.OffscreenPathing.TryWalkTowardOffscreenTarget(nextLabel.ItemOnGround);
                        owner.DebugLog(() => "[ProcessRegularClick] Item is an altar and altar choices are not fully clickable yet; continuing pathing");
                        return true;
                    }

                    owner.DebugLog(() => "[ProcessRegularClick] Item is an altar, breaking");
                    return true;
                }

                if (owner.TryCorruptEssence(nextLabel, windowTopLeft))
                    return true;

                if (!owner.settings.IsInitialUltimatumClickEnabled() || !IsUltimatumLabel(nextLabel))
                    return false;

                if (owner.TryClickPreferredUltimatumModifier(nextLabel, windowTopLeft))
                    return true;

                owner.DebugLog(() => "[ProcessRegularClick] Ultimatum label detected but no preferred modifier matched; skipping generic label click");
                return true;
            }

            public bool ShouldSuppressLeverClick(LabelOnGround label)
            {
                if (!owner.settings.LazyMode.Value)
                    return false;
                if (!IsLeverLabel(label))
                    return false;

                int cooldownMs = owner.settings.LazyModeLeverReclickDelay?.Value ?? 1200;
                ulong currentLeverKey = GetLeverIdentityKey(label);
                long now = Environment.TickCount64;

                return IsLeverClickSuppressedByCooldown(owner._lastLeverKey, owner._lastLeverClickTimestampMs, currentLeverKey, now, cooldownMs);
            }

            public void MarkLeverClicked(LabelOnGround label)
            {
                if (!owner.settings.LazyMode.Value)
                    return;
                if (!IsLeverLabel(label))
                    return;

                ulong key = GetLeverIdentityKey(label);
                if (key == 0)
                    return;

                owner._lastLeverKey = key;
                owner._lastLeverClickTimestampMs = Environment.TickCount64;
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
                        || owner.inputHandler.IsLabelFullyOverlapped(candidate, allLabels))
                    {
                        continue;
                    }

                    string? mechanicId = owner.labelFilterService.GetMechanicIdForLabel(candidate);
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

            private bool TryClickManualCursorVisibleMechanic(Vector2 cursorAbsolute, Vector2 windowTopLeft)
            {
                int selectedType = 0;
                float bestDistanceSq = float.MaxValue;
                Vector2 selectedClickPos = default;
                Entity? selectedEntity = null;
                string? selectedSettlersMechanicId = null;

                Entity? shrine = owner.VisibleMechanics.ResolveNextShrineCandidate();
                if (shrine != null)
                {
                    var shrineScreenRaw = owner.gameController.Game.IngameState.Camera.WorldToScreen(shrine.PosNum);
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

                owner.VisibleMechanics.ResolveVisibleMechanicCandidates(out LostShipmentCandidate? lostShipment, out SettlersOreCandidate? settlers);
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
                    ? owner.PerformLabelHoldClick(selectedClickPos, null, owner.gameController, holdDurationMs: 0, forceUiHoverVerification: false, allowWhenHotkeyInactive: true, avoidCursorMove: true)
                    : owner.PerformLabelClick(selectedClickPos, null, owner.gameController, forceUiHoverVerification: false, allowWhenHotkeyInactive: true, avoidCursorMove: true);

                if (!clicked)
                    return false;

                if (selectedType == 1)
                {
                    owner.shrineService.InvalidateCache();
                }

                owner.VisibleMechanics.HandleSuccessfulMechanicEntityClick(selectedEntity);
                return true;
            }

            private bool TryGetGroundProjectionPoint(Entity? item, Vector2 windowTopLeft, out Vector2 projectedPoint)
            {
                projectedPoint = default;
                if (item == null || !item.IsValid)
                    return false;

                try
                {
                    var worldScreenRaw = owner.gameController.Game.IngameState.Camera.WorldToScreen(item.PosNum);
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

                var uiHover = owner.gameController?.IngameState?.UIHoverElement;
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
                    owner.DebugLog(() => "[ProcessRegularClick] UIHover-first: switching target to UIHover label");
                    return hovered;
                }

                return nextLabel;
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

            private LabelOnGround? FindNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels)
            {
                if (allLabels == null || allLabels.Count == 0)
                    return null;

                int searchLimit = GetGroundLabelSearchLimit(allLabels.Count);
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
                    LabelOnGround? label = owner.labelFilterService.GetNextLabelToClick(allLabels, currentStart, endExclusive - currentStart);
                    if (label == null)
                    {
                        if (owner.ShouldCaptureClickDebug())
                        {
                            string noLabelSummary = owner.BuildLabelRangeRejectionDebugSummary(allLabels, start, endExclusive, examined);
                            owner.PublishClickFlowDebugStage("FindLabelNull", noLabelSummary);
                        }
                        if (examined > 0)
                        {
                            owner.DebugLog(() =>
                                $"[LabelSelectDiag] range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} im:{indexMisses}");
                        }
                        return null;
                    }

                    examined++;

                    bool suppressLever = ShouldSuppressLeverClick(label);
                    bool suppressUltimatum = ClickService.ShouldSuppressInactiveUltimatumLabel(label);
                    bool fullyOverlapped = owner.inputHandler.IsLabelFullyOverlapped(label, allLabels);

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
                        owner.PublishClickFlowDebugStage("FindLabelMatch", $"range:{start}-{endExclusive} examined:{examined}");
                        return label;
                    }

                    if (fullyOverlapped)
                    {
                        owner.DebugLog(() => "[ProcessRegularClick] Skipping fully-overlapped label");
                    }

                    int idx = IndexOfLabelReference(allLabels, label, currentStart, endExclusive);
                    if (idx < 0)
                    {
                        indexMisses++;
                        owner.PublishClickFlowDebugStage("FindLabelIndexMiss", $"range:{start}-{endExclusive} examined:{examined} misses:{indexMisses}");
                        owner.DebugLog(() =>
                            $"[LabelSelectDiag] index-miss range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} im:{indexMisses}");
                        return null;
                    }

                    currentStart = idx + 1;
                }

                if (examined > 0)
                {
                    owner.PublishClickFlowDebugStage("FindLabelExhausted", $"range:{start}-{endExclusive} examined:{examined}");
                    owner.DebugLog(() =>
                        $"[LabelSelectDiag] exhausted range:{start}-{endExclusive} examined:{examined} lv:{leverSuppressed} ul:{ultimatumSuppressed} ov:{overlappedSuppressed} im:{indexMisses}");
                }

                return null;
            }
        }
    }
}