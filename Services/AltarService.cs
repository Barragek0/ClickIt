using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
using ClickIt.Components;
using ClickIt.Definitions;
using ClickIt.Utils;
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

    public partial class AltarService(ClickIt clickIt, ClickItSettings settings, TimeCache<List<LabelOnGround>>? cachedLabels)
    {
        private const int AltarModsTextReadLength = 4096;

        private readonly ClickIt _clickIt = clickIt;
        private readonly ClickItSettings _settings = settings;
        private readonly TimeCache<List<LabelOnGround>>? _cachedLabels = cachedLabels;
        private readonly AltarRepository _altarRepository = new();

        public AltarServiceDebugInfo DebugInfo { get; private set; } = new();

        private readonly AltarMatcher _altarMatcher = new();

        public List<PrimaryAltarComponent> GetAltarComponents() => _altarRepository.GetAltarComponents();
        public IReadOnlyList<PrimaryAltarComponent> GetAltarComponentsReadOnly() => _altarRepository.GetAltarComponentsReadOnly();
        public int GetAltarComponentCount() => _altarRepository.GetAltarComponentCount();
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
        public List<LabelOnGround> GetAltarLabels(AltarType type)
        {
            List<LabelOnGround> result = [];
            List<LabelOnGround>? labelsFromCache = _cachedLabels?.Value;
            if (labelsFromCache == null)
                return result;
            string typeStr = type == AltarType.SearingExarch
                ? Constants.CleansingFireAltar
                : Constants.TangleAltar;
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
            Element ElementToExtractDataFrom, AltarType altarType)
        {
            var (negativeModType, mods) = ExtractModsFromElement(ElementToExtractDataFrom);
            var (upsides, downsides, hasUnmatchedMods) = ProcessMods(mods, negativeModType);
            UpdateAltarComponent(top, altarComponent, ElementToExtractDataFrom, upsides, downsides, hasUnmatchedMods);
        }
        private (string negativeModType, List<string> mods) ExtractModsFromElement(Element element)
        {
            string negativeModType = "";
            List<string> mods = [];
            string altarMods = _altarMatcher.CleanAltarModsText(element.GetText(AltarModsTextReadLength));
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
                    _clickIt.GetAlertService().TryTriggerAlertForMatchedMod(matchedId);
                }
            }

            if (unmatched.Count > 0)
            {
                foreach (var mod in unmatched)
                    RecordUnmatchedMod(mod, negativeModType);
                return (upsides ?? [], downsides ?? [], true);
            }

            return (upsides ?? [], downsides ?? [], false);
        }

        // Records unmatched mod info into debug structures and logs when in debug mode.
        private void RecordUnmatchedMod(string mod, string negativeModType)
        {
            DebugInfo.ModsUnmatched++;
            string cleanedMod = AltarModMatcher.NormalizeLetters(mod);
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

        private static void UpdateAltarComponent(bool top, PrimaryAltarComponent altarComponent, Element? element,
            List<string> upsides, List<string> downsides, bool hasUnmatchedMods)
        {
            if (top)
            {
                altarComponent.TopButton = new AltarButton(element?.Parent);
                altarComponent.TopMods = new SecondaryAltarComponent(element, upsides, downsides, hasUnmatchedMods);
            }
            else
            {
                altarComponent.BottomButton = new AltarButton(element?.Parent);
                altarComponent.BottomMods = new SecondaryAltarComponent(element, upsides, downsides, hasUnmatchedMods);
            }
        }

    }
}

