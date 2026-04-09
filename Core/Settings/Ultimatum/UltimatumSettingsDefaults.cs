namespace ClickIt
{
    public partial class ClickItSettings
    {
        internal void ResetUltimatumModifierPriorityDefaults()
        {
            UltimatumModifierPriority = [.. UltimatumModifiersConstants.AllModifierNames];
        }

        internal void ResetUltimatumTakeRewardModifierDefaults()
        {
            UltimatumTakeRewardModifierNames.Clear();
            UltimatumContinueModifierNames = new HashSet<string>(UltimatumModifiersConstants.AllModifierNamesWithStages, StringComparer.OrdinalIgnoreCase);
        }

        internal void EnsureUltimatumModifiersInitialized()
        {
            UltimatumModifierPriority ??= [];

            if (UltimatumModifierPriority.Count == 0)
            {
                UltimatumModifierPriority = [.. UltimatumModifiersConstants.AllModifierNames];
                return;
            }

            HashSet<string> valid = new(UltimatumModifiersConstants.AllModifierNames, StringComparer.OrdinalIgnoreCase);
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            List<string> sanitized = new(UltimatumModifierPriority.Count);
            foreach (string modifier in UltimatumModifierPriority)
            {
                if (string.IsNullOrWhiteSpace(modifier))
                    continue;
                if (!valid.Contains(modifier))
                    continue;
                if (!seen.Add(modifier))
                    continue;

                sanitized.Add(modifier);
            }

            foreach (string modifier in UltimatumModifiersConstants.AllModifierNames)
                if (seen.Add(modifier))
                    sanitized.Add(modifier);


            UltimatumModifierPriority = sanitized;
        }

        internal void EnsureUltimatumTakeRewardModifiersInitialized()
        {
            UltimatumTakeRewardModifierNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            UltimatumContinueModifierNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (UltimatumTakeRewardModifierNames.Count == 0 && UltimatumContinueModifierNames.Count == 0)
            {
                UltimatumContinueModifierNames = new HashSet<string>(UltimatumModifiersConstants.AllModifierNamesWithStages, StringComparer.OrdinalIgnoreCase);
                return;
            }

            HashSet<string> allowed = new(UltimatumModifiersConstants.AllModifierNamesWithStages, StringComparer.OrdinalIgnoreCase);
            SettingsDefaultsService.SanitizeMutuallyExclusiveSets(
                UltimatumTakeRewardModifierNames,
                UltimatumContinueModifierNames,
                allowed,
                UltimatumModifiersConstants.AllModifierNamesWithStages);
        }
    }
}