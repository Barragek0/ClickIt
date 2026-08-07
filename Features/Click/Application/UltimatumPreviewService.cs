
namespace ClickIt.Features.Click.Application
{
    internal sealed record UltimatumPreviewServiceDependencies(
        UltimatumAutomationServiceDependencies Automation,
        Func<bool> IsGruelingGauntletPassiveActive);

    internal sealed class UltimatumPreviewService(UltimatumPreviewServiceDependencies dependencies)
    {
        private readonly UltimatumPreviewServiceDependencies _dependencies = dependencies;
        private readonly Lock _snapshotLock = new();
        private List<UltimatumPanelOptionPreview> _snapshotPreviews = [];
        private bool _snapshotFound;

        // The panel walk and ground-label scan run on the background UltimatumPreviewRefresh
        // coroutine (fixed cadence), never the render thread; the renderer only reads the snapshot.
        private IReadOnlyList<LabelOnGround>? _groundPreviewLabelsSource;
        private List<UltimatumPanelOptionPreview>? _cachedGroundPreviews;
        private bool _cachedGroundPreviewFound;

        internal void Refresh()
            => Refresh(ResolveWindowArea());

        internal void Refresh(RectangleF windowArea)
        {
            bool found = TryGetPanelOptionPreview(out List<UltimatumPanelOptionPreview> computed) && computed.Count > 0;
            if (!found)
                found = TryGetGroundLabelOptionPreviewCached(windowArea, out computed);

            lock (_snapshotLock)
            {
                _snapshotFound = found;
                _snapshotPreviews = computed;
            }
        }

        internal bool TryGetOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            lock (_snapshotLock)
            {
                previews = _snapshotPreviews;
                return _snapshotFound;
            }
        }

        private bool TryGetGroundLabelOptionPreviewCached(RectangleF windowArea, out List<UltimatumPanelOptionPreview> previews)
        {
            // The 50ms label cache returns a fresh List reference when its window expires, so
            // re-scanning only on a reference change keeps the ground-label scan from running on
            // every refresh tick when the labels are unchanged.
            IReadOnlyList<LabelOnGround>? labels = _dependencies.Automation.CachedLabels?.Value;
            if (!ReferenceEquals(labels, _groundPreviewLabelsSource))
            {
                _groundPreviewLabelsSource = labels;
                _cachedGroundPreviewFound = TryGetGroundLabelOptionPreview(windowArea, out List<UltimatumPanelOptionPreview> freshPreviews);
                _cachedGroundPreviews = freshPreviews;
            }

            if (!_cachedGroundPreviewFound)
            {
                previews = [];
                return false;
            }

            previews = _cachedGroundPreviews!;
            return true;
        }

        private RectangleF ResolveWindowArea()
            => _dependencies.Automation.GameController?.Window is { } window
                ? window.GetWindowRectangleTimeCache
                : RectangleF.Empty;

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
                previews.Add(new UltimatumPanelOptionPreview(rect, candidate.ChoiceElement, candidate.ModifierName, candidate.PriorityIndex, isSelected));
            }

            return previews.Count > 0;
        }

        private bool TryGetGroundLabelOptionPreview(RectangleF windowArea, out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];
            if (!TryGetActiveGroundLabel(windowArea, out LabelOnGround? ultimatumLabel) || ultimatumLabel == null)
                return false;

            List<(Element OptionElement, string ModifierName)> options = UltimatumUiTreeResolver.GetUltimatumOptions(ultimatumLabel);
            if (options.Count == 0)
                return false;

            IReadOnlyList<string> priorities = _dependencies.Automation.Settings.GetUltimatumModifierPriority();
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
                previews.Add(new UltimatumPanelOptionPreview(rect, candidate.OptionElement, candidate.ModifierName, candidate.PriorityIndex, isSelected));
            }

            return previews.Count > 0;
        }

        private bool TryGetActiveGroundLabel(RectangleF windowArea, out LabelOnGround? ultimatumLabel)
        {
            ultimatumLabel = null;

            List<LabelOnGround>? labels = _dependencies.Automation.CachedLabels?.Value;
            if (labels == null || labels.Count == 0)
                return false;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label == null)
                    continue;
                if (!LabelGeometry.TryGetLabelRectOnScreen(label, windowArea, out _))
                    continue;
                if (!UltimatumLabelMath.IsUltimatumLabel(label))
                    continue;

                ultimatumLabel = label;
                return true;
            }

            return false;
        }
    }
}