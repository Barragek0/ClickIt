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

        // Bounded rolling window per section: max reflects the full 100-sample window, average the
        // most recent 20 samples, last the most recent frame. Matches the coroutine/render tables'
        // semantics so a section's max never drifts out of step with its avg/last (a one-off spike
        // rolls out instead of persisting forever).
        private sealed class RollingSampleBuffer
        {
            private const int MaxWindow = 100;
            private const int AverageWindow = 20;
            private readonly Queue<double> _samples = new(MaxWindow);
            private double _last;

            public (double LastMs, double AverageMs, double MaxMs, long SampleCount) Stats
            {
                get
                {
                    if (_samples.Count == 0)
                        return (0, 0, 0, 0);
                    double max = double.MinValue;
                    foreach (double value in _samples)
                    {
                        if (value > max)
                            max = value;
                    }
                    return (_last, AverageOfLast(_samples, AverageWindow), max, _samples.Count);
                }
            }

            public void Record(double ms)
            {
                _last = ms;
                _samples.Enqueue(ms);
                if (_samples.Count > MaxWindow)
                    _samples.Dequeue();
            }

            private static double AverageOfLast(Queue<double> samples, int recentWindow)
            {
                int count = samples.Count;
                int take = SystemMath.Min(count, recentWindow);
                int skip = count - take;
                double sum = 0;
                int i = 0;
                foreach (double value in samples)
                {
                    if (i >= skip)
                        sum += value;
                    i++;
                }
                return sum / take;
            }
        }
    }
}