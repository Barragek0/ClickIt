namespace ClickIt.Core.Runtime
{
    public sealed class PluginServices
    {
        internal PluginServices()
        {
        }

        public PerformanceMonitor? PerformanceMonitor { get; set; }
        public ErrorHandler? ErrorHandler { get; set; }
        public TimeCache<List<LabelOnGround>>? CachedLabels { get; set; }
        public AreaService? AreaService { get; set; }
        public AltarService? AltarService { get; set; }
        public ShrineService? ShrineService { get; set; }
        public LabelFilterPort? LabelFilterPort { get; set; }
        public ClickAutomationPort? ClickAutomationPort { get; set; }
        public PathfindingService? PathfindingService { get; set; }
        public AlertService? AlertService { get; set; }
        public Camera? Camera { get; set; }
        public InputHandler? InputHandler { get; set; }
        public WeightCalculator? WeightCalculator { get; set; }

        internal void Clear()
        {
            PerformanceMonitor = null;
            ErrorHandler = null;
            CachedLabels = null;
            AreaService = null;
            AltarService = null;
            ShrineService = null;
            LabelFilterPort = null;
            ClickAutomationPort = null;
            PathfindingService = null;
            AlertService = null;
            Camera = null;
            InputHandler = null;
            WeightCalculator = null;
        }
    }
}