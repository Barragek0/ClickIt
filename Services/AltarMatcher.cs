using ClickIt.Utils;

namespace ClickIt.Services
{
    public class AltarMatcher
    {
        private readonly Dictionary<string, (bool isUpside, string matchedId)> _modMatchCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _textCleanCache = new(StringComparer.Ordinal);
        private readonly object _modMatchCacheLock = new();
        private readonly object _textCleanCacheLock = new();

        public bool TryMatchModCached(string mod, string negativeModType, out bool isUpside, out string matchedId)
        {
            string cacheKey = $"{mod}|{negativeModType}";

            if (TryGetCachedEntry(cacheKey, out var cached))
            {
                isUpside = cached.isUpside;
                matchedId = cached.matchedId;

                if (!string.IsNullOrEmpty(matchedId) && !matchedId.Contains('|'))
                {
                    string cleanedNegative = AltarModMatcher.NormalizeLetters(negativeModType);
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

                string cleaned = AltarParser.CleanAltarModsText_NoCache(text);

                if (_textCleanCache.Count < 1000)
                    _textCleanCache[text] = cleaned;

                return cleaned;
            }
        }

        public void ClearCaches()
        {
            lock (_modMatchCacheLock)
            {
                _modMatchCache.Clear();
            }

            lock (_textCleanCacheLock)
            {
                _textCleanCache.Clear();
            }
        }

        internal void SeedModMatchCacheEntry(string mod, string negativeModType, bool isUpside, string matchedId)
        {
            string cacheKey = $"{mod}|{negativeModType}";
            lock (_modMatchCacheLock)
            {
                _modMatchCache[cacheKey] = (isUpside, matchedId);
            }
        }

        internal bool HasCleanedTextCacheEntry(string text)
        {
            lock (_textCleanCacheLock)
            {
                return _textCleanCache.ContainsKey(text);
            }
        }

        internal int GetModMatchCacheCount()
        {
            lock (_modMatchCacheLock)
            {
                return _modMatchCache.Count;
            }
        }

        internal int GetTextCleanCacheCount()
        {
            lock (_textCleanCacheLock)
            {
                return _textCleanCache.Count;
            }
        }
    }
}

