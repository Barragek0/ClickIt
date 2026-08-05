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
                RenderSection.Unknown => (0, 0, 0, 0),
                _ => (0, 0, 0, 0)
            };
        }

        // Bounded rolling window (matches the render total's last-60 window) so a section's avg/max
        // reflect recent frames. The old all-time cumulative average/max drifted out of step with the
        // render total (e.g. a one-off 200ms debug spike stayed in max forever while the total rolled
        // past it), which made the debug table look inconsistent.
        private sealed class RollingSampleBuffer
        {
            private const int Window = 60;
            private readonly Queue<double> _samples = new(Window);
            private double _last;

            public (double LastMs, double AverageMs, double MaxMs, long SampleCount) Stats
            {
                get
                {
                    if (_samples.Count == 0)
                        return (0, 0, 0, 0);
                    double sum = 0;
                    double max = double.MinValue;
                    foreach (double value in _samples)
                    {
                        sum += value;
                        if (value > max)
                            max = value;
                    }
                    return (_last, sum / _samples.Count, max, _samples.Count);
                }
            }

            public void Record(double ms)
            {
                _last = ms;
                _samples.Enqueue(ms);
                if (_samples.Count > Window)
                    _samples.Dequeue();
            }
        }
    }
}