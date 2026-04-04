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

        private static LabelSelectionScanEngine CreateEngine(
            ILabelInteractionPort? labelInteractionPort = null,
            Func<LabelOnGround, bool>? shouldSuppressLeverClick = null)
        {
            GameController gameController = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            var settings = new ClickItSettings();
            settings.AvoidOverlappingLabelClickPoints.Value = false;
            var labelClickPointResolver = new LabelClickPointResolver(settings);
            var mechanicPriorityContextProvider = new MechanicPriorityContextProvider(settings, new MechanicPrioritySnapshotService());
            var labelInteraction = ClickTestServiceFactory.CreateLabelInteractionService(gameController: gameController);

            return new LabelSelectionScanEngine(new LabelSelectionScanEngineDependencies(
                gameController,
                labelInteractionPort ?? new FakeLabelInteractionPort(),
                labelClickPointResolver,
                ShouldSuppressLeverClick: shouldSuppressLeverClick ?? (_ => false),
                ShouldSuppressInactiveUltimatumLabel: static _ => false,
                labelInteraction,
                mechanicPriorityContextProvider,
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(),
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