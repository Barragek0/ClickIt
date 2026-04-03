namespace ClickIt.Core.Settings.Mechanics
{
    internal sealed record UltimatumModifierGroupEntry(string Id, string DisplayName, string[] Members);

    internal static class UltimatumModifierGroupCatalog
    {
        private static readonly string[] TierSuffixes = [" I", " II", " III", " IV"];

        public static readonly UltimatumModifierGroupEntry[] Groups = BuildGroups();
        public static readonly HashSet<string> TieredModifierNames = BuildTieredModifierNames();

        internal static bool TryGetUltimatumModifierBaseName(string modifierName, out string baseModifierName)
        {
            baseModifierName = string.Empty;
            if (string.IsNullOrWhiteSpace(modifierName))
                return false;

            string trimmed = modifierName.Trim();
            for (int i = 0; i < TierSuffixes.Length; i++)
            {
                string suffix = TierSuffixes[i];
                if (!trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                baseModifierName = trimmed[..^suffix.Length].Trim();
                return baseModifierName.Length > 0;
            }

            return false;
        }

        private static UltimatumModifierGroupEntry[] BuildGroups()
        {
            Dictionary<string, List<string>> membersByBase = new(StringComparer.OrdinalIgnoreCase);
            List<string> groupOrder = [];

            for (int i = 0; i < UltimatumModifiersConstants.AllModifierNamesWithStages.Length; i++)
            {
                string modifier = UltimatumModifiersConstants.AllModifierNamesWithStages[i];
                if (!TryGetUltimatumModifierBaseName(modifier, out string baseName))
                    continue;

                if (!membersByBase.TryGetValue(baseName, out List<string>? members))
                {
                    members = [];
                    membersByBase[baseName] = members;
                    groupOrder.Add(baseName);
                }

                members.Add(modifier);
            }

            List<UltimatumModifierGroupEntry> groups = new(groupOrder.Count);
            for (int i = 0; i < groupOrder.Count; i++)
            {
                string baseName = groupOrder[i];
                List<string> members = membersByBase[baseName];
                if (members.Count == 0)
                    continue;

                groups.Add(new UltimatumModifierGroupEntry(baseName, baseName, [.. members.Distinct(StringComparer.OrdinalIgnoreCase)]));
            }

            return [.. groups];
        }

        private static HashSet<string> BuildTieredModifierNames()
        {
            HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Groups.Length; i++)
            {
                result.Add(Groups[i].Id);

                string[] members = Groups[i].Members;
                for (int j = 0; j < members.Length; j++)
                {
                    result.Add(members[j]);
                }
            }

            return result;
        }

    }
}