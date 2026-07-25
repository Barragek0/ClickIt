namespace ClickIt.Features.Altars
{
    public static class AltarModMatcher
    {
        private static readonly Dictionary<string, (bool IsUpside, string MatchedId)> ModLookup = BuildModLookup();

        public static bool TryMatchMod(string mod, string negativeModType, out bool isUpside, out string matchedId)
        {
            isUpside = false;
            matchedId = string.Empty;
            if (string.IsNullOrEmpty(mod)) return false;

            string cleanedMod = NormalizeLetters(mod);
            if (cleanedMod.Length == 0)
                return false;

            string cleanedNegativeModType = NormalizeLetters(negativeModType);
            string modTarget = GetModTarget(cleanedNegativeModType);
            if (modTarget.Length == 0)
                return false;

            if (!ModLookup.TryGetValue(BuildLookupKey(cleanedMod, modTarget), out (bool IsUpside, string MatchedId) match))
                return false;

            isUpside = match.IsUpside;
            matchedId = match.MatchedId;
            return true;
        }

        private static Dictionary<string, (bool IsUpside, string MatchedId)> BuildModLookup()
        {
            Dictionary<string, (bool IsUpside, string MatchedId)> lookup = new(StringComparer.OrdinalIgnoreCase);
            AddMods(lookup, AltarModsConstants.UpsideMods, isUpside: true);
            AddMods(lookup, AltarModsConstants.DownsideMods, isUpside: false);
            return lookup;
        }

        private static void AddMods(Dictionary<string, (bool IsUpside, string MatchedId)> lookup, IReadOnlyList<(string Id, string Name, string Type, int DefaultValue)> mods, bool isUpside)
        {
            foreach ((string? id, string _, string? type, int _) in mods)
            {
                string cleanedId = NormalizeLetters(id);
                if (cleanedId.Length == 0 || string.IsNullOrWhiteSpace(type))
                    continue;

                string key = BuildLookupKey(cleanedId, type);
                if (!lookup.ContainsKey(key))
                    lookup[key] = (isUpside, $"{type}|{id}");

            }
        }

        private static string BuildLookupKey(string cleanedId, string type)
        {
            return $"{cleanedId}|{type}";
        }

        public static string NormalizeLetters(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            char[] buffer = new char[value.Length];
            int length = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetter(c))
                    buffer[length++] = c;

            }

            if (length == 0)
                return string.Empty;

            return new string(buffer, 0, length);
        }

        public static string GetModTarget(string cleanedNegativeModType)
        {
            if (cleanedNegativeModType.Contains("Mapboss")) return "Boss";
            if (cleanedNegativeModType.Contains("EldritchMinions")) return "Minion";
            if (cleanedNegativeModType.Contains("Player")) return "Player";
            return string.Empty;
        }
    }
}
