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

        [TestMethod]
        public void GetNextLabelToClick_PublishesSelectionLifecycle_WhenNoCandidateIsSelected()
        {
            List<LabelDebugEvent> events = [];
            LabelOnGround nullItemLabel = CreateOpaqueLabel();
            LabelOnGround noMechanicLabel = CreateOpaqueLabel();

            var rejections = new Dictionary<LabelOnGround, LabelCandidateRejectReason>
            {
                [nullItemLabel] = LabelCandidateRejectReason.NoMechanic,
                [noMechanicLabel] = LabelCandidateRejectReason.NoMechanic,
            };

            var service = new LabelSelectionService(new LabelSelectionServiceDependencies(
                GameController: null,
                CreateClickSettings: static _ => new ClickSettings(),
                ShouldCaptureLabelDebug: static () => true,
                PublishLabelDebugStage: events.Add,
                TryBuildLabelCandidate: (LabelOnGround label, ClickSettings _, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                {
                    item = null;
                    mechanicId = null;
                    rejectReason = rejections[label];
                    return false;
                },
                GetMechanicIdForLabelCore: static _ => null));

            LabelOnGround? selected = service.GetNextLabelToClick([nullItemLabel, noMechanicLabel], 0, 5);

            selected.Should().BeNull();
            events.Select(debugEvent => debugEvent.Stage).Should().ContainInOrder("SelectionRequested", "SelectionScanNone", "SelectionReturnedNone");

            LabelDebugEvent scanEvent = events.Single(debugEvent => debugEvent.Stage == "SelectionScanNone");
            scanEvent.ConsideredCandidates.Should().Be(2);
            scanEvent.NoMechanicRejected.Should().Be(2);
            scanEvent.TotalLabels.Should().Be(2);
        }

        [TestMethod]
        public void GetMechanicIdForLabel_ForwardsToCoreDelegate()
        {
            LabelOnGround label = CreateOpaqueLabel();
            var service = new LabelSelectionService(new LabelSelectionServiceDependencies(
                GameController: null,
                CreateClickSettings: static _ => new ClickSettings(),
                ShouldCaptureLabelDebug: static () => false,
                PublishLabelDebugStage: static _ => { },
                TryBuildLabelCandidate: static (LabelOnGround _, ClickSettings _, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                {
                    item = null;
                    mechanicId = null;
                    rejectReason = LabelCandidateRejectReason.None;
                    return false;
                },
                GetMechanicIdForLabelCore: current => current == label ? MechanicIds.Shrines : null));

            string? mechanicId = service.GetMechanicIdForLabel(label);

            mechanicId.Should().Be(MechanicIds.Shrines);
        }

        [TestMethod]
        public void GetNextLabelToClick_PublishesEmptyScan_WhenRequestedRangeExcludesAllLabels()
        {
            List<LabelDebugEvent> events = [];
            LabelOnGround label = CreateOpaqueLabel();
            var service = new LabelSelectionService(new LabelSelectionServiceDependencies(
                GameController: null,
                CreateClickSettings: static _ => new ClickSettings(),
                ShouldCaptureLabelDebug: static () => true,
                PublishLabelDebugStage: events.Add,
                TryBuildLabelCandidate: static (LabelOnGround _, ClickSettings _, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                {
                    item = null;
                    mechanicId = null;
                    rejectReason = LabelCandidateRejectReason.None;
                    return false;
                },
                GetMechanicIdForLabelCore: static _ => null));

            LabelOnGround? selected = service.GetNextLabelToClick([label], startIndex: 5, maxCount: 1);

            selected.Should().BeNull();
            events.Select(debugEvent => debugEvent.Stage).Should().ContainInOrder("SelectionRequested", "SelectionScanNone", "SelectionReturnedNone");

            LabelDebugEvent scanEvent = events.Single(debugEvent => debugEvent.Stage == "SelectionScanNone");
            scanEvent.ConsideredCandidates.Should().Be(0);
            scanEvent.TotalLabels.Should().Be(1);
        }

        private static LabelOnGround CreateOpaqueLabel()
            => (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
    }
}