namespace ClickIt.Features.Observability.Performance;

// Rolling allocation samples share the same retained window for averages and maxima.
internal sealed class AllocationSampleWindow
{
    private const long ExpiryMs = 10_000;
    private const int MaxWindow = 10_000;
    private const long MinSampleRatePeriodMs = 50;

    private readonly Queue<(long TimestampMs, long Bytes)> _samples = new();
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

            long sum = 0;
            long max = 0;
            long periodTotal = 0;
            int periodCount = 0;
            for (int s = 0; s < count; s++)
            {
                if (_bytesScratch[s] > max)
                    max = _bytesScratch[s];
                sum += _bytesScratch[s];
                if (s > 0)
                {
                    periodTotal += SystemMath.Max(1, _tsScratch[s] - _tsScratch[s - 1]);
                    periodCount++;
                }
            }

            double avgBytes = sum / (double)count;
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

    // Highest per-sample allocation rate: each run's bytes divided by the period it was observed in, floored to MinSampleRatePeriodMs so a gap that is tiny or TickCount64-quantized cannot inflate a single run into an absurd rate. A run can therefore claim at most the rate it would have if it repeated every MinSampleRatePeriodMs.
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
