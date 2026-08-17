namespace ClickIt.Tests.Features.Observability.Performance
{
    [TestClass]
    public class SectionMetricsStoreTests
    {
        [TestMethod]
        public void Record_And_GetStats_AreTrackedPerValue()
        {
            var store = new SectionMetricsStore<RenderSection>();
            store.Record(RenderSection.LazyMode, 1.0);
            store.Record(RenderSection.DebugOverlay, 5.0);
            store.Record(RenderSection.LazyMode, 2.0);

            (double last, double average, double max, long count) = store.GetStats(RenderSection.LazyMode);
            last.Should().Be(2.0);
            average.Should().Be(1.5);
            max.Should().Be(2.0);
            count.Should().Be(2);

            (double lastDebug, double averageDebug, double maxDebug, long countDebug) = store.GetStats(RenderSection.DebugOverlay);
            lastDebug.Should().Be(5.0);
            maxDebug.Should().Be(5.0);
            countDebug.Should().Be(1);
        }

        [TestMethod]
        public void GetStats_ReturnsZeros_ForNeverRecordedValue()
        {
            var store = new SectionMetricsStore<RenderSection>();

            (double last, double average, double max, long count) = store.GetStats(RenderSection.PerformanceOverlay);
            last.Should().Be(0);
            average.Should().Be(0);
            max.Should().Be(0);
            count.Should().Be(0);
        }

        [TestMethod]
        public void GetStats_IsPerValue_ForOutOfOrderEnumValues()
        {
            // RenderSection values are 0-14 contiguous but declared out of order (TextFlush=7, FrameFlush=8, HarvestOverlay=9...) - array indexing by the numeric value must keep them separate.
            var store = new SectionMetricsStore<RenderSection>();
            store.Record(RenderSection.TextFlush, 3.0);
            store.Record(RenderSection.FrameFlush, 4.0);
            store.Record(RenderSection.HarvestOverlay, 5.0);

            store.GetStats(RenderSection.TextFlush).SampleCount.Should().Be(1);
            store.GetStats(RenderSection.TextFlush).LastMs.Should().Be(3.0);
            store.GetStats(RenderSection.FrameFlush).LastMs.Should().Be(4.0);
            store.GetStats(RenderSection.HarvestOverlay).LastMs.Should().Be(5.0);
        }
    }
}
