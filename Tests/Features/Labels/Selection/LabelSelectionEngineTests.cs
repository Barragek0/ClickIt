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
                _ => 0f);

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
    }
}