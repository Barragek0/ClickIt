namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumModifierPriorityMatcher
    {
        internal static int GetModifierPriorityIndex(string modifierName, IReadOnlyList<string> priorities)
        {
            // Exact and prefix matches are checked across the whole list before the contains fallback, so a longer modifier name that embeds another priority (e.g. "Stalking Ruin" embeds "Ruin", and its "Stalking Ruin I-IV" tiers) still resolves to its own entry instead of the shorter one's.
            int exact = FindFirstMatch(modifierName, priorities, static (name, priority) => name.Equals(priority, StringComparison.OrdinalIgnoreCase));
            if (exact != int.MaxValue)
                return exact;

            int prefix = FindFirstMatch(modifierName, priorities, static (name, priority) => name.StartsWith(priority + " ", StringComparison.OrdinalIgnoreCase));
            if (prefix != int.MaxValue)
                return prefix;

            return FindFirstMatch(modifierName, priorities, static (name, priority) => name.Contains(priority, StringComparison.OrdinalIgnoreCase));
        }

        private static int FindFirstMatch(string modifierName, IReadOnlyList<string> priorities, Func<string, string, bool> matches)
        {
            for (int i = 0; i < priorities.Count; i++)
            {
                string priority = priorities[i];
                if (string.IsNullOrWhiteSpace(priority))
                    continue;

                if (matches(modifierName, priority))
                    return i;
            }

            return int.MaxValue;
        }
    }
}
