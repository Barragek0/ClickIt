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

    internal sealed class UltimatumAutomationService(UltimatumAutomationServiceDependencies dependencies)
    {
        private const int UltimatumChoiceToBeginDelayMs = 150;
        private const int UltimatumPostBeginDelayMs = 60;
        private const int UltimatumPostBeginAdditionalClickDelayMs = 200;

        private readonly UltimatumAutomationServiceDependencies _dependencies = dependencies;
        private readonly UltimatumGruelingGauntletDetector _gruelingGauntletDetector = new();

        public bool TryGetOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

            if (TryGetPanelOptionPreview(out previews) && previews.Count > 0)
                return true;

            return TryGetGroundLabelOptionPreview(out previews);
        }

        public bool TryHandlePanelUi(Vector2 windowTopLeft)
        {
            if (!_dependencies.Settings.IsOtherUltimatumClickEnabled())
            {
                _dependencies.PublishUltimatumDebug(new UltimatumDebugEvent("PanelSkip", "PanelUi", false, false)
                {
                    Notes = "Other Ultimatum click setting disabled"
                });
                return false;
            }

            if (!UltimatumPanelUiQuery.TryGetVisiblePanel(_dependencies.GameController, logFailures: true, message => _dependencies.DebugLog(() => message), out UltimatumPanel? panelObj) || panelObj == null)
            {
                _dependencies.PublishUltimatumDebug(new UltimatumDebugEvent("PanelMissing", "PanelUi", false, GetGruelingGauntletDetectionForDebug())
                {
                    Notes = "Ultimatum panel not visible/available"
                });
                return false;
            }

            _dependencies.DebugLog(() => "[TryHandleUltimatumPanelUi] UltimatumPanel detected.");

            bool isGruelingGauntletActive = IsGruelingGauntletPassiveActive();
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

            _dependencies.PublishUltimatumDebug(new UltimatumDebugEvent("PanelHandled", "PanelUi", true, false)
            {
                ClickedChoice = clickedChoice,
                ClickedConfirm = clickedConfirm,
                ClickedTakeRewards = false,
                Notes = clickedAny ? "Panel action executed" : "No panel action executed"
            });

            return clickedAny;
        }

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

        private bool TryHandleGruelingGauntletPanelUi(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            int candidateCount = 0;
            bool canClickTakeRewards = _dependencies.Settings.IsUltimatumTakeRewardButtonClickEnabled();
            UltimatumGruelingPanelDecision decision = UltimatumGruelingPanelDecision.Empty;

            if (UltimatumPanelChoiceCollector.TryCollectCandidates(
                    panelObj,
                    _dependencies.Settings.GetUltimatumModifierPriority(),
                    isGruelingGauntletActive: true,
                    logFailures: true,
                    message => _dependencies.DebugLog(() => message),
                    out List<UltimatumPanelChoiceCandidate> candidates))
            {
                candidateCount = candidates.Count;
                decision = UltimatumGruelingPanelDecisionEngine.Resolve(
                    candidates,
                    isGruelingGauntletActive: true,
                    _dependencies.Settings.ShouldTakeRewardForGruelingGauntletModifier,
                    canClickTakeRewards);
            }
            else
            {
                _dependencies.DebugLog(() => "[TryHandleUltimatumPanelUi] Grueling Gauntlet active but no saturated choice was found. Falling back to confirm-only action.");
            }

            _dependencies.DebugLog(() => $"[TryHandleUltimatumPanelUi] Grueling Gauntlet action={decision.Saturation.Action}, saturatedModifier='{decision.Saturation.SaturatedModifier}', shouldTakeReward={decision.Saturation.ShouldTakeReward}");

            if (UltimatumGruelingGauntletPolicy.ShouldSuppressClick(decision.Saturation.ShouldTakeReward, canClickTakeRewards))
            {
                _dependencies.PublishUltimatumDebug(new UltimatumDebugEvent("PanelGruelingHandled", "PanelUi", true, true)
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

            _dependencies.PublishUltimatumDebug(new UltimatumDebugEvent("PanelGruelingHandled", "PanelUi", true, true)
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

        private bool TryGetPanelOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

            if (!UltimatumPanelUiQuery.TryGetVisiblePanel(_dependencies.GameController, logFailures: false, message => _dependencies.DebugLog(() => message), out UltimatumPanel? panelObj) || panelObj == null)
                return false;

            bool isGruelingGauntletActive = IsGruelingGauntletPassiveActive();
            if (!UltimatumPanelChoiceCollector.TryCollectCandidates(
                    panelObj,
                    _dependencies.Settings.GetUltimatumModifierPriority(),
                    isGruelingGauntletActive,
                    logFailures: false,
                    message => _dependencies.DebugLog(() => message),
                    out List<UltimatumPanelChoiceCandidate> candidates)
                || candidates.Count == 0)
                return false;

            UltimatumGruelingPanelDecision decision = UltimatumGruelingPanelDecisionEngine.Resolve(
                candidates,
                isGruelingGauntletActive,
                _dependencies.Settings.ShouldTakeRewardForGruelingGauntletModifier,
                _dependencies.Settings.IsUltimatumTakeRewardButtonClickEnabled());

            if (_dependencies.ShouldCaptureUltimatumDebug())
            {
                _dependencies.PublishUltimatumDebug(new UltimatumDebugEvent("OverlayPreview", "PanelPreview", true, isGruelingGauntletActive)
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

            var priorities = _dependencies.Settings.GetUltimatumModifierPriority();
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

            var labels = _dependencies.CachedLabels?.Value;
            if (labels == null || labels.Count == 0)
                return false;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label == null)
                    continue;
                if (!UltimatumLabelMath.IsUltimatumLabel(label))
                    continue;
                if (label.Label == null || !label.Label.IsValid)
                    continue;

                ultimatumLabel = label;
                return true;
            }

            return false;
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

        private bool TryClickPanelChoice(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            bool isGruelingGauntletActive = IsGruelingGauntletPassiveActive();
            if (!UltimatumPanelChoiceCollector.TryCollectCandidates(panelObj, _dependencies.Settings.GetUltimatumModifierPriority(), isGruelingGauntletActive, logFailures: true, message => _dependencies.DebugLog(() => message), out List<UltimatumPanelChoiceCandidate> candidates))
            {
                _dependencies.DebugLog(() => "[TryClickUltimatumPanelChoice] No ranked choice found.");
                return false;
            }

            if (isGruelingGauntletActive)
            {
                _dependencies.DebugLog(() => "[TryClickUltimatumPanelChoice] Grueling Gauntlet active - skipping modifier click because choice is game-selected.");
                return false;
            }

            if (!UltimatumPanelChoiceSelector.TryGetSelected(candidates, isGruelingGauntletActive, out UltimatumPanelChoiceCandidate best))
            {
                _dependencies.DebugLog(() => "[TryClickUltimatumPanelChoice] No ranked choice found.");
                return false;
            }

            return TryClickElement(best.ChoiceElement, windowTopLeft, "[TryClickUltimatumPanelChoice] Skipping click - cursor outside PoE window.", $"[TryClickUltimatumPanelChoice] Rejected by clickable-area check. best='{best.ModifierName}',", $"[TryClickUltimatumPanelChoice] Clicking choice '{best.ModifierName}' (priority={best.PriorityIndex}) at");
        }

        private bool TryClickPanelTakeRewards(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            Element? takeRewardsEl = panelObj.GetChildAtIndex(1)?.GetChildAtIndex(4)?.GetChildAtIndex(0);
            if (!UltimatumPanelButtonResolver.TryResolveTakeRewardsButton(takeRewardsEl, message => _dependencies.DebugLog(() => message), out Element resolvedTakeRewardsElement))
                return false;

            return TryClickElement(resolvedTakeRewardsElement, windowTopLeft, "[TryClickUltimatumPanelTakeRewards] Skipping click - cursor outside PoE window.", "[TryClickUltimatumPanelTakeRewards] Rejected by clickable-area check.", "[TryClickUltimatumPanelTakeRewards] Clicking Take Rewards at");
        }

        private bool TryClickPanelConfirm(UltimatumPanel panelObj, Vector2 windowTopLeft)
        {
            if (!UltimatumPanelButtonResolver.TryResolveConfirmButton(panelObj.ConfirmButton, message => _dependencies.DebugLog(() => message), out Element confirmElement))
                return false;

            return TryClickElement(confirmElement, windowTopLeft, "[TryClickUltimatumPanelConfirm] Skipping click - cursor outside PoE window.", "[TryClickUltimatumPanelConfirm] Rejected by clickable-area check.", "[TryClickUltimatumPanelConfirm] Clicking ConfirmButton at");
        }
    }
}