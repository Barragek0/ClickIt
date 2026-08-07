namespace ClickIt.Tests.Features.Observability.Performance
{
    [TestClass]
    public class TimingChannelMetricsTrackerTests
    {
        [TestMethod]
        public void RenderTimingRecordsSnapshotAndStats()
        {
            var tracker = new TimingChannelMetricsTracker();

            tracker.StartRenderTiming();
            tracker.StopRenderTiming();

            tracker.GetTimingSampleCount(TimingChannel.Render).Should().Be(1);
            tracker.GetRenderTimingsSnapshot().Should().ContainSingle();
            tracker.GetRenderTimingStats().SampleCount.Should().Be(1);
            tracker.GetLastTiming(TimingChannel.Render).Should().BeGreaterThanOrEqualTo(0);
            tracker.GetAverageTiming(TimingChannel.Render).Should().BeGreaterThanOrEqualTo(0);
        }

        [TestMethod]
        public void CoroutineTimingTracksEachChannelIndependently()
        {
            var tracker = new TimingChannelMetricsTracker();

            tracker.StartCoroutineTiming(TimingChannel.Click);
            tracker.StopCoroutineTiming(TimingChannel.Click);
            tracker.StartCoroutineTiming("altar");
            tracker.StopCoroutineTiming("altar");

            tracker.GetTimingSampleCount(TimingChannel.Click).Should().Be(1);
            tracker.GetTimingSampleCount(TimingChannel.Altar).Should().Be(1);
            tracker.GetTimingSampleCount(TimingChannel.Flare).Should().Be(0);
            tracker.GetLastTiming("click").Should().BeGreaterThanOrEqualTo(0);
            tracker.GetMaxTiming("altar").Should().BeGreaterThanOrEqualTo(0);
        }

        [TestMethod]
        public void CoroutineTiming_TracksUltimatumAndLabelOverlayChannelsIndependently()
        {
            var tracker = new TimingChannelMetricsTracker();

            tracker.StartCoroutineTiming(TimingChannel.Ultimatum);
            tracker.StopCoroutineTiming(TimingChannel.Ultimatum);
            tracker.StartCoroutineTiming("labeloverlay");
            tracker.StopCoroutineTiming("labeloverlay");

            tracker.GetTimingSampleCount(TimingChannel.Ultimatum).Should().Be(1);
            tracker.GetTimingSampleCount(TimingChannel.LabelOverlay).Should().Be(1);
            tracker.GetTimingSampleCount(TimingChannel.Click).Should().Be(0);
            tracker.GetLastTiming("ultimatum").Should().BeGreaterThanOrEqualTo(0);
            tracker.GetMaxTiming(TimingChannel.LabelOverlay).Should().BeGreaterThanOrEqualTo(0);
        }

        [TestMethod]
        public void CoroutineTiming_RecordsRunPeriod_BetweenConsecutiveStops()
        {
            var tracker = new TimingChannelMetricsTracker();

            // First stop has no prior timestamp, so no period sample is recorded.
            tracker.StartCoroutineTiming(TimingChannel.LabelOverlay);
            tracker.StopCoroutineTiming(TimingChannel.LabelOverlay);
            tracker.GetAveragePeriod(TimingChannel.LabelOverlay).Should().Be(0);

            // Second stop records the wall-clock gap since the first stop.
            tracker.StartCoroutineTiming(TimingChannel.LabelOverlay);
            tracker.StopCoroutineTiming(TimingChannel.LabelOverlay);
            tracker.GetAveragePeriod(TimingChannel.LabelOverlay).Should().BeGreaterThanOrEqualTo(0);
            tracker.GetAveragePeriod("labeloverlay").Should().BeGreaterThanOrEqualTo(0);
        }

        [TestMethod]
        public void ClearResetsRecordedState()
        {
            var tracker = new TimingChannelMetricsTracker();

            tracker.StartRenderTiming();
            tracker.StopRenderTiming();
            tracker.StartCoroutineTiming(TimingChannel.Flare);
            tracker.StopCoroutineTiming(TimingChannel.Flare);

            tracker.Clear();

            tracker.GetTimingSampleCount(TimingChannel.Render).Should().Be(0);
            tracker.GetTimingSampleCount(TimingChannel.Flare).Should().Be(0);
            tracker.GetLastTiming(TimingChannel.Render).Should().Be(0);
            tracker.GetMaxTiming(TimingChannel.Flare).Should().Be(0);
            tracker.GetRenderTimingsSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void CalculateMax_ReturnsMaximumOfCurrentWindow()
        {
            var queue = new Queue<long>();
            queue.Enqueue(1);
            queue.Enqueue(5);
            queue.Enqueue(3);

            TimingChannelMetricsTracker.CalculateMax(queue).Should().Be(5);
        }

        [TestMethod]
        public void CalculateMax_ReturnsZero_WhenWindowEmpty()
        {
            TimingChannelMetricsTracker.CalculateMax(new Queue<long>()).Should().Be(0);
        }

        [TestMethod]
        public void GetMaxTiming_ReflectsRollingWindow_NotAllTimeSpikes()
        {
            var tracker = new TimingChannelMetricsTracker();

            // One thousand and one quick runs fill the 1000-sample window; the reported max must
            // come from the current window (all ~0ms here), not from any historical spike.
            for (int i = 0; i < 1001; i++)
            {
                tracker.StartCoroutineTiming(TimingChannel.Blight);
                tracker.StopCoroutineTiming(TimingChannel.Blight);
            }

            tracker.GetTimingSampleCount(TimingChannel.Blight).Should().Be(1000);
            tracker.GetMaxTiming(TimingChannel.Blight).Should().BeLessThanOrEqualTo(tracker.GetLastTiming(TimingChannel.Blight) + 1);
        }

        [TestMethod]
        public void SuccessfulClickTiming_AverageCoversWholeWindow()
        {
            var tracker = new TimingChannelMetricsTracker();

            for (int i = 0; i < 10; i++)
                tracker.RecordSuccessfulClickTiming(10);
            for (int i = 0; i < 20; i++)
                tracker.RecordSuccessfulClickTiming(1);

            tracker.GetAverageSuccessfulClickTiming().Should().BeApproximately(4.0, 0.001, "the average covers the whole 100-sample window");
        }

        [TestMethod]
        public void SuccessfulClickTiming_UsesBoundedAverageAndClearsWithTracker()
        {
            var tracker = new TimingChannelMetricsTracker();

            tracker.RecordSuccessfulClickTiming(10);
            tracker.RecordSuccessfulClickTiming(20);
            tracker.RecordSuccessfulClickTiming(30);

            tracker.GetAverageSuccessfulClickTiming().Should().Be(20);

            tracker.Clear();

            tracker.GetAverageSuccessfulClickTiming().Should().Be(0);
        }
    }
}