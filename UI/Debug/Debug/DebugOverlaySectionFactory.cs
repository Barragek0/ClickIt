namespace ClickIt.UI.Debug
{
    internal readonly record struct DebugOverlaySectionFactoryDependencies(
        ClickingDebugOverlaySection ClickingSection,
        LabelDebugOverlaySection LabelSection,
        UltimatumDebugOverlaySection UltimatumSection,
        PerformanceDebugOverlaySection PerformanceSection,
        StatusDebugOverlaySection StatusSection,
        PathfindingDebugOverlaySection PathfindingSection,
        Func<int, int, int, int> RenderAltarDebug,
        Func<int, int, int, int> RenderAltarServiceDebug,
        Func<int, int, int, int> RenderHoveredItemMetadataDebug,
        Func<int, int, int, int> RenderErrorsDebug);

    internal sealed class DebugOverlaySectionFactory(DebugOverlaySectionFactoryDependencies dependencies)
    {
        private readonly DebugOverlaySectionFactoryDependencies _dependencies = dependencies;

        internal DebugOverlaySection[] CreateSections(ClickItSettings settings, PerformanceMetricsSnapshot performanceSnapshot)
        {
            return
            [
                new(settings.DebugShowStatus, (x, y, h) => (x, _dependencies.StatusSection.RenderPluginStatusDebug(x, y, h))),
                new(settings.DebugShowGameState, (x, y, h) => (x, _dependencies.StatusSection.RenderGameStateDebug(x, y, h))),
                new(settings.DebugShowPerformance, (x, y, h) => (x, _dependencies.PerformanceSection.RenderPerformanceDebug(x, y, h, performanceSnapshot))),
                new(settings.DebugShowClickFrequencyTarget, (x, y, h) => (x, _dependencies.PerformanceSection.RenderClickFrequencyTargetDebug(x, y, h, performanceSnapshot))),
                new(settings.DebugShowAltarDetection, (x, y, h) => (x, _dependencies.RenderAltarDebug(x, y, h))),
                new(settings.DebugShowAltarService, (x, y, h) => (x, _dependencies.RenderAltarServiceDebug(x, y, h))),
                new(settings.DebugShowLabels, (x, y, h) =>
                {
                    int localX = x;
                    int nextY = _dependencies.LabelSection.RenderLabelsDebug(ref localX, y, h);
                    return (localX, nextY);
                }),
                new(settings.DebugShowInventoryPickup, (x, y, h) =>
                {
                    int localX = x;
                    int nextY = _dependencies.LabelSection.RenderInventoryPickupDebug(ref localX, y, h);
                    return (localX, nextY);
                }),
                new(settings.DebugShowHoveredItemMetadata, (x, y, h) => (x, _dependencies.RenderHoveredItemMetadataDebug(x, y, h))),
                new(settings.DebugShowPathfinding, (x, y, h) => (x, _dependencies.PathfindingSection.RenderPathfindingDebug(x, y, h))),
                new(settings.DebugShowUltimatum, (x, y, h) =>
                {
                    int localX = x;
                    int nextY = _dependencies.UltimatumSection.RenderUltimatumDebug(ref localX, y, h);
                    return (localX, nextY);
                }),
                new(settings.DebugShowClicking, (x, y, h) =>
                {
                    int localX = x;
                    int nextY = _dependencies.ClickingSection.RenderClickingDebug(ref localX, y, h);
                    return (localX, nextY);
                }),
                new(settings.DebugShowRuntimeDebugLogOverlay, (x, y, h) =>
                {
                    int localX = x;
                    int nextY = _dependencies.ClickingSection.RenderRuntimeDebugLogOverlay(ref localX, y, h);
                    return (localX, nextY);
                }),
                new(settings.DebugShowRecentErrors, (x, y, h) => (x, _dependencies.RenderErrorsDebug(x, y, h)))
            ];
        }
    }
}