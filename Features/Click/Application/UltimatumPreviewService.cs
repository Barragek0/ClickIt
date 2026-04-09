#nullable enable

namespace ClickIt.Features.Click.Application
{
    internal sealed record UltimatumPreviewServiceDependencies(
        UltimatumAutomationServiceDependencies Automation,
        Func<bool> IsGruelingGauntletPassiveActive);

    internal sealed class UltimatumPreviewService(UltimatumPreviewServiceDependencies dependencies)
    {
        private readonly UltimatumPreviewServiceDependencies _dependencies = dependencies;

        public bool TryGetOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

            if (TryGetPanelOptionPreview(out previews) && previews.Count > 0)
                return true;

            return TryGetGroundLabelOptionPreview(out previews);
        }

        private bool TryGetPanelOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];
            UltimatumAutomationServiceDependencies automation = _dependencies.Automation;

            if (!UltimatumPanelUiQuery.TryGetVisiblePanel(automation.GameController, logFailures: false, message => automation.DebugLog(() => message), out UltimatumPanel? panelObj) || panelObj == null)
                return false;

            bool isGruelingGauntletActive = _dependencies.IsGruelingGauntletPassiveActive();
            if (!UltimatumPanelChoiceCollector.TryCollectCandidates(
                    panelObj,
                    automation.Settings.GetUltimatumModifierPriority(),
                    isGruelingGauntletActive,
                    logFailures: false,
                    message => automation.DebugLog(() => message),
                    out List<UltimatumPanelChoiceCandidate> candidates)
                || candidates.Count == 0)
                return false;

            UltimatumGruelingPanelDecision decision = UltimatumGruelingPanelDecisionEngine.Resolve(
                candidates,
                isGruelingGauntletActive,
                automation.Settings.ShouldTakeRewardForGruelingGauntletModifier,
                automation.Settings.IsUltimatumTakeRewardButtonClickEnabled());

            if (automation.ShouldCaptureUltimatumDebug())
            {
                automation.PublishUltimatumDebug(new UltimatumDebugEvent("OverlayPreview", "PanelPreview", true, isGruelingGauntletActive)
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

        private bool TryGetGroundLabelOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];
            if (!TryGetActiveGroundLabel(out LabelOnGround? ultimatumLabel) || ultimatumLabel == null)
                return false;

            List<(Element OptionElement, string ModifierName)> options = UltimatumUiTreeResolver.GetUltimatumOptions(ultimatumLabel);
            if (options.Count == 0)
                return false;

            var priorities = _dependencies.Automation.Settings.GetUltimatumModifierPriority();
            if (!UltimatumGroundOptionCollector.TryCollectCandidates(
                    options,
                    priorities,
                    includeSaturation: false,
                    logFailures: false,
                    _ => { },
                    out List<UltimatumGroundOptionCandidate> candidates))
            {
                return false;
            }

            UltimatumGruelingGroundDecision decision = UltimatumGruelingGroundDecisionEngine.Resolve(
                candidates,
                isGruelingGauntletActive: false,
                static _ => false,
                canClickTakeReward: false);

            for (int i = 0; i < candidates.Count; i++)
            {
                UltimatumGroundOptionCandidate candidate = candidates[i];

                RectangleF rect = candidate.OptionElement.GetClientRect();
                if (rect.Width <= 0 || rect.Height <= 0)
                    continue;

                bool isSelected = decision.HasBestChoice && ReferenceEquals(candidate.OptionElement, decision.BestChoiceElement);
                previews.Add(new UltimatumPanelOptionPreview(rect, candidate.ModifierName, candidate.PriorityIndex, isSelected));
            }

            return previews.Count > 0;
        }

        private bool TryGetActiveGroundLabel(out LabelOnGround? ultimatumLabel)
        {
            ultimatumLabel = null;

            var labels = _dependencies.Automation.CachedLabels?.Value;
            if (labels == null || labels.Count == 0)
                return false;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label == null)
                    continue;
                if (!UltimatumLabelMath.IsUltimatumLabel(label))
                    continue;
                if (!UltimatumLabelMath.IsLabelElementValid(label))
                    continue;

                ultimatumLabel = label;
                return true;
            }

            return false;
        }
    }
}