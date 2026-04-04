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

        [TestMethod]
        public void PublishClickDebugSnapshot_DoesNothing_WhenDebugCaptureDisabled()
        {
            bool published = false;
            var service = new ClickDebugPublicationService(new ClickDebugPublicationServiceDependencies(
                GameController: null!,
                ShouldCaptureClickDebug: static () => false,
                SetLatestClickDebug: _ => published = true,
                IsClickableInEitherSpace: static (_, _) => false,
                IsInsideWindowInEitherSpace: static _ => false));

            service.PublishClickDebugSnapshot(new ClickDebugSnapshot(
                HasData: true,
                Stage: "ProbeResolved",
                MechanicId: "settlers",
                EntityPath: "Metadata/Test",
                Distance: 10,
                WorldScreenRaw: default,
                WorldScreenAbsolute: default,
                ResolvedClickPoint: default,
                Resolved: true,
                CenterInWindow: false,
                CenterClickable: false,
                ResolvedInWindow: false,
                ResolvedClickable: false,
                Notes: "test",
                Sequence: 0,
                TimestampMs: 1));

            published.Should().BeFalse();
        }

        [TestMethod]
        public void PublishSettlersClickDebugSnapshot_PublishesResolvedWindowFlags()
        {
            ClickDebugSnapshot? snapshot = null;
            var service = new ClickDebugPublicationService(new ClickDebugPublicationServiceDependencies(
                GameController: null!,
                ShouldCaptureClickDebug: static () => true,
                SetLatestClickDebug: value => snapshot = value,
                IsClickableInEitherSpace: static (point, _) => point.X >= 10,
                IsInsideWindowInEitherSpace: static point => point.Y >= 20));

            service.PublishSettlersClickDebugSnapshot(
                stage: "ProbeResolved",
                mechanicId: "settlers-ore",
                entityPath: "Metadata/Test",
                distance: 25f,
                worldScreenRaw: new Vector2(3, 4),
                worldScreenAbsolute: new Vector2(15, 25),
                resolvedClickPoint: new Vector2(12, 22),
                resolved: true,
                notes: "Resolved nearby clickable point");

            snapshot.Should().NotBeNull();
            snapshot!.Stage.Should().Be("ProbeResolved");
            snapshot.MechanicId.Should().Be("settlers-ore");
            snapshot.CenterInWindow.Should().BeTrue();
            snapshot.CenterClickable.Should().BeTrue();
            snapshot.ResolvedInWindow.Should().BeTrue();
            snapshot.ResolvedClickable.Should().BeTrue();
        }
    }
}