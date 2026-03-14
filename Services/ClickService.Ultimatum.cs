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

        private readonly struct UltimatumPanelChoiceCandidate(Element choiceElement, string modifierName, int priorityIndex)
        {
            public Element ChoiceElement { get; } = choiceElement;
            public string ModifierName { get; } = modifierName;
            public int PriorityIndex { get; } = priorityIndex;
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
                return false;
            }

            string labelPath = label.ItemOnGround?.Path ?? string.Empty;
            ulong labelAddress = unchecked((ulong)(label.Label?.Address ?? 0));
            bool clickInitialUltimatum = settings.IsInitialUltimatumClickEnabled();
            bool clickOtherUltimatum = settings.IsOtherUltimatumClickEnabled();
            DebugLog(() => $"[TryClickPreferredUltimatumModifier] Entered. ClickInitialUltimatum={clickInitialUltimatum}, ClickUltimatumChoices={clickOtherUltimatum}, Path='{labelPath}', LabelAddr=0x{labelAddress:X}");

            if (!clickInitialUltimatum)
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] Disabled by settings.");
                return false;
            }

            if (!IsUltimatumLabel(label))
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] Label is not Ultimatum interactable path.");
                return false;
            }

            List<string> diagnostics = new(16);
            List<(Element OptionElement, string ModifierName)> options = GetUltimatumOptions(label, diagnostics);
            LogDiagnostics("[TryClickPreferredUltimatumModifier]", diagnostics);

            if (options.Count == 0)
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] No Ultimatum options found in UI tree.");
                return false;
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
                return false;
            }

            Thread.Sleep(UltimatumChoiceToBeginDelayMs);
            TryClickUltimatumBeginButton(label, windowTopLeft);

            return true;
        }

        private void TryClickUltimatumBeginButton(LabelOnGround label, Vector2 windowTopLeft)
        {
            List<string> diagnostics = new(8);
            Element? beginButton = GetUltimatumBeginButton(label, diagnostics);
            LogDiagnostics("[TryClickUltimatumBeginButton]", diagnostics);

            if (beginButton == null)
            {
                DebugLog(() => "[TryClickUltimatumBeginButton] Begin button not found.");
                return;
            }

            if (!TryClickUltimatumElement(
                beginButton,
                windowTopLeft,
                "[TryClickUltimatumBeginButton] Skipping click - cursor outside PoE window",
                "[TryClickUltimatumBeginButton] Rejected by clickable-area check.",
                "[TryClickUltimatumBeginButton] Clicking Begin at"))
            {
                return;
            }

            Thread.Sleep(UltimatumPostBeginDelayMs + UltimatumPostBeginAdditionalClickDelayMs);
        }

    }
}
