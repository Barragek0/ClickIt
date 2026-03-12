using System.Threading;
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
                return false;

            if (!TryGetVisibleUltimatumPanel(out object? panelObj) || panelObj == null)
                return false;

            DebugLog(() => "[TryHandleUltimatumPanelUi] UltimatumPanel detected.");

            bool clickedAny = false;

            if (TryClickUltimatumPanelChoice(panelObj, windowTopLeft))
            {
                clickedAny = true;
                Thread.Sleep(UltimatumChoiceToBeginDelayMs);
            }

            if (TryClickUltimatumPanelConfirm(panelObj, windowTopLeft))
            {
                clickedAny = true;
                Thread.Sleep(UltimatumPostBeginDelayMs);
            }

            return clickedAny;
        }

        public bool TryGetUltimatumPanelOptionPreview(out List<UltimatumPanelOptionPreview> previews)
        {
            previews = [];

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

            if (!TryGetUltimatumChoiceElements(panelObj, out object? choiceElementsObj, logFailures))
                return false;

            List<string> modifierNamesByIndex = GetUltimatumPanelModifierNames(panelObj);

            var priorities = settings.GetUltimatumModifierPriority();
            int seen = 0;
            foreach (object? choiceObj in EnumerateObjects(choiceElementsObj))
            {
                if (TryCreateUltimatumPanelChoiceCandidate(
                    choiceObj,
                    seen,
                    modifierNamesByIndex,
                    priorities,
                    logFailures,
                    out UltimatumPanelChoiceCandidate candidate))
                {
                    candidates.Add(candidate);
                }

                seen++;
            }

            return candidates.Count > 0;
        }

        private bool TryGetUltimatumChoiceElements(object panelObj, out object? choiceElementsObj, bool logFailures)
        {
            choiceElementsObj = null;

            if (!TryGetPropertyValue(panelObj, "ChoicesPanel", out object? choicesPanelObj) || choicesPanelObj == null)
            {
                if (logFailures)
                    DebugLog(() => "[TryClickUltimatumPanelChoice] ChoicesPanel missing.");
                return false;
            }

            if (!TryGetPropertyValue(choicesPanelObj, "ChoiceElements", out choiceElementsObj) || choiceElementsObj == null)
            {
                if (logFailures)
                    DebugLog(() => "[TryClickUltimatumPanelChoice] ChoiceElements missing.");
                return false;
            }

            return true;
        }

        private bool TryCreateUltimatumPanelChoiceCandidate(
            object? choiceObj,
            int seen,
            List<string> modifierNamesByIndex,
            IReadOnlyList<string> priorities,
            bool logFailures,
            out UltimatumPanelChoiceCandidate candidate)
        {
            candidate = default;

            if (!TryExtractElement(choiceObj, out Element? choiceEl) || choiceEl == null)
            {
                if (logFailures)
                    DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] is not an Element.");
                return false;
            }

            if (!choiceEl.IsValid)
            {
                if (logFailures)
                    DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] ignored - valid={choiceEl.IsValid}");
                return false;
            }

            RectangleF choiceRect = choiceEl.GetClientRect();
            if (choiceRect.Width <= 0 || choiceRect.Height <= 0)
            {
                if (logFailures)
                    DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] ignored - empty rect {choiceRect}.");
                return false;
            }

            string modifierName = ResolveUltimatumPanelModifierName(choiceEl, seen, modifierNamesByIndex);
            int priorityIndex = GetModifierPriorityIndex(modifierName, priorities);

            if (logFailures)
                DebugLog(() => $"[TryClickUltimatumPanelChoice] Choice[{seen}] modifier='{modifierName}', priority={priorityIndex}, center={choiceRect.Center}, visible={choiceEl.IsVisible}, valid={choiceEl.IsValid}");

            candidate = new UltimatumPanelChoiceCandidate(choiceEl, modifierName, priorityIndex);
            return true;
        }

        private static string ResolveUltimatumPanelModifierName(Element choiceEl, int seen, List<string> modifierNamesByIndex)
        {
            if (seen < modifierNamesByIndex.Count)
            {
                string modifierFromPanel = modifierNamesByIndex[seen];
                if (!string.IsNullOrWhiteSpace(modifierFromPanel))
                    return modifierFromPanel;
            }

            string modifierName = GetUltimatumModifierName(choiceEl);
            if (!string.IsNullOrWhiteSpace(modifierName))
                return modifierName;

            return NormalizeModifierText(choiceEl.GetText(1024) ?? string.Empty);
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

            return TryClickUltimatumElement(
                best.ChoiceElement,
                windowTopLeft,
                "[TryClickUltimatumPanelChoice] Skipping click - cursor outside PoE window.",
                $"[TryClickUltimatumPanelChoice] Rejected by clickable-area check. best='{best.ModifierName}',",
                $"[TryClickUltimatumPanelChoice] Clicking choice '{best.ModifierName}' (priority={best.PriorityIndex}) at");
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

            return TryClickUltimatumElement(
                confirmEl,
                windowTopLeft,
                "[TryClickUltimatumPanelConfirm] Skipping click - cursor outside PoE window.",
                "[TryClickUltimatumPanelConfirm] Rejected by clickable-area check.",
                "[TryClickUltimatumPanelConfirm] Clicking ConfirmButton at");
        }
    }
}