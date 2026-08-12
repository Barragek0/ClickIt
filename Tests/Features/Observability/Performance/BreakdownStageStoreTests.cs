namespace ClickIt.Tests.Features.Observability.Performance;

[TestClass]
public class BreakdownStageStoreTests
{
    [TestMethod]
    public void Record_AndGetStats_ReportPerStageBytesAndTime()
    {
        var store = new BreakdownStageStore(trackTiming: true, "Terrain", "Goal", "AStar");

        Span<long> bytes = stackalloc long[3];
        Span<double> ms = stackalloc double[3];
        bytes[0] = 1024; bytes[1] = 2048; bytes[2] = 4096;
        ms[0] = 1.5; ms[1] = 2.5; ms[2] = 3.5;
        store.Record(bytes, ms);
        bytes[0] = 2048; bytes[1] = 1024; bytes[2] = 8192;
        ms[0] = 2.0; ms[1] = 1.0; ms[2] = 4.0;
        store.Record(bytes, ms);

        BreakdownStats stats = store.GetStats();

        stats.SampleCount.Should().Be(2);
        stats.Stages.Should().HaveCount(3);
        stats.Stages[0].Name.Should().Be("Terrain");
        stats.Stages[0].Allocation.AvgBytesPerRun.Should().Be(1536);
        stats.Stages[0].Allocation.MaxBytesPerRun.Should().Be(2048);
        stats.Stages[0].Allocation.LastBytesPerRun.Should().Be(2048);
        stats.Stages[1].Name.Should().Be("Goal");
        stats.Stages[1].Allocation.AvgBytesPerRun.Should().Be(1536);
        stats.Stages[2].Name.Should().Be("AStar");
        stats.Stages[2].Allocation.AvgBytesPerRun.Should().Be(6144);
        stats.Stages[2].Allocation.MaxBytesPerRun.Should().Be(8192);
        stats.Stages[2].Time.LastMs.Should().Be(4.0);
        stats.Stages[2].Time.AvgMs.Should().Be(3.75);
        stats.Stages[2].Time.MaxMs.Should().Be(4.0);
    }

    [TestMethod]
    public void Record_IgnoresExtraStageValuesBeyondRegisteredCount()
    {
        var store = new BreakdownStageStore(trackTiming: true, "Terrain", "Goal");

        Span<long> bytes = stackalloc long[3];
        Span<double> ms = stackalloc double[3];
        bytes[0] = 100; bytes[1] = 200; bytes[2] = 999999;
        ms[0] = 0.5; ms[1] = 0.6; ms[2] = 999;
        store.Record(bytes, ms);

        BreakdownStats stats = store.GetStats();

        stats.Stages.Should().HaveCount(2);
        stats.Stages[0].Allocation.AvgBytesPerRun.Should().Be(100);
        stats.Stages[1].Allocation.AvgBytesPerRun.Should().Be(200);
    }

    [TestMethod]
    public void GetStats_NoSamples_ReturnsZeroedStages()
    {
        var store = new BreakdownStageStore(trackTiming: true, "Terrain", "Goal");

        BreakdownStats stats = store.GetStats();

        stats.SampleCount.Should().Be(0);
        stats.Stages.Should().HaveCount(2);
        stats.Stages[0].Allocation.AvgBytesPerRun.Should().Be(0);
        stats.Stages[0].Time.AvgMs.Should().Be(0);
    }

    [TestMethod]
    public void GetStats_WithoutTiming_ReturnsDefaultTimeSnapshots()
    {
        var store = new BreakdownStageStore(trackTiming: false, "Terrain");

        Span<long> bytes = stackalloc long[1];
        bytes[0] = 512;
        store.Record(bytes, default);

        BreakdownStats stats = store.GetStats();

        stats.Stages[0].Allocation.AvgBytesPerRun.Should().Be(512);
        stats.Stages[0].Time.AvgMs.Should().Be(0);
    }

    [TestMethod]
    public void RecordStage_UpdatesOnlyTheTargetStage_NotSiblings()
    {
        var store = new BreakdownStageStore(trackTiming: true, "Entities", "Foundations", "Coverage", "Executor");

        // Refresh-style full record into stages 0-2.
        Span<long> bytes = stackalloc long[3];
        Span<double> ms = stackalloc double[3];
        bytes[0] = 100; bytes[1] = 200; bytes[2] = 300;
        ms[0] = 1.0; ms[1] = 2.0; ms[2] = 3.0;
        store.Record(bytes, ms);

        // Executor-style single-stage record into stage 3 (runs on a different thread/cadence).
        store.RecordStage(3, 4096, 7.5);

        BreakdownStats stats = store.GetStats();

        stats.SampleCount.Should().Be(2);
        stats.Stages.Should().HaveCount(4);
        stats.Stages[3].Name.Should().Be("Executor");
        stats.Stages[3].Allocation.AvgBytesPerRun.Should().Be(4096);
        stats.Stages[3].Allocation.MaxBytesPerRun.Should().Be(4096);
        stats.Stages[3].Time.AvgMs.Should().Be(7.5);
        stats.Stages[3].Time.LastMs.Should().Be(7.5);
        // Sibling stages must be untouched by the executor record.
        stats.Stages[0].Allocation.AvgBytesPerRun.Should().Be(100);
        stats.Stages[1].Allocation.AvgBytesPerRun.Should().Be(200);
        stats.Stages[2].Allocation.AvgBytesPerRun.Should().Be(300);
        stats.Stages[0].Time.AvgMs.Should().Be(1.0);
        stats.Stages[1].Time.AvgMs.Should().Be(2.0);
        stats.Stages[2].Time.AvgMs.Should().Be(3.0);
    }

    [TestMethod]
    public void RecordStage_OutOfRangeIndex_IsIgnored()
    {
        var store = new BreakdownStageStore(trackTiming: true, "Entities", "Foundations");

        store.RecordStage(5, 123, 1.0);
        store.RecordStage(-1, 123, 1.0);

        BreakdownStats stats = store.GetStats();
        stats.SampleCount.Should().Be(0);
        stats.Stages[0].Allocation.AvgBytesPerRun.Should().Be(0);
        stats.Stages[1].Allocation.AvgBytesPerRun.Should().Be(0);
    }
}
