
namespace ClickIt.UI.Debug
{
    public class DebugRenderer
    {
        private const int DetailedDebugStartY = 120;
        private const int DetailedDebugLineHeight = 18;
        private const int DetailedDebugBaseX = 10;
        private const int DetailedDebugLinesPerColumn = 34;
        private const int DetailedDebugColumnShiftPx = 600;
        private const int DetailedDebugMaxColumns = 4;

        private readonly BaseSettingsPlugin<ClickItSettings> _plugin;
        private readonly AltarService? _altarService;
        private readonly AreaService? _areaService;
        private readonly WeightCalculator? _weightCalculator;
        private readonly DeferredTextQueue _deferredTextQueue;
        private readonly DeferredFrameQueue _deferredFrameQueue;
        private readonly IDebugTelemetrySource _debugTelemetrySource;
        private readonly DebugOverlayRenderContext _overlayContext;
        private readonly StatusDebugOverlaySection _statusDebugOverlaySection;
        private readonly ClickingDebugOverlaySection _clickingDebugOverlaySection;
        private readonly LabelDebugOverlaySection _labelDebugOverlaySection;
        private readonly PathfindingDebugOverlaySection _pathfindingDebugOverlaySection;
        private readonly UltimatumDebugOverlaySection _ultimatumDebugOverlaySection;
        private readonly PerformanceDebugOverlaySection _performanceDebugOverlaySection;
        private readonly FrameDebugOverlaySection _frameDebugOverlaySection;
        private readonly IDebugLayoutEngine _layoutEngine = new DebugLayoutEngine();
        private readonly DebugOverlayComposer _overlayComposer;
        private readonly DebugOverlaySectionFactory _sectionFactory;
        private readonly DebugTextBlockRenderer _textBlockRenderer;
        private static readonly DebugLayoutSettings LayoutSettings = new(
            StartY: DetailedDebugStartY,
            LineHeight: DetailedDebugLineHeight,
            LinesPerColumn: DetailedDebugLinesPerColumn,
            MaxColumns: DetailedDebugMaxColumns,
            BaseX: DetailedDebugBaseX,
            ColumnShiftPx: DetailedDebugColumnShiftPx);

        public DebugRenderer(BaseSettingsPlugin<ClickItSettings> plugin,
                             AltarService? altarService = null,
                             AreaService? areaService = null,
                             WeightCalculator? weightCalculator = null,
                             DeferredTextQueue? deferredTextQueue = null,
                             DeferredFrameQueue? deferredFrameQueue = null)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _altarService = altarService;
            _areaService = areaService;
            _weightCalculator = weightCalculator;
            _deferredTextQueue = deferredTextQueue ?? new DeferredTextQueue();
            _deferredFrameQueue = deferredFrameQueue ?? new DeferredFrameQueue();
            _debugTelemetrySource = new PluginDebugTelemetrySource(_plugin);
            _overlayContext = new DebugOverlayRenderContext(
                _plugin,
                _altarService,
                _areaService,
                _weightCalculator,
                _deferredTextQueue,
                _deferredFrameQueue,
                _debugTelemetrySource);
            _statusDebugOverlaySection = new StatusDebugOverlaySection(_overlayContext);
            _clickingDebugOverlaySection = new ClickingDebugOverlaySection(_overlayContext);
            _labelDebugOverlaySection = new LabelDebugOverlaySection(_overlayContext);
            _pathfindingDebugOverlaySection = new PathfindingDebugOverlaySection(_overlayContext);
            _ultimatumDebugOverlaySection = new UltimatumDebugOverlaySection(_overlayContext);
            _performanceDebugOverlaySection = new PerformanceDebugOverlaySection(_overlayContext);
            _frameDebugOverlaySection = new FrameDebugOverlaySection(_overlayContext);
            _overlayComposer = new DebugOverlayComposer(_layoutEngine, LayoutSettings);
            _sectionFactory = new DebugOverlaySectionFactory(new DebugOverlaySectionFactoryDependencies(
                _clickingDebugOverlaySection,
                _labelDebugOverlaySection,
                _ultimatumDebugOverlaySection,
                _performanceDebugOverlaySection,
                _statusDebugOverlaySection,
                _pathfindingDebugOverlaySection,
                RenderAltarDebug,
                RenderAltarServiceDebug,
                RenderHoveredItemMetadataDebug,
                RenderErrorsDebug));
            _textBlockRenderer = new DebugTextBlockRenderer(_deferredTextQueue, _layoutEngine, LayoutSettings);
        }

        public void RenderDetailedDebugInfo(ClickItSettings settings, PerformanceMonitor performanceMonitor)
        {
            if (settings == null || performanceMonitor == null) return;

            // avoid doing any work if nothing to render
            if (!settings.IsAnyDetailedDebugSectionEnabled())
            {
                return;
            }

            PerformanceMetricsSnapshot performanceSnapshot = performanceMonitor.GetDebugSnapshot();
            DebugOverlaySection[] sections = _sectionFactory.CreateSections(settings, performanceSnapshot);

            _overlayComposer.RenderSections(sections);
        }

        public void RenderDebugFrames(ClickItSettings settings)
            => _frameDebugOverlaySection.RenderDebugFrames(settings);

        public int RenderRuntimeDebugLogOverlay(int xPos, int yPos, int lineHeight)
        {
            int localX = xPos;
            return _clickingDebugOverlaySection.RenderRuntimeDebugLogOverlay(ref localX, yPos, lineHeight);
        }

        public int RenderClickingDebug(int xPos, int yPos, int lineHeight)
        {
            int localX = xPos;
            return _clickingDebugOverlaySection.RenderClickingDebug(ref localX, yPos, lineHeight);
        }

        internal static IReadOnlyList<string> BuildClickSettingsDebugSnapshotLines(ClickItSettings settings)
            => ClickSettingsTelemetrySnapshot.FromSettings(settings).SummaryLines;

        public int RenderAltarDebug(int xPos, int yPos, int lineHeight)
            => _labelDebugOverlaySection.RenderAltarDebug(xPos, yPos, lineHeight);

        public int RenderAltarServiceDebug(int xPos, int yPos, int lineHeight)
            => _labelDebugOverlaySection.RenderAltarServiceDebug(xPos, yPos, lineHeight);

        public int RenderLabelsDebug(int xPos, int yPos, int lineHeight)
        {
            int localX = xPos;
            return _labelDebugOverlaySection.RenderLabelsDebug(ref localX, yPos, lineHeight);
        }

        public int RenderInventoryPickupDebug(int xPos, int yPos, int lineHeight)
        {
            int localX = xPos;
            return _labelDebugOverlaySection.RenderInventoryPickupDebug(ref localX, yPos, lineHeight);
        }

        public int RenderHoveredItemMetadataDebug(int xPos, int yPos, int lineHeight)
            => _labelDebugOverlaySection.RenderHoveredItemMetadataDebug(xPos, yPos, lineHeight);

        internal readonly record struct ClickFrequencyTargetDebugMetrics(
            double ClickTargetMs,
            double ProcessingMs,
            double ClickDelayMs,
            double ModeledTotalMs,
            double ObservedTotalMs,
            double SchedulerDeltaMs,
            double TargetDeviationRatio);

        internal static ClickFrequencyTargetDebugMetrics BuildClickFrequencyTargetDebugMetrics(
            double clickTargetMs,
            double processingMs,
            double observedIntervalMs)
        {
            PerformanceDebugOverlaySection.ClickFrequencyTargetDebugMetrics metrics = PerformanceDebugOverlaySection.BuildClickFrequencyTargetDebugMetrics(
                clickTargetMs,
                processingMs,
                observedIntervalMs);
            return new ClickFrequencyTargetDebugMetrics(
                ClickTargetMs: metrics.ClickTargetMs,
                ProcessingMs: metrics.ProcessingMs,
                ClickDelayMs: metrics.ClickDelayMs,
                ModeledTotalMs: metrics.ModeledTotalMs,
                ObservedTotalMs: metrics.ObservedTotalMs,
                SchedulerDeltaMs: metrics.SchedulerDeltaMs,
                TargetDeviationRatio: metrics.TargetDeviationRatio);
        }

        public int RenderErrorsDebug(int xPos, int yPos, int lineHeight)
            => _performanceDebugOverlaySection.RenderErrorsDebug(xPos, yPos, lineHeight);

        protected int EnqueueWrappedDebugLine(
            ref int xPos,
            int yPos,
            int lineHeight,
            string text,
            Color color,
            int fontSize,
            int maxCharsPerLine = 72)
            => _textBlockRenderer.EnqueueWrappedLine(ref xPos, yPos, lineHeight, text, color, fontSize, maxCharsPerLine);

        public int RenderWrappedText(string text, Vector2 position, Color color, int fontSize, int lineHeight, int maxCharsPerLine)
            => _textBlockRenderer.RenderWrappedText(text, position, color, fontSize, lineHeight, maxCharsPerLine);
    }
}
