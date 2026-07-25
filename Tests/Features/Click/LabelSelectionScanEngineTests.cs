namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class LabelSelectionScanEngineTests
    {
        [TestMethod]
        public void ResolveNextLabelCandidate_ReturnsNull_WhenNoLabels()
        {
            var engine = CreateEngine();

            engine.ResolveNextLabelCandidate(null).Should().BeNull();
        }

        [TestMethod]
        public void ResolveNextLabelCandidate_ReturnsFirstUnsuppressedLabel()
        {
            LabelOnGround suppressed = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
            LabelOnGround selected = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
            IReadOnlyList<LabelOnGround> labels = [suppressed, selected];
            var port = new FakeLabelInteractionPort(
                getNextLabelToClick: (allLabels, startIndex, maxCount) => allLabels?[startIndex],
                getMechanicIdForLabel: _ => MechanicIds.Shrines);
            var engine = CreateEngine(
                labelInteractionPort: port,
                shouldSuppressLeverClick: label => ReferenceEquals(label, suppressed));

            engine.ResolveNextLabelCandidate(labels).Should().BeSameAs(selected);
        }

        [TestMethod]
        public void ResolveNextLabelCandidate_PublishesFindLabelNull_WhenPortReturnsNullImmediately()
        {
            ClickDebugSnapshot? latestSnapshot = null;
            IReadOnlyList<LabelOnGround> labels =
            [
                (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround))
            ];
            var engine = CreateEngine(
                labelInteractionPort: new FakeLabelInteractionPort(
                    getNextLabelToClick: static (_, _, _) => null),
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshot => latestSnapshot = snapshot));

            LabelOnGround? result = engine.ResolveNextLabelCandidate(labels);

            Assert.IsNull(result);

            latestSnapshot.Should().NotBeNull();
            latestSnapshot!.Stage.Should().Be("FindLabelNull");
            latestSnapshot.Notes.Should().Contain("range:0-");
        }

        [TestMethod]
        public void ResolveNextLabelCandidate_PublishesFindLabelExhausted_WhenEveryCandidateIsSuppressed()
        {
            ClickDebugSnapshot? latestSnapshot = null;
            LabelOnGround first = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
            LabelOnGround second = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
            IReadOnlyList<LabelOnGround> labels = [first, second];
            var engine = CreateEngine(
                labelInteractionPort: new FakeLabelInteractionPort(
                    getNextLabelToClick: (allLabels, startIndex, _) => allLabels?[startIndex]),
                shouldSuppressInactiveUltimatumLabel: static _ => true,
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshot => latestSnapshot = snapshot));

            LabelOnGround? result = engine.ResolveNextLabelCandidate(labels);

            Assert.IsNull(result);

            latestSnapshot.Should().NotBeNull();
            latestSnapshot!.Stage.Should().Be("FindLabelExhausted");
            latestSnapshot.Notes.Should().Contain("examined:2");
        }

        [TestMethod]
        public void ResolveNextLabelCandidate_ReturnsNull_WhenPortReturnsLabelOutsideRequestedRange()
        {
            ClickDebugSnapshot? latestSnapshot = null;
            LabelOnGround inRange = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
            LabelOnGround foreign = (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));
            IReadOnlyList<LabelOnGround> labels = [inRange];
            var engine = CreateEngine(
                labelInteractionPort: new FakeLabelInteractionPort(
                    getNextLabelToClick: (_, _, _) => foreign),
                shouldSuppressLeverClick: static _ => true,
                clickDebugPublisher: ClickTestDebugPublisherFactory.Create(
                    shouldCaptureClickDebug: static () => true,
                    setLatestClickDebug: snapshot => latestSnapshot = snapshot));

            LabelOnGround? result = engine.ResolveNextLabelCandidate(labels);

            Assert.IsNull(result);

            latestSnapshot.Should().NotBeNull();
            latestSnapshot!.Stage.Should().Be("FindLabelIndexMiss");
            latestSnapshot.Notes.Should().Contain("misses:1");
        }

        private static LabelSelectionScanEngine CreateEngine(
            ILabelInteractionPort? labelInteractionPort = null,
            Func<LabelOnGround, bool>? shouldSuppressLeverClick = null,
            Func<LabelOnGround, bool>? shouldSuppressInactiveUltimatumLabel = null,
            ClickDebugPublicationService? clickDebugPublisher = null)
        {
            GameController gameController = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            ILabelInteractionPort safeLabelInteractionPort = labelInteractionPort ?? new FakeLabelInteractionPort();
            var settings = new ClickItSettings();
            settings.AvoidOverlappingLabelClickPoints.Value = false;
            var labelClickPointResolver = new LabelClickPointResolver(settings);
            var mechanicPriorityContextProvider = new MechanicPriorityContextProvider(settings, new MechanicPrioritySnapshotService());
            var labelInteraction = ClickTestServiceFactory.CreateLabelInteractionService(
                gameController: gameController,
                labelInteractionPort: safeLabelInteractionPort);

            return new LabelSelectionScanEngine(new LabelSelectionScanEngineDependencies(
                gameController,
                safeLabelInteractionPort,
                labelClickPointResolver,
                ShouldSuppressLeverClick: shouldSuppressLeverClick ?? (_ => false),
                ShouldSuppressInactiveUltimatumLabel: shouldSuppressInactiveUltimatumLabel ?? (_ => false),
                labelInteraction,
                mechanicPriorityContextProvider,
                ClickDebugPublisher: clickDebugPublisher ?? ClickTestDebugPublisherFactory.Create(),
                DebugLog: static _ => { }));
        }

        private sealed class FakeLabelInteractionPort(
            Func<IReadOnlyList<LabelOnGround>?, int, int, LabelOnGround?>? getNextLabelToClick = null,
            Func<LabelOnGround?, string?>? getMechanicIdForLabel = null) : ILabelInteractionPort
        {
            public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            {
            }

            public string? GetMechanicIdForLabel(LabelOnGround? label)
                => getMechanicIdForLabel?.Invoke(label);

            public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => getNextLabelToClick?.Invoke(allLabels, startIndex, maxCount);

            public bool ShouldCorruptEssence(LabelOnGround label)
                => false;
        }
    }
}