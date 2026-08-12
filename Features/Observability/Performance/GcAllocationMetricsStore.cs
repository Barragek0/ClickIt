namespace ClickIt.Features.Observability.Performance
{
    // Rolling per-section allocation pressure: bytes allocated per run (GC.GetAllocatedBytesForCurrentThread
    // delta across a processing boundary) plus the wall-clock period between runs, so the debug table can
    // show a steady-state alloc/s rate alongside the per-run bytes. Locked because the label-scan boundary
    // can run on any coroutine that touches CachedLabels.Value.
    internal sealed class GcAllocationMetricsStore
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<ProcessingSection, AllocationSampleWindow> _samples = [];

        internal void Record(ProcessingSection section, long bytes)
        {
            if (section == ProcessingSection.Unknown || bytes < 0)
                return;

            lock (_lock)
            {
                if (!_samples.TryGetValue(section, out AllocationSampleWindow? samples))
                {
                    samples = new AllocationSampleWindow();
                    _samples[section] = samples;
                }
                samples.Record(bytes);
            }
        }

        internal GcAllocationSnapshot GetStats(ProcessingSection section)
        {
            lock (_lock)
            {
                if (!_samples.TryGetValue(section, out AllocationSampleWindow? samples))
                    return default;
                AllocationSampleStats s = samples.Stats;
                return new GcAllocationSnapshot(
                    s.AllocPerSecond, s.AvgBytesPerRun, s.MaxBytesPerRun,
                    s.SampleCount, s.LastBytesPerRun, s.AvgPeriodMs, s.MaxAllocPerSecond);
            }
        }
    }
}
