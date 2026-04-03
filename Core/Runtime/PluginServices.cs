namespace ClickIt.Core.Runtime
{
    public sealed class PluginServices
    {
        private readonly PluginFeaturePorts _ports;

        internal PluginServices(PluginFeaturePorts ports)
        {
            _ports = ports ?? throw new ArgumentNullException(nameof(ports));
        }

        public PerformanceMonitor? PerformanceMonitor { get => _ports.PerformanceMonitor; set => _ports.PerformanceMonitor = value; }
        public ErrorHandler? ErrorHandler { get => _ports.ErrorHandler; set => _ports.ErrorHandler = value; }
        public TimeCache<List<LabelOnGround>>? CachedLabels { get => _ports.CachedLabels; set => _ports.CachedLabels = value; }
        public AreaService? AreaService { get => _ports.AreaService; set => _ports.AreaService = value; }
        public AltarService? AltarService { get => _ports.AltarService; set => _ports.AltarService = value; }
        public ShrineService? ShrineService { get => _ports.ShrineService; set => _ports.ShrineService = value; }
        public LabelFilterService? LabelFilterPort { get => _ports.LabelFilterPort; set => _ports.LabelFilterPort = value; }
        public LabelService? LabelService { get => _ports.LabelService; set => _ports.LabelService = value; }
        public ClickService? ClickAutomationPort { get => _ports.ClickAutomationPort; set => _ports.ClickAutomationPort = value; }
        public PathfindingService? PathfindingService { get => _ports.PathfindingService; set => _ports.PathfindingService = value; }
        public AlertService? AlertService { get => _ports.AlertService; set => _ports.AlertService = value; }
        public Camera? Camera { get => _ports.Camera; set => _ports.Camera = value; }
        public InputHandler? InputHandler { get => _ports.InputHandler; set => _ports.InputHandler = value; }
    }
}