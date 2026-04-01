using ClickIt.Services.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class UltimatumGruelingGroundDecisionEngineTests
    {
        [TestMethod]
        public void Resolve_SelectsLowestPriorityCandidate_WhenGruelingIsInactive()
        {
            var candidates = new[]
            {
                new UltimatumGroundOptionCandidate(null!, "A", 7, false),
                new UltimatumGroundOptionCandidate(null!, "B", 2, false),
                new UltimatumGroundOptionCandidate(null!, "C", 4, false)
            };

            UltimatumGruelingGroundDecision decision = UltimatumGruelingGroundDecisionEngine.Resolve(
                candidates,
                isGruelingGauntletActive: false,
                static _ => true,
                canClickTakeReward: true);

            decision.HasBestChoice.Should().BeTrue();
            decision.BestModifier.Should().Be("B");
            decision.BestPriority.Should().Be(2);
            decision.Saturation.Should().Be(UltimatumGruelingSaturationSummary.Empty);
        }

        [TestMethod]
        public void Resolve_ProjectsTakeRewardDecision_WhenGruelingSaturatedChoiceIsRewardable()
        {
            var candidates = new[]
            {
                new UltimatumGroundOptionCandidate(null!, "A", 1, false),
                new UltimatumGroundOptionCandidate(null!, "B", 9, true)
            };

            UltimatumGruelingGroundDecision decision = UltimatumGruelingGroundDecisionEngine.Resolve(
                candidates,
                isGruelingGauntletActive: true,
                modifier => modifier == "B",
                canClickTakeReward: true);

            decision.Saturation.HasSaturatedChoice.Should().BeTrue();
            decision.Saturation.SaturatedModifier.Should().Be("B");
            decision.Saturation.ShouldTakeReward.Should().BeTrue();
            decision.Saturation.SaturatedCandidateCount.Should().Be(1);
            decision.Saturation.Action.Should().Be(GruelingGauntletAction.TakeRewards);
            decision.HasBestChoice.Should().BeTrue();
            decision.BestModifier.Should().Be("B");
        }

        [TestMethod]
        public void Resolve_FallsBackToBestPriority_WhenGruelingHasNoSaturatedChoices()
        {
            var candidates = new[]
            {
                new UltimatumGroundOptionCandidate(null!, "A", 5, false),
                new UltimatumGroundOptionCandidate(null!, "B", 1, false)
            };

            UltimatumGruelingGroundDecision decision = UltimatumGruelingGroundDecisionEngine.Resolve(
                candidates,
                isGruelingGauntletActive: true,
                static _ => true,
                canClickTakeReward: true);

            decision.Saturation.HasSaturatedChoice.Should().BeFalse();
            decision.Saturation.SaturatedCandidateCount.Should().Be(0);
            decision.Saturation.Action.Should().Be(GruelingGauntletAction.ConfirmOnly);
            decision.HasBestChoice.Should().BeTrue();
            decision.BestModifier.Should().Be("B");
            decision.BestPriority.Should().Be(1);
        }
    }
}