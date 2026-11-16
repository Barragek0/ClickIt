namespace ClickIt.Features.Altars
{
    public static class AltarModMatcher
    {
        private static readonly Dictionary<string, List<(bool IsUpside, string MatchedId)>> ModLookup = BuildModLookup();

        public static bool TryMatchMod(string mod, string negativeModType, out bool isUpside, out string matchedId)
        {
            isUpside = false;
            matchedId = string.Empty;
            if (string.IsNullOrEmpty(mod)) return false;

            string cleanedNegativeModType = NormalizeLetters(negativeModType);
            string modTarget = GetModTarget(cleanedNegativeModType);
            if (modTarget.Length == 0)
                return false;

            List<(bool IsUpside, string MatchedId)>? matches = null;
            foreach (string cleanedMod in ExpandBracketAlternatives(mod)
                         .Select(NormalizeLetters)
                         .Where(candidate => candidate.Length > 0)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (ModLookup.TryGetValue(BuildLookupKey(cleanedMod, modTarget), out matches))
                    break;
            }

            if (matches is null)
                return false;

            (bool IsUpside, string MatchedId) match = SelectMatch(mod, matches);
            isUpside = match.IsUpside;
            matchedId = match.MatchedId;
            return true;
        }

        private static IEnumerable<string> ExpandBracketAlternatives(string value)
        {
            Match alternativeMatch = Regex.Match(value, @"\[(?<alternatives>[^\[\]]*\|[^\[\]]*)\]");
            if (!alternativeMatch.Success)
            {
                yield return value;
                yield break;
            }

            string prefix = value[..alternativeMatch.Index];
            string suffix = value[(alternativeMatch.Index + alternativeMatch.Length)..];
            string[] alternatives = alternativeMatch.Groups["alternatives"].Value
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string alternative in alternatives)
            {
                foreach (string expanded in ExpandBracketAlternatives(prefix + alternative + suffix))
                    yield return expanded;
            }
        }

        private static Dictionary<string, List<(bool IsUpside, string MatchedId)>> BuildModLookup()
        {
            Dictionary<string, List<(bool IsUpside, string MatchedId)>> lookup = new(StringComparer.OrdinalIgnoreCase);
            AddMods(lookup, AltarModsConstants.UpsideMods, isUpside: true);
            AddMods(lookup, AltarModsConstants.DownsideMods, isUpside: false);
            return lookup;
        }

        private static void AddMods(Dictionary<string, List<(bool IsUpside, string MatchedId)>> lookup, IReadOnlyList<(string Id, string Name, string Type, int DefaultValue)> mods, bool isUpside)
        {
            foreach ((string? id, string _, string? type, int _) in mods)
            {
                string cleanedId = NormalizeLetters(id);
                if (cleanedId.Length == 0 || string.IsNullOrWhiteSpace(type))
                    continue;

                string key = BuildLookupKey(cleanedId, type);
                if (!lookup.TryGetValue(key, out List<(bool IsUpside, string MatchedId)>? matches))
                    lookup[key] = matches = [];
                matches.Add((isUpside, $"{type}|{id}"));
            }
        }

        private static (bool IsUpside, string MatchedId) SelectMatch(
            string mod,
            List<(bool IsUpside, string MatchedId)> matches)
        {
            if (matches.Count <= 1)
                return matches[0];

            Match valueMatch = Regex.Match(mod, @"(?<!\d)(?<value>\d+(?:\.\d+)?)\s*%");
            if (!valueMatch.Success
                || !decimal.TryParse(valueMatch.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                return matches[0];

            List<(bool IsUpside, string MatchedId, decimal Min, decimal Max)> rangedMatches = [];
            foreach ((bool isUpside, string matchedId) in matches)
            {
                int separatorIndex = matchedId.IndexOf('|');
                string id = separatorIndex >= 0 ? matchedId[(separatorIndex + 1)..] : matchedId;
                Match rangeMatch = Regex.Match(id, @"\((?<min>\d+(?:\.\d+)?)-(?<max>\d+(?:\.\d+)?)\)\s*$");
                if (!rangeMatch.Success
                    || !decimal.TryParse(rangeMatch.Groups["min"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal min)
                    || !decimal.TryParse(rangeMatch.Groups["max"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal max))
                    continue;

                rangedMatches.Add((isUpside, matchedId, min, max));
            }

            List<(bool IsUpside, string MatchedId, decimal Min, decimal Max)> containingRanges = rangedMatches
                .Where(candidate => value >= candidate.Min && value < candidate.Max)
                .ToList();
            if (containingRanges.Count == 1)
                return (containingRanges[0].IsUpside, containingRanges[0].MatchedId);

            if (containingRanges.Count == 0)
            {
                List<(bool IsUpside, string MatchedId, decimal Min, decimal Max)> boundaryMatches = rangedMatches
                    .Where(candidate => value == candidate.Max)
                    .ToList();
                if (boundaryMatches.Count == 1)
                    return (boundaryMatches[0].IsUpside, boundaryMatches[0].MatchedId);
            }

            return matches[0];
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
            if (cleanedNegativeModType.Contains("Mapboss", StringComparison.Ordinal)) return "Boss";
            if (cleanedNegativeModType.Contains("EldritchMinions", StringComparison.Ordinal)) return "Minion";
            if (cleanedNegativeModType.Contains("Player", StringComparison.Ordinal)) return "Player";
            return string.Empty;
        }
    }
}
