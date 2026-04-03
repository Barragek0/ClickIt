namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ClickDebugPublicationServiceTests
    {
        [TestMethod]
        public void PublishClickFlowDebugStage_PublishesSnapshot_WhenDebugCaptureEnabled()
        {
            ClickDebugSnapshot? snapshot = null;
            var service = new ClickDebugPublicationService(new ClickDebugPublicationServiceDependencies(
                GameController: null!,
                ShouldCaptureClickDebug: static () => true,
                SetLatestClickDebug: value => snapshot = value,
                IsClickableInEitherSpace: static (_, _) => false,
                IsInsideWindowInEitherSpace: static _ => false));

            service.PublishClickFlowDebugStage("TickStart", "entered", "mechanic-a");

            snapshot.Should().NotBeNull();
            snapshot!.HasData.Should().BeTrue();
            snapshot.Stage.Should().Be("TickStart");
            snapshot.MechanicId.Should().Be("mechanic-a");
            snapshot.Notes.Should().Be("entered");
        }

        [TestMethod]
        public void PublishClickFlowDebugStage_DoesNothing_WhenDebugCaptureDisabled()
        {
            bool published = false;
            var service = new ClickDebugPublicationService(new ClickDebugPublicationServiceDependencies(
                GameController: null!,
                ShouldCaptureClickDebug: static () => false,
                SetLatestClickDebug: value => published = true,
                IsClickableInEitherSpace: static (_, _) => true,
                IsInsideWindowInEitherSpace: static _ => true));

            service.PublishClickFlowDebugStage("TickStart", "entered", null);

            published.Should().BeFalse();
        }
    }
}