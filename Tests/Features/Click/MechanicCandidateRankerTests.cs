namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class MechanicCandidateRankerTests
    {
        [TestMethod]
        public void Build_ActivatesIgnoreDistance_WhenMechanicConfiguredAndWithinThreshold()
        {
            var context = new MechanicCandidateRanker.RankContext(
                priorityIndexMap: new Dictionary<string, int> { ["lost-shipment"] = 4 },
                ignoreDistanceSet: new HashSet<string> { "lost-shipment" },
                ignoreDistanceWithinByMechanicId: new Dictionary<string, int> { ["lost-shipment"] = 60 },
                priorityDistancePenalty: 25);

            var score = MechanicCandidateRanker.Build(30f, "lost-shipment", 10f, context);

            score.Ignored.Should().BeTrue();
            score.PriorityIndex.Should().Be(4);
        }

        [TestMethod]
        public void Build_UsesDefaultIgnoreDistanceThreshold_WhenMechanicHasNoExplicitDistance()
        {
            var context = new MechanicCandidateRanker.RankContext(
                priorityIndexMap: new Dictionary<string, int> { [MechanicIds.LostShipment] = 2 },
                ignoreDistanceSet: new HashSet<string> { MechanicIds.LostShipment },
                ignoreDistanceWithinByMechanicId: new Dictionary<string, int>(),
                priorityDistancePenalty: 25);

            var score = MechanicCandidateRanker.Build(100f, MechanicIds.LostShipment, 9f, context);

            score.Ignored.Should().BeTrue();
            score.RawDistance.Should().Be(100f);
            score.CursorDistance.Should().Be(9f);
        }

        [TestMethod]
        public void ResolvePriorityIndex_ReturnsMaxValue_WhenMechanicIsMissingOrUnknown()
        {
            var priorities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [MechanicIds.LostShipment] = 3,
            };

            MechanicCandidateRanker.ResolvePriorityIndex(null, priorities).Should().Be(int.MaxValue);
            MechanicCandidateRanker.ResolvePriorityIndex("unknown", priorities).Should().Be(int.MaxValue);
        }

        [TestMethod]
        public void CalculateWeightedDistance_ClampsNegativePenalty_AndReturnsMaxValue_ForUnknownPriority()
        {
            MechanicCandidateRanker.CalculateWeightedDistance(12f, priorityIndex: 2, penalty: -5).Should().Be(12f);
            MechanicCandidateRanker.CalculateWeightedDistance(12f, priorityIndex: int.MaxValue, penalty: 25).Should().Be(float.MaxValue);
        }

        [TestMethod]
        public void Compare_PrefersIgnoredCandidate_WhenPriorityIsNotWorse()
        {
            var ignored = new MechanicCandidateRanker.CandidateRank(
                ignored: true,
                priorityIndex: 1,
                weightedDistance: 999,
                rawDistance: 120,
                cursorDistance: 20);

            var nonIgnored = new MechanicCandidateRanker.CandidateRank(
                ignored: false,
                priorityIndex: 1,
                weightedDistance: 10,
                rawDistance: 10,
                cursorDistance: 5);

            MechanicCandidateRanker.Compare(ignored, nonIgnored).Should().BeLessThan(0);
        }

        [TestMethod]
        public void Compare_PrefersNonIgnoredCandidate_WhenIgnoredPriorityIsWorse()
        {
            var ignored = new MechanicCandidateRanker.CandidateRank(
                ignored: true,
                priorityIndex: 5,
                weightedDistance: 999,
                rawDistance: 15,
                cursorDistance: 4);

            var nonIgnored = new MechanicCandidateRanker.CandidateRank(
                ignored: false,
                priorityIndex: 1,
                weightedDistance: 25,
                rawDistance: 25,
                cursorDistance: 6);

            MechanicCandidateRanker.Compare(ignored, nonIgnored).Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void Compare_UsesRawDistanceAndCursor_WhenBothCandidatesAreIgnored()
        {
            var left = new MechanicCandidateRanker.CandidateRank(
                ignored: true,
                priorityIndex: 1,
                weightedDistance: 999,
                rawDistance: 12,
                cursorDistance: 8);

            var right = new MechanicCandidateRanker.CandidateRank(
                ignored: true,
                priorityIndex: 1,
                weightedDistance: 5,
                rawDistance: 12,
                cursorDistance: 14);

            MechanicCandidateRanker.Compare(left, right).Should().BeLessThan(0);
        }

        [TestMethod]
        public void Compare_UsesPriorityAsFinalTieBreak_WhenNonIgnoredDistancesMatch()
        {
            var left = new MechanicCandidateRanker.CandidateRank(
                ignored: false,
                priorityIndex: 1,
                weightedDistance: 30,
                rawDistance: 10,
                cursorDistance: 5);

            var right = new MechanicCandidateRanker.CandidateRank(
                ignored: false,
                priorityIndex: 2,
                weightedDistance: 30,
                rawDistance: 10,
                cursorDistance: 5);

            MechanicCandidateRanker.Compare(left, right).Should().BeLessThan(0);
        }

    }
}