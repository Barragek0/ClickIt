#nullable enable

namespace ClickIt.Features.Click.Application
{
    internal sealed record UltimatumAutomationServiceDependencies(
        ClickItSettings Settings,
        GameController GameController,
        TimeCache<List<LabelOnGround>> CachedLabels,
        Func<string, bool> EnsureCursorInsideGameWindowForClick,
        Func<Vector2, string, bool> IsClickableInEitherSpace,
        Action<Func<string>> DebugLog,
        Action<Vector2, Element?> PerformClick,
        Action RecordClickInterval,
        Func<bool> ShouldCaptureUltimatumDebug,
        Action<UltimatumDebugEvent> PublishUltimatumDebug);

    internal sealed class UltimatumAutomationService
    {
        private const int UltimatumChoiceToBeginDelayMs = 150;
        private const int UltimatumPostBeginDelayMs = 60;
        private const int UltimatumPostBeginAdditionalClickDelayMs = 200;

        private readonly UltimatumAutomationServiceDependencies _dependencies;
        private readonly UltimatumGruelingGauntletDetector _gruelingGauntletDetector = new();
        private readonly UltimatumPanelAutomationService _panelAutomationService;
        private readonly UltimatumPreviewService _previewService;

        public UltimatumAutomationService(UltimatumAutomationServiceDependencies dependencies)
        {
            _dependencies = dependencies;
            _panelAutomationService = new UltimatumPanelAutomationService(new UltimatumPanelAutomationServiceDependencies(
                dependencies,
                TryClickElement,
                IsGruelingGauntletPassiveActive,
                GetGruelingGauntletDetectionForDebug));
            _previewService = new UltimatumPreviewService(new UltimatumPreviewServiceDependencies(
                dependencies,
                IsGruelingGauntletPassiveActive));
        }

        public bool TryGetOptionPreview(out List<UltimatumPanelOptionPreview> previews)
            => _previewService.TryGetOptionPreview(out previews);

        public bool TryHandlePanelUi(Vector2 windowTopLeft)
            => _panelAutomationService.TryHandlePanelUi(windowTopLeft);

        public bool TryClickPreferredModifier(LabelOnGround label, Vector2 windowTopLeft)
        {
            if (label == null)
            {
                _dependencies.DebugLog(() => "[TryClickPreferredUltimatumModifier] Label was null.");
                return PublishInitialFailure("InitialLabelNull", "Label was null");
            }

            string labelPath = label.ItemOnGround?.Path ?? string.Empty;
            ulong labelAddress = unchecked((ulong)(label.Label?.Address ?? 0));
            bool clickInitialUltimatum = _dependencies.Settings.IsInitialUltimatumClickEnabled();
            bool clickOtherUltimatum = _dependencies.Settings.IsOtherUltimatumClickEnabled();
            _dependencies.DebugLog(() => $"[TryClickPreferredUltimatumModifier] Entered. ClickInitialUltimatum={clickInitialUltimatum}, ClickUltimatumChoices={clickOtherUltimatum}, Path='{labelPath}', LabelAddr=0x{labelAddress:X}");

            if (!clickInitialUltimatum)
            {
                _dependencies.DebugLog(() => "[TryClickPreferredUltimatumModifier] Disabled by settings.");
                return PublishInitialFailure("InitialDisabled", "Initial ultimatum click setting disabled");
            }

            if (!UltimatumLabelMath.IsUltimatumLabel(label))
            {
                _dependencies.DebugLog(() => "[TryClickPreferredUltimatumModifier] Label is not Ultimatum interactable path.");
                return PublishInitialFailure("InitialNotUltimatum", "Label path is not ultimatum interactable");
            }

            List<string> diagnostics = new(16);
            List<(Element OptionElement, string ModifierName)> options = UltimatumUiTreeResolver.GetUltimatumOptions(label, diagnostics);
            LogDiagnostics("[TryClickPreferredUltimatumModifier]", diagnostics);

            if (options.Count == 0)
            {
                _dependencies.DebugLog(() => "[TryClickPreferredUltimatumModifier] No Ultimatum options found in UI tree.");
                return PublishInitialFailure("InitialNoOptions", "No options discovered from ultimatum label tree");
            }

            var priorities = _dependencies.Settings.GetUltimatumModifierPriority();

            if (IsGruelingGauntletPassiveActive())
            {
                UltimatumGroundOptionCollector.TryCollectCandidates(
                    options,
                    priorities,
                    includeSaturation: true,
                    logFailures: true,
                    message => _dependencies.DebugLog(() => message),
                    out List<UltimatumGroundOptionCandidate> candidates);

                UltimatumGruelingGroundDecision decision = UltimatumGruelingGroundDecisionEngine.Resolve(
                    candidates,
                    isGruelingGauntletActive: true,
                    _dependencies.Settings.ShouldTakeRewardForGruelingGauntletModifier,
                    _dependencies.Settings.IsUltimatumTakeRewardButtonClickEnabled());
                _dependencies.DebugLog(() => $"[TryClickPreferredUltimatumModifier] Grueling Gauntlet action={decision.Saturation.Action}, saturatedModifier='{decision.Saturation.SaturatedModifier}', shouldTakeReward={decision.Saturation.ShouldTakeReward}");

                bool clickedBegin = TryClickBeginButton(label, windowTopLeft);
                _dependencies.PublishUltimatumDebug(new UltimatumDebugEvent("InitialGruelingHandled", "InitialLabel", false, true)
                {
                    HasSaturatedChoice = decision.Saturation.HasSaturatedChoice,
                    SaturatedModifier = decision.Saturation.SaturatedModifier,
                    ShouldTakeReward = decision.Saturation.ShouldTakeReward,
                    Action = decision.Saturation.Action.ToString(),
                    CandidateCount = options.Count,
                    SaturatedCandidateCount = decision.Saturation.SaturatedCandidateCount,
                    ClickedChoice = false,
                    ClickedConfirm = clickedBegin,
                    ClickedTakeRewards = false,
                    Notes = clickedBegin ? "Clicked begin/confirm path on initial label" : "Begin/confirm click failed on initial label"
                });
                return clickedBegin;
            }

            if (!UltimatumGroundOptionCollector.TryCollectCandidates(
                    options,
                    priorities,
                    includeSaturation: false,
                    logFailures: true,
                    message => _dependencies.DebugLog(() => message),
                    out List<UltimatumGroundOptionCandidate> rankedCandidates))
            {
                _dependencies.DebugLog(() => "[TryClickPreferredUltimatumModifier] No valid Ultimatum options were eligible.");
                _dependencies.PublishUltimatumDebug(new UltimatumDebugEvent("InitialNoPriorityCandidate", "InitialLabel", false, false)
                {
                    CandidateCount = options.Count,
                    Notes = "No candidate matched ultimatum priority table"
                });
                return false;
            }

            _dependencies.DebugLog(() => $"[TryClickPreferredUltimatumModifier] Found {rankedCandidates.Count} ranked Ultimatum option(s).");

            UltimatumGruelingGroundDecision bestDecision = UltimatumGruelingGroundDecisionEngine.Resolve(
                rankedCandidates,
                isGruelingGauntletActive: false,
                static _ => false,
                canClickTakeReward: false);

            if (!bestDecision.HasBestChoice || bestDecision.BestChoiceElement == null)
            {
                _dependencies.DebugLog(() => "[TryClickPreferredUltimatumModifier] No candidate matched configured priorities.");
                _dependencies.PublishUltimatumDebug(new UltimatumDebugEvent("InitialNoPriorityCandidate", "InitialLabel", false, false)
                {
                    CandidateCount = options.Count,
                    Notes = "No candidate matched ultimatum priority table"
                });
                return false;
            }

            Element bestOption = bestDecision.BestChoiceElement;
            string bestModifier = bestDecision.BestModifier;
            int bestIndex = bestDecision.BestPriority;

            bool clicked = TryClickElement(
                bestOption,
                windowTopLeft,
                "[TryClickPreferredUltimatumModifier] Skipping click - cursor outside PoE window",
                $"[TryClickPreferredUltimatumModifier] Rejected by clickable-area check. best='{bestModifier}',",
                $"[TryClickPreferredUltimatumModifier] Clicking '{bestModifier}' (priority index {bestIndex}) at");

            if (!clicked)
            {
                _dependencies.PublishUltimatumDebug(new UltimatumDebugEvent("InitialChoiceClickFailed", "InitialLabel", false, false)
                {
                    CandidateCount = options.Count,
                    BestModifier = bestModifier,
                    BestPriority = bestIndex,
                    ClickedChoice = false,
                    Notes = "Preferred choice click failed"
                });
                return false;
            }

            Thread.Sleep(UltimatumChoiceToBeginDelayMs);
            bool clickedBeginButton = TryClickBeginButton(label, windowTopLeft);
            _dependencies.PublishUltimatumDebug(new UltimatumDebugEvent("InitialHandled", "InitialLabel", false, false)
            {
                CandidateCount = options.Count,
                BestModifier = bestModifier,
                BestPriority = bestIndex,
                ClickedChoice = true,
                ClickedConfirm = clickedBeginButton,
                Notes = clickedBeginButton ? "Clicked preferred choice and begin" : "Choice clicked but begin click failed"
            });
            return clickedBeginButton;
        }

        private bool TryClickElement(Element element, Vector2 windowTopLeft, string outsideWindowLog, string rejectedClickableAreaLogPrefix, string clickLog)
        {
            RectangleF rect = element.GetClientRect();

            return UltimatumElementClickExecutor.TryClickElement(
                rect,
                element,
                windowTopLeft,
                outsideWindowLog,
                rejectedClickableAreaLogPrefix,
                clickLog,
                _dependencies.EnsureCursorInsideGameWindowForClick,
                _dependencies.IsClickableInEitherSpace,
                message => _dependencies.DebugLog(() => message),
                (clickPos, clickElement) => _dependencies.PerformClick(clickPos, clickElement),
                _dependencies.RecordClickInterval);
        }

        private bool PublishInitialFailure(string stage, string notes, int candidateCount = 0)
        {
            _dependencies.PublishUltimatumDebug(new UltimatumDebugEvent(
                stage,
                "InitialLabel",
                false,
                _dependencies.ShouldCaptureUltimatumDebug() && IsGruelingGauntletPassiveActive())
            {
                CandidateCount = candidateCount,
                Notes = notes
            });
            return false;
        }

        private bool TryClickBeginButton(LabelOnGround label, Vector2 windowTopLeft)
        {
            List<string> diagnostics = new(8);
            Element? beginButton = UltimatumUiTreeResolver.GetUltimatumBeginButton(label, diagnostics);
            LogDiagnostics("[TryClickUltimatumBeginButton]", diagnostics);

            if (beginButton == null)
            {
                _dependencies.DebugLog(() => "[TryClickUltimatumBeginButton] Begin button not found.");
                return false;
            }

            if (!TryClickElement(beginButton, windowTopLeft, "[TryClickUltimatumBeginButton] Skipping click - cursor outside PoE window", "[TryClickUltimatumBeginButton] Rejected by clickable-area check.", "[TryClickUltimatumBeginButton] Clicking Begin at"))
                return false;

            Thread.Sleep(UltimatumPostBeginDelayMs + UltimatumPostBeginAdditionalClickDelayMs);
            return true;
        }

        private void LogDiagnostics(string prefix, List<string> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                string msg = diagnostics[i];
                _dependencies.DebugLog(() => $"{prefix} {msg}");
            }
        }

        private bool IsGruelingGauntletPassiveActive()
        {
            bool isActive = _gruelingGauntletDetector.IsActive(_dependencies.GameController?.IngameState?.Data);
            UltimatumGruelingGauntletDetectionStore.Publish(isActive);
            return isActive;
        }

        private bool GetGruelingGauntletDetectionForDebug()
            => _dependencies.ShouldCaptureUltimatumDebug() && IsGruelingGauntletPassiveActive();

    }
}