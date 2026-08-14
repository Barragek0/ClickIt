namespace ClickIt.Tests.Features.Observability.Performance;

[TestClass]
public class IntervalTimingStoreTests
{
    [TestMethod]
    public void Mark_RecordsDeltaBetweenConsecutiveMarks()
    {
        var store = new IntervalTimingStore();

        store.Mark(IntervalKind.Click);
        Thread.Sleep(25);
        store.Mark(IntervalKind.Click);

        IReadOnlyDictionary<IntervalKind, IntervalTimingSnapshot> snapshots = store.GetSnapshots();
        snapshots.Should().ContainKey(IntervalKind.Click);
        snapshots[IntervalKind.Click].SampleCount.Should().Be(1);
        snapshots[IntervalKind.Click].LastMs.Should().BeGreaterThanOrEqualTo(10);
        snapshots[IntervalKind.Click].LastMs.Should().BeLessThan(1000);
    }

    [TestMethod]
    public void Mark_RequiresTwoMarks_ForSample()
    {
        var store = new IntervalTimingStore();

        store.Mark(IntervalKind.Blight);

        store.GetSnapshots().Should().BeEmpty();
    }

    [TestMethod]
    public void Mark_KeepsKindsIndependent()
    {
        var store = new IntervalTimingStore();

        store.Mark(IntervalKind.Click);
        store.Mark(IntervalKind.Blight);
        store.Mark(IntervalKind.Click);

        IReadOnlyDictionary<IntervalKind, IntervalTimingSnapshot> snapshots = store.GetSnapshots();
        snapshots.Should().ContainKey(IntervalKind.Click);
        snapshots.Should().NotContainKey(IntervalKind.Blight);
        snapshots.Should().NotContainKey(IntervalKind.Label);
    }

    [TestMethod]
    public void Mark_AveragesRecentDeltas()
    {
        var store = new IntervalTimingStore();

        for (int i = 0; i < 5; i++)
        {
            store.Mark(IntervalKind.Click);
            Thread.Sleep(25);
        }

        IntervalTimingSnapshot s = store.GetSnapshots()[IntervalKind.Click];
        s.SampleCount.Should().Be(4);
        s.AvgMs.Should().BeGreaterThanOrEqualTo(10);
        s.MaxMs.Should().BeGreaterThanOrEqualTo(10);
    }

    [TestMethod]
    public void Mark_DoesNotRecordStaleGap_WhenDeltaExceedsFloor()
    {
        long now = 100_000;
        var store = new IntervalTimingStore(() => now);

        store.Mark(IntervalKind.Click);
        now += 3000;
        store.Mark(IntervalKind.Click);

        store.GetSnapshots().Should().BeEmpty();
    }

    [TestMethod]
    public void Mark_DoesNotRecordStaleGap_WhenDeltaExceedsRelativeThreshold()
    {
        long now = 100_000;
        var store = new IntervalTimingStore(() => now);

        for (int i = 0; i < 3; i++)
        {
            store.Mark(IntervalKind.Click);
            now += 100;
        }
        now += 3000;
        store.Mark(IntervalKind.Click);

        IntervalTimingSnapshot s = store.GetSnapshots()[IntervalKind.Click];
        s.SampleCount.Should().Be(2);
    }

    [TestMethod]
    public void Mark_RecordsDelta_WithinStaleFloor()
    {
        long now = 100_000;
        var store = new IntervalTimingStore(() => now);

        store.Mark(IntervalKind.Click);
        now += 100;
        store.Mark(IntervalKind.Click);

        IntervalTimingSnapshot s = store.GetSnapshots()[IntervalKind.Click];
        s.SampleCount.Should().Be(1);
        s.LastMs.Should().BeApproximately(100, 0.01);
    }
}
