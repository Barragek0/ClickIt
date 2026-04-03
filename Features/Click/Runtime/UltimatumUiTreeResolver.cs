using ClickIt.Shared;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;

namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumUiTreeResolver
    {
        internal static IEnumerable<object?> EnumerateObjects(object? source)
            => DynamicObjectAdapter.EnumerateObjects(source);

        internal static bool TryExtractElement(object? source, out Element? element)
        {
            element = source as Element;
            return element != null;
        }

        internal static List<(Element OptionElement, string ModifierName)> GetUltimatumOptions(LabelOnGround label, List<string>? diagnostics = null)
        {
            var results = new List<(Element OptionElement, string ModifierName)>(3);
            Element? root = label?.Label;
            if (root == null)
            {
                diagnostics?.Add("Tree fail: label.Label is null.");
                return results;
            }

            if (TryGetUltimatumOptionsFromChoicePanelObject(root, diagnostics, out List<(Element OptionElement, string ModifierName)> panelResults))
            {
                return panelResults;
            }

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
                    modifierName = $"Unknown Option {i + 1}";
                    diagnostics?.Add($"Option[{i}] text unavailable, using fallback name '{modifierName}'. option=0x{option.Address:X}");
                }

                diagnostics?.Add($"Option[{i}] modifier='{modifierName}', option=0x{option.Address:X}, visible={option.IsVisible}, valid={option.IsValid}");
                results.Add((option, modifierName));
            }

            return results;
        }

        internal static Element? GetUltimatumBeginButton(LabelOnGround label, List<string>? diagnostics = null)
        {
            Element? root = label?.Label;
            if (root == null)
            {
                diagnostics?.Add("Tree fail: label.Label is null.");
                return null;
            }

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

        internal static IReadOnlyList<string> ExtractUltimatumModifierNames(object? modifiersObj, List<string>? diagnostics = null, string? missingModifiersMessage = null)
        {
            if (modifiersObj == null)
            {
                if (!string.IsNullOrWhiteSpace(missingModifiersMessage))
                    diagnostics?.Add(missingModifiersMessage);
                return [];
            }

            List<string>? names = null;

            foreach (object? modifierObj in DynamicObjectAdapter.EnumerateObjects(modifiersObj))
            {
                names ??= new List<string>(3);

                if (modifierObj is string modifierName)
                {
                    names.Add(NormalizeModifierText(modifierName));
                    continue;
                }

                names.Add(NormalizeModifierText(modifierObj?.ToString() ?? string.Empty));
            }

            return names == null ? [] : [.. names];
        }

        internal static string GetUltimatumModifierName(Element option)
        {
            string text = NormalizeModifierText(option.GetText(1024) ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            // Ultimatum option text can sit in child labels before tooltip hydration.
            for (int i = 0; i < 8; i++)
            {
                Element? child = option.GetChildAtIndex(i);
                if (child == null)
                    continue;

                text = NormalizeModifierText(child.GetText(1024) ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(text))
                    return text;

                for (int j = 0; j < 8; j++)
                {
                    Element? grandChild = child.GetChildAtIndex(j);
                    if (grandChild == null)
                        continue;

                    text = NormalizeModifierText(grandChild.GetText(1024) ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }

            Element? tooltipName = option.Tooltip?.GetChildAtIndex(1)?.GetChildAtIndex(3);
            text = tooltipName?.GetText(512) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = option.GetText(512) ?? string.Empty;
            }

            return NormalizeModifierText(text);
        }

        private static bool TryGetUltimatumOptionsFromChoicePanelObject(
            Element root,
            List<string>? diagnostics,
            out List<(Element OptionElement, string ModifierName)> results)
        {
            results = new List<(Element OptionElement, string ModifierName)>(3);

            Element? panelElement = root.GetChildFromIndices(0, 0, 2)
                ?? root.GetChildAtIndex(0)?.GetChildAtIndex(0)?.GetChildAtIndex(2);
            if (panelElement == null)
            {
                diagnostics?.Add("ChoicePanel fail: Label->Child(0)->Child(0)->Child(2) is null.");
                return false;
            }

            UltimatumChoicePanel? choicePanel = panelElement.AsObject<UltimatumChoicePanel>();
            if (choicePanel == null)
            {
                diagnostics?.Add($"ChoicePanel fail: AsObject<UltimatumChoicePanel> returned null for 0x{panelElement.Address:X}.");
                return false;
            }

            if (!choicePanel.IsVisible)
            {
                diagnostics?.Add("ChoicePanel fail: panel object exists but is not visible.");
                return false;
            }

            var choiceElements = choicePanel.ChoiceElements;
            if (choiceElements == null)
            {
                diagnostics?.Add("ChoicePanel fail: ChoiceElements missing.");
                return false;
            }

            IReadOnlyList<string> modifierNamesByIndex = GetUltimatumChoicePanelModifierNames(choicePanel, diagnostics);

            int seen = 0;
            foreach (object? choiceObj in DynamicObjectAdapter.EnumerateObjects(choiceElements))
            {
                if (!TryExtractElement(choiceObj, out Element? option) || option == null)
                {
                    diagnostics?.Add($"ChoicePanel option[{seen}] is not an Element.");
                    seen++;
                    continue;
                }

                string modifierName = ResolveUltimatumChoiceModifierName(option, seen, modifierNamesByIndex);
                if (string.IsNullOrWhiteSpace(modifierName))
                {
                    modifierName = $"Unknown Option {seen + 1}";
                }

                diagnostics?.Add($"ChoicePanel option[{seen}] modifier='{modifierName}', option=0x{option.Address:X}, visible={option.IsVisible}, valid={option.IsValid}");
                results.Add((option, modifierName));
                seen++;
            }

            return results.Count > 0;
        }

        private static IReadOnlyList<string> GetUltimatumChoicePanelModifierNames(UltimatumChoicePanel choicePanel, List<string>? diagnostics)
        {
            var modifiersObj = choicePanel.Modifiers;
            return ExtractUltimatumModifierNames(modifiersObj, diagnostics, "ChoicePanel: Modifiers missing.");
        }

        private static string ResolveUltimatumChoiceModifierName(Element option, int seen, IReadOnlyList<string> modifierNamesByIndex)
        {
            if (seen >= 0 && seen < modifierNamesByIndex.Count)
            {
                string modifierFromPanel = modifierNamesByIndex[seen];
                if (!string.IsNullOrWhiteSpace(modifierFromPanel))
                    return modifierFromPanel;
            }

            return GetUltimatumModifierName(option);
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
    }
}