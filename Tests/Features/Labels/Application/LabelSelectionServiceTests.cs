namespace ClickIt.Tests.Features.Labels.Application
{
    [TestClass]
    public class LabelSelectionServiceTests
    {
        [TestMethod]
        public void GetNextLabelToClick_PublishesNoLabelsDebugEvent_WhenInputIsNull()
        {
            LabelDebugEvent? publishedEvent = null;
            var service = new LabelSelectionService(new LabelSelectionServiceDependencies(
                GameController: null,
                CreateClickSettings: static _ => new ClickSettings(),
                ShouldCaptureLabelDebug: static () => true,
                PublishLabelDebugStage: debugEvent => publishedEvent = debugEvent,
                TryBuildLabelCandidate: static (LabelOnGround _, ClickSettings _, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                {
                    item = null;
                    mechanicId = null;
                    rejectReason = LabelCandidateRejectReason.None;
                    return false;
                },
                GetMechanicIdForLabelCore: static _ => null));

            var selected = service.GetNextLabelToClick(null, 0, 10);

            selected.Should().BeNull();
            publishedEvent.Should().NotBeNull();
            publishedEvent!.Stage.Should().Be("NoLabels");
            publishedEvent.TotalLabels.Should().Be(0);
        }
    }
}