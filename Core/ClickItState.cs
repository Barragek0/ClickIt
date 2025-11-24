

namespace ClickIt
{
    public class ClickItState
    {
        public Utils.PerformanceMonitor? PerformanceMonitor { get; set; }
        public Utils.ErrorHandler? ErrorHandler { get; set; }
        public Random Random { get; } = new Random();
        public TimeCache<List<LabelOnGround>>? CachedLabels { get; set; }
        public Coroutine? AltarCoroutine { get; set; }
        public Coroutine? ClickLabelCoroutine { get; set; }
        public Coroutine? DelveFlareCoroutine { get; set; }
        public Coroutine? ShrineCoroutine { get; set; }
        // Input safety fields removed
        public Stopwatch LastRenderTimer { get; } = new Stopwatch();
        public Stopwatch LastTickTimer { get; } = new Stopwatch();
        public Stopwatch Timer { get; } = new Stopwatch();
        public Stopwatch SecondTimer { get; } = new Stopwatch();
        public bool LastHotkeyState { get; set; } = false;
        public Services.AreaService? AreaService { get; set; }
        public Services.AltarService? AltarService { get; set; }
        public Services.ShrineService? ShrineService { get; set; }
        public Utils.InputHandler? InputHandler { get; set; }
        public Rendering.DebugRenderer? DebugRenderer { get; set; }
        public Rendering.AltarDisplayRenderer? AltarDisplayRenderer { get; set; }
        public Utils.DeferredTextQueue? DeferredTextQueue { get; set; }
        public Utils.DeferredFrameQueue? DeferredFrameQueue { get; set; }
        public Services.LabelFilterService? LabelFilterService { get; set; }
        public Camera? Camera { get; set; }
        public bool WorkFinished { get; set; } = false;
    }
}