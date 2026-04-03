namespace ClickIt.Tests.UI
{
    [TestClass]
    public class DebugRendererLogicTests
    {
        [TestMethod]
        public void BuildClickSettingsDebugSnapshotLines_ReturnsExpectedStructuredLines()
        {
            var settings = new ClickItSettings();

            var lines = DebugRenderer.BuildClickSettingsDebugSnapshotLines(settings);

            lines.Should().HaveCount(5);
            lines[0].Should().Contain("hotkeyToggle:");
            lines[1].Should().Contain("freqTarget:");
            lines[2].Should().Contain("blockPanels:");
            lines[3].Should().Contain("walkOffscreen:");
            lines[4].Should().Contain("waitBasicChestDrops:");
        }

        [TestMethod]
        public void BuildClickFrequencyTargetDebugMetrics_UsesObservedInterval_WhenPositive()
        {
            var metrics = DebugRenderer.BuildClickFrequencyTargetDebugMetrics(
                clickTargetMs: 100,
                processingMs: 30,
                observedIntervalMs: 150);

            metrics.ClickDelayMs.Should().Be(70);
            metrics.ModeledTotalMs.Should().Be(100);
            metrics.ObservedTotalMs.Should().Be(150);
            metrics.SchedulerDeltaMs.Should().Be(50);
            metrics.TargetDeviationRatio.Should().Be(0.5);
        }

        [TestMethod]
        public void BuildClickFrequencyTargetDebugMetrics_FallsBackToModeled_WhenObservedMissing()
        {
            var metrics = DebugRenderer.BuildClickFrequencyTargetDebugMetrics(
                clickTargetMs: 120,
                processingMs: 20,
                observedIntervalMs: 0);

            metrics.ClickDelayMs.Should().Be(100);
            metrics.ModeledTotalMs.Should().Be(120);
            metrics.ObservedTotalMs.Should().Be(120);
            metrics.SchedulerDeltaMs.Should().Be(0);
            metrics.TargetDeviationRatio.Should().Be(0);
        }
    }
}