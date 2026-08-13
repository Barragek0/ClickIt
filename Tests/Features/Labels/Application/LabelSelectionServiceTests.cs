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

        [TestMethod]
        public void GetNextLabelToClick_ReusesPerLabelBuild_AcrossRanges_ButRereadsLiveDistance()
        {
            LabelOnGround nearLabel = CreateOpaqueLabel(address: 0x1000);
            LabelOnGround farLabel = CreateOpaqueLabel(address: 0x2000);
            EntityProbe nearProbe = (EntityProbe)EntityProbeFactory.Create(distancePlayer: 100f);
            EntityProbe farProbe = (EntityProbe)EntityProbeFactory.Create(distancePlayer: 50f);

            int buildCount = 0;
            var service = new LabelSelectionService(new LabelSelectionServiceDependencies(
                GameController: null,
                CreateClickSettings: static _ => TestClickSettings(),
                ShouldCaptureLabelDebug: static () => false,
                PublishLabelDebugStage: static _ => { },
                TryBuildLabelCandidate: (LabelOnGround candidate, ClickSettings _, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                {
                    buildCount++;
                    item = ReferenceEquals(candidate, nearLabel) ? nearProbe : farProbe;
                    mechanicId = MechanicIds.Items;
                    rejectReason = LabelCandidateRejectReason.None;
                    return true;
                },
                GetMechanicIdForLabelCore: static _ => null));

            IReadOnlyList<LabelOnGround> labels = [nearLabel, farLabel];

            // Both in range; farLabel is nearer (50 < 100) so it ranks first.
            LabelOnGround? first = service.GetNextLabelToClick(labels, 0, 10);
            first.Should().BeSameAs(farLabel);

            // The player closes in on nearLabel. The expensive per-label build must NOT re-run (same addresses), but the live distance must be re-read so the ranking flips to nearLabel.
            nearProbe.DistancePlayer = 10f;
            farProbe.DistancePlayer = 200f;

            LabelOnGround? second = service.GetNextLabelToClick(labels, 0, 5);
            second.Should().BeSameAs(nearLabel, "the selection must re-read the live distance instead of reusing the cached build");
            buildCount.Should().Be(2, "each label builds once; a different query range re-scans but the per-label build cache must be reused");
        }

        [TestMethod]
        public void GetNextLabelToClick_ReevaluatesOutOfDistanceRejection_WhenPlayerClosesIn()
        {
            LabelOnGround label = CreateOpaqueLabel(address: 0x1000);
            EntityProbe probe = (EntityProbe)EntityProbeFactory.Create(distancePlayer: 200f);

            var service = new LabelSelectionService(new LabelSelectionServiceDependencies(
                GameController: null,
                CreateClickSettings: static _ => TestClickSettings() with { ClickDistance = 100 },
                ShouldCaptureLabelDebug: static () => false,
                PublishLabelDebugStage: static _ => { },
                TryBuildLabelCandidate: (LabelOnGround candidate, ClickSettings _, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                {
                    item = probe;
                    mechanicId = MechanicIds.Items;
                    rejectReason = probe.DistancePlayer > 100f
                        ? LabelCandidateRejectReason.OutOfDistance
                        : LabelCandidateRejectReason.None;
                    return rejectReason == LabelCandidateRejectReason.None;
                },
                GetMechanicIdForLabelCore: static _ => null));

            // Far away: rejected as out of range, so nothing is selected.
            service.GetNextLabelToClick([label], 0, 10).Should().BeNull("the label is initially out of distance");

            // The player walks closer; the cached OutOfDistance rejection must be re-evaluated, not held for the rest of the 1s cache window.
            probe.DistancePlayer = 10f;

            service.GetNextLabelToClick([label], 0, 10).Should().BeSameAs(label,
                "an OutOfDistance rejection must be re-checked against the live distance when the player closes in");
        }

        [TestMethod]
        public void GetNextLabelToClick_ReevaluatesNullSelection_AfterCacheWindow()
        {
            // Regression guard: a stable label-list reference plus a cached null selection deadlocked item pickup (the scan never re-ran, so items that became clickable were never picked up). A stale null result must be re-evaluated after the cache window.
            LabelOnGround label = CreateOpaqueLabel(address: 0x1000);
            bool shouldSelect = false;
            int buildCount = 0;
            var service = new LabelSelectionService(new LabelSelectionServiceDependencies(
                GameController: null,
                CreateClickSettings: static _ => TestClickSettings(),
                ShouldCaptureLabelDebug: static () => false,
                PublishLabelDebugStage: static _ => { },
                TryBuildLabelCandidate: (LabelOnGround _, ClickSettings _, out Entity? item, out string? mechanicId, out LabelCandidateRejectReason rejectReason) =>
                {
                    buildCount++;
                    if (!shouldSelect)
                    {
                        item = null;
                        mechanicId = null;
                        rejectReason = LabelCandidateRejectReason.NullItem;
                        return false;
                    }

                    item = EntityProbeFactory.Create();
                    mechanicId = MechanicIds.Items;
                    rejectReason = LabelCandidateRejectReason.None;
                    return true;
                },
                GetMechanicIdForLabelCore: static _ => null));

            IReadOnlyList<LabelOnGround> labels = [label];

            // Initial scan rejects everything and caches the null result for this stable reference.
            service.GetNextLabelToClick(labels, 0, 10).Should().BeNull();
            int buildsAfterFirstScan = buildCount;

            // Within the cache window the null is served from cache (no re-scan).
            service.GetNextLabelToClick(labels, 0, 10).Should().BeNull();
            buildCount.Should().Be(buildsAfterFirstScan, "the cached null must be served within the window");

            // The label becomes selectable; after the window the selection must re-evaluate and pick it up instead of being pinned to the stale null result.
            shouldSelect = true;
            Thread.Sleep(300);

            service.GetNextLabelToClick(labels, 0, 10).Should().BeSameAs(label,
                "a stale null selection must be re-evaluated after the cache window");
            buildCount.Should().BeGreaterThan(buildsAfterFirstScan,
                "the re-scan must rebuild the previously NullItem-rejected label");
        }
    }
}
