using System.Threading;
using ClickIt.Definitions;
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

        public readonly struct UltimatumPanelOptionPreview(RectangleF rect, string modifierName, int priorityIndex, bool isSelected)
        {
            public RectangleF Rect { get; } = rect;
            public string ModifierName { get; } = modifierName;
            public int PriorityIndex { get; } = priorityIndex;
            public bool IsSelected { get; } = isSelected;
        }

        private readonly struct UltimatumPanelChoiceCandidate(
            Element choiceElement,
            string modifierName,
            int priorityIndex,
            bool isSaturated)
        {
            public Element ChoiceElement { get; } = choiceElement;
            public string ModifierName { get; } = modifierName;
            public int PriorityIndex { get; } = priorityIndex;
            public bool IsSaturated { get; } = isSaturated;
        }

        private static bool IsUltimatumPath(string? path) => Constants.IsUltimatumInteractablePath(path);

        private static bool IsUltimatumLabel(LabelOnGround? label)
        {
            if (!IsUltimatumPath(label?.ItemOnGround?.Path))
                return false;

            Element? child0 = label?.Label?.GetChildAtIndex(0);
            return child0?.IsVisible == true;
        }

        private static bool ShouldSuppressInactiveUltimatumLabel(LabelOnGround? label)
        {
            return IsUltimatumPath(label?.ItemOnGround?.Path) && !IsUltimatumLabel(label);
        }


        public bool TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

            return (TryGetUltimatumPanelOptionPreview(out previews) && previews.Count > 0)
                || TryGetUltimatumGroundLabelOptionPreview(out previews);
        }

        private bool TryGetUltimatumGroundLabelOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

            if (!TryGetActiveUltimatumGroundLabel(out LabelOnGround? ultimatumLabel) || ultimatumLabel == null)
                return false;

            List<(Element OptionElement, string ModifierName)> options = GetUltimatumOptions(ultimatumLabel);
            if (options.Count == 0)
                return false;

            var priorities = settings.GetUltimatumModifierPriority();
            Element? bestOption = GetBestUltimatumGroundOptionElement(options, priorities);

            for (int i = 0; i < options.Count; i++)
            {
                Element optionElement = options[i].OptionElement;
                if (!optionElement.IsValid)
                    continue;

                RectangleF rect = optionElement.GetClientRect();
                if (rect.Width <= 0 || rect.Height <= 0)
                    continue;

                int priorityIndex = GetModifierPriorityIndex(options[i].ModifierName, priorities);
                bool isSelected = bestOption != null && ReferenceEquals(optionElement, bestOption);
                previews.Add(new UltimatumPanelOptionPreview(rect, options[i].ModifierName, priorityIndex, isSelected));
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
                if (!IsUltimatumLabel(label))
                    continue;
                if (label.Label == null || !label.Label.IsValid)
                    continue;

                ultimatumLabel = label;
                return true;
            }

            return false;
        }

        private static Element? GetBestUltimatumGroundOptionElement(
            List<(Element OptionElement, string ModifierName)> options,
            IReadOnlyList<string> priorities)
        {
            int bestIndex = int.MaxValue;
            Element? bestOption = null;

            for (int i = 0; i < options.Count; i++)
            {
                Element optionElement = options[i].OptionElement;
                if (!optionElement.IsValid)
                    continue;

                int priorityIndex = GetModifierPriorityIndex(options[i].ModifierName, priorities);
                if (priorityIndex < bestIndex)
                {
                    bestIndex = priorityIndex;
                    bestOption = optionElement;
                }
            }

            return bestOption;
        }


        private bool TryClickUltimatumElement(
            Element element,
            Vector2 windowTopLeft,
            string outsideWindowLog,
            string rejectedClickableAreaLogPrefix,
            string clickLog)
        {
            if (!EnsureCursorInsideGameWindowForClick(outsideWindowLog))
            {
                return false;
            }

            RectangleF rect = element.GetClientRect();
            if (!IsClickableInEitherSpace(rect.Center, "Ultimatum"))
            {
                DebugLog(() => $"{rejectedClickableAreaLogPrefix} center={rect.Center}");
                return false;
            }

            Vector2 clickPos = rect.Center + windowTopLeft;
            DebugLog(() => $"{clickLog} {clickPos}");

            PerformLockedClick(clickPos, element, gameController);
            performanceMonitor.RecordClickInterval();
            return true;
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

            if (!IsUltimatumLabel(label))
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] Label is not Ultimatum interactable path.");
                return PublishInitialUltimatumFailure("InitialNotUltimatum", "Label path is not ultimatum interactable");
            }

            List<string> diagnostics = new(16);
            List<(Element OptionElement, string ModifierName)> options = GetUltimatumOptions(label, diagnostics);
            LogDiagnostics("[TryClickPreferredUltimatumModifier]", diagnostics);

            if (options.Count == 0)
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] No Ultimatum options found in UI tree.");
                return PublishInitialUltimatumFailure("InitialNoOptions", "No options discovered from ultimatum label tree");
            }

            if (IsGruelingGauntletPassiveActive())
            {
                bool hasSaturatedChoice = TryGetSaturatedUltimatumGroundOption(options, out (Element OptionElement, string ModifierName) saturatedChoice);
                bool shouldTakeReward = hasSaturatedChoice
                    && settings.ShouldTakeRewardForGruelingGauntletModifier(saturatedChoice.ModifierName);

                GruelingGauntletAction action = DetermineGruelingGauntletActionCore(hasSaturatedChoice, shouldTakeReward);
                string saturatedModifierName = hasSaturatedChoice ? saturatedChoice.ModifierName : string.Empty;
                DebugLog(() => $"[TryClickPreferredUltimatumModifier] Grueling Gauntlet action={action}, saturatedModifier='{saturatedModifierName}', shouldTakeReward={shouldTakeReward}");

                bool clickedBegin = TryClickUltimatumBeginButton(label, windowTopLeft);
                PublishUltimatumDebug(
                    stage: "InitialGruelingHandled",
                    source: "InitialLabel",
                    isPanelVisible: false,
                    isGruelingGauntletActive: true,
                    hasSaturatedChoice: hasSaturatedChoice,
                    saturatedModifier: saturatedModifierName,
                    shouldTakeReward: shouldTakeReward,
                    action: action.ToString(),
                    candidateCount: options.Count,
                    saturatedCandidateCount: hasSaturatedChoice ? 1 : 0,
                    clickedChoice: false,
                    clickedConfirm: clickedBegin,
                    clickedTakeRewards: false,
                    notes: clickedBegin ? "Clicked begin/confirm path on initial label" : "Begin/confirm click failed on initial label");
                return clickedBegin;
            }

            var priorities = settings.GetUltimatumModifierPriority();

            DebugLog(() => $"[TryClickPreferredUltimatumModifier] Found {options.Count} Ultimatum option(s).");
            int bestIndex = int.MaxValue;
            Element? bestOption = null;
            string bestModifier = string.Empty;

            foreach ((Element optionElement, string modifierName) in options)
            {
                if (optionElement == null || !optionElement.IsValid)
                    continue;

                int priorityIndex = GetModifierPriorityIndex(modifierName, priorities);
                RectangleF candidateRect = optionElement.GetClientRect();
                DebugLog(() => $"[TryClickPreferredUltimatumModifier] Candidate '{modifierName}' priority={priorityIndex}, center={candidateRect.Center}, visible={optionElement.IsVisible}, valid={optionElement.IsValid}");
                if (priorityIndex < bestIndex)
                {
                    bestIndex = priorityIndex;
                    bestOption = optionElement;
                    bestModifier = modifierName;
                }
            }

            if (bestOption == null || bestIndex == int.MaxValue)
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] No candidate matched configured priorities.");
                PublishUltimatumDebug(
                    stage: "InitialNoPriorityCandidate",
                    source: "InitialLabel",
                    isPanelVisible: false,
                    isGruelingGauntletActive: false,
                    candidateCount: options.Count,
                    notes: "No candidate matched ultimatum priority table");
                return false;
            }

            bool clicked = TryClickUltimatumElement(
                bestOption,
                windowTopLeft,
                "[TryClickPreferredUltimatumModifier] Skipping click - cursor outside PoE window",
                $"[TryClickPreferredUltimatumModifier] Rejected by clickable-area check. best='{bestModifier}',",
                $"[TryClickPreferredUltimatumModifier] Clicking '{bestModifier}' (priority index {bestIndex}) at");

            if (!clicked)
            {
                PublishUltimatumDebug(
                    stage: "InitialChoiceClickFailed",
                    source: "InitialLabel",
                    isPanelVisible: false,
                    isGruelingGauntletActive: false,
                    candidateCount: options.Count,
                    bestModifier: bestModifier,
                    bestPriority: bestIndex,
                    clickedChoice: false,
                    notes: "Preferred choice click failed");
                return false;
            }

            Thread.Sleep(UltimatumChoiceToBeginDelayMs);
            bool clickedBeginButton = TryClickUltimatumBeginButton(label, windowTopLeft);
            PublishUltimatumDebug(
                stage: "InitialHandled",
                source: "InitialLabel",
                isPanelVisible: false,
                isGruelingGauntletActive: false,
                candidateCount: options.Count,
                bestModifier: bestModifier,
                bestPriority: bestIndex,
                clickedChoice: true,
                clickedConfirm: clickedBeginButton,
                notes: clickedBeginButton ? "Clicked preferred choice and begin" : "Choice clicked but begin click failed");
            return clickedBeginButton;
        }

        private bool PublishInitialUltimatumFailure(string stage, string notes, int candidateCount = 0)
        {
            PublishUltimatumDebug(
                stage: stage,
                source: "InitialLabel",
                isPanelVisible: false,
                isGruelingGauntletActive: ShouldCaptureUltimatumDebug() && IsGruelingGauntletPassiveActive(),
                candidateCount: candidateCount,
                notes: notes);
            return false;
        }

        private static bool TryGetSaturatedUltimatumGroundOption(
            IReadOnlyList<(Element OptionElement, string ModifierName)> options,
            out (Element OptionElement, string ModifierName) saturatedChoice)
        {
            saturatedChoice = default;

            for (int i = 0; i < options.Count; i++)
            {
                (Element optionElement, string modifierName) = options[i];
                if (optionElement == null || !optionElement.IsValid)
                    continue;

                bool hasSaturationState = TryReadUltimatumChoiceSaturation(optionElement, out bool isSaturated);
                bool saturatedForSelection = ShouldTreatUltimatumChoiceAsSaturatedCore(hasSaturationState, isSaturated, optionElement.IsVisible);
                if (!saturatedForSelection)
                    continue;

                saturatedChoice = (optionElement, modifierName);
                return true;
            }

            return false;
        }

        private bool TryClickUltimatumBeginButton(LabelOnGround label, Vector2 windowTopLeft)
        {
            List<string> diagnostics = new(8);
            Element? beginButton = GetUltimatumBeginButton(label, diagnostics);
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

    }
}
