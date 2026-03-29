using System.Threading;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

#nullable enable

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private enum GruelingGauntletAction
        {
            ConfirmOnly = 1,
            TakeRewards = 2
        }

        private const int GruelingGauntletAtlasPassiveSkillId = 9882;
        private const int GruelingGauntletPassiveCacheWindowMs = 100;
        private static bool _lastGruelingGauntletDetectionIsActive;
        private static bool _lastGruelingGauntletDetectionHasValue;

        internal static bool TryGetGruelingGauntletDetectionForSettings(out bool isActive)
        {
            isActive = _lastGruelingGauntletDetectionIsActive;
            return _lastGruelingGauntletDetectionHasValue;
        }

        private bool TryHandleUltimatumPanelUi(Vector2 windowTopLeft)
        {
            if (!settings.IsOtherUltimatumClickEnabled())
            {
                PublishUltimatumDebug(new UltimatumDebugEvent("PanelSkip", "PanelUi", false, false)
                {
                    Notes = "Other Ultimatum click setting disabled"
                });
                return false;
            }

            if (!TryGetVisibleUltimatumPanel(out UltimatumPanel? panelObj) || panelObj == null)
            {
                PublishUltimatumDebug(new UltimatumDebugEvent("PanelMissing", "PanelUi", false, GetGruelingGauntletDetectionForDebug())
                {
                    Notes = "Ultimatum panel not visible/available"
                });
                return false;
            }

            DebugLog(() => "[TryHandleUltimatumPanelUi] UltimatumPanel detected.");

            bool isGruelingGauntletActive = IsGruelingGauntletPassiveActive();
            if (isGruelingGauntletActive)
                return TryHandleGruelingGauntletPanelUi(panelObj, windowTopLeft);

            bool clickedAny = false;
            bool clickedChoice = false;
            bool clickedConfirm = false;

            if (TryClickUltimatumPanelChoice(panelObj, windowTopLeft))
            {
                clickedAny = true;
                clickedChoice = true;
                Thread.Sleep(UltimatumChoiceToBeginDelayMs);
            }

            if (TryClickUltimatumPanelConfirm(panelObj, windowTopLeft))
            {
                clickedAny = true;
                clickedConfirm = true;
                Thread.Sleep(UltimatumPostBeginDelayMs);
            }

            PublishUltimatumDebug(new UltimatumDebugEvent("PanelHandled", "PanelUi", true, false)
            {
                ClickedChoice = clickedChoice,
                ClickedConfirm = clickedConfirm,
                ClickedTakeRewards = false,
                Notes = clickedAny ? "Panel action executed" : "No panel action executed"
            });

            return clickedAny;
        }

        private bool TryHandleGruelingGauntletPanelUi(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            bool clickedAny;

            bool hasSaturatedChoice = false;
            string saturatedModifier = string.Empty;
            bool shouldTakeReward = false;
            int candidateCount = 0;
            int saturatedCandidateCount = 0;
            string bestModifier = string.Empty;
            int bestPriority = int.MaxValue;

            if (TryGetUltimatumPanelChoiceCandidates(panelObj, out List<UltimatumPanelChoiceCandidate> candidates, isGruelingGauntletActive: true, logFailures: true))
            {
                candidateCount = candidates.Count;
                ResolveGruelingSaturation(candidates, out hasSaturatedChoice, out saturatedModifier, out shouldTakeReward, out saturatedCandidateCount);

                if (TryGetSelectedUltimatumPanelChoice(candidates, isGruelingGauntletActive: true, out UltimatumPanelChoiceCandidate best))
                {
                    bestModifier = best.ModifierName;
                    bestPriority = best.PriorityIndex;
                }
            }
            else
            {
                DebugLog(() => "[TryHandleUltimatumPanelUi] Grueling Gauntlet active but no saturated choice was found. Falling back to confirm-only action.");
            }

            bool canClickTakeRewards = settings.IsUltimatumTakeRewardButtonClickEnabled();
            GruelingGauntletAction action = DetermineGruelingGauntletActionCore(hasSaturatedChoice, shouldTakeReward, canClickTakeRewards);
            DebugLog(() => $"[TryHandleUltimatumPanelUi] Grueling Gauntlet action={action}, saturatedModifier='{saturatedModifier}', shouldTakeReward={shouldTakeReward}");

            if (ShouldSuppressGruelingGauntletClickCore(shouldTakeReward, canClickTakeRewards))
            {
                PublishUltimatumDebug(new UltimatumDebugEvent("PanelGruelingHandled", "PanelUi", true, true)
                {
                    HasSaturatedChoice = hasSaturatedChoice,
                    SaturatedModifier = saturatedModifier,
                    ShouldTakeReward = shouldTakeReward,
                    Action = action.ToString(),
                    CandidateCount = candidateCount,
                    SaturatedCandidateCount = saturatedCandidateCount,
                    BestModifier = bestModifier,
                    BestPriority = bestPriority,
                    ClickedChoice = false,
                    ClickedConfirm = false,
                    ClickedTakeRewards = false,
                    Notes = "Take Reward matched but Click Take Reward Button is disabled; no click performed"
                });
                return false;
            }

            bool clickedConfirm = false;
            bool clickedTakeRewards = false;
            if (action == GruelingGauntletAction.TakeRewards)
                clickedTakeRewards = TryClickUltimatumPanelTakeRewards(panelObj, windowTopLeft);
            else
                clickedConfirm = TryClickUltimatumPanelConfirm(panelObj, windowTopLeft);

            clickedAny = clickedConfirm || clickedTakeRewards;
            if (clickedAny)
                Thread.Sleep(UltimatumPostBeginDelayMs);

            string note = action == GruelingGauntletAction.TakeRewards
                ? (clickedTakeRewards ? "Take Rewards clicked" : "Take Rewards action selected but click failed")
                : (clickedConfirm ? "Confirm clicked" : "Confirm action selected but click failed");

            PublishUltimatumDebug(new UltimatumDebugEvent("PanelGruelingHandled", "PanelUi", true, true)
            {
                HasSaturatedChoice = hasSaturatedChoice,
                SaturatedModifier = saturatedModifier,
                ShouldTakeReward = shouldTakeReward,
                Action = action.ToString(),
                CandidateCount = candidateCount,
                SaturatedCandidateCount = saturatedCandidateCount,
                BestModifier = bestModifier,
                BestPriority = bestPriority,
                ClickedChoice = false,
                ClickedConfirm = clickedConfirm,
                ClickedTakeRewards = clickedTakeRewards,
                Notes = note
            });

            return clickedAny;
        }

        public bool TryGetUltimatumPanelOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

            if (!TryGetVisibleUltimatumPanel(out UltimatumPanel? panelObj, logFailures: false) || panelObj == null)
                return false;

            bool isGruelingGauntletActive = IsGruelingGauntletPassiveActive();
            if (!TryGetUltimatumPanelChoiceCandidates(panelObj, out List<UltimatumPanelChoiceCandidate> candidates, isGruelingGauntletActive, logFailures: false)
                || candidates.Count == 0)
                return false;

            bool hasBest = TryGetSelectedUltimatumPanelChoice(candidates, isGruelingGauntletActive, out UltimatumPanelChoiceCandidate best);
            if (ShouldCaptureUltimatumDebug())
            {
                ResolveGruelingSaturation(candidates, out bool hasSaturatedChoice, out string saturatedModifier, out bool shouldTakeReward, out int saturatedCount);
                GruelingGauntletAction action = DetermineGruelingGauntletActionCore(
                    hasSaturatedChoice,
                    shouldTakeReward,
                    settings.IsUltimatumTakeRewardButtonClickEnabled());

                PublishUltimatumDebug(new UltimatumDebugEvent("OverlayPreview", "PanelPreview", true, isGruelingGauntletActive)
                {
                    HasSaturatedChoice = hasSaturatedChoice,
                    SaturatedModifier = saturatedModifier,
                    ShouldTakeReward = shouldTakeReward,
                    Action = action.ToString(),
                    CandidateCount = candidates.Count,
                    SaturatedCandidateCount = saturatedCount,
                    BestModifier = hasBest ? best.ModifierName : string.Empty,
                    BestPriority = hasBest ? best.PriorityIndex : int.MaxValue,
                    Notes = "Snapshot published from overlay preview polling"
                });
            }

            foreach (UltimatumPanelChoiceCandidate candidate in candidates)
            {
                if (!candidate.ChoiceElement.IsValid)
                    continue;

                RectangleF rect = candidate.ChoiceElement.GetClientRect();
                if (rect.Width <= 0 || rect.Height <= 0)
                    continue;

                bool isSelected = hasBest && ReferenceEquals(candidate.ChoiceElement, best.ChoiceElement);
                previews.Add(new UltimatumPanelOptionPreview(rect, candidate.ModifierName, candidate.PriorityIndex, isSelected));
            }

            return previews.Count > 0;
        }

        private bool TryGetVisibleUltimatumPanel(out UltimatumPanel? panelObj, bool logFailures = true)
        {
            panelObj = null;

            panelObj = gameController?.IngameState?.IngameUi?.UltimatumPanel;
            if (panelObj == null)
            {
                if (logFailures)
                    DebugLog(() => "[TryHandleUltimatumPanelUi] UltimatumPanel not available.");
                return false;
            }

            if (!panelObj.IsVisible)
            {
                if (logFailures)
                    DebugLog(() => "[TryHandleUltimatumPanelUi] UltimatumPanel exists but is not visible.");
                return false;
            }

            return true;
        }

        private bool TryGetUltimatumPanelChoiceCandidates(
            UltimatumPanel panelObj,
            out List<UltimatumPanelChoiceCandidate> candidates,
            bool isGruelingGauntletActive,
            bool logFailures)
        {
            candidates = [];

            if (!TryGetUltimatumChoiceElements(panelObj, out object? choiceElementsObj, logFailures))
                return false;

            IReadOnlyList<string> modifierNamesByIndex = GetUltimatumPanelModifierNames(panelObj);

            var priorities = settings.GetUltimatumModifierPriority();
            int seen = 0;
            foreach (object? choiceObj in EnumerateObjects(choiceElementsObj))
            {
                if (TryCreateUltimatumPanelChoiceCandidate(
                    choiceObj,
                    seen,
                    modifierNamesByIndex,
                    priorities,
                    isGruelingGauntletActive,
                    logFailures,
                    out UltimatumPanelChoiceCandidate candidate))
                {
                    candidates.Add(candidate);
                }

                seen++;
            }

            return candidates.Count > 0;
        }

        private void ResolveGruelingSaturation(
            IReadOnlyList<UltimatumPanelChoiceCandidate> candidates,
            out bool hasSaturatedChoice,
            out string saturatedModifier,
            out bool shouldTakeReward,
            out int saturatedCount)
        {
            saturatedCount = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].IsSaturated)
                    saturatedCount++;
            }

            hasSaturatedChoice = TryGetSaturatedUltimatumPanelChoice(candidates, out UltimatumPanelChoiceCandidate saturatedChoice);
            saturatedModifier = hasSaturatedChoice ? saturatedChoice.ModifierName : string.Empty;
            shouldTakeReward = hasSaturatedChoice && settings.ShouldTakeRewardForGruelingGauntletModifier(saturatedModifier);
        }

        private bool TryGetUltimatumChoiceElements(UltimatumPanel panelObj, out object? choiceElementsObj, bool logFailures)
        {
            choiceElementsObj = null;

            var choicesPanelObj = panelObj.ChoicesPanel;
            if (choicesPanelObj == null)
            {
                if (logFailures)
                    DebugLog(() => "[TryClickUltimatumPanelChoice] ChoicesPanel missing.");
                return false;
            }

            choiceElementsObj = choicesPanelObj.ChoiceElements;
            if (choiceElementsObj == null)
            {
                if (logFailures)
                    DebugLog(() => "[TryClickUltimatumPanelChoice] ChoiceElements missing.");
                return false;
            }

            return true;
        }

        private bool TryCreateUltimatumPanelChoiceCandidate(
            object? choiceObj,
            int seen,
            IReadOnlyList<string> modifierNamesByIndex,
            IReadOnlyList<string> priorities,
            bool isGruelingGauntletActive,
            bool logFailures,
            out UltimatumPanelChoiceCandidate candidate)
        {
            candidate = default;

            if (!TryExtractElement(choiceObj, out Element? choiceEl) || choiceEl == null)
            {
                if (logFailures)
                    DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] is not an Element.");
                return false;
            }

            if (!choiceEl.IsValid)
            {
                if (logFailures)
                    DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] ignored - valid={choiceEl.IsValid}");
                return false;
            }

            RectangleF choiceRect = choiceEl.GetClientRect();
            if (choiceRect.Width <= 0 || choiceRect.Height <= 0)
            {
                if (logFailures)
                    DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] ignored - empty rect {choiceRect}.");
                return false;
            }

            string modifierName = ResolveUltimatumPanelModifierName(choiceEl, seen, modifierNamesByIndex);
            int priorityIndex = GetModifierPriorityIndex(modifierName, priorities);
            bool saturatedForSelection = false;
            if (isGruelingGauntletActive)
            {
                bool hasSaturationState = TryReadUltimatumChoiceSaturation(choiceEl, out bool isSaturated);
                saturatedForSelection = ShouldTreatUltimatumChoiceAsSaturatedCore(hasSaturationState, isSaturated, choiceEl.IsVisible);
            }

            if (logFailures)
                DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] modifier='{modifierName}', priority={priorityIndex}, saturated={saturatedForSelection}, center={choiceRect.Center}, visible={choiceEl.IsVisible}, valid={choiceEl.IsValid}");

            if (isGruelingGauntletActive && !saturatedForSelection)
            {
                if (logFailures)
                    DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] ignored in Grueling Gauntlet mode because it is not saturated.");
                return false;
            }

            candidate = new UltimatumPanelChoiceCandidate(choiceEl, modifierName, priorityIndex, saturatedForSelection);
            return true;
        }

        private static string ResolveUltimatumPanelModifierName(Element choiceEl, int seen, IReadOnlyList<string> modifierNamesByIndex)
        {
            if (seen < modifierNamesByIndex.Count)
            {
                string modifierFromPanel = modifierNamesByIndex[seen];
                if (!string.IsNullOrWhiteSpace(modifierFromPanel))
                    return modifierFromPanel;
            }

            string modifierName = GetUltimatumModifierName(choiceEl);
            if (!string.IsNullOrWhiteSpace(modifierName))
                return modifierName;

            return NormalizeModifierText(choiceEl.GetText(1024) ?? string.Empty);
        }

        private static IReadOnlyList<string> GetUltimatumPanelModifierNames(UltimatumPanel panelObj)
        {
            return ExtractUltimatumModifierNames(panelObj.Modifiers);
        }

        private static bool TryGetBestUltimatumPanelChoice(IReadOnlyList<UltimatumPanelChoiceCandidate> candidates, out UltimatumPanelChoiceCandidate best)
        {
            best = default;
            int bestIndex = int.MaxValue;
            bool found = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                UltimatumPanelChoiceCandidate candidate = candidates[i];
                if (candidate.PriorityIndex < bestIndex)
                {
                    bestIndex = candidate.PriorityIndex;
                    best = candidate;
                    found = true;
                }
            }

            return found && bestIndex != int.MaxValue;
        }

        private static bool TryGetSaturatedUltimatumPanelChoice(IReadOnlyList<UltimatumPanelChoiceCandidate> candidates, out UltimatumPanelChoiceCandidate best)
        {
            best = default;

            for (int i = 0; i < candidates.Count; i++)
            {
                UltimatumPanelChoiceCandidate candidate = candidates[i];
                if (!candidate.IsSaturated)
                    continue;

                best = candidate;
                return true;
            }

            return false;
        }

        private static bool TryGetSelectedUltimatumPanelChoice(
            IReadOnlyList<UltimatumPanelChoiceCandidate> candidates,
            bool isGruelingGauntletActive,
            out UltimatumPanelChoiceCandidate best)
        {
            if (isGruelingGauntletActive && TryGetSaturatedUltimatumPanelChoice(candidates, out best))
                return true;

            return TryGetBestUltimatumPanelChoice(candidates, out best);
        }

        private static GruelingGauntletAction DetermineGruelingGauntletActionCore(bool hasSaturatedChoice, bool shouldTakeReward, bool canClickTakeReward)
        {
            return hasSaturatedChoice && shouldTakeReward && canClickTakeReward
                ? GruelingGauntletAction.TakeRewards
                : GruelingGauntletAction.ConfirmOnly;
        }

        internal static bool ShouldSuppressGruelingGauntletClickCore(bool shouldTakeReward, bool canClickTakeReward)
            => shouldTakeReward && !canClickTakeReward;

        internal static bool ShouldTreatUltimatumChoiceAsSaturatedCore(bool hasSaturationState, bool isSaturated, bool fallbackVisible)
            => hasSaturationState ? isSaturated : fallbackVisible;

        private static bool TryReadUltimatumChoiceSaturation(Element choiceElement, out bool isSaturated)
        {
            isSaturated = false;
            if (!TryGetDynamicValue(choiceElement, s => s.IsSaturated, out object? rawSaturated) || rawSaturated == null)
                return false;

            if (rawSaturated is bool boolValue)
            {
                isSaturated = boolValue;
                return true;
            }

            try
            {
                isSaturated = Convert.ToBoolean(rawSaturated);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsGruelingGauntletPassiveActive()
        {
            long now = Environment.TickCount64;
            if (_gruelingGauntletPassiveCacheHasValue
                && now - _gruelingGauntletPassiveCacheTimestampMs >= 0
                && now - _gruelingGauntletPassiveCacheTimestampMs <= GruelingGauntletPassiveCacheWindowMs)
            {
                PublishGruelingGauntletDetection(_gruelingGauntletPassiveCachedValue);
                return _gruelingGauntletPassiveCachedValue;
            }

            bool isActive = false;
            object? data = gameController?.IngameState?.Data;
            if (TryGetDynamicValue(data, s => s.ServerData, out object? serverData)
                && serverData != null
                && TryGetDynamicValue(serverData, s => s.AtlasPassiveSkillIds, out object? atlasPassiveIds)
                && atlasPassiveIds != null)
            {
                isActive = ContainsAtlasPassiveSkillId(atlasPassiveIds, GruelingGauntletAtlasPassiveSkillId);
            }

            _gruelingGauntletPassiveCacheTimestampMs = now;
            _gruelingGauntletPassiveCachedValue = isActive;
            _gruelingGauntletPassiveCacheHasValue = true;
            PublishGruelingGauntletDetection(isActive);
            return isActive;
        }

        private bool GetGruelingGauntletDetectionForDebug()
        {
            return ShouldCaptureUltimatumDebug() && IsGruelingGauntletPassiveActive();
        }

        private static void PublishGruelingGauntletDetection(bool isActive)
        {
            _lastGruelingGauntletDetectionIsActive = isActive;
            _lastGruelingGauntletDetectionHasValue = true;
        }

        private static bool ContainsAtlasPassiveSkillId(object atlasPassiveIds, int targetId)
        {
            foreach (object? entry in EnumerateObjects(atlasPassiveIds))
            {
                if (entry == null)
                    continue;

                if (entry is int intId && intId == targetId)
                    return true;

                try
                {
                    int converted = Convert.ToInt32(entry);
                    if (converted == targetId)
                        return true;
                }
                catch
                {
                }
            }

            return false;
        }

        private bool TryClickUltimatumPanelChoice(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            bool isGruelingGauntletActive = IsGruelingGauntletPassiveActive();
            if (!TryGetUltimatumPanelChoiceCandidates(panelObj, out List<UltimatumPanelChoiceCandidate> candidates, isGruelingGauntletActive, logFailures: true))
            {
                DebugLog(() => "[TryClickUltimatumPanelChoice] No ranked choice found.");
                return false;
            }
            if (isGruelingGauntletActive)
            {
                DebugLog(() => "[TryClickUltimatumPanelChoice] Grueling Gauntlet active - skipping modifier click because choice is game-selected.");
                return false;
            }

            if (!TryGetSelectedUltimatumPanelChoice(candidates, isGruelingGauntletActive, out UltimatumPanelChoiceCandidate best))
            {
                DebugLog(() => "[TryClickUltimatumPanelChoice] No ranked choice found.");
                return false;
            }

            return TryClickUltimatumElement(
                best.ChoiceElement,
                windowTopLeft,
                "[TryClickUltimatumPanelChoice] Skipping click - cursor outside PoE window.",
                $"[TryClickUltimatumPanelChoice] Rejected by clickable-area check. best='{best.ModifierName}',",
                $"[TryClickUltimatumPanelChoice] Clicking choice '{best.ModifierName}' (priority={best.PriorityIndex}) at");
        }

        private bool TryClickUltimatumPanelTakeRewards(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            Element? takeRewardsEl = panelObj
                .GetChildAtIndex(1)
                ?.GetChildAtIndex(4)
                ?.GetChildAtIndex(0);

            if (takeRewardsEl == null)
            {
                DebugLog(() => "[TryClickUltimatumPanelTakeRewards] Take Rewards button missing at UltimatumPanel.Child(1).Child(4).Child(0).");
                return false;
            }

            if (!takeRewardsEl.IsValid || !takeRewardsEl.IsVisible)
            {
                DebugLog(() => $"[TryClickUltimatumPanelTakeRewards] Take Rewards button ignored - valid={takeRewardsEl.IsValid}, visible={takeRewardsEl.IsVisible}");
                return false;
            }

            return TryClickUltimatumElement(
                takeRewardsEl,
                windowTopLeft,
                "[TryClickUltimatumPanelTakeRewards] Skipping click - cursor outside PoE window.",
                "[TryClickUltimatumPanelTakeRewards] Rejected by clickable-area check.",
                "[TryClickUltimatumPanelTakeRewards] Clicking Take Rewards at");
        }

        private bool TryClickUltimatumPanelConfirm(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            var confirmObj = panelObj.ConfirmButton;
            if (confirmObj == null)
            {
                DebugLog(() => "[TryClickUltimatumPanelConfirm] ConfirmButton missing.");
                return false;
            }

            if (!TryExtractElement(confirmObj, out Element? confirmEl) || confirmEl == null)
            {
                DebugLog(() => "[TryClickUltimatumPanelConfirm] ConfirmButton is not an Element.");
                return false;
            }

            if (!confirmEl.IsValid || !confirmEl.IsVisible)
            {
                DebugLog(() => $"[TryClickUltimatumPanelConfirm] ConfirmButton ignored - valid={confirmEl.IsValid}, visible={confirmEl.IsVisible}");
                return false;
            }

            return TryClickUltimatumElement(
                confirmEl,
                windowTopLeft,
                "[TryClickUltimatumPanelConfirm] Skipping click - cursor outside PoE window.",
                "[TryClickUltimatumPanelConfirm] Rejected by clickable-area check.",
                "[TryClickUltimatumPanelConfirm] Clicking ConfirmButton at");
        }
    }
}