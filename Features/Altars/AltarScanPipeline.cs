namespace ClickIt.Features.Altars
{
    internal sealed class AltarScanPipeline(
        AltarComponentStore altarStore,
        AltarServiceDebugInfo debugInfo,
        AltarComponentFactory componentFactory,
        BreakdownRecorder? recordBreakdown = null)
    {
        private readonly AltarComponentStore _altarStore = altarStore;
        private readonly AltarServiceDebugInfo _debugInfo = debugInfo;
        private readonly AltarComponentFactory _componentFactory = componentFactory;
        private readonly BreakdownRecorder? _recordBreakdown = recordBreakdown;
        private List<LabelOnGround>? _lastProcessedLabels;
        private bool _lastScanFoundAltarLabels;

        internal void ProcessScan(
            TimeCache<List<LabelOnGround>>? cachedLabels,
            bool includeExarch,
            bool includeEater)
        {
            _debugInfo.ResetForScan(DateTime.Now);

            List<LabelOnGround>? labelsFromCache = cachedLabels?.Value;
            if (labelsFromCache == null || labelsFromCache.Count == 0)
            {
                _altarStore.Clear();
                _lastProcessedLabels = null;
                _lastScanFoundAltarLabels = false;
                return;
            }

            // The read model returns the same list reference while the visible label set is unchanged — skip the per-label element-tree walk and component rebuild (warmed components stay valid; the store prunes invalid ones on the next real label-set change). Re-process if the store was cleared externally so it re-populates even with an unchanged label set.
            if (ReferenceEquals(labelsFromCache, _lastProcessedLabels)
                && (!_lastScanFoundAltarLabels || _altarStore.GetComponentCount() > 0))
                return;

            _lastProcessedLabels = labelsFromCache;

            long labelsStart = Stopwatch.GetTimestamp();
            long labelsAllocStart = GC.GetAllocatedBytesForCurrentThread();
            List<LabelOnGround> altarLabels = AltarScanner.CollectVisibleAltarLabels(
                cachedLabels,
                includeExarch,
                includeEater,
                _debugInfo);
            double labelsMs = (Stopwatch.GetTimestamp() - labelsStart) * 1000.0 / Stopwatch.Frequency;
            long labelsBytes = GC.GetAllocatedBytesForCurrentThread() - labelsAllocStart;
            if (altarLabels.Count == 0)
            {
                _lastScanFoundAltarLabels = false;
                _altarStore.Clear();
                return;
            }
            _lastScanFoundAltarLabels = true;

            long buildStart = Stopwatch.GetTimestamp();
            long buildAllocStart = GC.GetAllocatedBytesForCurrentThread();
            ProcessLabels(altarLabels);
            double buildMs = (Stopwatch.GetTimestamp() - buildStart) * 1000.0 / Stopwatch.Frequency;
            long buildBytes = GC.GetAllocatedBytesForCurrentThread() - buildAllocStart;

            if (_recordBreakdown != null)
            {
                Span<long> bytes = stackalloc long[2];
                Span<double> ms = stackalloc double[2];
                bytes[0] = labelsBytes; ms[0] = labelsMs;
                bytes[1] = buildBytes; ms[1] = buildMs;
                _recordBreakdown.Invoke(bytes, ms);
            }
        }

        internal void ProcessLabels(List<LabelOnGround> altarLabels)
        {
            List<(Element element, string path)> elementsToProcess = AltarScanner.CollectElementsFromLabels(altarLabels);
            _debugInfo.ElementsFound = elementsToProcess.Count;

            _altarStore.RemoveWhere(AltarComponentValidation.ShouldRemoveInvalidCachedComponent);

            foreach ((Element? element, string path) in elementsToProcess)
            {
                if (element == null)
                    continue;

                AltarType altarType = AltarScanner.DetermineAltarType(path);
                PrimaryAltarComponent altarComponent = _componentFactory.CreateFromElement(element, altarType);

                if (!TryValidate(altarComponent))
                    continue;

                bool wasAdded = _altarStore.Add(altarComponent);
                AltarComponentFactory.WarmAddedData(altarComponent, wasAdded);
                _debugInfo.RecordProcessedComponent(altarType, wasAdded);
            }
        }

        private bool TryValidate(PrimaryAltarComponent altarComponent)
        {
            bool isValid = AltarComponentValidation.IsComponentComplete(altarComponent);
            if (!isValid)
                _debugInfo.RecordInvalidComponent("Invalid altar component - missing parts");

            return isValid;
        }
    }
}
