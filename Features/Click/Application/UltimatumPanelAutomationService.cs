
namespace ClickIt.Features.Click.Application
{
    internal sealed record UltimatumPanelAutomationServiceDependencies(
        UltimatumAutomationServiceDependencies Automation,
        Func<Element, Vector2, string, string, string, bool> TryClickElement,
        Func<bool> IsGruelingGauntletPassiveActive,
        Func<bool> GetGruelingGauntletDetectionForDebug,
        IUltimatumPanelRuntimeSeam? PanelRuntimeSeam = null);

    internal sealed class UltimatumPanelAutomationService(UltimatumPanelAutomationServiceDependencies dependencies)
    {
        private const int UltimatumChoiceToBeginDelayMs = 150;
        private const int UltimatumPostBeginDelayMs = 60;

        private readonly UltimatumPanelAutomationServiceDependencies _dependencies = dependencies;
        private readonly IUltimatumPanelRuntimeSeam _panelRuntimeSeam = dependencies.PanelRuntimeSeam ?? UltimatumPanelRuntimeSeam.Instance;

        public bool TryHandlePanelUi(Vector2 windowTopLeft)
        {
            UltimatumAutomationServiceDependencies automation = _dependencies.Automation;
            if (!automation.Settings.IsOtherUltimatumClickEnabled())
            {
                automation.PublishUltimatumDebug(new UltimatumDebugEvent("PanelSkip", "PanelUi", false, false)
                {
                    Notes = "Other Ultimatum click setting disabled"
                });
                return false;
            }

            if (!_panelRuntimeSeam.TryGetVisiblePanel(automation.GameController, logFailures: true, message => automation.DebugLog(() => message), out UltimatumPanel? panelObj) || panelObj == null)
            {
                automation.PublishUltimatumDebug(new UltimatumDebugEvent("PanelMissing", "PanelUi", false, _dependencies.GetGruelingGauntletDetectionForDebug())
                {
                    Notes = "Ultimatum panel not visible/available"
                });
                return false;
            }

            automation.DebugLog(() => "[TryHandleUltimatumPanelUi] UltimatumPanel detected.");

            bool isGruelingGauntletActive = _dependencies.IsGruelingGauntletPassiveActive();
            if (isGruelingGauntletActive)
                return TryHandleGruelingGauntletPanelUi(panelObj, windowTopLeft);

            bool clickedAny = false;
            bool clickedChoice = false;
            bool clickedConfirm = false;

            if (TryClickPanelChoice(panelObj, windowTopLeft))
            {
                clickedAny = true;
                clickedChoice = true;
                Thread.Sleep(UltimatumChoiceToBeginDelayMs);
            }

            if (TryClickPanelConfirm(panelObj, windowTopLeft))
            {
                clickedAny = true;
                clickedConfirm = true;
                Thread.Sleep(UltimatumPostBeginDelayMs);
            }

            automation.PublishUltimatumDebug(new UltimatumDebugEvent("PanelHandled", "PanelUi", true, false)
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
            UltimatumAutomationServiceDependencies automation = _dependencies.Automation;
            int candidateCount = 0;
            bool canClickTakeRewards = automation.Settings.IsUltimatumTakeRewardButtonClickEnabled();
            UltimatumGruelingPanelDecision decision = UltimatumGruelingPanelDecision.Empty;

            if (_panelRuntimeSeam.TryCollectPanelChoiceCandidates(
                panelObj,
                automation.Settings.GetUltimatumModifierPriority(),
                isGruelingGauntletActive: true,
                logFailures: true,
                message => automation.DebugLog(() => message),
                out List<UltimatumPanelChoiceCandidate> candidates))
            {
                candidateCount = candidates.Count;
                decision = UltimatumGruelingPanelDecisionEngine.Resolve(
                    candidates,
                    isGruelingGauntletActive: true,
                    automation.Settings.ShouldTakeRewardForGruelingGauntletModifier,
                    canClickTakeRewards);
            }
            else
            {
                automation.DebugLog(() => "[TryHandleUltimatumPanelUi] Grueling Gauntlet active but no saturated choice was found. Falling back to confirm-only action.");
            }

            automation.DebugLog(() => $"[TryHandleUltimatumPanelUi] Grueling Gauntlet action={decision.Saturation.Action}, saturatedModifier='{decision.Saturation.SaturatedModifier}', shouldTakeReward={decision.Saturation.ShouldTakeReward}");

            if (UltimatumGruelingGauntletPolicy.ShouldSuppressClick(decision.Saturation.ShouldTakeReward, canClickTakeRewards))
            {
                automation.PublishUltimatumDebug(new UltimatumDebugEvent("PanelGruelingHandled", "PanelUi", true, true)
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
                clickedTakeRewards = TryClickPanelTakeRewards(panelObj, windowTopLeft);
            else
                clickedConfirm = TryClickPanelConfirm(panelObj, windowTopLeft);

            bool clickedAny = clickedConfirm || clickedTakeRewards;
            if (clickedAny)
                Thread.Sleep(UltimatumPostBeginDelayMs);

            string note = decision.Saturation.Action == GruelingGauntletAction.TakeRewards
                ? (clickedTakeRewards ? "Take Rewards clicked" : "Take Rewards action selected but click failed")
                : (clickedConfirm ? "Confirm clicked" : "Confirm action selected but click failed");

            automation.PublishUltimatumDebug(new UltimatumDebugEvent("PanelGruelingHandled", "PanelUi", true, true)
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

        private bool TryClickPanelChoice(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            UltimatumAutomationServiceDependencies automation = _dependencies.Automation;
            bool isGruelingGauntletActive = _dependencies.IsGruelingGauntletPassiveActive();
            if (!_panelRuntimeSeam.TryCollectPanelChoiceCandidates(panelObj, automation.Settings.GetUltimatumModifierPriority(), isGruelingGauntletActive, logFailures: true, message => automation.DebugLog(() => message), out List<UltimatumPanelChoiceCandidate> candidates))
            {
                automation.DebugLog(() => "[TryClickUltimatumPanelChoice] No ranked choice found.");
                return false;
            }

            if (isGruelingGauntletActive)
            {
                automation.DebugLog(() => "[TryClickUltimatumPanelChoice] Grueling Gauntlet active - skipping modifier click because choice is game-selected.");
                return false;
            }

            if (!UltimatumPanelChoiceSelector.TryGetSelected(candidates, isGruelingGauntletActive, out UltimatumPanelChoiceCandidate best))
            {
                automation.DebugLog(() => "[TryClickUltimatumPanelChoice] No ranked choice found.");
                return false;
            }

            return _dependencies.TryClickElement(best.ChoiceElement, windowTopLeft, "[TryClickUltimatumPanelChoice] Skipping click - cursor outside PoE window.", $"[TryClickUltimatumPanelChoice] Rejected by clickable-area check. best='{best.ModifierName}',", $"[TryClickUltimatumPanelChoice] Clicking choice '{best.ModifierName}' (priority={best.PriorityIndex}) at");
        }

        private bool TryClickPanelTakeRewards(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            UltimatumAutomationServiceDependencies automation = _dependencies.Automation;
            if (!_panelRuntimeSeam.TryResolveTakeRewardsButton(panelObj, message => automation.DebugLog(() => message), out Element resolvedTakeRewardsElement))
                return false;

            return _dependencies.TryClickElement(resolvedTakeRewardsElement, windowTopLeft, "[TryClickUltimatumPanelTakeRewards] Skipping click - cursor outside PoE window.", "[TryClickUltimatumPanelTakeRewards] Rejected by clickable-area check.", "[TryClickUltimatumPanelTakeRewards] Clicking Take Rewards at");
        }

        private bool TryClickPanelConfirm(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            UltimatumAutomationServiceDependencies automation = _dependencies.Automation;
            if (!_panelRuntimeSeam.TryResolveConfirmButton(panelObj, message => automation.DebugLog(() => message), out Element confirmElement))
                return false;

            return _dependencies.TryClickElement(confirmElement, windowTopLeft, "[TryClickUltimatumPanelConfirm] Skipping click - cursor outside PoE window.", "[TryClickUltimatumPanelConfirm] Rejected by clickable-area check.", "[TryClickUltimatumPanelConfirm] Clicking ConfirmButton at");
        }
    }
}