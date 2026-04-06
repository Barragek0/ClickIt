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

    }
}