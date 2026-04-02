using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using ClickIt.Services;

namespace ClickIt.Core.Runtime
{
    public sealed class PluginServices
    {
        public Utils.PerformanceMonitor? PerformanceMonitor { get; set; }
        public Utils.ErrorHandler? ErrorHandler { get; set; }
        public TimeCache<List<LabelOnGround>>? CachedLabels { get; set; }
        public AreaService? AreaService { get; set; }
        public AltarService? AltarService { get; set; }
        public ShrineService? ShrineService { get; set; }
        public LabelFilterService? LabelFilterService { get; set; }
        public LabelService? LabelService { get; set; }
        public ClickService? ClickService { get; set; }
        public PathfindingService? PathfindingService { get; set; }
        public AlertService? AlertService { get; set; }
        public Camera? Camera { get; set; }
        public Utils.InputHandler? InputHandler { get; set; }
    }
}