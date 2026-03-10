using System.Collections;
using System.Linq;
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
        private const int UltimatumPreHoverDelayMs = 30;

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

        private static bool IsUltimatumPath(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.Contains(Constants.UltimatumChallengeInteractablePath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUltimatumLabel(LabelOnGround? label)
        {
            if (!IsUltimatumPath(label?.ItemOnGround?.Path))
                return false;

            // The top-level Ultimatum label can remain present after encounter end, but
            // Child(0).IsVisible is a reliable active-state signal for whether choices are live.
            Element? child0 = label?.Label?.GetChildAtIndex(0);
            return child0?.IsVisible == true;
        }

        private static bool ShouldSuppressInactiveUltimatumLabel(LabelOnGround? label)
        {
            if (!IsUltimatumPath(label?.ItemOnGround?.Path))
                return false;

            return !IsUltimatumLabel(label);
        }

        private bool TryHandleUltimatumPanelUi(Vector2 windowTopLeft)
        {
            if (!settings.ClickUltimatum.Value)
                return false;

            if (!TryGetVisibleUltimatumPanel(out object? panelObj) || panelObj == null)
                return false;

            DebugLog(() => "[TryHandleUltimatumPanelUi] UltimatumPanel detected.");

            bool clickedAny = false;

            if (TryClickUltimatumPanelChoice(panelObj, windowTopLeft))
            {
                clickedAny = true;
                // Match ground-label pacing: let UI update after selecting a modifier.
                Thread.Sleep(UltimatumChoiceToBeginDelayMs);
            }

            if (TryClickUltimatumPanelConfirm(panelObj, windowTopLeft))
            {
                clickedAny = true;
                // Match ground-label pacing: short settle time after confirm.
                Thread.Sleep(UltimatumPostBeginDelayMs);
            }

            return clickedAny;
        }

        public bool TryGetUltimatumPanelOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

            if (!settings.ClickUltimatum.Value)
                return false;

            if (!TryGetVisibleUltimatumPanel(out object? panelObj) || panelObj == null)
                return false;

            if (!TryGetUltimatumPanelChoiceCandidates(panelObj, out List<UltimatumPanelChoiceCandidate>? candidates, logFailures: false) || candidates == null || candidates.Count == 0)
                return false;

            bool hasBest = TryGetBestUltimatumPanelChoice(candidates, out UltimatumPanelChoiceCandidate best);

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

        public bool TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

            if (!settings.ClickUltimatum.Value)
                return false;

            if (TryGetUltimatumPanelOptionPreview(out previews) && previews.Count > 0)
                return true;

            return TryGetUltimatumGroundLabelOptionPreview(out previews);
        }

        private bool TryGetUltimatumGroundLabelOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

            var labels = cachedLabels?.Value;
            if (labels == null || labels.Count == 0)
                return false;

            LabelOnGround? ultimatumLabel = null;
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
                break;
            }

            if (ultimatumLabel == null)
                return false;

            List<(Element OptionElement, string ModifierName)> options = GetUltimatumOptions(ultimatumLabel);
            if (options.Count == 0)
                return false;

            var priorities = settings.GetUltimatumModifierPriority();
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

        private bool TryGetVisibleUltimatumPanel(out object? panelObj)
        {
            panelObj = null;

            object? ingameUi = gameController?.IngameState?.IngameUi;
            if (ingameUi == null)
                return false;

            if (!TryGetPropertyValue(ingameUi, "UltimatumPanel", out panelObj) || panelObj == null)
            {
                DebugLog(() => "[TryHandleUltimatumPanelUi] UltimatumPanel not available.");
                return false;
            }

            if (!TryGetPropertyValue(panelObj, "IsVisible", out object? visibleObj) || visibleObj is not bool isVisible || !isVisible)
            {
                DebugLog(() => "[TryHandleUltimatumPanelUi] UltimatumPanel exists but is not visible.");
                return false;
            }

            return true;
        }

        private bool TryGetUltimatumPanelChoiceCandidates(object panelObj, out List<UltimatumPanelChoiceCandidate>? candidates, bool logFailures)
        {
            candidates = [];

            if (!TryGetPropertyValue(panelObj, "ChoicesPanel", out object? choicesPanelObj) || choicesPanelObj == null)
            {
                if (logFailures)
                    DebugLog(() => "[TryClickUltimatumPanelChoice] ChoicesPanel missing.");
                return false;
            }

            if (!TryGetPropertyValue(choicesPanelObj, "ChoiceElements", out object? choiceElementsObj) || choiceElementsObj == null)
            {
                if (logFailures)
                    DebugLog(() => "[TryClickUltimatumPanelChoice] ChoiceElements missing.");
                return false;
            }

            List<string> modifierNamesByIndex = GetUltimatumPanelModifierNames(panelObj);

            var priorities = settings.GetUltimatumModifierPriority();
            int seen = 0;
            foreach (object? choiceObj in EnumerateObjects(choiceElementsObj))
            {
                if (!TryExtractElement(choiceObj, out Element? choiceEl) || choiceEl == null)
                {
                    if (logFailures)
                        DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] is not an Element.");
                    seen++;
                    continue;
                }

                if (!choiceEl.IsValid)
                {
                    if (logFailures)
                        DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] ignored - valid={choiceEl.IsValid}");
                    seen++;
                    continue;
                }

                RectangleF choiceRect = choiceEl.GetClientRect();
                if (choiceRect.Width <= 0 || choiceRect.Height <= 0)
                {
                    if (logFailures)
                        DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] ignored - empty rect {choiceRect}.");
                    seen++;
                    continue;
                }

                string modifierName = string.Empty;
                if (seen < modifierNamesByIndex.Count)
                {
                    string modifierFromPanel = modifierNamesByIndex[seen];
                    if (!string.IsNullOrWhiteSpace(modifierFromPanel))
                    {
                        modifierName = modifierFromPanel;
                    }
                }

                if (string.IsNullOrWhiteSpace(modifierName))
                {
                    // Minimal fallback for safety if panel Modifiers list is unavailable.
                    modifierName = GetUltimatumModifierName(choiceEl);
                    if (string.IsNullOrWhiteSpace(modifierName))
                    {
                        modifierName = NormalizeModifierText(choiceEl.GetText(1024) ?? string.Empty);
                    }
                }

                int priorityIndex = GetModifierPriorityIndex(modifierName, priorities);

                if (logFailures)
                    DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] modifier='{modifierName}', priority={priorityIndex}, center={choiceRect.Center}, visible={choiceEl.IsVisible}, valid={choiceEl.IsValid}");

                candidates.Add(new UltimatumPanelChoiceCandidate(choiceEl, modifierName, priorityIndex));
                seen++;
            }

            return candidates.Count > 0;
        }

        private static List<string> GetUltimatumPanelModifierNames(object panelObj)
        {
            var names = new List<string>(3);

            if (!TryGetPropertyValue(panelObj, "Modifiers", out object? modifiersObj) || modifiersObj == null)
                return names;

            foreach (object? modifierObj in EnumerateObjects(modifiersObj))
            {
                if (modifierObj == null)
                    continue;

                if (TryGetPropertyValue(modifierObj, "Name", out object? nameObj) && nameObj != null)
                {
                    string normalized = NormalizeModifierText(nameObj.ToString() ?? string.Empty);
                    names.Add(normalized);
                    continue;
                }

                names.Add(string.Empty);
            }

            return names;
        }

        private static bool TryGetBestUltimatumPanelChoice(List<UltimatumPanelChoiceCandidate> candidates, out UltimatumPanelChoiceCandidate best)
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

            // Avoid selecting an arbitrary option when all choices fail priority matching.
            return found && bestIndex != int.MaxValue;
        }

        private bool TryClickUltimatumPanelChoice(object panelObj, Vector2 windowTopLeft)
        {
            if (!TryGetUltimatumPanelChoiceCandidates(panelObj, out List<UltimatumPanelChoiceCandidate>? candidates, logFailures: true) || candidates == null)
            {
                DebugLog(() => "[TryClickUltimatumPanelChoice] No ranked choice found.");
                return false;
            }

            if (!TryGetBestUltimatumPanelChoice(candidates, out UltimatumPanelChoiceCandidate best))
            {
                DebugLog(() => "[TryClickUltimatumPanelChoice] No ranked choice found.");
                return false;
            }

            if (settings.VerifyCursorInGameWindowBeforeClick?.Value == true && !IsCursorInsideGameWindow())
            {
                DebugLog(() => "[TryClickUltimatumPanelChoice] Skipping click - cursor outside PoE window.");
                return false;
            }

            RectangleF bestRect = best.ChoiceElement.GetClientRect();
            if (!pointIsInClickableArea(bestRect.Center, "Ultimatum"))
            {
                DebugLog(() => $"[TryClickUltimatumPanelChoice] Rejected by clickable-area check. best='{best.ModifierName}', center={bestRect.Center}");
                return false;
            }

            Vector2 clickPos = bestRect.Center + windowTopLeft;
            DebugLog(() => $"[TryClickUltimatumPanelChoice] Clicking choice '{best.ModifierName}' (priority={best.PriorityIndex}) at {clickPos}");

            using (LockManager.AcquireStatic(_elementAccessLock))
            {
                inputHandler.PerformClick(clickPos, best.ChoiceElement, gameController);
            }

            performanceMonitor.RecordClickInterval();
            return true;
        }

        private bool TryClickUltimatumPanelConfirm(object panelObj, Vector2 windowTopLeft)
        {
            if (!TryGetPropertyValue(panelObj, "ConfirmButton", out object? confirmObj) || confirmObj == null)
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

            if (settings.VerifyCursorInGameWindowBeforeClick?.Value == true && !IsCursorInsideGameWindow())
            {
                DebugLog(() => "[TryClickUltimatumPanelConfirm] Skipping click - cursor outside PoE window.");
                return false;
            }

            RectangleF confirmRect = confirmEl.GetClientRect();
            if (!pointIsInClickableArea(confirmRect.Center, "Ultimatum"))
            {
                DebugLog(() => $"[TryClickUltimatumPanelConfirm] Rejected by clickable-area check. center={confirmRect.Center}");
                return false;
            }

            Vector2 confirmClick = confirmRect.Center + windowTopLeft;
            DebugLog(() => $"[TryClickUltimatumPanelConfirm] Clicking ConfirmButton at {confirmClick}");

            using (LockManager.AcquireStatic(_elementAccessLock))
            {
                inputHandler.PerformClick(confirmClick, confirmEl, gameController);
            }

            performanceMonitor.RecordClickInterval();
            return true;
        }

        private static IEnumerable<object?> EnumerateObjects(object source)
        {
            if (source is IEnumerable enumerable)
            {
                foreach (object? item in enumerable)
                {
                    yield return item;
                }
            }
        }

        private static bool TryExtractElement(object? source, out Element? element)
        {
            element = null;
            if (source == null)
                return false;

            if (source is Element direct)
            {
                element = direct;
                return true;
            }

            if (TryGetPropertyValue(source, "Element", out object? nested) && nested is Element nestedElement)
            {
                element = nestedElement;
                return true;
            }

            return false;
        }

        private static bool TryGetPropertyValue(object source, string propertyName, out object? value)
        {
            value = null;
            if (source == null)
                return false;

            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.IgnoreCase;

            Type sourceType = source.GetType();

            var prop = sourceType.GetProperty(propertyName, flags);
            if (prop != null)
            {
                try
                {
                    value = prop.GetValue(source);
                    return true;
                }
                catch
                {
                    value = null;
                    return false;
                }
            }

            var field = sourceType.GetField(propertyName, flags);
            if (field != null)
            {
                try
                {
                    value = field.GetValue(source);
                    return true;
                }
                catch
                {
                    value = null;
                    return false;
                }
            }

            return false;
        }

        private bool TryClickPreferredUltimatumModifier(LabelOnGround label, Vector2 windowTopLeft)
        {
            string labelPath = label?.ItemOnGround?.Path ?? string.Empty;
            ulong labelAddress = unchecked((ulong)(label?.Label?.Address ?? 0));
            DebugLog(() => $"[TryClickPreferredUltimatumModifier] Entered. ClickUltimatum={settings.ClickUltimatum.Value}, Path='{labelPath}', LabelAddr=0x{labelAddress:X}");

            if (!settings.ClickUltimatum.Value)
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
            for (int i = 0; i < diagnostics.Count; i++)
            {
                string msg = diagnostics[i];
                DebugLog(() => $"[TryClickPreferredUltimatumModifier] {msg}");
            }

            if (options.Count == 0)
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] No Ultimatum options found in UI tree.");
                return false;
            }

            var priorities = settings.GetUltimatumModifierPriority();
            bool shouldPrimeTooltips = ShouldPrimeUltimatumGroundLabelTooltips(options.Select(static o => o.ModifierName), priorities);

            // Ground-label Ultimatum text is often hover-initialized, so pre-hover only when
            // at least one option is still unresolved (gray overlay / unknown priority).
            if (shouldPrimeTooltips)
            {
                PrimeUltimatumGroundLabelOptionTooltips(options, windowTopLeft);

                diagnostics.Clear();
                List<(Element OptionElement, string ModifierName)> refreshedOptions = GetUltimatumOptions(label, diagnostics);
                for (int i = 0; i < diagnostics.Count; i++)
                {
                    string msg = diagnostics[i];
                    DebugLog(() => $"[TryClickPreferredUltimatumModifier] {msg}");
                }

                if (refreshedOptions.Count > 0)
                {
                    options = refreshedOptions;
                }
            }
            else
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] Skipping pre-hover: all Ultimatum modifiers are already recognized.");
            }

            DebugLog(() => $"[TryClickPreferredUltimatumModifier] Found {options.Count} Ultimatum option(s).");
            int bestIndex = int.MaxValue;
            Element? bestOption = null;
            string bestModifier = string.Empty;

            foreach ((Element optionElement, string modifierName) in options)
            {
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

            if (settings.VerifyCursorInGameWindowBeforeClick?.Value == true && !IsCursorInsideGameWindow())
            {
                DebugLog(() => "[TryClickPreferredUltimatumModifier] Skipping click - cursor outside PoE window");
                return false;
            }

            RectangleF optionRect = bestOption.GetClientRect();
            if (!pointIsInClickableArea(optionRect.Center, "Ultimatum"))
            {
                DebugLog(() => $"[TryClickPreferredUltimatumModifier] Rejected by clickable-area check. best='{bestModifier}', center={optionRect.Center}");
                return false;
            }

            Vector2 clickPos = optionRect.Center + windowTopLeft;
            DebugLog(() => $"[TryClickPreferredUltimatumModifier] Clicking '{bestModifier}' (priority index {bestIndex})");

            using (LockManager.AcquireStatic(_elementAccessLock))
            {
                inputHandler.PerformClick(clickPos, bestOption, gameController);
            }

            // Let the panel state update before attempting to click Begin.
            Thread.Sleep(UltimatumChoiceToBeginDelayMs);

            // Attempt to click Begin immediately after selecting a modifier.
            TryClickUltimatumBeginButton(label, windowTopLeft);

            performanceMonitor.RecordClickInterval();
            return true;
        }

        private static bool ShouldPrimeUltimatumGroundLabelTooltips(IEnumerable<string> modifierNames, IReadOnlyList<string> priorities)
        {
            foreach (string modifierName in modifierNames)
            {
                if (string.IsNullOrWhiteSpace(modifierName))
                    return true;

                if (GetModifierPriorityIndex(modifierName, priorities) == int.MaxValue)
                    return true;
            }

            return false;
        }

        private void PrimeUltimatumGroundLabelOptionTooltips(List<(Element OptionElement, string ModifierName)> options, Vector2 windowTopLeft)
        {
            if (options == null || options.Count == 0)
                return;

            if (settings.VerifyCursorInGameWindowBeforeClick?.Value == true && !IsCursorInsideGameWindow())
            {
                DebugLog(() => "[PrimeUltimatumGroundLabelOptionTooltips] Skipping pre-hover - cursor outside PoE window");
                return;
            }

            int[] hoverIndices = ClickServiceSeams.GetUltimatumPreHoverIndices(options.Count);
            for (int i = 0; i < hoverIndices.Length; i++)
            {
                int index = hoverIndices[i];
                if (index < 0 || index >= options.Count)
                    continue;

                Element optionElement = options[index].OptionElement;
                if (optionElement == null || !optionElement.IsValid || !optionElement.IsVisible)
                    continue;

                RectangleF optionRect = optionElement.GetClientRect();
                if (optionRect.Width <= 0 || optionRect.Height <= 0)
                    continue;

                Vector2 hoverPoint = optionRect.Center + windowTopLeft;
                inputHandler.HoverAndGetUIHover(hoverPoint, gameController, UltimatumPreHoverDelayMs);
            }
        }

        private bool TryClickUltimatumBeginButton(LabelOnGround label, Vector2 windowTopLeft)
        {
            List<string> diagnostics = new(8);
            Element? beginButton = GetUltimatumBeginButton(label, diagnostics);
            for (int i = 0; i < diagnostics.Count; i++)
            {
                string msg = diagnostics[i];
                DebugLog(() => $"[TryClickUltimatumBeginButton] {msg}");
            }

            if (beginButton == null)
            {
                DebugLog(() => "[TryClickUltimatumBeginButton] Begin button not found.");
                return false;
            }

            if (settings.VerifyCursorInGameWindowBeforeClick?.Value == true && !IsCursorInsideGameWindow())
            {
                DebugLog(() => "[TryClickUltimatumBeginButton] Skipping click - cursor outside PoE window");
                return false;
            }

            RectangleF beginRect = beginButton.GetClientRect();
            if (!pointIsInClickableArea(beginRect.Center, "Ultimatum"))
            {
                DebugLog(() => $"[TryClickUltimatumBeginButton] Rejected by clickable-area check. center={beginRect.Center}");
                return false;
            }

            Vector2 beginClickPos = beginRect.Center + windowTopLeft;
            DebugLog(() => $"[TryClickUltimatumBeginButton] Clicking Begin at {beginClickPos}");

            using (LockManager.AcquireStatic(_elementAccessLock))
            {
                inputHandler.PerformClick(beginClickPos, beginButton, gameController);
            }

            // Give the encounter UI a brief moment to transition after Begin.
            Thread.Sleep(UltimatumPostBeginDelayMs);

            return true;
        }

        private static List<(Element OptionElement, string ModifierName)> GetUltimatumOptions(LabelOnGround label, List<string>? diagnostics = null)
        {
            var results = new List<(Element OptionElement, string ModifierName)>(3);
            Element? root = label?.Label;
            if (root == null)
            {
                diagnostics?.Add("Tree fail: label.Label is null.");
                return results;
            }

            // Verified tree:
            // ItemsOnGroundLabelsVisible -> UltimatumChallengeInteractable -> Label
            // -> Child(0) -> Child(0) -> Child(2) -> Child(0) -> Child(0..2)
            Element? n0 = root.GetChildAtIndex(0);
            if (n0 == null)
            {
                diagnostics?.Add("Tree fail: Label->Child(0) is null.");
                return results;
            }

            Element? n1 = n0.GetChildAtIndex(0);
            if (n1 == null)
            {
                diagnostics?.Add("Tree fail: Label->Child(0)->Child(0) is null.");
                return results;
            }

            Element? n2 = n1.GetChildAtIndex(2);
            if (n2 == null)
            {
                diagnostics?.Add("Tree fail: Label->Child(0)->Child(0)->Child(2) is null.");
                return results;
            }

            Element? container = n2.GetChildAtIndex(0);
            if (container == null)
            {
                diagnostics?.Add("Tree fail: Label->Child(0)->Child(0)->Child(2)->Child(0) is null.");
                return results;
            }

            diagnostics?.Add($"Tree ok: container=0x{container.Address:X}, visible={container.IsVisible}, valid={container.IsValid}");

            for (int i = 0; i < 3; i++)
            {
                Element? option = container.GetChildAtIndex(i);
                if (option == null)
                {
                    diagnostics?.Add($"Option[{i}] missing at container->Child({i}).");
                    continue;
                }

                string modifierName = GetUltimatumModifierName(option);
                if (string.IsNullOrWhiteSpace(modifierName))
                {
                    // Keep the option for overlay/click candidate evaluation even when text is unavailable
                    // (text can be hover-populated on some Ultimatum UI states).
                    modifierName = $"Unknown Option {i + 1}";
                    diagnostics?.Add($"Option[{i}] text unavailable, using fallback name '{modifierName}'. option=0x{option.Address:X}");
                }

                diagnostics?.Add($"Option[{i}] modifier='{modifierName}', option=0x{option.Address:X}, visible={option.IsVisible}, valid={option.IsValid}");
                results.Add((option, modifierName));
            }

            return results;
        }

        private static Element? GetUltimatumBeginButton(LabelOnGround label, List<string>? diagnostics = null)
        {
            Element? root = label?.Label;
            if (root == null)
            {
                diagnostics?.Add("Tree fail: label.Label is null.");
                return null;
            }

            // Verified tree:
            // ItemsOnGroundLabelsVisible -> UltimatumChallengeInteractable -> Label
            // -> Child(0) -> Child(0) -> Child(4) -> Child(0)
            Element? n0 = root.GetChildAtIndex(0);
            if (n0 == null)
            {
                diagnostics?.Add("Tree fail: Label->Child(0) is null.");
                return null;
            }

            Element? n1 = n0.GetChildAtIndex(0);
            if (n1 == null)
            {
                diagnostics?.Add("Tree fail: Label->Child(0)->Child(0) is null.");
                return null;
            }

            Element? n2 = n1.GetChildAtIndex(4);
            if (n2 == null)
            {
                diagnostics?.Add("Tree fail: Label->Child(0)->Child(0)->Child(4) is null.");
                return null;
            }

            Element? beginButton = n2.GetChildAtIndex(0);
            if (beginButton == null)
            {
                diagnostics?.Add("Tree fail: Label->Child(0)->Child(0)->Child(4)->Child(0) is null.");
                return null;
            }

            diagnostics?.Add($"Tree ok: beginButton=0x{beginButton.Address:X}, visible={beginButton.IsVisible}, valid={beginButton.IsValid}");
            return beginButton;
        }

        private static string GetUltimatumModifierName(Element option)
        {
            Element? tooltipName = option.Tooltip?.GetChildAtIndex(1)?.GetChildAtIndex(3);
            string text = tooltipName?.GetText(512) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = option.GetText(512) ?? string.Empty;
            }

            return NormalizeModifierText(text);
        }

        private static string NormalizeModifierText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
            while (normalized.Contains("  ", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            }

            return normalized;
        }

        private static int GetModifierPriorityIndex(string modifierName, IReadOnlyList<string> priorities)
        {
            for (int i = 0; i < priorities.Count; i++)
            {
                string priority = priorities[i];
                if (string.IsNullOrWhiteSpace(priority))
                    continue;

                if (modifierName.Equals(priority, StringComparison.OrdinalIgnoreCase))
                    return i;

                if (modifierName.StartsWith(priority + " ", StringComparison.OrdinalIgnoreCase))
                    return i;

                if (modifierName.Contains(priority, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return int.MaxValue;
        }
    }
}
