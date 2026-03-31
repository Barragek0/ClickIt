using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
using ClickIt.Components;
using ClickIt.Definitions;
using ClickIt.Utils;
using System.Collections.ObjectModel;
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
        public AltarServiceDebugInfo DebugInfo { get; private set; } = new();
        private readonly AltarMatcher _altarMatcher = new();

        private readonly List<PrimaryAltarComponent> _altarComponents = [];
        private readonly HashSet<string> _altarKeys = new(StringComparer.Ordinal);
        private readonly object _altarComponentsLock = new();
        private volatile PrimaryAltarComponent[] _altarSnapshot = [];
        private volatile ReadOnlyCollection<PrimaryAltarComponent> _altarReadOnlySnapshot = Array.AsReadOnly(Array.Empty<PrimaryAltarComponent>());

        public List<PrimaryAltarComponent> GetAltarComponents() => [.. _altarSnapshot];
        public IReadOnlyList<PrimaryAltarComponent> GetAltarComponentsReadOnly() => _altarReadOnlySnapshot;
        public int GetAltarComponentCount() => _altarSnapshot.Length;

        public void ClearAltarComponents()
        {
            lock (_altarComponentsLock)
            {
                foreach (var component in _altarComponents)
                    component.InvalidateCache();
                _altarComponents.Clear();
                _altarKeys.Clear();
                RefreshAltarSnapshotUnderLock();
            }
        }

        public void ClearRuntimeCaches()
        {
            _altarMatcher.ClearCaches();
            DebugInfo.RecentUnmatchedMods.Clear();
        }

        public void RemoveAltarComponentsByElement(Element element)
        {
            if (element == null) return;

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

            bool ShouldRemove(PrimaryAltarComponent altar)
            {
                bool match = MatchesElement(altar, element);
                if (match)
                {
                    altar.InvalidateCache();
                }
                return match;
            }

            RemoveAltarComponents(ShouldRemove);
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
        public bool AddAltarComponent(PrimaryAltarComponent component)
        {
            if (component == null)
                return false;

            lock (_altarComponentsLock)
            {
                string newKey = BuildAltarKey(component);
                if (!_altarKeys.Add(newKey))
                    return false;

                _altarComponents.Add(component);
                RefreshAltarSnapshotUnderLock();
                return true;
            }
        }

        private void RemoveAltarComponents(Func<PrimaryAltarComponent, bool> predicate)
        {
            if (predicate == null)
                return;

            lock (_altarComponentsLock)
            {
                int removed = _altarComponents.RemoveAll(component =>
                {
                    bool remove = predicate(component);
                    if (remove)
                        component.InvalidateCache();
                    return remove;
                });

                if (removed <= 0)
                    return;

                RebuildAltarKeySnapshotUnderLock();
                RefreshAltarSnapshotUnderLock();
            }
        }

        private void RefreshAltarSnapshotUnderLock()
        {
            PrimaryAltarComponent[] snapshot = [.. _altarComponents];
            _altarSnapshot = snapshot;
            _altarReadOnlySnapshot = Array.AsReadOnly(snapshot);
        }

        private void RebuildAltarKeySnapshotUnderLock()
        {
            _altarKeys.Clear();
            for (int i = 0; i < _altarComponents.Count; i++)
                _altarKeys.Add(BuildAltarKey(_altarComponents[i]));
        }

        private static string BuildAltarKey(PrimaryAltarComponent component)
        {
            var topUpside = GetAltarModStrings(component.TopMods, false);
            var topDownside = GetAltarModStrings(component.TopMods, true);
            var bottomUpside = GetAltarModStrings(component.BottomMods, false);
            var bottomDownside = GetAltarModStrings(component.BottomMods, true);

            var allMods = topUpside.Concat(topDownside)
                .Concat(bottomUpside)
                .Concat(bottomDownside);

            return string.Join("|", allMods);
        }

        private static string[] GetAltarModStrings(SecondaryAltarComponent? component, bool isDownside)
        {
            if (component == null)
                return new string[8];

            string[] values = new string[8];
            for (int i = 0; i < values.Length; i++)
                values[i] = isDownside ? component.GetDownsideByIndex(i) : component.GetUpsideByIndex(i);

            return values;
        }

        private (List<string> upsides, List<string> downsides, bool hasUnmatchedMods) ProcessMods(List<string> mods, string negativeModType)
        {
            var (upsides, downsides, unmatched) = AltarParser.ProcessMods(mods, negativeModType, (mod, neg) =>
            {
                if (_altarMatcher.TryMatchModCached(mod, neg, out bool isUpside, out string matchedId))
                    return (true, isUpside, matchedId);
                return (false, false, string.Empty);
            });

            DebugInfo.ModsMatched += upsides.Count + downsides.Count;

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

        internal void RecordUnmatchedMod(string mod, string negativeModType)
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

        internal static void UpdateAltarComponentFromAdapter(bool top, PrimaryAltarComponent altarComponent, IElementAdapter element,
            List<string> upsides, List<string> downsides, bool hasUnmatchedMods)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            UpdateAltarComponent(top, altarComponent, element.Underlying, upsides, downsides, hasUnmatchedMods);
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

