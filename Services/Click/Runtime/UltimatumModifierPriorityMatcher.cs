namespace ClickIt.Services.Click.Runtime
{
    internal static class UltimatumModifierPriorityMatcher
    {
        internal static int GetModifierPriorityIndex(string modifierName, IReadOnlyList<string> priorities)
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