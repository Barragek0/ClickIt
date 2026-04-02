using System.Threading;
using ClickIt.Services.Click.Runtime;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

#nullable enable

namespace ClickIt.Services
{
    public partial class ClickService
    {
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

            if (!UltimatumPanelUiQuery.TryGetVisiblePanel(gameController, logFailures: true, message => DebugLog(() => message), out UltimatumPanel? panelObj) || panelObj == null)
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

            int candidateCount = 0;
            bool canClickTakeRewards = settings.IsUltimatumTakeRewardButtonClickEnabled();
            UltimatumGruelingPanelDecision decision = UltimatumGruelingPanelDecision.Empty;

            if (UltimatumPanelChoiceCollector.TryCollectCandidates(
                    panelObj,
                    settings.GetUltimatumModifierPriority(),
                    isGruelingGauntletActive: true,
                    logFailures: true,
                    message => DebugLog(() => message),
                    out List<UltimatumPanelChoiceCandidate> candidates))
            {
                candidateCount = candidates.Count;
                decision = UltimatumGruelingPanelDecisionEngine.Resolve(
                    candidates,
                    isGruelingGauntletActive: true,
                    settings.ShouldTakeRewardForGruelingGauntletModifier,
                    canClickTakeRewards);
            }
            else
            {
                DebugLog(() => "[TryHandleUltimatumPanelUi] Grueling Gauntlet active but no saturated choice was found. Falling back to confirm-only action.");
            }

            DebugLog(() => $"[TryHandleUltimatumPanelUi] Grueling Gauntlet action={decision.Saturation.Action}, saturatedModifier='{decision.Saturation.SaturatedModifier}', shouldTakeReward={decision.Saturation.ShouldTakeReward}");

            if (UltimatumGruelingGauntletPolicy.ShouldSuppressClick(decision.Saturation.ShouldTakeReward, canClickTakeRewards))
            {
                PublishUltimatumDebug(new UltimatumDebugEvent("PanelGruelingHandled", "PanelUi", true, true)
                {
                    HasSaturatedChoice = decision.Saturation.HasSaturatedChoice,
                    SaturatedModifier = decision.Saturation.SaturatedModifier,
                    ShouldTakeReward = decision.Saturation.ShouldTakeReward,
                    Action = decision.Saturation.Action.ToString(),
                    CandidateCount = candidateCount,
                    SaturatedCandidateCount = decision.Saturation.SaturatedCandidateCount,
                    BestModifier = decision.BestModifier,
                    BestPriority = decision.BestPriority,
                    ClickedChoice = false,
                    ClickedConfirm = false,
                    ClickedTakeRewards = false,
                    Notes = "Take Reward matched but Click Take Reward Button is disabled; no click performed"
                });
                return false;
            }

            bool clickedConfirm = false;
            bool clickedTakeRewards = false;
            if (decision.Saturation.Action == GruelingGauntletAction.TakeRewards)
                clickedTakeRewards = TryClickUltimatumPanelTakeRewards(panelObj, windowTopLeft);
            else
                clickedConfirm = TryClickUltimatumPanelConfirm(panelObj, windowTopLeft);

            clickedAny = clickedConfirm || clickedTakeRewards;
            if (clickedAny)
                Thread.Sleep(UltimatumPostBeginDelayMs);

            string note = decision.Saturation.Action == GruelingGauntletAction.TakeRewards
                ? (clickedTakeRewards ? "Take Rewards clicked" : "Take Rewards action selected but click failed")
                : (clickedConfirm ? "Confirm clicked" : "Confirm action selected but click failed");

            PublishUltimatumDebug(new UltimatumDebugEvent("PanelGruelingHandled", "PanelUi", true, true)
            {
                HasSaturatedChoice = decision.Saturation.HasSaturatedChoice,
                SaturatedModifier = decision.Saturation.SaturatedModifier,
                ShouldTakeReward = decision.Saturation.ShouldTakeReward,
                Action = decision.Saturation.Action.ToString(),
                CandidateCount = candidateCount,
                SaturatedCandidateCount = decision.Saturation.SaturatedCandidateCount,
                BestModifier = decision.BestModifier,
                BestPriority = decision.BestPriority,
                ClickedChoice = false,
                ClickedConfirm = clickedConfirm,
                ClickedTakeRewards = clickedTakeRewards,
                Notes = note
            });

            return clickedAny;
        }

        private bool TryGetUltimatumPanelOptionPreviewCore(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

            if (!UltimatumPanelUiQuery.TryGetVisiblePanel(gameController, logFailures: false, message => DebugLog(() => message), out UltimatumPanel? panelObj) || panelObj == null)
                return false;

            bool isGruelingGauntletActive = IsGruelingGauntletPassiveActive();
            if (!UltimatumPanelChoiceCollector.TryCollectCandidates(
                    panelObj,
                    settings.GetUltimatumModifierPriority(),
                    isGruelingGauntletActive,
                    logFailures: false,
                    message => DebugLog(() => message),
                    out List<UltimatumPanelChoiceCandidate> candidates)
                || candidates.Count == 0)
                return false;

            UltimatumGruelingPanelDecision decision = UltimatumGruelingPanelDecisionEngine.Resolve(
                candidates,
                isGruelingGauntletActive,
                settings.ShouldTakeRewardForGruelingGauntletModifier,
                settings.IsUltimatumTakeRewardButtonClickEnabled());

            if (ShouldCaptureUltimatumDebug())
            {
                PublishUltimatumDebug(new UltimatumDebugEvent("OverlayPreview", "PanelPreview", true, isGruelingGauntletActive)
                {
                    HasSaturatedChoice = decision.Saturation.HasSaturatedChoice,
                    SaturatedModifier = decision.Saturation.SaturatedModifier,
                    ShouldTakeReward = decision.Saturation.ShouldTakeReward,
                    Action = decision.Saturation.Action.ToString(),
                    CandidateCount = candidates.Count,
                    SaturatedCandidateCount = decision.Saturation.SaturatedCandidateCount,
                    BestModifier = decision.BestModifier,
                    BestPriority = decision.BestPriority,
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

                bool isSelected = decision.HasBestChoice && ReferenceEquals(candidate.ChoiceElement, decision.BestChoiceElement);
                previews.Add(new UltimatumPanelOptionPreview(rect, candidate.ModifierName, candidate.PriorityIndex, isSelected));
            }

            return previews.Count > 0;
        }

        private bool IsGruelingGauntletPassiveActive()
        {
            bool isActive = _gruelingGauntletDetector.IsActive(gameController?.IngameState?.Data);
            UltimatumGruelingGauntletDetectionStore.Publish(isActive);
            return isActive;
        }

        private bool GetGruelingGauntletDetectionForDebug()
        {
            return ShouldCaptureUltimatumDebug() && IsGruelingGauntletPassiveActive();
        }

        private bool TryClickUltimatumPanelChoice(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            bool isGruelingGauntletActive = IsGruelingGauntletPassiveActive();
            if (!UltimatumPanelChoiceCollector.TryCollectCandidates(
                    panelObj,
                    settings.GetUltimatumModifierPriority(),
                    isGruelingGauntletActive,
                    logFailures: true,
                    message => DebugLog(() => message),
                    out List<UltimatumPanelChoiceCandidate> candidates))
            {
                DebugLog(() => "[TryClickUltimatumPanelChoice] No ranked choice found.");
                return false;
            }
            if (isGruelingGauntletActive)
            {
                DebugLog(() => "[TryClickUltimatumPanelChoice] Grueling Gauntlet active - skipping modifier click because choice is game-selected.");
                return false;
            }

            if (!UltimatumPanelChoiceSelector.TryGetSelected(candidates, isGruelingGauntletActive, out UltimatumPanelChoiceCandidate best))
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

            if (!UltimatumPanelButtonResolver.TryResolveTakeRewardsButton(
                    takeRewardsEl,
                    message => DebugLog(() => message),
                    out Element resolvedTakeRewardsElement))
            {
                return false;
            }

            return TryClickUltimatumElement(
                resolvedTakeRewardsElement,
                windowTopLeft,
                "[TryClickUltimatumPanelTakeRewards] Skipping click - cursor outside PoE window.",
                "[TryClickUltimatumPanelTakeRewards] Rejected by clickable-area check.",
                "[TryClickUltimatumPanelTakeRewards] Clicking Take Rewards at");
        }

        private bool TryClickUltimatumPanelConfirm(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            if (!UltimatumPanelButtonResolver.TryResolveConfirmButton(
                    panelObj.ConfirmButton,
                    message => DebugLog(() => message),
                    out Element confirmElement))
            {
                return false;
            }

            return TryClickUltimatumElement(
                confirmElement,
                windowTopLeft,
                "[TryClickUltimatumPanelConfirm] Skipping click - cursor outside PoE window.",
                "[TryClickUltimatumPanelConfirm] Rejected by clickable-area check.",
                "[TryClickUltimatumPanelConfirm] Clicking ConfirmButton at");
        }
    }
}