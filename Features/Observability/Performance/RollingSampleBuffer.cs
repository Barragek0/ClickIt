namespace ClickIt.Features.Observability.Performance
{
    // Time-window sample buffer shared by the render and processing metric stores: every sample
    // expires 10 seconds after it was recorded, so a section that stops recording drains to zero
    // instead of reporting a stale all-time average/max. Last/Average/Max cover the live samples
    // only. Locked because label-scan processing can be recorded from any coroutine that touches
    // CachedLabels.Value, unlike render sections which are render-thread only.
    internal sealed class RollingSampleBuffer
    {
        private readonly ExpiringSampleBuffer _samples;

        internal RollingSampleBuffer(Func<long>? nowProvider = null)
            => _samples = new(nowProvider: nowProvider);

        internal void Record(double ms) => _samples.Record(ms);

        internal (double LastMs, double AverageMs, double MaxMs, long SampleCount) Stats
        {
            get
            {
                (double last, double average, double max, long count) = _samples.Stats;
                return (last, average, max, count);
            }
        }
    }
}
