namespace ClickIt.Features.Observability.Performance
{
    internal sealed class RenderSectionMetricsStore
    {
        private readonly RollingSampleBuffer _lazyMode = new();
        private readonly RollingSampleBuffer _debugOverlay = new();
        private readonly RollingSampleBuffer _altarOverlay = new();
        private readonly RollingSampleBuffer _ultimatumOverlay = new();
        private readonly RollingSampleBuffer _strongboxOverlay = new();
        private readonly RollingSampleBuffer _textFlush = new();
        private readonly RollingSampleBuffer _pathfindingOverlay = new();
        private readonly RollingSampleBuffer _frameFlush = new();
        private readonly RollingSampleBuffer _harvestOverlay = new();
        private readonly RollingSampleBuffer _blightOverlay = new();
        private readonly RollingSampleBuffer _clickHotkeyToggle = new();
        private readonly RollingSampleBuffer _inventoryFullWarning = new();
        private readonly RollingSampleBuffer _uiRegionRectangle = new();
        private readonly RollingSampleBuffer _performanceOverlay = new();

        internal void Record(RenderSection section, double ms)
        {
            switch (section)
            {
                case RenderSection.LazyMode:
                    _lazyMode.Record(ms);
                    break;
                case RenderSection.DebugOverlay:
                    _debugOverlay.Record(ms);
                    break;
                case RenderSection.AltarOverlay:
                    _altarOverlay.Record(ms);
                    break;
                case RenderSection.UltimatumOverlay:
                    _ultimatumOverlay.Record(ms);
                    break;
                case RenderSection.StrongboxOverlay:
                    _strongboxOverlay.Record(ms);
                    break;
                case RenderSection.TextFlush:
                    _textFlush.Record(ms);
                    break;
                case RenderSection.PathfindingOverlay:
                    _pathfindingOverlay.Record(ms);
                    break;
                case RenderSection.FrameFlush:
                    _frameFlush.Record(ms);
                    break;
                case RenderSection.HarvestOverlay:
                    _harvestOverlay.Record(ms);
                    break;
                case RenderSection.BlightOverlay:
                    _blightOverlay.Record(ms);
                    break;
                case RenderSection.ClickHotkeyToggle:
                    _clickHotkeyToggle.Record(ms);
                    break;
                case RenderSection.InventoryFullWarning:
                    _inventoryFullWarning.Record(ms);
                    break;
                case RenderSection.UiRegionRectangle:
                    _uiRegionRectangle.Record(ms);
                    break;
                case RenderSection.PerformanceOverlay:
                    _performanceOverlay.Record(ms);
                    break;
                case RenderSection.Unknown:
                default:
                    break;
            }
        }

        internal (double LastMs, double AverageMs, double MaxMs, long SampleCount) GetStats(RenderSection section)
        {
            return section switch
            {
                RenderSection.LazyMode => _lazyMode.Stats,
                RenderSection.DebugOverlay => _debugOverlay.Stats,
                RenderSection.AltarOverlay => _altarOverlay.Stats,
                RenderSection.UltimatumOverlay => _ultimatumOverlay.Stats,
                RenderSection.StrongboxOverlay => _strongboxOverlay.Stats,
                RenderSection.PathfindingOverlay => _pathfindingOverlay.Stats,
                RenderSection.TextFlush => _textFlush.Stats,
                RenderSection.FrameFlush => _frameFlush.Stats,
                RenderSection.HarvestOverlay => _harvestOverlay.Stats,
                RenderSection.BlightOverlay => _blightOverlay.Stats,
                RenderSection.ClickHotkeyToggle => _clickHotkeyToggle.Stats,
                RenderSection.InventoryFullWarning => _inventoryFullWarning.Stats,
                RenderSection.UiRegionRectangle => _uiRegionRectangle.Stats,
                RenderSection.PerformanceOverlay => _performanceOverlay.Stats,
                RenderSection.Unknown => (0, 0, 0, 0),
                _ => (0, 0, 0, 0)
            };
        }
    }
}