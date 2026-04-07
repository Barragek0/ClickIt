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

        [TestMethod]
        public void TryResolveEvaluatedCandidates_ReturnsFalse_WhenCandidatesMissing()
        {
            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(null, out LabelOnGround? selectedLabel, out string? mechanicId);

            resolved.Should().BeFalse();
            selectedLabel.Should().BeNull();
            mechanicId.Should().BeNull();
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_PicksLowerLabelRectScore_WhenMultipleHoveredLabelsMatch()
        {
            LabelOnGround farther = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            LabelOnGround closer = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(farther, "far", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 64f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(closer, "close", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 9f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(closer);
            mechanicId.Should().Be("close");
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_UsesGroundProjectionFallback_WhenCursorMissesLabelRect()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(label, "projected", IsSuppressed: false, CursorInsideLabelRect: false, LabelRectScore: float.MaxValue, CursorNearGroundProjection: true, GroundProjectionScore: 16f)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(label);
            mechanicId.Should().Be("projected");
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_PrefersProjectionScore_WhenItBeatsRectScore()
        {
            LabelOnGround projectedWinner = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            LabelOnGround rectOnlyCandidate = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(projectedWinner, "projection", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 49f, CursorNearGroundProjection: true, GroundProjectionScore: 4f),
                    new ManualCursorEvaluatedCandidate(rectOnlyCandidate, "rect", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 9f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(projectedWinner);
            mechanicId.Should().Be("projection");
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_SkipsSuppressedBlankAndNonHoveredCandidates()
        {
            LabelOnGround valid = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(ExileCoreOpaqueFactory.CreateOpaqueLabel(), "suppressed", IsSuppressed: true, CursorInsideLabelRect: true, LabelRectScore: 1f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(ExileCoreOpaqueFactory.CreateOpaqueLabel(), "   ", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 1f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(ExileCoreOpaqueFactory.CreateOpaqueLabel(), "not-hovered", IsSuppressed: false, CursorInsideLabelRect: false, LabelRectScore: float.MaxValue, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(valid, "valid", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 25f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(valid);
            mechanicId.Should().Be("valid");
        }

        [TestMethod]
        public void TryResolveEvaluatedCandidates_KeepsFirstCandidate_WhenScoresTie()
        {
            LabelOnGround first = ExileCoreOpaqueFactory.CreateOpaqueLabel();
            LabelOnGround second = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            bool resolved = ManualCursorLabelSelector.TryResolveEvaluatedCandidates(
                [
                    new ManualCursorEvaluatedCandidate(first, "first", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 16f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue),
                    new ManualCursorEvaluatedCandidate(second, "second", IsSuppressed: false, CursorInsideLabelRect: true, LabelRectScore: 16f, CursorNearGroundProjection: false, GroundProjectionScore: float.MaxValue)
                ],
                out LabelOnGround? selectedLabel,
                out string? mechanicId);

            resolved.Should().BeTrue();
            selectedLabel.Should().BeSameAs(first);
            mechanicId.Should().Be("first");
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