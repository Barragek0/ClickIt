using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
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
        private readonly AltarRepository _altarRepository = new();
        private const string CleansingFireAltar = "CleansingFireAltar";
        private const string TangleAltar = "TangleAltar";

        public AltarServiceDebugInfo DebugInfo { get; private set; } = new();

        private readonly AltarMatcher _altarMatcher = new();

        public List<PrimaryAltarComponent> GetAltarComponents() => _altarRepository.GetAltarComponents();
        public IReadOnlyList<PrimaryAltarComponent> GetAltarComponentsReadOnly() => _altarRepository.GetAltarComponentsReadOnly();
        public void ClearAltarComponents()
        {
            _altarRepository.ClearAltarComponents();
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

            _altarRepository.RemoveAltarComponentsByElement(ShouldRemove);
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
        public bool AddAltarComponent(PrimaryAltarComponent component) => _altarRepository.AddAltarComponent(component);
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
            string altarMods = _altarMatcher.CleanAltarModsText(element.GetText(512));
            int lineCount = TextHelpers.CountLines(altarMods);
            for (int i = 0; i < lineCount; i++)
            {
                string line = TextHelpers.GetLine(altarMods, i);
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
        private (List<string> upsides, List<string> downsides, bool hasUnmatchedMods) ProcessMods(List<string> mods, string negativeModType)
        {
            var (upsides, downsides, unmatched) = AltarParser.ProcessMods(mods, negativeModType, (mod, neg) =>
            {
                if (_altarMatcher.TryMatchModCached(mod, neg, out bool isUpside, out string matchedId))
                    return (true, isUpside, matchedId);
                return (false, false, string.Empty);
            });

            // Update debug counters and record unmatched mods
            DebugInfo.ModsMatched += upsides.Count + downsides.Count;

            // Trigger alert sound for any matched upside mods with alerts enabled
            if (upsides?.Count > 0)
            {
                foreach (var matchedId in upsides)
                {
                    _clickIt.TryTriggerAlertForMatchedMod(matchedId);
                }
            }

            if (unmatched.Count > 0)
            {
                foreach (var mod in unmatched)
                    RecordUnmatchedMod(mod, negativeModType);
                return (upsides ?? new List<string>(), downsides ?? new List<string>(), true);
            }

            return (upsides ?? new List<string>(), downsides ?? new List<string>(), false);
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
            var elementsToProcess = AltarScanner.CollectElementsFromLabels(altarLabels);
            DebugInfo.ElementsFound = elementsToProcess.Count;

            // Remove invalid altars before processing new ones
            CleanupInvalidAltars();

            // Process elements in a single pass
            foreach (var (element, path) in elementsToProcess)
            {
                if (element == null) continue;
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
            _altarRepository.RemoveAltarComponentsByElement(altar =>
            {
                bool isInvalid = altar.TopMods?.Element == null ||
                                altar.BottomMods?.Element == null ||
                                !altar.TopMods.Element.IsValid ||
                                !altar.BottomMods.Element.IsValid;
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
        private static ClickIt.AltarType DetermineAltarType(string path)
        {
            if (string.IsNullOrEmpty(path)) return ClickIt.AltarType.Unknown;

            // Use OrdinalIgnoreCase for robust, culture-insensitive matching
            if (path.Contains(CleansingFireAltar, StringComparison.OrdinalIgnoreCase))
            {
                return ClickIt.AltarType.SearingExarch;
            }

            if (path.Contains(TangleAltar, StringComparison.OrdinalIgnoreCase))
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
    }
}
