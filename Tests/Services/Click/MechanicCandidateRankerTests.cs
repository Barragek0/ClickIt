using ClickIt.Services.Click.Ranking;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ClickIt.Tests.Unit
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

        [TestMethod]
        public void Compare_PrefersLowerWeightedDistance_WhenBothNotIgnored()
        {
            var left = new MechanicCandidateRanker.CandidateRank(
                ignored: false,
                priorityIndex: 3,
                weightedDistance: 80,
                rawDistance: 20,
                cursorDistance: 10);

            var right = new MechanicCandidateRanker.CandidateRank(
                ignored: false,
                priorityIndex: 2,
                weightedDistance: 90,
                rawDistance: 5,
                cursorDistance: 2);

            MechanicCandidateRanker.Compare(left, right).Should().BeLessThan(0);
        }

        [TestMethod]
        public void ResolvePriorityIndex_ReturnsMaxValue_WhenMechanicIsMissing()
        {
            var index = MechanicCandidateRanker.ResolvePriorityIndex(
                "missing",
                new Dictionary<string, int> { ["items"] = 0 });

            index.Should().Be(int.MaxValue);
        }
    }
}