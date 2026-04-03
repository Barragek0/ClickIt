using ClickIt.Features.Click.Ranking;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class OffscreenTargetRankerTests
    {
        [TestMethod]
        public void ShouldPromoteCandidate_ReturnsTrue_WhenNoBestExists()
        {
            MechanicRank candidate = new(ignored: false, priorityIndex: 1, weightedDistance: 10f, rawDistance: 10f, cursorDistance: 5f);
            MechanicRank best = new(ignored: false, priorityIndex: 2, weightedDistance: 20f, rawDistance: 20f, cursorDistance: 10f);

            bool shouldPromote = OffscreenTargetRanker.ShouldPromoteCandidate(candidate, best, hasBest: false);

            shouldPromote.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPromoteCandidate_ReturnsTrue_WhenCandidateRankIsBetter()
        {
            MechanicRank candidate = new(ignored: false, priorityIndex: 0, weightedDistance: 12f, rawDistance: 12f, cursorDistance: 4f);
            MechanicRank best = new(ignored: false, priorityIndex: 1, weightedDistance: 20f, rawDistance: 20f, cursorDistance: 9f);

            bool shouldPromote = OffscreenTargetRanker.ShouldPromoteCandidate(candidate, best, hasBest: true);

            shouldPromote.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldPromoteCandidate_ReturnsFalse_WhenCandidateRankIsWorse()
        {
            MechanicRank candidate = new(ignored: false, priorityIndex: 3, weightedDistance: 30f, rawDistance: 30f, cursorDistance: 15f);
            MechanicRank best = new(ignored: false, priorityIndex: 1, weightedDistance: 10f, rawDistance: 10f, cursorDistance: 5f);

            bool shouldPromote = OffscreenTargetRanker.ShouldPromoteCandidate(candidate, best, hasBest: true);

            shouldPromote.Should().BeFalse();
        }
    }
}