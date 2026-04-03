namespace ClickIt.Core.Runtime
{
    internal sealed class PluginFeaturePorts
    {
        internal PerformanceMonitor? PerformanceMonitor { get; set; }
        internal ErrorHandler? ErrorHandler { get; set; }
        internal TimeCache<List<LabelOnGround>>? CachedLabels { get; set; }
        internal AreaService? AreaService { get; set; }
        internal AltarService? AltarService { get; set; }
        internal ShrineService? ShrineService { get; set; }
        internal LabelFilterService? LabelFilterPort { get; set; }
        internal LabelService? LabelService { get; set; }
        internal ClickService? ClickAutomationPort { get; set; }
        internal PathfindingService? PathfindingService { get; set; }
        internal AlertService? AlertService { get; set; }
        internal Camera? Camera { get; set; }
        internal InputHandler? InputHandler { get; set; }

        internal void Clear()
        {
            PerformanceMonitor = null;
            ErrorHandler = null;
            CachedLabels = null;
            AreaService = null;
            AltarService = null;
            ShrineService = null;
            LabelFilterPort = null;
            LabelService = null;
            ClickAutomationPort = null;
            PathfindingService = null;
            AlertService = null;
            Camera = null;
            InputHandler = null;
        }
    }
}