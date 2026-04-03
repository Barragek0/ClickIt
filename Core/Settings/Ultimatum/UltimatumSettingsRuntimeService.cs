namespace ClickIt.Core.Settings.Ultimatum
{
    internal static class UltimatumSettingsRuntimeService
    {
        internal static IReadOnlyList<string> GetModifierPriority(ClickItSettings settings)
        {
            settings.EnsureUltimatumModifiersInitialized();
            ClickItSettingsRuntimeCacheState runtimeCache = settings.TransientState.RuntimeCache;

            if (HasMatchingPrioritySnapshot(settings))
            {
                return runtimeCache.UltimatumPrioritySnapshot;
            }

            runtimeCache.UltimatumPrioritySnapshot = settings.UltimatumModifierPriority.ToArray();
            return runtimeCache.UltimatumPrioritySnapshot;
        }

        internal static IReadOnlyCollection<string> GetTakeRewardModifierNames(ClickItSettings settings)
        {
            settings.EnsureUltimatumTakeRewardModifiersInitialized();
            return settings.UltimatumTakeRewardModifierNames;
        }

        internal static bool ShouldTakeRewardForGruelingGauntletModifier(ClickItSettings settings, string? modifierName)
        {
            if (settings.UltimatumTakeRewardModifierNames == null || settings.UltimatumContinueModifierNames == null)
                settings.EnsureUltimatumTakeRewardModifiersInitialized();

            HashSet<string> takeRewardSet = settings.UltimatumTakeRewardModifierNames ?? [];

            if (string.IsNullOrWhiteSpace(modifierName))
                return false;

            string normalized = NormalizeModifierForMatching(modifierName);
            if (takeRewardSet.Contains(normalized))
                return true;

            if (takeRewardSet.Contains($"{normalized} I"))
                return true;

            return UltimatumModifierGroupCatalog.TryGetUltimatumModifierBaseName(normalized, out string baseName)
                && takeRewardSet.Contains(baseName);
        }

        internal static string NormalizeModifierForMatching(string modifierName)
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

        private static bool HasMatchingPrioritySnapshot(ClickItSettings settings)
        {
            string[] snapshot = settings.TransientState.RuntimeCache.UltimatumPrioritySnapshot;
            if (snapshot.Length != settings.UltimatumModifierPriority.Count)
                return false;

            for (int i = 0; i < settings.UltimatumModifierPriority.Count; i++)
            {
                if (!string.Equals(snapshot[i], settings.UltimatumModifierPriority[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }
}