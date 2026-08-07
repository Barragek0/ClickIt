namespace ClickIt.Tests.Features.Observability.Performance
{
    [TestClass]
    public class GcAllocationMetricsStoreTests
    {
        [TestMethod]
        public void Record_UnknownSection_IsIgnored()
        {
            var store = new GcAllocationMetricsStore();

            store.Record(ProcessingSection.Unknown, 1000);

            store.GetStats(ProcessingSection.Unknown).SampleCount.Should().Be(0);
        }

        [TestMethod]
        public void Record_NegativeBytes_IsIgnored()
        {
            var store = new GcAllocationMetricsStore();

            store.Record(ProcessingSection.Label, -1);

            store.GetStats(ProcessingSection.Label).SampleCount.Should().Be(0);
        }

        [TestMethod]
        public void Record_AndGetStats_ReportPerRunBytes()
        {
            var store = new GcAllocationMetricsStore();

            store.Record(ProcessingSection.Blight, 2000);
            store.Record(ProcessingSection.Blight, 4000);

            GcAllocationSnapshot stats = store.GetStats(ProcessingSection.Blight);

            stats.SampleCount.Should().Be(2);
            stats.AvgBytesPerRun.Should().Be(3000);
            stats.MaxBytesPerRun.Should().Be(4000);
            store.GetStats(ProcessingSection.Altar).SampleCount.Should().Be(0);
        }

        [TestMethod]
        public void Record_TracksLastBytesAndAveragePeriod()
        {
            var store = new GcAllocationMetricsStore();

            store.Record(ProcessingSection.Blight, 2000);
            Thread.Sleep(80);
            store.Record(ProcessingSection.Blight, 6000);

            GcAllocationSnapshot stats = store.GetStats(ProcessingSection.Blight);

            stats.LastBytesPerRun.Should().Be(6000);
            stats.AvgPeriodMs.Should().BeGreaterThan(0);
            stats.AvgPeriodMs.Should().BeLessThan(500);
        }

        [TestMethod]
        public void AllocationRate_IsZero_UntilSecondSampleProvidesPeriod()
        {
            var store = new GcAllocationMetricsStore();

            store.Record(ProcessingSection.Label, 1000);

            store.GetStats(ProcessingSection.Label).AllocPerSecond.Should().Be(0);
        }

        [TestMethod]
        public void AllocationRate_ScalesByRunPeriod()
        {
            var store = new GcAllocationMetricsStore();

            // 1000 bytes per run, recorded ~100ms apart -> ~10,000 bytes/s. TickCount64's ~15ms
            // granularity widens the measured period, so assert a loose band instead of an exact value.
            store.Record(ProcessingSection.Label, 1000);
            Thread.Sleep(100);
            store.Record(ProcessingSection.Label, 1000);

            GcAllocationSnapshot stats = store.GetStats(ProcessingSection.Label);
            stats.AllocPerSecond.Should().BeGreaterThan(5000);
            stats.AllocPerSecond.Should().BeLessThan(20000);
        }

        [TestMethod]
        public void Record_EverySection_IsTrackedIndependently()
        {
            var store = new GcAllocationMetricsStore();

            foreach (ProcessingSection section in Enum.GetValues<ProcessingSection>())
            {
                if (section == ProcessingSection.Unknown)
                    continue;
                store.Record(section, 512);
            }

            foreach (ProcessingSection section in Enum.GetValues<ProcessingSection>())
            {
                if (section == ProcessingSection.Unknown)
                    continue;
                store.GetStats(section).SampleCount.Should().Be(1, $"{section} should have one sample");
            }
        }
    }
}
