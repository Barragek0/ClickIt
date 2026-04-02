using System.Threading;
using ClickIt.Services.Click.Application;
using ClickIt.Services.Click.Runtime;
using ClickIt.Utils;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

#nullable enable

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private const int UltimatumChoiceToBeginDelayMs = 150;
        private const int UltimatumPostBeginDelayMs = 60;
        private const int UltimatumPostBeginAdditionalClickDelayMs = 200;

        public bool TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
            => UltimatumAutomation.TryGetOptionPreview(out previews);

        private bool TryGetUltimatumGroundLabelOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

            if (!TryGetActiveUltimatumGroundLabel(out LabelOnGround? ultimatumLabel) || ultimatumLabel == null)
                return false;

            List<(Element OptionElement, string ModifierName)> options = UltimatumUiTreeResolver.GetUltimatumOptions(ultimatumLabel);
            if (options.Count == 0)
                return false;

            var priorities = settings.GetUltimatumModifierPriority();
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

        private bool TryGetActiveUltimatumGroundLabel(out LabelOnGround? ultimatumLabel)
        {
            ultimatumLabel = null;

            var labels = cachedLabels?.Value;
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

        private bool TryClickUltimatumElement(
            Element element,
            Vector2 windowTopLeft,
            string outsideWindowLog,
            string rejectedClickableAreaLogPrefix,
            string clickLog)
        {
            RectangleF rect = element.GetClientRect();

            return UltimatumElementClickExecutor.TryClickElement(
                rect,
                element,
                windowTopLeft,
                outsideWindowLog,
                rejectedClickableAreaLogPrefix,
                clickLog,
                EnsureCursorInsideGameWindowForClick,
                IsClickableInEitherSpace,
                message => DebugLog(() => message),
                (clickPos, clickElement) => PerformLockedClick(clickPos, clickElement, gameController),
                performanceMonitor.RecordClickInterval);
        }

        private bool TryClickPreferredUltimatumModifier(LabelOnGround label, Vector2 windowTopLeft)
        {
            if (label == null)
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] Label was null.");
                return PublishInitialUltimatumFailure("InitialLabelNull", "Label was null");
            }

            string labelPath = label.ItemOnGround?.Path ?? string.Empty;
            ulong labelAddress = unchecked((ulong)(label.Label?.Address ?? 0));
            bool clickInitialUltimatum = settings.IsInitialUltimatumClickEnabled();
            bool clickOtherUltimatum = settings.IsOtherUltimatumClickEnabled();
            DebugLog(() => $"[TryClickPreferredUltimatumModifier] Entered. ClickInitialUltimatum={clickInitialUltimatum}, ClickUltimatumChoices={clickOtherUltimatum}, Path='{labelPath}', LabelAddr=0x{labelAddress:X}");

            if (!clickInitialUltimatum)
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] Disabled by settings.");
                return PublishInitialUltimatumFailure("InitialDisabled", "Initial ultimatum click setting disabled");
            }

            if (!UltimatumLabelMath.IsUltimatumLabel(label))
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] Label is not Ultimatum interactable path.");
                return PublishInitialUltimatumFailure("InitialNotUltimatum", "Label path is not ultimatum interactable");
            }

            List<string> diagnostics = new(16);
            List<(Element OptionElement, string ModifierName)> options = UltimatumUiTreeResolver.GetUltimatumOptions(label, diagnostics);
            LogDiagnostics("[TryClickPreferredUltimatumModifier]", diagnostics);

            if (options.Count == 0)
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] No Ultimatum options found in UI tree.");
                return PublishInitialUltimatumFailure("InitialNoOptions", "No options discovered from ultimatum label tree");
            }

            var priorities = settings.GetUltimatumModifierPriority();

            if (IsGruelingGauntletPassiveActive())
            {
                UltimatumGroundOptionCollector.TryCollectCandidates(
                    options,
                    priorities,
                    includeSaturation: true,
                    logFailures: true,
                    message => DebugLog(() => message),
                    out List<UltimatumGroundOptionCandidate> candidates);

                UltimatumGruelingGroundDecision decision = UltimatumGruelingGroundDecisionEngine.Resolve(
                    candidates,
                    isGruelingGauntletActive: true,
                    settings.ShouldTakeRewardForGruelingGauntletModifier,
                    settings.IsUltimatumTakeRewardButtonClickEnabled());
                DebugLog(() => $"[TryClickPreferredUltimatumModifier] Grueling Gauntlet action={decision.Saturation.Action}, saturatedModifier='{decision.Saturation.SaturatedModifier}', shouldTakeReward={decision.Saturation.ShouldTakeReward}");

                bool clickedBegin = TryClickUltimatumBeginButton(label, windowTopLeft);
                PublishUltimatumDebug(new UltimatumDebugEvent("InitialGruelingHandled", "InitialLabel", false, true)
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
                    message => DebugLog(() => message),
                    out List<UltimatumGroundOptionCandidate> rankedCandidates))
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] No valid Ultimatum options were eligible.");
                PublishUltimatumDebug(new UltimatumDebugEvent("InitialNoPriorityCandidate", "InitialLabel", false, false)
                {
                    CandidateCount = options.Count,
                    Notes = "No candidate matched ultimatum priority table"
                });
                return false;
            }

            DebugLog(() => $"[TryClickPreferredUltimatumModifier] Found {rankedCandidates.Count} ranked Ultimatum option(s).");

            UltimatumGruelingGroundDecision bestDecision = UltimatumGruelingGroundDecisionEngine.Resolve(
                rankedCandidates,
                isGruelingGauntletActive: false,
                static _ => false,
                canClickTakeReward: false);

            if (!bestDecision.HasBestChoice || bestDecision.BestChoiceElement == null)
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] No candidate matched configured priorities.");
                PublishUltimatumDebug(new UltimatumDebugEvent("InitialNoPriorityCandidate", "InitialLabel", false, false)
                {
                    CandidateCount = options.Count,
                    Notes = "No candidate matched ultimatum priority table"
                });
                return false;
            }

            Element bestOption = bestDecision.BestChoiceElement;
            string bestModifier = bestDecision.BestModifier;
            int bestIndex = bestDecision.BestPriority;

            bool clicked = TryClickUltimatumElement(
                bestOption,
                windowTopLeft,
                "[TryClickPreferredUltimatumModifier] Skipping click - cursor outside PoE window",
                $"[TryClickPreferredUltimatumModifier] Rejected by clickable-area check. best='{bestModifier}',",
                $"[TryClickPreferredUltimatumModifier] Clicking '{bestModifier}' (priority index {bestIndex}) at");

            if (!clicked)
            {
                PublishUltimatumDebug(new UltimatumDebugEvent("InitialChoiceClickFailed", "InitialLabel", false, false)
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
            bool clickedBeginButton = TryClickUltimatumBeginButton(label, windowTopLeft);
            PublishUltimatumDebug(new UltimatumDebugEvent("InitialHandled", "InitialLabel", false, false)
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

        private bool PublishInitialUltimatumFailure(string stage, string notes, int candidateCount = 0)
        {
            PublishUltimatumDebug(new UltimatumDebugEvent(
                stage,
                "InitialLabel",
                false,
                ShouldCaptureUltimatumDebug() && IsGruelingGauntletPassiveActive())
            {
                CandidateCount = candidateCount,
                Notes = notes
            });
            return false;
        }

        private bool TryClickUltimatumBeginButton(LabelOnGround label, Vector2 windowTopLeft)
        {
            List<string> diagnostics = new(8);
            Element? beginButton = UltimatumUiTreeResolver.GetUltimatumBeginButton(label, diagnostics);
            LogDiagnostics("[TryClickUltimatumBeginButton]", diagnostics);

            if (beginButton == null)
            {
                DebugLog(() => "[TryClickUltimatumBeginButton] Begin button not found.");
                return false;
            }

            if (!TryClickUltimatumElement(
                beginButton,
                windowTopLeft,
                "[TryClickUltimatumBeginButton] Skipping click - cursor outside PoE window",
                "[TryClickUltimatumBeginButton] Rejected by clickable-area check.",
                "[TryClickUltimatumBeginButton] Clicking Begin at"))
            {
                return false;
            }

            Thread.Sleep(UltimatumPostBeginDelayMs + UltimatumPostBeginAdditionalClickDelayMs);
            return true;
        }

        private void LogDiagnostics(string prefix, List<string> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                string msg = diagnostics[i];
                DebugLog(() => $"{prefix} {msg}");
            }
        }

    }
}
