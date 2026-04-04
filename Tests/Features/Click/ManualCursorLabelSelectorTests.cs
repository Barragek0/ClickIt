namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class ManualCursorLabelSelectorTests
    {
        [TestMethod]
        public void TryResolveCandidate_ReturnsFalse_WhenLabelsMissing()
        {
            var selector = CreateSelector();

            bool resolved = selector.TryResolveCandidate(null, Vector2.Zero, Vector2.Zero, out LabelOnGround? selectedLabel, out string? mechanicId);

            resolved.Should().BeFalse();
            selectedLabel.Should().BeNull();
            mechanicId.Should().BeNull();
        }

        private static ManualCursorLabelSelector CreateSelector(
            Func<LabelOnGround?, string?>? getMechanicIdForLabel = null)
        {
            GameController gameController = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            var settings = new ClickItSettings();
            settings.AvoidOverlappingLabelClickPoints.Value = false;
            var runtimeState = new ClickRuntimeState();
            var pathfindingLabelSuppression = new PathfindingLabelSuppressionEvaluator(new PathfindingLabelSuppressionEvaluatorDependencies(
                settings,
                runtimeState));
            var labelClickPointResolver = new LabelClickPointResolver(settings);

            ILabelInteractionPort port = new StubLabelInteractionPort(getMechanicIdForLabel ?? (_ => null));

            return new ManualCursorLabelSelector(new ManualCursorLabelSelectorDependencies(
                gameController,
                port,
                pathfindingLabelSuppression,
                labelClickPointResolver));
        }

        private sealed class StubLabelInteractionPort(Func<LabelOnGround?, string?> getMechanicIdForLabel) : ILabelInteractionPort
        {
            public SelectionDebugSummary GetSelectionDebugSummary(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => default;

            public void LogSelectionDiagnostics(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
            {
            }

            public string? GetMechanicIdForLabel(LabelOnGround? label)
                => getMechanicIdForLabel(label);

            public LabelOnGround? GetNextLabelToClick(IReadOnlyList<LabelOnGround>? allLabels, int startIndex, int maxCount)
                => null;

            public bool ShouldCorruptEssence(LabelOnGround label)
                => false;
        }
    }
}