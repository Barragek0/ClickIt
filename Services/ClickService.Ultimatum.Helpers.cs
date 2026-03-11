using System.Collections;
using ClickIt.Definitions;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using SharpDX;

#nullable enable

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private static IEnumerable<object?> EnumerateObjects(object? source)
        {
            if (source == null)
                yield break;

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

        private void LogDiagnostics(string prefix, List<string> diagnostics)
        {
            for (int i = 0; i < diagnostics.Count; i++)
            {
                string msg = diagnostics[i];
                DebugLog(() => $"{prefix} {msg}");
            }
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
    }
}