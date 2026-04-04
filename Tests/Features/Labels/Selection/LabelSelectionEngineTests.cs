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

        [TestMethod]
        public void SelectNextLabelByPriority_TracksRejectCounters_AndIgnoredDistanceStats()
        {
            LabelOnGround rejectedByDistance = CreateLabel();
            LabelOnGround rejectedUntargetable = CreateLabel();
            LabelOnGround selectedIgnored = CreateLabel();

            IReadOnlyList<LabelOnGround> labels = [rejectedByDistance, rejectedUntargetable, selectedIgnored];

            ClickSettings settings = CreateClickSettings(
                priorityIndexMap: new Dictionary<string, int> { ["lost-shipment"] = 0 },
                ignoreDistanceSet: new HashSet<string> { "lost-shipment" },
                ignoreDistanceWithinByMechanicId: new Dictionary<string, int> { ["lost-shipment"] = 100 },
                mechanicPriorityDistancePenalty: 25);

            LabelSelectionResult result = LabelSelectionEngine.SelectNextLabelByPriority(
                labels,
                startIndex: 0,
                endExclusive: labels.Count,
                settings,
                label => ReferenceEquals(label, rejectedByDistance)
                    ? CreateRejectedCandidate(LabelCandidateRejectReason.NullItemOrOutOfDistance)
                    : ReferenceEquals(label, rejectedUntargetable)
                        ? CreateRejectedCandidate(LabelCandidateRejectReason.Untargetable)
                        : CreateSuccessfulCandidate("lost-shipment"),
                _ => 5f);

            result.SelectedCandidate.Should().BeSameAs(selectedIgnored);
            result.SelectedMechanicId.Should().Be("lost-shipment");
            result.Stats.ConsideredCandidates.Should().Be(3);
            result.Stats.NullOrDistanceRejected.Should().Be(1);
            result.Stats.UntargetableRejected.Should().Be(1);
            result.Stats.NoMechanicRejected.Should().Be(0);
            result.Stats.IgnoredByDistanceCandidates.Should().Be(1);
        }

        [TestMethod]
        public void SelectNextLabelByPriority_PrefersLowerCursorDistance_WhenIgnoredCandidatesTieOtherwise()
        {
            LabelOnGround fartherCursor = CreateLabel();
            LabelOnGround nearerCursor = CreateLabel();

            IReadOnlyList<LabelOnGround> labels = [fartherCursor, nearerCursor];

            ClickSettings settings = CreateClickSettings(
                priorityIndexMap: new Dictionary<string, int> { ["lost-shipment"] = 0 },
                ignoreDistanceSet: new HashSet<string> { "lost-shipment" },
                ignoreDistanceWithinByMechanicId: new Dictionary<string, int> { ["lost-shipment"] = 100 },
                mechanicPriorityDistancePenalty: 25);

            LabelSelectionResult result = LabelSelectionEngine.SelectNextLabelByPriority(
                labels,
                startIndex: 0,
                endExclusive: labels.Count,
                settings,
                _ => CreateSuccessfulCandidate("lost-shipment"),
                label => ReferenceEquals(label, fartherCursor) ? 25f : 5f);

            result.SelectedCandidate.Should().BeSameAs(nearerCursor);
            result.SelectedMechanicId.Should().Be("lost-shipment");
            result.Stats.IgnoredByDistanceCandidates.Should().Be(2);
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