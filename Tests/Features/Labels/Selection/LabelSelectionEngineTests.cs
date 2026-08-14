namespace ClickIt.Tests.Features.Labels.Selection
{
    [TestClass]
    public class LabelSelectionEngineTests
    {
        [TestMethod]
        public void SelectNextLabelByPriority_ReturnsDefault_WhenRangeIsOutsideAvailableLabels()
        {
            IReadOnlyList<LabelOnGround> labels = [CreateLabel(), CreateLabel()];

            LabelSelectionResult result = LabelSelectionEngine.SelectNextLabelByPriority(
                labels,
                startIndex: 2,
                endExclusive: 4,
                CreateClickSettings(),
                _ => CreateSuccessfulCandidate("items"),
                _ => new LabelRankInput(0f, 0f));

            result.SelectedCandidate.Should().BeNull();
            result.SelectedMechanicId.Should().BeNull();
            result.Stats.ConsideredCandidates.Should().Be(0);
        }

        private static ClickSettings CreateClickSettings(
            IReadOnlyDictionary<string, int>? priorityIndexMap = null,
            IReadOnlySet<string>? ignoreDistanceSet = null,
            IReadOnlyDictionary<string, int>? ignoreDistanceWithinByMechanicId = null,
            int mechanicPriorityDistancePenalty = 0)
        {
            return new ClickSettings
            {
                MechanicPriorityIndexMap = priorityIndexMap ?? new Dictionary<string, int>(),
                IgnoreDistanceMechanicIds = ignoreDistanceSet ?? new HashSet<string>(),
                IgnoreDistanceWithinByMechanicId = ignoreDistanceWithinByMechanicId ?? new Dictionary<string, int>(),
                MechanicPriorityDistancePenalty = mechanicPriorityDistancePenalty
            };
        }

        private static LabelCandidateBuildResult CreateSuccessfulCandidate(string mechanicId)
        {
            return new LabelCandidateBuildResult(
                Success: true,
                Item: (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity)),
                MechanicId: mechanicId,
                RejectReason: LabelCandidateRejectReason.None);
        }

        private static LabelCandidateBuildResult CreateRejectedCandidate(LabelCandidateRejectReason rejectReason)
        {
            return new LabelCandidateBuildResult(
                Success: false,
                Item: null,
                MechanicId: null,
                RejectReason: rejectReason);
        }

        private static LabelOnGround CreateLabel()
            => (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));

        [TestMethod]
        public void SelectNextLabelByPriority_GatedSelection_SkipsSuppressedLabels_AndPicksBestAcceptable()
        {
            // Regression: the scan now applies suppression INLINE in the selection pass (a single O(n) scan
            // instead of re-querying the remaining range per suppressed label). A suppressed label must be
            // skipped even when it would otherwise rank best, and the best ACCEPTABLE label must win.
            LabelOnGround wouldBeBest = CreateLabel();
            LabelOnGround nextBest = CreateLabel();
            LabelOnGround worst = CreateLabel();
            IReadOnlyList<LabelOnGround> labels = [wouldBeBest, nextBest, worst];

            LabelSelectionResult result = LabelSelectionEngine.SelectNextLabelByPriority(
                labels,
                startIndex: 0,
                endExclusive: labels.Count,
                CreateClickSettings(),
                static _ => CreateSuccessfulCandidate("items"),
                label => new LabelRankInput(
                    ReferenceEquals(label, wouldBeBest) ? 1f : ReferenceEquals(label, nextBest) ? 2f : 3f,
                    0f),
                (label, _) => !ReferenceEquals(label, wouldBeBest));

            result.SelectedCandidate.Should().BeSameAs(nextBest,
                "the would-be-best label is suppressed, so the next best acceptable label must be picked in the single pass");
            result.Stats.ConsideredCandidates.Should().Be(3);
        }
    }
}