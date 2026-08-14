namespace ClickIt.Tests.Features.Observability.Performance;

[TestClass]
public class ClickAllocationStoreTests
{
    [TestMethod]
    public void Record_AndGetStats_ReportPerStagePerRunBytes()
    {
        var store = new ClickAllocationStore();

        store.Record(new ClickAllocationBreakdown(
            ContextBytes: 2048, AcquireBytes: 1024 * 1024, RankBytes: 32768, ExecuteBytes: 65536, PostBytes: 1024, OtherBytes: 8192, TotalBytes: 2048 + 1024 * 1024 + 32768 + 65536 + 1024 + 8192));
        store.Record(new ClickAllocationBreakdown(
            ContextBytes: 4096, AcquireBytes: 2 * 1024 * 1024, RankBytes: 16384, ExecuteBytes: 131072, PostBytes: 0, OtherBytes: 16384, TotalBytes: 4096 + 2 * 1024 * 1024 + 16384 + 131072 + 16384));

        ClickAllocationStats stats = store.GetStats();

        stats.SampleCount.Should().Be(2);
        stats.Context.AvgBytesPerRun.Should().Be((2048 + 4096) / 2.0);
        stats.Acquire.AvgBytesPerRun.Should().Be((1024 * 1024 + 2 * 1024 * 1024) / 2.0);
        stats.Acquire.MaxBytesPerRun.Should().Be(2 * 1024 * 1024);
        stats.Acquire.LastBytesPerRun.Should().Be(2 * 1024 * 1024);
        stats.Rank.MaxBytesPerRun.Should().Be(32768);
        stats.Execute.AvgBytesPerRun.Should().Be((65536 + 131072) / 2.0);
        stats.Post.LastBytesPerRun.Should().Be(0);
        stats.Other.AvgBytesPerRun.Should().Be((8192 + 16384) / 2.0);
        stats.Other.MaxBytesPerRun.Should().Be(16384);
    }

    [TestMethod]
    public void Record_AndGetStats_ReportAltarAndOtherTimeStages()
    {
        var store = new ClickAllocationStore();

        store.Record(new ClickAllocationBreakdown(
            ContextBytes: 0, AcquireBytes: 0, RankBytes: 0, ExecuteBytes: 0, PostBytes: 0,
            AltarBytes: 8 * 1024 * 1024, AltarMs: 108.0, OtherMs: 2.5));
        store.Record(new ClickAllocationBreakdown(
            ContextBytes: 128, AcquireBytes: 256, RankBytes: 0, ExecuteBytes: 512, PostBytes: 0,
            OtherBytes: 64, TotalBytes: 128 + 256 + 512 + 64, OtherMs: 1.0));

        ClickAllocationStats stats = store.GetStats();

        stats.SampleCount.Should().Be(2);
        stats.Altar.AvgBytesPerRun.Should().Be(4 * 1024 * 1024);
        stats.AltarTime.AvgMs.Should().Be(54.0);
        stats.AltarTime.LastMs.Should().Be(0, "the second run has no altar work");
        stats.OtherTime.AvgMs.Should().Be(1.75);
        stats.OtherTime.LastMs.Should().Be(1.0);
        stats.Other.AvgBytesPerRun.Should().Be(32.0);
    }

    [TestMethod]
    public void GetStats_NoSamples_ReturnsEmptyStats()
    {
        var store = new ClickAllocationStore();

        ClickAllocationStats stats = store.GetStats();

        stats.SampleCount.Should().Be(0);
        stats.Execute.AvgBytesPerRun.Should().Be(0);
    }

    [TestMethod]
    public void MaxAllocPerSecond_RequiresPeriod_ThenReflectsFastestStageSample()
    {
        var store = new ClickAllocationStore();

        store.Record(new ClickAllocationBreakdown(ContextBytes: 1000, AcquireBytes: 0, RankBytes: 0, ExecuteBytes: 0, PostBytes: 0, OtherBytes: 0));
        store.GetStats().Context.MaxAllocPerSecond.Should().Be(0, "a single sample has no period to derive a rate from");

        Thread.Sleep(80);
        store.Record(new ClickAllocationBreakdown(ContextBytes: 5000, AcquireBytes: 0, RankBytes: 0, ExecuteBytes: 0, PostBytes: 0, OtherBytes: 0));

        ClickAllocationStats stats = store.GetStats();
        // The second 5000-byte sample over a real ~80ms period = ~62KB/s peak; keep the bound loose.
        stats.Context.MaxAllocPerSecond.Should().BeGreaterThan(20_000);
        stats.Rank.MaxAllocPerSecond.Should().Be(0, "stages without a second sample have no peak rate");
    }
}
