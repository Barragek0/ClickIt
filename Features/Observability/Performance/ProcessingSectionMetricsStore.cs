namespace ClickIt.Features.Observability.Performance
{
    // One rolling buffer per feature-processing section. Processing measures the domain work a
    // feature does each run (label scan, altar scan, blight refresh, path build, ...) independent
    // of rendering or the coroutine framework. Run periods are tracked so the debug table can
    // normalize per-run ms to per-frame ms (like the coroutine table) and correlate with the
    // render table. Locked because the label-scan boundary can run on any coroutine.
    internal sealed class ProcessingSectionMetricsStore
    {
        private readonly PeriodTrackedBuffer _altar = new();
        private readonly PeriodTrackedBuffer _blight = new();
        private readonly PeriodTrackedBuffer _click = new();
        private readonly PeriodTrackedBuffer _flare = new();
        private readonly PeriodTrackedBuffer _harvest = new();
        private readonly PeriodTrackedBuffer _label = new();
        private readonly PeriodTrackedBuffer _pathfinding = new();
        private readonly PeriodTrackedBuffer _strongbox = new();
        private readonly PeriodTrackedBuffer _ultimatum = new();
        private readonly PeriodTrackedBuffer _areaBlockedUi = new();
        private readonly PeriodTrackedBuffer _manualUiHover = new();

        internal void Record(ProcessingSection section, double ms)
        {
            switch (section)
            {
                case ProcessingSection.Altar:
                    _altar.Record(ms);
                    break;
                case ProcessingSection.Blight:
                    _blight.Record(ms);
                    break;
                case ProcessingSection.Click:
                    _click.Record(ms);
                    break;
                case ProcessingSection.Flare:
                    _flare.Record(ms);
                    break;
                case ProcessingSection.Harvest:
                    _harvest.Record(ms);
                    break;
                case ProcessingSection.Label:
                    _label.Record(ms);
                    break;
                case ProcessingSection.Pathfinding:
                    _pathfinding.Record(ms);
                    break;
                case ProcessingSection.Strongbox:
                    _strongbox.Record(ms);
                    break;
                case ProcessingSection.Ultimatum:
                    _ultimatum.Record(ms);
                    break;
                case ProcessingSection.AreaBlockedUi:
                    _areaBlockedUi.Record(ms);
                    break;
                case ProcessingSection.ManualUiHover:
                    _manualUiHover.Record(ms);
                    break;
                case ProcessingSection.Unknown:
                default:
                    break;
            }
        }

        internal (double LastMs, double AverageMs, double MaxMs, long SampleCount, double AveragePeriodMs) GetStats(ProcessingSection section)
        {
            return section switch
            {
                ProcessingSection.Altar => _altar.Stats,
                ProcessingSection.Blight => _blight.Stats,
                ProcessingSection.Click => _click.Stats,
                ProcessingSection.Flare => _flare.Stats,
                ProcessingSection.Harvest => _harvest.Stats,
                ProcessingSection.Label => _label.Stats,
                ProcessingSection.Pathfinding => _pathfinding.Stats,
                ProcessingSection.Strongbox => _strongbox.Stats,
                ProcessingSection.Ultimatum => _ultimatum.Stats,
                ProcessingSection.AreaBlockedUi => _areaBlockedUi.Stats,
                ProcessingSection.ManualUiHover => _manualUiHover.Stats,
                ProcessingSection.Unknown => (0, 0, 0, 0, 0),
                _ => (0, 0, 0, 0, 0)
            };
        }

        private sealed class PeriodTrackedBuffer
        {
            private readonly RollingSampleBuffer _samples = new();
            private readonly ExpiringSampleBuffer _periods = new();
            private readonly object _lock = new();
            private long _lastTimestampMs;
            private bool _hasPrevious;

            public void Record(double ms)
            {
                lock (_lock)
                {
                    long now = Environment.TickCount64;
                    if (_hasPrevious)
                        _periods.Record(SystemMath.Max(1, now - _lastTimestampMs));
                    _lastTimestampMs = now;
                    _hasPrevious = true;
                    _samples.Record(ms);
                }
            }

            public (double LastMs, double AverageMs, double MaxMs, long SampleCount, double AveragePeriodMs) Stats
            {
                get
                {
                    lock (_lock)
                    {
                        (double last, double avg, double max, long count) = _samples.Stats;
                        return (last, avg, max, count, _periods.Stats.Average);
                    }
                }
            }
        }
    }
}
