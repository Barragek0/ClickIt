using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ClickIt.Utils;

namespace ClickIt.Services
{
    public class AltarMatcher
    {
        private readonly Dictionary<string, (bool isUpside, string matchedId)> _modMatchCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _textCleanCache = new(StringComparer.Ordinal);
        private readonly object _modMatchCacheLock = new();
        private readonly object _textCleanCacheLock = new();
        private static readonly Regex RgbRegex = new(@"<rgb\(\d+,\d+,\d+\)>", RegexOptions.Compiled);

        public bool TryMatchModCached(string mod, string negativeModType, out bool isUpside, out string matchedId)
        {
            string cacheKey = $"{mod}|{negativeModType}";

            if (TryGetCachedEntry(cacheKey, out var cached))
            {
                isUpside = cached.isUpside;
                matchedId = cached.matchedId;

                if (!string.IsNullOrEmpty(matchedId) && !matchedId.Contains('|'))
                {
                    string cleanedNegative = new(negativeModType.Where(char.IsLetter).ToArray());
                    string modTarget = AltarModMatcher.GetModTarget(cleanedNegative);
                    if (!string.IsNullOrEmpty(modTarget))
                        matchedId = $"{modTarget}|{matchedId}";
                }

                return !string.IsNullOrEmpty(matchedId);
            }

            bool matched = AltarModMatcher.TryMatchMod(mod, negativeModType, out isUpside, out matchedId);

            lock (_modMatchCacheLock)
            {
                if (_modMatchCache.Count < 5000)
                    _modMatchCache[cacheKey] = (isUpside, matchedId);
            }

            return matched;

            bool TryGetCachedEntry(string key, out (bool isUpside, string matchedId) value)
            {
                value = (false, string.Empty);
                lock (_modMatchCacheLock)
                {
                    return _modMatchCache.TryGetValue(key, out value);
                }
            }
        }

        public string CleanAltarModsText(string text)
        {
            if (text == null) return string.Empty;

            lock (_textCleanCacheLock)
            {
                if (_textCleanCache.TryGetValue(text, out string? cached))
                {
                    return cached;
                }

                string cleaned = text.Replace("<valuedefault>", "").Replace("{", "")
                    .Replace("}", "").Replace("<enchanted>", "").Replace(" ", "")
                    .Replace("gain:", "").Replace("gains:", "");
                cleaned = RgbRegex.Replace(cleaned, "");

                if (_textCleanCache.Count < 1000)
                    _textCleanCache[text] = cleaned;

                return cleaned;
            }
        }
    }
}
