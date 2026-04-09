namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumUiTreeResolver
    {
        internal static IEnumerable<object?> EnumerateObjects(object? source)
            => DynamicObjectAdapter.EnumerateObjects(source);

        internal static bool TryExtractElement(object? source, out Element? element)
        {
            element = source as Element;
            if (element != null)
                return true;

            if (TryGetDynamicValue(source, static s => s.Element, out object? wrappedElement) && wrappedElement is Element directElement)
            {
                element = directElement;
                return true;
            }

            if (TryGetDynamicValue(source, static s => s.OptionElement, out object? optionElement) && optionElement is Element wrappedOptionElement)
            {
                element = wrappedOptionElement;
                return true;
            }

            return false;
        }

        internal static List<(Element OptionElement, string ModifierName)> GetUltimatumOptions(LabelOnGround label, List<string>? diagnostics = null)
        {
            List<(Element OptionElement, string ModifierName)> results = new(3);
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

            if (!TryGetPrimaryTreeBranch(root, diagnostics, out object? branch) || branch == null)
                return null;

            if (!TryGetElementChildNode(branch, diagnostics, "Label->Child(0)->Child(0)->Child(4)", 4, out Element? beginNode) || beginNode == null)
                return null;

            if (!TryGetElementChildNode(beginNode, diagnostics, "Label->Child(0)->Child(0)->Child(4)->Child(0)", 0, out Element? beginButton) || beginButton == null)
                return null;

            diagnostics?.Add($"Tree ok: beginButton={DescribeElement(beginButton)}");
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
            => ResolveModifierNameFromNode(option, option);

        private static string ResolveModifierNameFromNode(object? optionSource, Element? optionElement)
        {
            string text = GetNormalizedNodeText(optionSource, 1024);
            if (!string.IsNullOrWhiteSpace(text))
                return text;

            if (TryGetModifierNameFromChildren(optionSource, out text))
                return text;

            object? tooltipName = ResolveTooltipNameNode(optionSource);
            text = GetNormalizedNodeText(tooltipName, 512);
            if (string.IsNullOrWhiteSpace(text))
                text = GetNormalizedNodeText(optionSource, 512);

            if (string.IsNullOrWhiteSpace(text) && optionElement != null && !ReferenceEquals(optionSource, optionElement))
                text = GetNormalizedNodeText(optionElement, 512);

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
            object root,
            List<string>? diagnostics,
            out List<(Element OptionElement, string ModifierName)> results)
        {
            results = new List<(Element OptionElement, string ModifierName)>(3);

            if (!TryGetVisibleChoicePanel(root, diagnostics, out object? choicePanel, out object? panelElement)
                || choicePanel == null
                || panelElement == null)
                return false;

            if (!TryGetDynamicValue(choicePanel, static s => s.ChoiceElements, out object? choiceElements)
                || choiceElements == null)
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
            object root,
            List<string>? diagnostics,
            List<(Element OptionElement, string ModifierName)> results)
        {
            if (!TryGetTreeOptionContainer(root, diagnostics, out object? container) || container == null)
                return results;

            diagnostics?.Add($"Tree ok: container={DescribeNode(container)}");

            for (int i = 0; i < 3; i++)
            {
                if (!TryGetTreeNode(container, diagnostics, $"container->Child({i})", i, out object? optionNode) || optionNode == null)
                    continue;

                if (!TryExtractElement(optionNode, out Element? option) || option == null)
                {
                    diagnostics?.Add($"Tree fail: container->Child({i}) is not an Element.");
                    continue;
                }

                string modifierName = ResolveModifierNameFromNode(optionNode, option);
                if (string.IsNullOrWhiteSpace(modifierName))
                {
                    modifierName = $"Unknown Option {i + 1}";
                    diagnostics?.Add($"Option[{i}] text unavailable, using fallback name '{modifierName}'. option={DescribeElement(option)}");
                }

                diagnostics?.Add($"Option[{i}] modifier='{modifierName}', option={DescribeElement(option)}");
                results.Add((option, modifierName));
            }

            return results;
        }

        private static bool TryGetTreeOptionContainer(object root, List<string>? diagnostics, out object? container)
        {
            container = null;

            if (!TryGetPrimaryTreeBranch(root, diagnostics, out object? branch) || branch == null)
                return false;

            if (!TryGetTreeNode(branch, diagnostics, "Label->Child(0)->Child(0)->Child(2)", 2, out object? child2) || child2 == null)
                return false;

            return TryGetTreeNode(child2, diagnostics, "Label->Child(0)->Child(0)->Child(2)->Child(0)", 0, out container);
        }

        private static bool TryGetPrimaryTreeBranch(object root, List<string>? diagnostics, out object? branch)
        {
            branch = null;

            if (!TryGetTreeNode(root, diagnostics, "Label->Child(0)", 0, out object? child0) || child0 == null)
                return false;

            return TryGetTreeNode(child0, diagnostics, "Label->Child(0)->Child(0)", 0, out branch);
        }

        private static bool TryGetChoicePanelElement(object root, List<string>? diagnostics, out object? panelElement)
        {
            if (TryGetNodeFromIndices(root, [0, 0, 2], out panelElement) && panelElement != null)
                return true;

            if (panelElement != null)
                return true;

            diagnostics?.Add("ChoicePanel fail: Label->Child(0)->Child(0)->Child(2) is null.");
            return false;
        }

        private static bool TryGetVisibleChoicePanel(
            object root,
            List<string>? diagnostics,
            out object? choicePanel,
            out object? panelElement)
        {
            choicePanel = null;

            if (!TryGetChoicePanelElement(root, diagnostics, out panelElement) || panelElement == null)
                return false;

            return TryResolveChoicePanelObject(panelElement, diagnostics, out choicePanel);
        }

        private static bool TryResolveChoicePanelObject(object? panelElement, List<string>? diagnostics, out object? choicePanel)
        {
            choicePanel = null;
            if (panelElement == null)
                return false;

            choicePanel = panelElement is UltimatumChoicePanel directChoicePanel
                ? directChoicePanel
                : panelElement is Element element ? element.AsObject<UltimatumChoicePanel>() : panelElement;

            if (choicePanel == null)
            {
                diagnostics?.Add($"ChoicePanel fail: AsObject<UltimatumChoicePanel> returned null for 0x{ResolveElementAddress(panelElement):X}.");
                return false;
            }

            if (!TryReadBoolFromEither(choicePanel, panelElement, static s => s.IsVisible, out bool isVisible))
            {
                choicePanel = null;
                diagnostics?.Add("ChoicePanel fail: panel visibility unavailable.");
                return false;
            }

            if (!isVisible)
            {
                choicePanel = null;
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

                string modifierName = ResolveUltimatumModifierNameFromChoiceObject(choiceObj, option, seen, modifierNamesByIndex);
                if (string.IsNullOrWhiteSpace(modifierName))
                    modifierName = $"Unknown Option {seen + 1}";

                diagnostics?.Add($"ChoicePanel option[{seen}] modifier='{modifierName}', option={DescribeElement(option)}");
                results.Add((option, modifierName));
                seen++;
            }
        }

        private static bool TryGetTreeNode(object? parent, List<string>? diagnostics, string path, int index, out object? child)
        {
            if (TryGetChildNode(parent, index, out child) && child != null)
                return true;

            diagnostics?.Add($"Tree fail: {path} is null.");
            return false;
        }

        private static bool TryGetElementChildNode(object? parent, List<string>? diagnostics, string path, int index, out Element? child)
        {
            child = null;

            if (!TryGetTreeNode(parent, diagnostics, path, index, out object? childNode) || childNode == null)
                return false;

            if (TryExtractElement(childNode, out child) && child != null)
                return true;

            diagnostics?.Add($"Tree fail: {path} is not an Element.");
            child = null;
            return false;
        }

        private static IReadOnlyList<string> GetUltimatumChoicePanelModifierNames(object? choicePanel, List<string>? diagnostics)
        {
            TryGetDynamicValue(choicePanel, static s => s.Modifiers, out object? modifiersObj);
            return ExtractUltimatumModifierNames(modifiersObj, diagnostics, "ChoicePanel: Modifiers missing.");
        }

        private static string ResolveUltimatumModifierNameFromChoiceObject(object? choiceObject, Element option, int seen, IReadOnlyList<string> modifierNamesByIndex)
        {
            if (seen >= 0 && seen < modifierNamesByIndex.Count)
                return ResolveUltimatumModifierName(option, modifierNamesByIndex[seen]);

            return ResolveModifierNameFromNode(choiceObject, option);
        }

        private static bool TryGetModifierNameFromChildren(object? option, out string text)
        {
            for (int i = 0; i < 8; i++)
            {
                if (!TryGetChildNode(option, i, out object? child) || child == null)
                    continue;

                text = GetNormalizedNodeText(child, 1024);
                if (!string.IsNullOrWhiteSpace(text))
                    return true;

                for (int j = 0; j < 8; j++)
                {
                    if (!TryGetChildNode(child, j, out object? grandChild) || grandChild == null)
                        continue;

                    text = GetNormalizedNodeText(grandChild, 1024);
                    if (!string.IsNullOrWhiteSpace(text))
                        return true;
                }
            }

            text = string.Empty;
            return false;
        }

        private static object? ResolveTooltipNameNode(object? optionSource)
        {
            if (!TryGetDynamicValue(optionSource, static s => s.Tooltip, out object? tooltip) || tooltip == null)
                return null;

            if (!TryGetChildNode(tooltip, 1, out object? tooltipSection) || tooltipSection == null)
                return null;

            if (!TryGetChildNode(tooltipSection, 3, out object? tooltipName) || tooltipName == null)
                return null;

            return tooltipName;
        }

        private static string GetNormalizedNodeText(object? node, int maxLength)
        {
            if (node == null)
                return string.Empty;

            if (TryReadString(n => n.GetText(maxLength), node, out string text)
                || TryReadString(n => n.Text, node, out text)
                || TryReadString(n => n.Label, node, out text)
                || TryReadString(n => n.KeyText, node, out text)
                || TryReadString(n => n.ModifierName, node, out text)
                || TryReadString(n => n.Name, node, out text))
            {
                return NormalizeModifierText(text);
            }

            return string.Empty;
        }

        private static string DescribeElement(Element? element)
        {
            if (element == null)
                return "0x0, visible=False, valid=False";

            long address = ResolveElementAddress(element);
            bool visible = TryReadBool(element, static s => s.IsVisible, out bool rawVisible) && rawVisible;
            bool valid = TryReadBool(element, static s => s.IsValid, out bool rawValid) && rawValid;
            return $"0x{address:X}, visible={visible}, valid={valid}";
        }

        private static string DescribeNode(object? node)
            => node is Element element ? DescribeElement(element) : $"0x{ResolveElementAddress(node):X}";

        private static bool TryGetNodeFromIndices(object? source, int[] indices, out object? value)
        {
            value = null;
            if (source == null)
                return false;

            if (source is Element element)
            {
                try
                {
                    value = element.GetChildFromIndices(indices);
                    if (value != null)
                        return true;
                }
                catch
                {
                }
            }

            object? current = source;
            for (int i = 0; i < indices.Length; i++)
            {
                if (!TryGetChildNode(current, indices[i], out current) || current == null)
                    return false;
            }

            value = current;
            return value != null;
        }

        private static bool TryGetChildNode(object? node, int index, out object? child)
        {
            child = null;
            if (node == null || index < 0)
                return false;

            if (TryGetDynamicValue(node, n => n.GetChildAtIndex(index), out object? directChild) && directChild != null)
            {
                child = directChild;
                return true;
            }

            if (TryGetDynamicValue(node, n => n.Child(index), out object? dynamicChild) && dynamicChild != null)
            {
                child = dynamicChild;
                return true;
            }

            if (TryGetDynamicValue(node, n => n.Children, out object? childrenObj) && childrenObj is IList list && index < list.Count)
            {
                child = list[index];
                return child != null;
            }

            return false;
        }

        private static long ResolveElementAddress(object? source)
        {
            if (source == null)
                return 0L;

            if (TryGetDynamicValue(source, static s => s.Address, out object? rawAddress) && rawAddress != null)
            {
                try
                {
                    return Convert.ToInt64(rawAddress);
                }
                catch
                {
                }
            }

            return 0L;
        }

        private static bool TryReadBool(object? source, Func<dynamic, object?> accessor, out bool value)
            => DynamicObjectAdapter.TryReadBool(source, accessor, out value);

        private static bool TryReadString(Func<dynamic, object?> accessor, object? source, out string value)
            => DynamicObjectAdapter.TryReadString(source, accessor, out value);

        private static bool TryReadBoolFromEither(object? primarySource, object? secondarySource, Func<dynamic, object?> accessor, out bool value)
            => DynamicObjectAdapter.TryReadBoolFromEither(primarySource, secondarySource, accessor, out value);

        private static bool TryGetDynamicValue(object? source, Func<dynamic, object?> accessor, out object? value)
            => DynamicObjectAdapter.TryGetValue(source, accessor, out value);

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