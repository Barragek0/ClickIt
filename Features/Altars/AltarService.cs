namespace ClickIt.Features.Altars
{
    public class AltarService(ClickIt clickIt, ClickItSettings settings, TimeCache<List<LabelOnGround>>? cachedLabels)
    {
        private readonly ClickIt _clickIt = clickIt;
        private readonly ClickItSettings _settings = settings;
        private readonly TimeCache<List<LabelOnGround>>? _cachedLabels = cachedLabels;
        private readonly AltarComponentStore _altarStore = new();
        public AltarServiceDebugInfo DebugInfo { get; private set; } = new();
        private readonly AltarMatcher _altarMatcher = new();
        private AltarComponentFactory? _componentFactory;
        private AltarScanPipeline? _scanPipeline;

        private AltarComponentFactory ComponentFactory
            => _componentFactory ??= new AltarComponentFactory(
                _altarMatcher,
                matchedId => _clickIt.GetAlertService().TryTriggerAlertForMatchedMod(matchedId),
                matchedCount => DebugInfo.ModsMatched += matchedCount,
                RecordUnmatchedMod);

        private AltarScanPipeline ScanPipeline
            => _scanPipeline ??= new AltarScanPipeline(_altarStore, DebugInfo, ComponentFactory);

        public List<PrimaryAltarComponent> GetAltarComponents() => _altarStore.GetComponents();
        public IReadOnlyList<PrimaryAltarComponent> GetAltarComponentsReadOnly() => _altarStore.GetComponentsReadOnly();
        public int GetAltarComponentCount() => _altarStore.GetComponentCount();

        public void ClearAltarComponents()
            => _altarStore.Clear();

        public void ClearRuntimeCaches()
        {
            _altarMatcher.ClearCaches();
            DebugInfo.RecentUnmatchedMods.Clear();
        }

        public void RemoveAltarComponentsByElement(Element element)
            => _altarStore.RemoveByElement(element);

        public bool AddAltarComponent(PrimaryAltarComponent component)
            => _altarStore.Add(component);

        internal void RecordUnmatchedMod(string mod, string negativeModType)
        {
            DebugInfo.RecordUnmatchedMod(mod, negativeModType);

            if (_settings.DebugMode)
            {
                string cleanedMod = AltarModMatcher.NormalizeLetters(mod);
                _clickIt.LogError($"Failed to match mod: '{mod}' (Cleaned: '{cleanedMod}') with NegativeModType: '{negativeModType}'", 10);
            }
        }

        public void ProcessAltarScanningLogic()
            => ScanPipeline.ProcessScan(
                _cachedLabels,
                _settings.HighlightExarchAltars,
                _settings.HighlightEaterAltars);

    }
}

