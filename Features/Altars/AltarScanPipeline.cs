namespace ClickIt.Features.Altars
{
    internal sealed class AltarScanPipeline(
        AltarComponentStore altarStore,
        AltarServiceDebugInfo debugInfo,
        AltarComponentFactory componentFactory)
    {
        private readonly AltarComponentStore _altarStore = altarStore;
        private readonly AltarServiceDebugInfo _debugInfo = debugInfo;
        private readonly AltarComponentFactory _componentFactory = componentFactory;

        internal void ProcessScan(
            TimeCache<List<LabelOnGround>>? cachedLabels,
            bool includeExarch,
            bool includeEater)
        {
            _debugInfo.ResetForScan(DateTime.Now);

            List<LabelOnGround> altarLabels = AltarScanner.CollectVisibleAltarLabels(
                cachedLabels,
                includeExarch,
                includeEater,
                _debugInfo);
            if (altarLabels.Count == 0)
            {
                _altarStore.Clear();
                return;
            }

            ProcessLabels(altarLabels);
        }

        internal void ProcessLabels(List<LabelOnGround> altarLabels)
        {
            var elementsToProcess = AltarScanner.CollectElementsFromLabels(altarLabels);
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