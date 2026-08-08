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

        private static ClickSettings TestClickSettings()
            => new()
            {
                MechanicPriorityIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                IgnoreDistanceMechanicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                IgnoreDistanceWithinByMechanicId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                MechanicPriorityDistancePenalty = 0,
            };

        private static LabelOnGround CreateOpaqueLabel(long address = 0)
        {
            LabelOnGround label = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
            if (address != 0)
                SetLabelAddress(label, address);
            return label;
        }

        private static void SetLabelAddress(LabelOnGround label, long address)
        {
            System.Reflection.PropertyInfo addressProperty = typeof(RemoteMemoryObject).GetProperty(
                nameof(RemoteMemoryObject.Address),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!;
            addressProperty!.SetValue(label, address);
        }

        [TestMethod]
        public void GetNextLabelToClick_ReusesCache_WhenLabelsReferenceUnchanged()
        {
            LabelOnGround label = CreateOpaqueLabel();
            int scanCount = 0;
            var service = new LabelSelectionService(new LabelSelectionServiceDependencies(
                GameController: null,
                CreateClickSettings: static _ => TestClickSettings(),
                ShouldCaptureLabelDebug: static () => false,
                PublishLabelDebugStage: static _ => { },
                TryBuildLabelCandidate: (LabelOnGround candidate, ClickSettings _, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                {
                    scanCount++;
                    item = candidate == label ? EntityProbeFactory.Create() : null;
                    mechanicId = candidate == label ? MechanicIds.Items : null;
                    rejectReason = LabelCandidateRejectReason.None;
                    return candidate == label;
                },
                GetMechanicIdForLabelCore: static _ => null));

            IReadOnlyList<LabelOnGround> labels = [label];

            LabelOnGround? first = service.GetNextLabelToClick(labels, 0, 10);
            LabelOnGround? second = service.GetNextLabelToClick(labels, 0, 10);
            LabelOnGround? third = service.GetNextLabelToClick(labels, 0, 10);

            first.Should().BeSameAs(label);
            second.Should().BeSameAs(label);
            third.Should().BeSameAs(label);
            scanCount.Should().Be(1, "the full scan must run once per labels reference, then be cached");
        }

        [TestMethod]
        public void GetNextLabelToClick_Invalidates_WhenLabelsReferenceChanges()
        {
            LabelOnGround firstLabel = CreateOpaqueLabel(address: 0x1000);
            LabelOnGround secondLabel = CreateOpaqueLabel(address: 0x2000);
            int scanCount = 0;
            var service = new LabelSelectionService(new LabelSelectionServiceDependencies(
                GameController: null,
                CreateClickSettings: static _ => TestClickSettings(),
                ShouldCaptureLabelDebug: static () => false,
                PublishLabelDebugStage: static _ => { },
                TryBuildLabelCandidate: (LabelOnGround candidate, ClickSettings _, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                {
                    scanCount++;
                    item = EntityProbeFactory.Create();
                    mechanicId = MechanicIds.Items;
                    rejectReason = LabelCandidateRejectReason.None;
                    return true;
                },
                GetMechanicIdForLabelCore: static _ => null));

            IReadOnlyList<LabelOnGround> firstList = [firstLabel];
            IReadOnlyList<LabelOnGround> secondList = [secondLabel];

            LabelOnGround? a = service.GetNextLabelToClick(firstList, 0, 10);
            LabelOnGround? b = service.GetNextLabelToClick(secondList, 0, 10);

            a.Should().BeSameAs(firstLabel);
            b.Should().BeSameAs(secondLabel);
            scanCount.Should().Be(2, "a new labels reference must invalidate the cache and re-scan");
        }

        [TestMethod]
        public void GetNextLabelToClick_DifferentRange_RerunsSelectionScan()
        {
            LabelOnGround label = CreateOpaqueLabel();
            int scanCount = 0;
            var service = new LabelSelectionService(new LabelSelectionServiceDependencies(
                GameController: null,
                CreateClickSettings: _ =>
                {
                    scanCount++;
                    return TestClickSettings();
                },
                ShouldCaptureLabelDebug: static () => false,
                PublishLabelDebugStage: static _ => { },
                TryBuildLabelCandidate: static (LabelOnGround candidate, ClickSettings _, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                {
                    item = EntityProbeFactory.Create();
                    mechanicId = MechanicIds.Items;
                    rejectReason = LabelCandidateRejectReason.None;
                    return true;
                },
                GetMechanicIdForLabelCore: static _ => null));

            IReadOnlyList<LabelOnGround> labels = [label];

            service.GetNextLabelToClick(labels, 0, 10);
            service.GetNextLabelToClick(labels, 0, 5);

            scanCount.Should().Be(2, "a different query range must re-run the selection scan");
        }
    }
}