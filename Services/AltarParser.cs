
using ClickIt.Utils;
using System.Text.RegularExpressions;

namespace ClickIt.Services
{
    public static class AltarParser
    {
        private static readonly Regex RgbRegex = new(@"<[^>]*>", RegexOptions.Compiled);

        // Stateless cleaning logic — does not include any caching.
        public static string CleanAltarModsText_NoCache(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string cleaned = text.Replace("<valuedefault>", "").Replace("{", "")
                .Replace("}", "").Replace("<enchanted>", "").Replace(" ", "")
                .Replace("gain:", "").Replace("gains:", "");

            cleaned = RgbRegex.Replace(cleaned, "");
            return cleaned;
        }

        public static (string negativeModType, List<string> mods) ExtractModsFromText(string text)
        {
            string negativeModType = string.Empty;
            var mods = new List<string>();

            string cleaned = CleanAltarModsText_NoCache(text);
            int lines = TextHelpers.CountLines(cleaned);
            for (int i = 0; i < lines; i++)
            {
                string line = TextHelpers.GetLine(cleaned, i);
                if (i == 0)
                    negativeModType = line;
                else if (!string.IsNullOrEmpty(line))
                    mods.Add(line);
            }

            return (negativeModType, mods);
        }

        // Process a list of mod strings using a supplied matcher delegate.
        // matcher(mod, negativeModType) -> (matched, isUpside, matchedId)
        public static (List<string> upsides, List<string> downsides, List<string> unmatched) ProcessMods(
            List<string> mods,
            string negativeModType,
            Func<string, string, (bool matched, bool isUpside, string matchedId)> matcher)
        {
            var upsides = new List<string>();
            var downsides = new List<string>();
            var unmatched = new List<string>();

            if (mods == null || mods.Count == 0) return (upsides, downsides, unmatched);

            foreach (var mod in mods)
            {
                var (matched, isUpside, matchedId) = matcher(mod, negativeModType);
                if (!matched)
                {
                    unmatched.Add(mod);
                    continue;
                }
                if (isUpside) upsides.Add(matchedId);
                else downsides.Add(matchedId);
            }

            return (upsides, downsides, unmatched);
        }
    }
}
