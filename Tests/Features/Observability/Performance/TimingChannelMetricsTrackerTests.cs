using ClickIt.Features.Observability.Performance;
using ClickIt.Shared;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    }
}