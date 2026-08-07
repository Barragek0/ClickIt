namespace ClickIt.Tests.Features.Observability.Performance
{
    [TestClass]
    public class ProcessingSectionMetricsStoreTests
    {
        [TestMethod]
        public void Record_UnknownSection_IsIgnored()
        {
            var store = new ProcessingSectionMetricsStore();

            store.Record(ProcessingSection.Unknown, 5.0);

            store.GetStats(ProcessingSection.Unknown).SampleCount.Should().Be(0);
        }

        [TestMethod]
        public void Record_AndGetStats_RoundTripPerSection()
        {
            var store = new ProcessingSectionMetricsStore();

            store.Record(ProcessingSection.Blight, 3.0);
            store.Record(ProcessingSection.Blight, 7.0);
            store.Record(ProcessingSection.Label, 2.0);

            (double lastMs, double avgMs, double maxMs, long count, double _) = store.GetStats(ProcessingSection.Blight);
            lastMs.Should().Be(7.0);
            avgMs.Should().Be(5.0);
            maxMs.Should().Be(7.0);
            count.Should().Be(2);

            store.GetStats(ProcessingSection.Label).LastMs.Should().Be(2.0);
            store.GetStats(ProcessingSection.Altar).SampleCount.Should().Be(0);
        }

        [TestMethod]
        public void Record_ReportsAveragePeriodBetweenRuns()
        {
            var store = new ProcessingSectionMetricsStore();

            store.Record(ProcessingSection.Label, 1.0);
            Thread.Sleep(50);
            store.Record(ProcessingSection.Label, 1.0);

            (double lastMs, double avgMs, double maxMs, long count, double avgPeriod) = store.GetStats(ProcessingSection.Label);
            avgPeriod.Should().BeGreaterThanOrEqualTo(25);
            avgPeriod.Should().BeLessThan(200);
            lastMs.Should().Be(1.0);
            avgMs.Should().Be(1.0);
            count.Should().Be(2);
        }

        [TestMethod]
        public void Record_EverySection_IsTrackedIndependently()
        {
            var store = new ProcessingSectionMetricsStore();

            foreach (ProcessingSection section in Enum.GetValues<ProcessingSection>())
            {
                if (section == ProcessingSection.Unknown)
                    continue;
                store.Record(section, 1.0);
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
