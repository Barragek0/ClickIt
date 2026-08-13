namespace ClickIt.Tests.Features.Observability.Performance;

[TestClass]
public class AllocationSampleWindowTests
{
    private sealed class FakeClock
    {
        public long Now { get; set; }
    }

    [TestMethod]
    public void Stats_Empty_ReturnsZeros()
    {
        var window = new AllocationSampleWindow(nowProvider: () => 0);

        AllocationSampleStats stats = window.Stats;
        stats.SampleCount.Should().Be(0);
        stats.LastBytesPerRun.Should().Be(0);
        stats.AvgBytesPerRun.Should().Be(0);
        stats.MaxBytesPerRun.Should().Be(0);
        stats.AllocPerSecond.Should().Be(0);
        stats.AvgPeriodMs.Should().Be(0);
        stats.MaxAllocPerSecond.Should().Be(0);
    }

    [TestMethod]
    public void Average_CoversOnlyTheMostRecentFiftySamples()
    {
        var clock = new FakeClock { Now = 0 };
        var window = new AllocationSampleWindow(nowProvider: () => clock.Now);

        for (int i = 0; i < 60; i++)
        {
            clock.Now += 16;
            window.Record(1000);
        }
        window.Record(2000);

        AllocationSampleStats stats = window.Stats;
        stats.LastBytesPerRun.Should().Be(2000);
        stats.AvgBytesPerRun.Should().BeApproximately((49 * 1000.0 + 2000) / 50, 0.001);
        stats.MaxBytesPerRun.Should().Be(2000, "max still covers the full live window");
        stats.SampleCount.Should().Be(61);
    }

    [TestMethod]
    public void MaxAllocPerSecond_DoesNotExplode_OnSubMillisecondGap()
    {
        // A normal ~16ms cadence with one big 500KB rescan that lands 1ms after the previous run. The per-sample rate floors the observation period to 50ms, so the run claims at most 500KB / 50ms = 10MB/s instead of extrapolating 500KB / 1ms = 500MB/s.
        var clock = new FakeClock { Now = 0 };
        var window = new AllocationSampleWindow(nowProvider: () => clock.Now);

        for (int i = 0; i < 60; i++)
        {
            clock.Now += 16;
            window.Record(10 * 1024);
        }
        clock.Now += 1;
        window.Record(500 * 1024);

        AllocationSampleStats stats = window.Stats;
        stats.MaxAllocPerSecond.Should().BeApproximately(500 * 1024 * 1000.0 / 50, 200_000);
        stats.MaxAllocPerSecond.Should().BeLessThan(100 * 1024 * 1024, "must not extrapolate a near-1ms gap to ~500MB/s");
    }

    [TestMethod]
    public void MaxAllocPerSecond_IsTheHighestPerSampleRate()
    {
        // Different-sized runs at the same cadence: the max is the fastest single run's rate.
        var clock = new FakeClock { Now = 0 };
        var window = new AllocationSampleWindow(nowProvider: () => clock.Now);

        for (int i = 0; i < 10; i++)
        {
            clock.Now += 100;
            window.Record(1000);
        }
        clock.Now += 100;
        window.Record(5000);

        AllocationSampleStats stats = window.Stats;
        // 5000 bytes over a real 100ms period = 50,000 bytes/s peak.
        stats.MaxAllocPerSecond.Should().BeApproximately(50_000, 1_000);
    }

    [TestMethod]
    public void MaxAllocPerSecond_SingleSample_IsZero()
    {
        var window = new AllocationSampleWindow(nowProvider: () => 0);
        window.Record(1024);

        window.Stats.MaxAllocPerSecond.Should().Be(0, "a single sample has no period to derive a rate from");
    }

    [TestMethod]
    public void AllocPerSecond_ScalesByAverageRunPeriod()
    {
        var clock = new FakeClock { Now = 0 };
        var window = new AllocationSampleWindow(nowProvider: () => clock.Now);

        // 1000 bytes per run every 100ms -> ~10,000 bytes/s average.
        for (int i = 0; i < 10; i++)
        {
            clock.Now += 100;
            window.Record(1000);
        }

        AllocationSampleStats stats = window.Stats;
        stats.AllocPerSecond.Should().BeApproximately(10_000, 2_000);
        stats.MaxAllocPerSecond.Should().BeApproximately(10_000, 2_000);
    }
}
