namespace ClickIt.Features.Altars
{
    public static class AltarParser
    {
        private static readonly Regex RgbRegex = new(@"<[^>]*>", RegexOptions.Compiled);

        public static string CleanAltarModsTextNoCache(string text)
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
            List<string> mods = [];

            string cleaned = CleanAltarModsTextNoCache(text);
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

        public static (List<string> upsides, List<string> downsides, List<string> unmatched) ProcessMods(
            List<string> mods,
            string negativeModType,
            Func<string, string, (bool matched, bool isUpside, string matchedId)> matcher)
        {
            List<string> upsides = [];
            List<string> downsides = [];
            List<string> unmatched = [];

            if (mods == null || mods.Count == 0) return (upsides, downsides, unmatched);

            foreach (string mod in mods)
            {
                (bool matched, bool isUpside, string? matchedId) = matcher(mod, negativeModType);
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
