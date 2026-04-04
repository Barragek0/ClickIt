namespace ClickIt.Tests.Features.Labels.Application
{
    [TestClass]
    public class LabelFilterPortTelemetryTests
    {
        [TestMethod]
        public void GetVisibleLabelCounts_ReturnsUnavailable_WhenGameControllerMissing()
        {
            var settings = new ClickItSettings();
            var labelFilterPort = new LabelFilterPort(
                settings,
                new EssenceService(settings),
                new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { }),
                gameController: null);

            (bool labelsAvailable, int totalVisibleLabels, int validVisibleLabels) = labelFilterPort.GetVisibleLabelCounts();

            labelsAvailable.Should().BeFalse();
            totalVisibleLabels.Should().Be(0);
            validVisibleLabels.Should().Be(0);
        }
    }
}