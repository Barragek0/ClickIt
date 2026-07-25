namespace ClickIt.Features.Observability.TelemetryProjection
{
    internal static partial class DebugTelemetryProjection
    {
        private static AltarTelemetrySnapshot BuildAltarTelemetry(AltarService? altarService, WeightCalculator? weightCalculator)
        {
            if (altarService == null)
                return AltarTelemetrySnapshot.Empty;

            IReadOnlyList<PrimaryAltarComponent> altarComponents = altarService.GetAltarComponentsReadOnly();
            List<AltarComponentTelemetrySnapshot> projectedComponents = [];

            for (int i = 0; i < SystemMath.Min(altarComponents.Count, 2); i++)
            {
                PrimaryAltarComponent altar = altarComponents[i];
                AltarWeights? weights = weightCalculator != null
                    ? altar.GetCachedWeights(weightCalculator.CalculateAltarWeights)
                    : null;

                decimal[]? topUpsideWeights = null;
                decimal[]? topDownsideWeights = null;
                decimal[]? bottomUpsideWeights = null;
                decimal[]? bottomDownsideWeights = null;
                if (weights.HasValue)
                {
                    AltarWeights localWeights = weights.Value;
                    topUpsideWeights = localWeights.GetTopUpsideWeights();
                    topDownsideWeights = localWeights.GetTopDownsideWeights();
                    bottomUpsideWeights = localWeights.GetBottomUpsideWeights();
                    bottomDownsideWeights = localWeights.GetBottomDownsideWeights();
                }

                projectedComponents.Add(new AltarComponentTelemetrySnapshot(
                    Top: BuildAltarModSectionTelemetry("Top", altar.TopMods, topUpsideWeights, topDownsideWeights),
                    Bottom: BuildAltarModSectionTelemetry("Bottom", altar.BottomMods, bottomUpsideWeights, bottomDownsideWeights)));
            }

            return new AltarTelemetrySnapshot(
                ServiceAvailable: true,
                ComponentCount: altarComponents.Count,
                Components: projectedComponents,
                ServiceDebug: BuildAltarServiceDebugTelemetry(altarService.DebugInfo));
        }

        private static AltarModSectionTelemetrySnapshot BuildAltarModSectionTelemetry(
            string sectionName,
            SecondaryAltarComponent? mods,
            decimal[]? upsideWeights,
            decimal[]? downsideWeights)
        {
            if (mods == null)
                return AltarModSectionTelemetrySnapshot.Empty(sectionName);

            return new AltarModSectionTelemetrySnapshot(
                SectionName: sectionName,
                UpsideCount: mods.Upsides?.Count ?? 0,
                DownsideCount: mods.Downsides?.Count ?? 0,
                Upsides: BuildWeightedMods(mods.Upsides, upsideWeights),
                Downsides: BuildWeightedMods(mods.Downsides, downsideWeights));
        }

        private static List<AltarWeightedModTelemetrySnapshot> BuildWeightedMods(
            List<string>? mods,
            decimal[]? weights)
        {
            int count = SystemMath.Min(mods?.Count ?? 0, 8);
            if (count <= 0)
                return [];

            List<AltarWeightedModTelemetrySnapshot> projectedMods = [];
            for (int i = 0; i < count; i++)
            {
                string mod = mods?[i] ?? string.Empty;
                if (string.IsNullOrEmpty(mod))
                    continue;

                decimal? weight = weights != null && i < weights.Length ? weights[i] : null;
                projectedMods.Add(new AltarWeightedModTelemetrySnapshot(mod, weight));
            }

            return projectedMods;
        }

        private static AltarServiceDebugTelemetrySnapshot BuildAltarServiceDebugTelemetry(AltarServiceDebugInfo? debugInfo)
        {
            if (debugInfo == null)
                return AltarServiceDebugTelemetrySnapshot.Empty;

            return new AltarServiceDebugTelemetrySnapshot(
                LastScanExarchLabels: debugInfo.LastScanExarchLabels,
                LastScanEaterLabels: debugInfo.LastScanEaterLabels,
                ElementsFound: debugInfo.ElementsFound,
                ComponentsProcessed: debugInfo.ComponentsProcessed,
                ComponentsAdded: debugInfo.ComponentsAdded,
                ComponentsDuplicated: debugInfo.ComponentsDuplicated,
                ModsMatched: debugInfo.ModsMatched,
                ModsUnmatched: debugInfo.ModsUnmatched,
                LastProcessedAltarType: debugInfo.LastProcessedAltarType,
                LastError: debugInfo.LastError,
                LastScanTime: debugInfo.LastScanTime);
        }
    }
}