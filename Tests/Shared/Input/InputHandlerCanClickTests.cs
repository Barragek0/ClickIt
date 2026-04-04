namespace ClickIt.Tests.Shared.Input
{
    [TestClass]
    public class InputHandlerCanClickTests
    {
        [TestMethod]
        public void CanClick_ReturnsFalse_WhenGameControllerIsNull()
        {
            var settings = new ClickItSettings();
            var handler = new InputHandler(settings);

            handler.CanClick(null!).Should().BeFalse();
        }

        [TestMethod]
        public void GetCanClickFailureReason_ReturnsDefault_WhenStateUnavailable()
        {
            var settings = new ClickItSettings();
            var handler = new InputHandler(settings);

            string reason = handler.GetCanClickFailureReason(null!);

            reason.Should().NotBeNullOrWhiteSpace();
        }
    }
}
