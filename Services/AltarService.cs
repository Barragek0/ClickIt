using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClickIt.Constants;
using ClickIt.Components;
using ClickIt.Utils;
#nullable enable
namespace ClickIt.Services
{
    public class AltarServiceDebugInfo
    {
        public int LastScanExarchLabels { get; set; } = 0;
        public int LastScanEaterLabels { get; set; } = 0;
        public int ElementsFound { get; set; } = 0;
        public int ComponentsProcessed { get; set; } = 0;
        public int ComponentsAdded { get; set; } = 0;
        public int ComponentsDuplicated { get; set; } = 0;
        public int ModsMatched { get; set; } = 0;
        public int ModsUnmatched { get; set; } = 0;
        public string LastProcessedAltarType { get; set; } = "";
        public string LastError { get; set; } = "";
        public DateTime LastScanTime { get; set; } = DateTime.MinValue;
        public List<string> RecentUnmatchedMods { get; set; } = [];
    }

    public class AltarService(ClickIt clickIt, ClickItSettings settings, TimeCache<List<LabelOnGround>>? cachedLabels)
    {
        private readonly ClickIt _clickIt = clickIt;
        private readonly ClickItSettings _settings = settings;
        private readonly TimeCache<List<LabelOnGround>>? _cachedLabels = cachedLabels;
        private readonly List<PrimaryAltarComponent> _altarComponents = [];
        private const string CleansingFireAltar = "CleansingFireAltar";
        private const string TangleAltar = "TangleAltar";

        public AltarServiceDebugInfo DebugInfo { get; private set; } = new();

        private readonly Dictionary<string, (bool isUpside, string matchedId)> _modMatchCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _textCleanCache = new(StringComparer.Ordinal);

        // Thread safety locks for cache dictionaries and shared collections
        private readonly object _modMatchCacheLock = new();
        private readonly object _textCleanCacheLock = new();
        private readonly object _altarComponentsLock = new();

        private static readonly Regex RgbRegex = new(
            @"<rgb\(\d+,\d+,\d+\)>",
            RegexOptions.Compiled);

        public List<PrimaryAltarComponent> GetAltarComponents() => _altarComponents.ToList();
        public IReadOnlyList<PrimaryAltarComponent> GetAltarComponentsReadOnly() => _altarComponents;
        public void ClearAltarComponents()
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (LockManager.Acquire(_altarComponentsLock))
                {
                    foreach (var component in _altarComponents)
                    {
                        component.InvalidateCache();
                    }
                    _altarComponents.Clear();
                }
            }
            else
            {
                foreach (var component in _altarComponents)
                {
                    component.InvalidateCache();
                }
                _altarComponents.Clear();
            }
        }
        public void RemoveAltarComponentsByElement(Element element)
        {
            if (element == null) return;

            // Local predicate to determine if an altar references the provided element
            static bool MatchesElement(PrimaryAltarComponent altar, Element element)
            {
                try
                {
                    return ReferenceEquals(altar.TopButton?.Element, element) ||
                           ReferenceEquals(altar.BottomButton?.Element, element) ||
                           ReferenceEquals(altar.TopMods?.Element, element) ||
                           ReferenceEquals(altar.BottomMods?.Element, element) ||
                           ReferenceEquals(altar.TopButton?.Element?.Parent, element) ||
                           ReferenceEquals(altar.BottomButton?.Element?.Parent, element);
                }
                catch
                {
                    return false;
                }
            }

            // Wrapper used by RemoveAll so we can invalidate cache on matched items
            bool ShouldRemove(PrimaryAltarComponent altar)
            {
                bool match = MatchesElement(altar, element);
                if (match)
                {
                    altar.InvalidateCache();
                }
                return match;
            }

            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (LockManager.Acquire(_altarComponentsLock))
                {
                    _altarComponents.RemoveAll(ShouldRemove);
                }
            }
            else
            {
                _altarComponents.RemoveAll(ShouldRemove);
            }
        }
        public List<LabelOnGround> GetAltarLabels(ClickIt.AltarType type)
        {
            List<LabelOnGround> result = [];
            List<LabelOnGround>? labelsFromCache = _cachedLabels?.Value;
            if (labelsFromCache == null)
                return result;
            string typeStr = type == ClickIt.AltarType.SearingExarch ? CleansingFireAltar : TangleAltar;
            for (int i = 0; i < labelsFromCache.Count; i++)
            {
                LabelOnGround label = labelsFromCache[i];
                if (label.ItemOnGround?.Path == null || !label.Label.IsVisible)
                    continue;
                if (label.ItemOnGround.Path.Contains(typeStr))
                    result.Add(label);
            }
            return result;
        }
        public bool AddAltarComponent(PrimaryAltarComponent component)
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (LockManager.Acquire(_altarComponentsLock))
                {
                    string newKey = BuildAltarKey(component);
                    bool exists = _altarComponents.Any(existingComp => BuildAltarKey(existingComp) == newKey);
                    if (!exists)
                    {
                        _altarComponents.Add(component);
                        return true;
                    }
                    return false;
                }
            }
            else
            {
                string newKey = BuildAltarKey(component);
                bool exists = _altarComponents.Any(existingComp => BuildAltarKey(existingComp) == newKey);
                if (!exists)
                {
                    _altarComponents.Add(component);
                    return true;
                }
                return false;
            }
        }
        public void UpdateComponentFromElementData(bool top, Element altarParent, PrimaryAltarComponent altarComponent,
            Element ElementToExtractDataFrom, ClickIt.AltarType altarType)
        {
            var (negativeModType, mods) = ExtractModsFromElement(ElementToExtractDataFrom);
            var (upsides, downsides, hasUnmatchedMods) = ProcessMods(mods, negativeModType);
            UpdateAltarComponent(top, altarComponent, ElementToExtractDataFrom, upsides, downsides, hasUnmatchedMods);
        }
        private (string negativeModType, List<string> mods) ExtractModsFromElement(Element element)
        {
            string negativeModType = "";
            List<string> mods = [];
            string altarMods = CleanAltarModsText(element.GetText(512));
            int lineCount = CountLines(element.GetText(512));
            for (int i = 0; i < lineCount; i++)
            {
                string line = GetLine(altarMods, i);
                if (i == 0)
                {
                    negativeModType = line;
                }
                else if (line != null)
                {
                    mods.Add(line);
                }
            }
            return (negativeModType, mods);
        }
        private string CleanAltarModsText(string text)
        {
            var gm = LockManager.Instance;
            if (gm != null)
            {
                using (LockManager.Acquire(_textCleanCacheLock))
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
                    {
                        _textCleanCache[text] = cleaned;
                    }

                    return cleaned;
                }
            }
            else
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
                {
                    _textCleanCache[text] = cleaned;
                }

                return cleaned;
            }
        }
        private (List<string> upsides, List<string> downsides, bool hasUnmatchedMods) ProcessMods(List<string> mods, string negativeModType)
        {
            List<string> upsides = [];
            List<string> downsides = [];
            bool hasUnmatchedMods = false;

            foreach (string mod in mods)
            {
                if (!TryMatchModCached(mod, negativeModType, out bool isUpside, out string matchedId))
                {
                    hasUnmatchedMods = true;
                    RecordUnmatchedMod(mod, negativeModType);
                    continue;
                }

                // Matched
                DebugInfo.ModsMatched++;
                if (isUpside)
                    upsides.Add(matchedId);
                else
                    downsides.Add(matchedId);
            }

            return (upsides, downsides, hasUnmatchedMods);
        }

        // Records unmatched mod info into debug structures and logs when in debug mode.
        private void RecordUnmatchedMod(string mod, string negativeModType)
        {
            DebugInfo.ModsUnmatched++;
            string cleanedMod = new(mod.Where(char.IsLetter).ToArray());
            string unmatchedInfo = $"{cleanedMod} ({negativeModType})";
            if (!DebugInfo.RecentUnmatchedMods.Contains(unmatchedInfo))
            {
                DebugInfo.RecentUnmatchedMods.Add(unmatchedInfo);
                if (DebugInfo.RecentUnmatchedMods.Count > 5)
                    DebugInfo.RecentUnmatchedMods.RemoveAt(0);
            }

            if (_settings.DebugMode)
            {
                _clickIt.LogError($"Failed to match mod: '{mod}' (Cleaned: '{cleanedMod}') with NegativeModType: '{negativeModType}'", 10);
            }
        }

        private bool TryMatchModCached(string mod, string negativeModType, out bool isUpside, out string matchedId)
        {
            string cacheKey = $"{mod}|{negativeModType}";

            var gm = LockManager.Instance;

            // Try to read from cache (with lock when available).
            if (TryGetCachedEntry(gm, cacheKey, out var cached))
            {
                isUpside = cached.isUpside;
                matchedId = cached.matchedId;

                // Normalize legacy cached matchedId (id-only) to composite "Type|Id" using negativeModType
                if (!string.IsNullOrEmpty(matchedId) && !matchedId.Contains('|'))
                {
                    string cleanedNegative = new(negativeModType.Where(char.IsLetter).ToArray());
                    string modTarget = GetModTarget(cleanedNegative);
                    if (!string.IsNullOrEmpty(modTarget))
                        matchedId = $"{modTarget}|{matchedId}";
                }

                return !string.IsNullOrEmpty(matchedId);
            }

            // Not cached - perform match and insert result into cache if possible.
            bool matched = TryMatchMod(mod, negativeModType, out isUpside, out matchedId);

            // Attempt to add to cache (respecting lock if present)
            if (gm != null)
            {
                using (LockManager.Acquire(_modMatchCacheLock))
                {
                    if (_modMatchCache.Count < 5000)
                        _modMatchCache[cacheKey] = (isUpside, matchedId);
                }
            }
            else
            {
                if (_modMatchCache.Count < 5000)
                    _modMatchCache[cacheKey] = (isUpside, matchedId);
            }

            return matched;

            // Local helper to unify cache reads with and without locking
            bool TryGetCachedEntry(object? lockManager, string key, out (bool isUpside, string matchedId) value)
            {
                value = (false, string.Empty);
                if (lockManager != null)
                {
                    using (LockManager.Acquire(_modMatchCacheLock))
                    {
                        return _modMatchCache.TryGetValue(key, out value);
                    }
                }

                return _modMatchCache.TryGetValue(key, out value);
            }
        }

        private static bool TryMatchMod(string mod, string negativeModType, out bool isUpside, out string matchedId)
        {
            isUpside = false;
            matchedId = string.Empty;
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
        private static string GetModTarget(string cleanedNegativeModType)
        {
            if (cleanedNegativeModType.Contains("Mapboss")) return "Boss";
            if (cleanedNegativeModType.Contains("EldritchMinions")) return "Minion";
            if (cleanedNegativeModType.Contains("Player")) return "Player";
            return "";
        }
        private static void UpdateAltarComponent(bool top, PrimaryAltarComponent altarComponent, Element element,
            List<string> upsides, List<string> downsides, bool hasUnmatchedMods)
        {
            if (top)
            {
                altarComponent.TopButton = new AltarButton(element.Parent);
                altarComponent.TopMods = new SecondaryAltarComponent(element, upsides, downsides, hasUnmatchedMods);
            }
            else
            {
                altarComponent.BottomButton = new AltarButton(element.Parent);
                altarComponent.BottomMods = new SecondaryAltarComponent(element, upsides, downsides, hasUnmatchedMods);
            }
        }
        private static string GetLine(string text, int lineNo)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            if (lineNo >= 0 && lineNo < lines.Length)
                return lines[lineNo];
            return string.Empty;
        }
        private static int CountLines(string text)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            return lines.Length;
        }
        public void ProcessAltarScanningLogic()
        {
            DebugInfo.LastScanTime = DateTime.Now;
            DebugInfo.ElementsFound = 0;
            DebugInfo.ComponentsProcessed = 0;
            DebugInfo.ComponentsAdded = 0;
            DebugInfo.ComponentsDuplicated = 0;
            DebugInfo.ModsMatched = 0;
            DebugInfo.ModsUnmatched = 0;
            DebugInfo.RecentUnmatchedMods.Clear();
            List<LabelOnGround> altarLabels = CollectAltarLabels();
            if (altarLabels.Count == 0)
            {
                ClearAltarComponents();
                return;
            }
            ProcessAltarLabels(altarLabels);
        }
        private List<LabelOnGround> CollectAltarLabels()
        {
            List<LabelOnGround> altarLabels = [];
            if (_settings.HighlightExarchAltars)
            {
                List<LabelOnGround> exarchLabels = GetAltarLabels(ClickIt.AltarType.SearingExarch);
                DebugInfo.LastScanExarchLabels = exarchLabels.Count;
                if (exarchLabels.Count > 0)
                {
                    altarLabels.AddRange(exarchLabels);
                }
            }
            if (_settings.HighlightEaterAltars)
            {
                List<LabelOnGround> eaterLabels = GetAltarLabels(ClickIt.AltarType.EaterOfWorlds);
                DebugInfo.LastScanEaterLabels = eaterLabels.Count;
                if (eaterLabels.Count > 0)
                {
                    altarLabels.AddRange(eaterLabels);
                }
            }
            return altarLabels;
        }
        private void ProcessAltarLabels(List<LabelOnGround> altarLabels)
        {
            var elementsToProcess = new List<(Element element, string path)>(altarLabels.Count * 2);

            foreach (LabelOnGround label in altarLabels)
            {
                if (label == null) continue;
                List<Element> elements = LabelUtils.GetElementsByStringContains(label.Label, "valuedefault");
                if (elements == null || elements.Count == 0) continue;
                string path = label.ItemOnGround?.Path ?? string.Empty;
                DebugInfo.ElementsFound += elements.Count;

                foreach (Element element in elements.Where(IsValidElement))
                {
                    elementsToProcess.Add((element, path));
                }
            }

            // Remove invalid altars before processing new ones
            CleanupInvalidAltars();

            // Process elements in a single pass
            foreach (var (element, path) in elementsToProcess)
            {
                DebugInfo.LastProcessedAltarType = DetermineAltarType(path).ToString();
                ClickIt.AltarType altarType = DetermineAltarType(path);
                PrimaryAltarComponent altarComponent = CreateAltarComponent(element, altarType);
                DebugInfo.ComponentsProcessed++;

                if (IsValidAltarComponent(altarComponent))
                {
                    PreCacheAltarData(altarComponent);

                    bool wasAdded = AddAltarComponent(altarComponent);
                    if (wasAdded)
                        DebugInfo.ComponentsAdded++;
                    else
                        DebugInfo.ComponentsDuplicated++;
                }
            }
        }

        private void CleanupInvalidAltars()
        {
            _altarComponents.RemoveAll(altar =>
            {
                bool isInvalid = altar.TopMods?.Element == null ||
                                altar.BottomMods?.Element == null ||
                                !altar.TopMods.Element.IsValid ||
                                !altar.BottomMods.Element.IsValid;

                if (isInvalid)
                {
                    // Invalidate cache when removing
                    altar.InvalidateCache();
                }

                return isInvalid;
            });
        }

        private static void PreCacheAltarData(PrimaryAltarComponent altar)
        {
            // Pre-calculate and cache all data that render loop and click logic will need
            // Trigger caching of validation state
            _ = altar.IsValidCached();

            // Pre-calculate rectangles (no longer cached)
            _ = altar.GetTopModsRect();
            _ = altar.GetBottomModsRect();

            // Note: Weights are cached lazily when first accessed since they need the calculator
        }
        private bool IsValidElement(Element element)
        {
            if (element == null || !element.IsVisible)
            {
                DebugInfo.LastError = "Element is null or not visible";
                return false;
            }
            return true;
        }
        private static ClickIt.AltarType DetermineAltarType(string path)
        {
            if (string.IsNullOrEmpty(path)) return ClickIt.AltarType.Unknown;

            // Use OrdinalIgnoreCase for robust, culture-insensitive matching
            if (path.IndexOf(CleansingFireAltar, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ClickIt.AltarType.SearingExarch;
            }

            if (path.IndexOf(TangleAltar, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ClickIt.AltarType.EaterOfWorlds;
            }

            return ClickIt.AltarType.Unknown;
        }
        private PrimaryAltarComponent CreateAltarComponent(Element element, ClickIt.AltarType altarType)
        {
            Element altarParent = element.Parent.Parent;
            Element? topAltarElement = altarParent.GetChildFromIndices(0, 1);
            Element? bottomAltarElement = altarParent.GetChildFromIndices(1, 1);

            // Create altar component with proper validation
            var topMods = topAltarElement != null ?
                new SecondaryAltarComponent(topAltarElement, [], []) :
                null;
            var bottomMods = bottomAltarElement != null ?
                new SecondaryAltarComponent(bottomAltarElement, [], []) :
                null;
            var topButton = topAltarElement != null ? new AltarButton(topAltarElement.Parent) : null;
            var bottomButton = bottomAltarElement != null ? new AltarButton(bottomAltarElement.Parent) : null;

            if (topMods == null || bottomMods == null || topButton == null || bottomButton == null)
            {
                throw new InvalidOperationException("Failed to create valid altar component - missing required elements");
            }

            PrimaryAltarComponent altarComponent = new(altarType, topMods, topButton, bottomMods, bottomButton);

            if (topAltarElement != null)
            {
                UpdateComponentFromElementData(true, altarParent, altarComponent, topAltarElement, altarType);
            }
            if (bottomAltarElement != null)
            {
                UpdateComponentFromElementData(false, altarParent, altarComponent, bottomAltarElement, altarType);
            }
            return altarComponent;
        }
        private bool IsValidAltarComponent(PrimaryAltarComponent altarComponent)
        {
            bool isValid = altarComponent.TopMods != null && altarComponent.TopButton != null &&
                          altarComponent.BottomMods != null && altarComponent.BottomButton != null;
            if (!isValid)
            {
                DebugInfo.LastError = "Invalid altar component - missing parts";
            }
            return isValid;
        }
        private static string BuildAltarKey(PrimaryAltarComponent comp)
        {
            // Support for 8 mods on top and bottom - build key from all available mods
            // Much cleaner approach using array concatenation
            var topUpside = GetModStrings(comp.TopMods, false);
            var topDownside = GetModStrings(comp.TopMods, true);
            var bottomUpside = GetModStrings(comp.BottomMods, false);
            var bottomDownside = GetModStrings(comp.BottomMods, true);

            // Join all mods with separator to create unique key
            var allMods = topUpside.Concat(topDownside)
                                  .Concat(bottomUpside)
                                  .Concat(bottomDownside);

            return string.Join("|", allMods);
        }

        // Helper method to extract mod strings for key building
        private static string[] GetModStrings(SecondaryAltarComponent? component, bool isDownside)
        {
            if (component == null)
            {
                return new string[8]; // Return empty array if no component
            }

            if (isDownside)
            {
                // Get all 8 downside mods (supporting up to 8 mods)
                var downsideMods = new string[8];
                for (int i = 0; i < 8; i++)
                {
                    downsideMods[i] = component.GetDownsideByIndex(i);
                }
                return downsideMods;
            }
            else
            {
                // Get all 8 upside mods (supporting up to 8 mods)
                var upsideMods = new string[8];
                for (int i = 0; i < 8; i++)
                {
                    upsideMods[i] = component.GetUpsideByIndex(i);
                }
                return upsideMods;
            }
        }
    }
}
