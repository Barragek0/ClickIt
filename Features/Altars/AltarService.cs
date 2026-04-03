namespace ClickIt.Features.Altars
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
        public AltarServiceDebugInfo DebugInfo { get; private set; } = new();
        private readonly AltarMatcher _altarMatcher = new();
        private AltarComponentFactory? _componentFactory;

        private readonly List<PrimaryAltarComponent> _altarComponents = [];
        private readonly HashSet<string> _altarKeys = new(StringComparer.Ordinal);
        private readonly object _altarComponentsLock = new();
        private volatile PrimaryAltarComponent[] _altarSnapshot = [];
        private volatile ReadOnlyCollection<PrimaryAltarComponent> _altarReadOnlySnapshot = Array.AsReadOnly(Array.Empty<PrimaryAltarComponent>());

        private AltarComponentFactory ComponentFactory
            => _componentFactory ??= new AltarComponentFactory(
                _altarMatcher,
                matchedId => _clickIt.GetAlertService().TryTriggerAlertForMatchedMod(matchedId),
                matchedCount => DebugInfo.ModsMatched += matchedCount,
                RecordUnmatchedMod);

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
                List<LabelOnGround> exarchLabels = GetAltarLabels(AltarType.SearingExarch);
                DebugInfo.LastScanExarchLabels = exarchLabels.Count;
                if (exarchLabels.Count > 0)
                    altarLabels.AddRange(exarchLabels);
            }

            if (_settings.HighlightEaterAltars)
            {
                List<LabelOnGround> eaterLabels = GetAltarLabels(AltarType.EaterOfWorlds);
                DebugInfo.LastScanEaterLabels = eaterLabels.Count;
                if (eaterLabels.Count > 0)
                    altarLabels.AddRange(eaterLabels);
            }

            return altarLabels;
        }

        private void ProcessAltarLabels(List<LabelOnGround> altarLabels)
        {
            var elementsToProcess = AltarScanner.CollectElementsFromLabels(altarLabels);
            DebugInfo.ElementsFound = elementsToProcess.Count;

            CleanupInvalidAltars();

            foreach ((Element? element, string path) in elementsToProcess)
            {
                if (element == null)
                    continue;

                AltarType altarType = DetermineAltarType(path);
                DebugInfo.LastProcessedAltarType = altarType.ToString();
                PrimaryAltarComponent altarComponent = ComponentFactory.CreateFromElement(element, altarType);
                DebugInfo.ComponentsProcessed++;

                if (!IsValidAltarComponent(altarComponent))
                    continue;

                bool wasAdded = AddAltarComponent(altarComponent);
                AltarComponentFactory.WarmAddedData(altarComponent, wasAdded);

                if (wasAdded)
                    DebugInfo.ComponentsAdded++;
                else
                    DebugInfo.ComponentsDuplicated++;
            }
        }

        private void CleanupInvalidAltars()
        {
            RemoveAltarComponents(altar =>
            {
                return altar.TopMods?.Element == null
                    || altar.BottomMods?.Element == null
                    || !altar.TopMods.Element.IsValid
                    || !altar.BottomMods.Element.IsValid;
            });
        }

        internal static AltarType DetermineAltarType(string path)
        {
            if (string.IsNullOrEmpty(path))
                return AltarType.Unknown;

            if (path.Contains(Constants.CleansingFireAltar, StringComparison.OrdinalIgnoreCase))
                return AltarType.SearingExarch;
            if (path.Contains(Constants.TangleAltar, StringComparison.OrdinalIgnoreCase))
                return AltarType.EaterOfWorlds;

            return AltarType.Unknown;
        }
        private bool IsValidAltarComponent(PrimaryAltarComponent altarComponent)
        {
            bool isValid = altarComponent.TopMods != null && altarComponent.TopButton != null &&
                           altarComponent.BottomMods != null && altarComponent.BottomButton != null;
            if (!isValid)
                DebugInfo.LastError = "Invalid altar component - missing parts";

            return isValid;
        }

    }
}

