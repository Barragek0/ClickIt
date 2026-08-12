namespace ClickIt.Features.Observability.Performance;

// Rolling per-section/per-stage allocation window shared by the GC, click-stage, and label-scan
// stores. Samples expire after a fixed window; average bytes + average period cover only the most
// recent AverageSampleCount samples so the table reacts quickly, while MaxBytesPerRun keeps the
// full live-window peak. MaxAllocPerSecond is the highest per-sample allocation rate in the live
// window (a run's bytes divided by its observed period), floored to MinSampleRatePeriodMs so a
// sub-resolution inter-sample gap (TickCount64 ~15ms) or an every-frame overlay can never
// extrapolate one run into a nonsensical hundreds-of-MB/s reading.
internal sealed class AllocationSampleWindow
{
    internal const int AverageSampleCount = 50;
    private const long ExpiryMs = 10_000;
    private const int MaxWindow = 1000;
    private const long MinSampleRatePeriodMs = 50;

    private readonly Queue<(long TimestampMs, long Bytes)> _samples = new(MaxWindow);
    private long[] _tsScratch = new long[64];
    private long[] _bytesScratch = new long[64];
    private readonly Func<long> _now;
    private long _last;

    internal AllocationSampleWindow(Func<long>? nowProvider = null)
    {
        _now = nowProvider ?? (static () => Environment.TickCount64);
    }

    internal void Record(long bytes)
    {
        if (bytes < 0)
            bytes = 0;
        long now = _now();
        Expire(now);
        _last = bytes;
        _samples.Enqueue((now, bytes));
        if (_samples.Count > MaxWindow)
            _samples.Dequeue();
    }

    internal AllocationSampleStats Stats
    {
        get
        {
            long now = _now();
            Expire(now);
            int count = _samples.Count;
            if (count == 0)
                return default;

            EnsureScratch(count);
            int i = 0;
            foreach ((long timestampMs, long bytes) in _samples)
            {
                _tsScratch[i] = timestampMs;
                _bytesScratch[i] = bytes;
                i++;
            }

            int take = SystemMath.Min(count, AverageSampleCount);
            int skip = count - take;
            long sum = 0;
            long max = 0;
            long periodTotal = 0;
            int periodCount = 0;
            for (int s = 0; s < count; s++)
            {
                if (_bytesScratch[s] > max)
                    max = _bytesScratch[s];
                if (s >= skip)
                {
                    sum += _bytesScratch[s];
                    if (s > 0)
                    {
                        periodTotal += SystemMath.Max(1, _tsScratch[s] - _tsScratch[s - 1]);
                        periodCount++;
                    }
                }
            }

            double avgBytes = take > 0 ? sum / (double)take : 0;
            double avgPeriodMs = periodCount > 0 ? periodTotal / (double)periodCount : 0;
            double allocPerSecond = avgPeriodMs > 0 ? avgBytes * 1000.0 / avgPeriodMs : 0;
            double maxAllocPerSecond = ComputeMaxSampleRate(count);
            return new AllocationSampleStats(_last, allocPerSecond, avgBytes, max, count, avgPeriodMs, maxAllocPerSecond);
        }
    }

    private void EnsureScratch(int count)
    {
        if (_tsScratch.Length >= count)
            return;
        int size = SystemMath.Max(count, _tsScratch.Length * 2);
        _tsScratch = new long[size];
        _bytesScratch = new long[size];
    }

    // Highest per-sample allocation rate: each run's bytes divided by the period it was observed in,
    // floored to MinSampleRatePeriodMs so a gap that is tiny or TickCount64-quantized cannot inflate
    // a single run into an absurd rate. A run can therefore claim at most the rate it would have if
    // it repeated every MinSampleRatePeriodMs.
    private double ComputeMaxSampleRate(int count)
    {
        double best = 0;
        for (int s = 1; s < count; s++)
        {
            long period = _tsScratch[s] - _tsScratch[s - 1];
            if (period < MinSampleRatePeriodMs)
                period = MinSampleRatePeriodMs;
            double rate = _bytesScratch[s] * 1000.0 / period;
            if (rate > best)
                best = rate;
        }
        return best;
    }

    private void Expire(long now)
    {
        while (_samples.Count > 0 && now - _samples.Peek().TimestampMs > ExpiryMs)
            _samples.Dequeue();
    }
}

internal readonly record struct AllocationSampleStats(
    double LastBytesPerRun,
    double AllocPerSecond,
    double AvgBytesPerRun,
    double MaxBytesPerRun,
    long SampleCount,
    double AvgPeriodMs,
    double MaxAllocPerSecond);
