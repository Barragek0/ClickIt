using System;
using System.Collections.Generic;
using System.Linq;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        internal IReadOnlyList<string> GetUltimatumModifierPriority()
        {
            EnsureUltimatumModifiersInitialized();

            if (HasMatchingUltimatumSnapshot())
            {
                return _ultimatumPrioritySnapshot;
            }

            _ultimatumPrioritySnapshot = UltimatumModifierPriority.ToArray();
            return _ultimatumPrioritySnapshot;
        }

        internal IReadOnlyCollection<string> GetUltimatumTakeRewardModifierNames()
        {
            EnsureUltimatumTakeRewardModifiersInitialized();
            return UltimatumTakeRewardModifierNames;
        }

        internal bool ShouldTakeRewardForGruelingGauntletModifier(string? modifierName)
        {
            if (UltimatumTakeRewardModifierNames == null || UltimatumContinueModifierNames == null)
                EnsureUltimatumTakeRewardModifiersInitialized();

            HashSet<string> takeRewardSet = UltimatumTakeRewardModifierNames ?? [];

            if (string.IsNullOrWhiteSpace(modifierName))
                return false;

            string normalized = NormalizeUltimatumModifierForMatching(modifierName);
            if (takeRewardSet.Contains(normalized))
                return true;

            if (takeRewardSet.Contains($"{normalized} I"))
                return true;

            if (UltimatumModifierGroupCatalog.TryGetUltimatumModifierBaseName(normalized, out string baseName)
                && takeRewardSet.Contains(baseName))
            {
                return true;
            }

            return false;
        }

        private bool HasMatchingUltimatumSnapshot()
        {
            if (_ultimatumPrioritySnapshot == null)
                return false;
            if (_ultimatumPrioritySnapshot.Length != UltimatumModifierPriority.Count)
                return false;

            for (int i = 0; i < UltimatumModifierPriority.Count; i++)
            {
                if (!string.Equals(_ultimatumPrioritySnapshot[i], UltimatumModifierPriority[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static string NormalizeUltimatumModifierForMatching(string modifierName)
        {
            if (string.IsNullOrWhiteSpace(modifierName))
                return string.Empty;

            string normalized = modifierName.Trim();

            int closeParen = normalized.LastIndexOf(')');
            if (closeParen == normalized.Length - 1)
            {
                int openParen = normalized.LastIndexOf('(');
                if (openParen >= 0 && openParen < closeParen)
                {
                    string inner = normalized[(openParen + 1)..closeParen].Trim();
                    if (!string.IsNullOrWhiteSpace(inner))
                        normalized = inner;
                }
            }

            return normalized;
        }
    }
}