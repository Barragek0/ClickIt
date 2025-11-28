using ClickIt.Constants;

namespace ClickIt.Utils
{
    public static class AltarModMatcher
    {
        // Attempt to match an altar mod to known mod IDs.
        // Returns true if matched and outputs isUpside + matchedId (format: "Type|Id").
        public static bool TryMatchMod(string mod, string negativeModType, out bool isUpside, out string matchedId)
        {
            isUpside = false;
            matchedId = string.Empty;
            if (string.IsNullOrEmpty(mod)) return false;

            string cleanedMod = new(mod.Where(char.IsLetter).ToArray());
            string cleanedNegativeModType = new(negativeModType.Where(char.IsLetter).ToArray());
            string modTarget = GetModTarget(cleanedNegativeModType);

            var searchLists = new[]
            {
                new { List = AltarModsConstants.UpsideMods, IsUpside = true },
                new { List = AltarModsConstants.DownsideMods, IsUpside = false }
            };

            foreach (var searchList in searchLists)
            {
                foreach (var (Id, _, Type, _) in searchList.List)
                {
                    string cleanedId = new(Id.Where(char.IsLetter).ToArray());
                    if (cleanedId.Equals(cleanedMod, StringComparison.OrdinalIgnoreCase) &&
                        Type.Equals(modTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        isUpside = searchList.IsUpside;
                        matchedId = $"{Type}|{Id}";
                        return true;
                    }
                }
            }

            return false;
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
