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
                _ => new LabelScanEntry(
                    CreateSuccessfulCandidate("items"),
                    new LabelRankInput(0f, 0f)));

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
                EntityPath: null,
                RejectReason: LabelCandidateRejectReason.None);
        }

        private static LabelCandidateBuildResult CreateRejectedCandidate(LabelCandidateRejectReason rejectReason)
        {
            return new LabelCandidateBuildResult(
                Success: false,
                Item: null,
                MechanicId: null,
                EntityPath: null,
                RejectReason: rejectReason);
        }

        private static LabelOnGround CreateLabel()
            => (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));

        [TestMethod]
        public void SelectNextLabelByPriority_GatedSelection_SkipsSuppressedLabels_AndPicksBestAcceptable()
        {
            LabelOnGround wouldBeBest = CreateLabel();
            LabelOnGround nextBest = CreateLabel();
            LabelOnGround worst = CreateLabel();
            IReadOnlyList<LabelOnGround> labels = [wouldBeBest, nextBest, worst];

            LabelSelectionResult result = LabelSelectionEngine.SelectNextLabelByPriority(
                labels,
                startIndex: 0,
                endExclusive: labels.Count,
                CreateClickSettings(),
                label => new LabelScanEntry(
                    CreateSuccessfulCandidate("items"),
                    new LabelRankInput(
                        ReferenceEquals(label, wouldBeBest) ? 1f : ReferenceEquals(label, nextBest) ? 2f : 3f,
                        0f)),
                (label, _) => !ReferenceEquals(label, wouldBeBest));

            result.SelectedCandidate.Should().BeSameAs(nextBest,
                "the would-be-best label is suppressed, so the next best acceptable label must be picked in the single pass");
            result.Stats.ConsideredCandidates.Should().Be(3);
        }

        [TestMethod]
        public void SelectNextLabelByPriority_ScanEntryOverload_ResolvesEachLabelOnce_AndPicksBest()
        {
            LabelOnGround near = CreateLabel();
            LabelOnGround far = CreateLabel();
            IReadOnlyList<LabelOnGround> labels = [far, near];
            int resolves = 0;

            LabelSelectionResult result = LabelSelectionEngine.SelectNextLabelByPriority(
                labels,
                startIndex: 0,
                endExclusive: labels.Count,
                CreateClickSettings(),
                label =>
                {
                    resolves++;
                    bool isNear = ReferenceEquals(label, near);
                    return new LabelScanEntry(
                        CreateSuccessfulCandidate("items"),
                        new LabelRankInput(isNear ? 1f : 100f, 0f));
                });

            result.SelectedCandidate.Should().BeSameAs(near);
            resolves.Should().Be(2, "each label is resolved exactly once");
        }
    }
}