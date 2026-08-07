namespace ClickIt.Tests.Features.Observability.Performance;

[TestClass]
public class LabelScanAllocationStoreTests
{
    [TestMethod]
    public void Record_AndGetStats_ReportPerStagePerRunBytes()
    {
        var store = new LabelScanAllocationStore();

        store.Record(new LabelScanAllocationBreakdown(
            ListReadBytes: 1024 * 1024, ListAllocBytes: 4096, ValidityBytes: 32768, SortBytes: 2048, TotalBytes: 1024 * 1024 + 4096 + 32768 + 2048));
        store.Record(new LabelScanAllocationBreakdown(
            ListReadBytes: 2 * 1024 * 1024, ListAllocBytes: 8192, ValidityBytes: 65536, SortBytes: 1024, TotalBytes: 2 * 1024 * 1024 + 8192 + 65536 + 1024));

        LabelScanAllocationStats stats = store.GetStats();

        stats.SampleCount.Should().Be(2);
        stats.ListRead.AvgBytesPerRun.Should().Be((1024 * 1024 + 2 * 1024 * 1024) / 2.0);
        stats.ListRead.MaxBytesPerRun.Should().Be(2 * 1024 * 1024);
        stats.ListRead.LastBytesPerRun.Should().Be(2 * 1024 * 1024);
        stats.ListAlloc.AvgBytesPerRun.Should().Be((4096 + 8192) / 2.0);
        stats.Validity.AvgBytesPerRun.Should().Be((32768 + 65536) / 2.0);
        stats.Sort.MaxBytesPerRun.Should().Be(2048);
        stats.Sort.LastBytesPerRun.Should().Be(1024);
    }

    [TestMethod]
    public void GetStats_NoSamples_ReturnsEmptyStats()
    {
        var store = new LabelScanAllocationStore();

        LabelScanAllocationStats stats = store.GetStats();

        stats.SampleCount.Should().Be(0);
        stats.ListRead.AvgBytesPerRun.Should().Be(0);
    }

    [TestMethod]
    public void Record_NegativeStageBytes_IsClampedToZero()
    {
        var store = new LabelScanAllocationStore();

        store.Record(new LabelScanAllocationBreakdown(ListReadBytes: -1, ListAllocBytes: 0, ValidityBytes: 0, SortBytes: 0, TotalBytes: 0));

        LabelScanAllocationStats stats = store.GetStats();

        stats.SampleCount.Should().Be(1);
        stats.ListRead.AvgBytesPerRun.Should().Be(0);
    }
}
