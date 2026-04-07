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
            if (!TryGetUltimatumRoot(label, diagnostics, out Element? root) || root == null)
                return results;

            if (TryGetUltimatumOptionsFromChoicePanelObject(root, diagnostics, out List<(Element OptionElement, string ModifierName)> panelResults))
                return panelResults;

            return CollectTreeOptions(root, diagnostics, results);
        }

        internal static Element? GetUltimatumBeginButton(LabelOnGround label, List<string>? diagnostics = null)
        {
            if (!TryGetUltimatumRoot(label, diagnostics, out Element? root) || root == null)
                return null;

            if (!TryGetPrimaryTreeBranch(root, diagnostics, out Element? branch) || branch == null)
                return null;

            if (!TryGetTreeNode(branch, diagnostics, "Label->Child(0)->Child(0)->Child(4)", 4, out Element? beginNode) || beginNode == null)
                return null;

            if (!TryGetTreeNode(beginNode, diagnostics, "Label->Child(0)->Child(0)->Child(4)->Child(0)", 0, out Element? beginButton) || beginButton == null)
                return null;

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
            string text = GetNormalizedElementText(option, 1024);
            if (!string.IsNullOrWhiteSpace(text))
                return text;

            if (TryGetModifierNameFromChildren(option, out text))
                return text;

            Element? tooltipName = option.Tooltip?.GetChildAtIndex(1)?.GetChildAtIndex(3);
            text = GetNormalizedElementText(tooltipName, 512);
            if (string.IsNullOrWhiteSpace(text))
                text = GetNormalizedElementText(option, 512);

            return text;
        }

        internal static string ResolveUltimatumModifierName(Element option, string? modifierName)
            => !string.IsNullOrWhiteSpace(modifierName)
                ? modifierName
                : GetUltimatumModifierName(option);

        internal static string ResolveUltimatumModifierName(Element option, int seen, IReadOnlyList<string> modifierNamesByIndex)
        {
            if (seen >= 0 && seen < modifierNamesByIndex.Count)
                return ResolveUltimatumModifierName(option, modifierNamesByIndex[seen]);

            return GetUltimatumModifierName(option);
        }

        private static bool TryGetUltimatumOptionsFromChoicePanelObject(
            Element root,
            List<string>? diagnostics,
            out List<(Element OptionElement, string ModifierName)> results)
        {
            results = new List<(Element OptionElement, string ModifierName)>(3);

            if (!TryGetVisibleChoicePanel(root, diagnostics, out UltimatumChoicePanel? choicePanel, out Element? panelElement)
                || choicePanel == null
                || panelElement == null)
                return false;

            var choiceElements = choicePanel.ChoiceElements;
            if (choiceElements == null)
            {
                diagnostics?.Add("ChoicePanel fail: ChoiceElements missing.");
                return false;
            }

            IReadOnlyList<string> modifierNamesByIndex = GetUltimatumChoicePanelModifierNames(choicePanel, diagnostics);

            CollectChoicePanelOptions(choiceElements, modifierNamesByIndex, diagnostics, results);

            return results.Count > 0;
        }

        private static bool TryGetUltimatumRoot(LabelOnGround label, List<string>? diagnostics, out Element? root)
        {
            root = label?.Label;
            if (root != null)
                return true;

            diagnostics?.Add("Tree fail: label.Label is null.");
            return false;
        }

        private static List<(Element OptionElement, string ModifierName)> CollectTreeOptions(
            Element root,
            List<string>? diagnostics,
            List<(Element OptionElement, string ModifierName)> results)
        {
            if (!TryGetTreeOptionContainer(root, diagnostics, out Element? container) || container == null)
                return results;

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

        private static bool TryGetTreeOptionContainer(Element root, List<string>? diagnostics, out Element? container)
        {
            container = null;

            if (!TryGetPrimaryTreeBranch(root, diagnostics, out Element? branch) || branch == null)
                return false;

            if (!TryGetTreeNode(branch, diagnostics, "Label->Child(0)->Child(0)->Child(2)", 2, out Element? child2) || child2 == null)
                return false;

            return TryGetTreeNode(child2, diagnostics, "Label->Child(0)->Child(0)->Child(2)->Child(0)", 0, out container);
        }

        private static bool TryGetPrimaryTreeBranch(Element root, List<string>? diagnostics, out Element? branch)
        {
            branch = null;

            if (!TryGetTreeNode(root, diagnostics, "Label->Child(0)", 0, out Element? child0) || child0 == null)
                return false;

            return TryGetTreeNode(child0, diagnostics, "Label->Child(0)->Child(0)", 0, out branch);
        }

        private static bool TryGetChoicePanelElement(Element root, List<string>? diagnostics, out Element? panelElement)
        {
            panelElement = root.GetChildFromIndices(0, 0, 2)
                ?? root.GetChildAtIndex(0)?.GetChildAtIndex(0)?.GetChildAtIndex(2);
            if (panelElement != null)
                return true;

            diagnostics?.Add("ChoicePanel fail: Label->Child(0)->Child(0)->Child(2) is null.");
            return false;
        }

        private static bool TryGetVisibleChoicePanel(
            Element root,
            List<string>? diagnostics,
            out UltimatumChoicePanel? choicePanel,
            out Element? panelElement)
        {
            choicePanel = null;
            panelElement = null;

            if (!TryGetChoicePanelElement(root, diagnostics, out panelElement) || panelElement == null)
                return false;

            choicePanel = panelElement.AsObject<UltimatumChoicePanel>();
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

            return true;
        }

        private static void CollectChoicePanelOptions(
            object? choiceElements,
            IReadOnlyList<string> modifierNamesByIndex,
            List<string>? diagnostics,
            List<(Element OptionElement, string ModifierName)> results)
        {
            int seen = 0;
            foreach (object? choiceObj in DynamicObjectAdapter.EnumerateObjects(choiceElements))
            {
                if (!TryExtractElement(choiceObj, out Element? option) || option == null)
                {
                    diagnostics?.Add($"ChoicePanel option[{seen}] is not an Element.");
                    seen++;
                    continue;
                }

                string modifierName = ResolveUltimatumModifierName(option, seen, modifierNamesByIndex);
                if (string.IsNullOrWhiteSpace(modifierName))
                    modifierName = $"Unknown Option {seen + 1}";

                diagnostics?.Add($"ChoicePanel option[{seen}] modifier='{modifierName}', option=0x{option.Address:X}, visible={option.IsVisible}, valid={option.IsValid}");
                results.Add((option, modifierName));
                seen++;
            }
        }

        private static bool TryGetTreeNode(Element parent, List<string>? diagnostics, string path, int index, out Element? child)
        {
            child = parent.GetChildAtIndex(index);
            if (child != null)
                return true;

            diagnostics?.Add($"Tree fail: {path} is null.");
            return false;
        }

        private static IReadOnlyList<string> GetUltimatumChoicePanelModifierNames(UltimatumChoicePanel choicePanel, List<string>? diagnostics)
        {
            var modifiersObj = choicePanel.Modifiers;
            return ExtractUltimatumModifierNames(modifiersObj, diagnostics, "ChoicePanel: Modifiers missing.");
        }

        private static bool TryGetModifierNameFromChildren(Element option, out string text)
        {
            for (int i = 0; i < 8; i++)
            {
                Element? child = option.GetChildAtIndex(i);
                if (child == null)
                    continue;

                text = GetNormalizedElementText(child, 1024);
                if (!string.IsNullOrWhiteSpace(text))
                    return true;

                for (int j = 0; j < 8; j++)
                {
                    Element? grandChild = child.GetChildAtIndex(j);
                    if (grandChild == null)
                        continue;

                    text = GetNormalizedElementText(grandChild, 1024);
                    if (!string.IsNullOrWhiteSpace(text))
                        return true;
                }
            }

            text = string.Empty;
            return false;
        }

        private static string GetNormalizedElementText(Element? element, int maxLength)
            => element == null
                ? string.Empty
                : NormalizeModifierText(element.GetText(maxLength) ?? string.Empty);

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