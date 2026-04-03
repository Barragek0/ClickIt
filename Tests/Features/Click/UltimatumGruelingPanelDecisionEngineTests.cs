using ClickIt.Features.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumGruelingPanelDecisionEngineTests
    {
        [TestMethod]
        public void Resolve_SelectsSaturatedChoice_WhenGruelingGauntletIsActive()
        {
            var candidates = new[]
            {
                new UltimatumPanelChoiceCandidate(null!, "A", 1, false),
                new UltimatumPanelChoiceCandidate(null!, "B", 5, true)
            };

            UltimatumGruelingPanelDecision decision = UltimatumGruelingPanelDecisionEngine.Resolve(
                candidates,
                isGruelingGauntletActive: true,
                modifier => modifier == "B",
                canClickTakeReward: true);

            decision.HasBestChoice.Should().BeTrue();
            decision.BestModifier.Should().Be("B");
            decision.BestPriority.Should().Be(5);
            decision.Saturation.Action.Should().Be(GruelingGauntletAction.TakeRewards);
        }

        [TestMethod]
        public void Resolve_SelectsBestPriorityChoice_WhenGruelingGauntletIsInactive()
        {
            var candidates = new[]
            {
                new UltimatumPanelChoiceCandidate(null!, "A", 1, false),
                new UltimatumPanelChoiceCandidate(null!, "B", 5, true)
            };

            UltimatumGruelingPanelDecision decision = UltimatumGruelingPanelDecisionEngine.Resolve(
                candidates,
                isGruelingGauntletActive: false,
                modifier => modifier == "B",
                canClickTakeReward: true);

            decision.HasBestChoice.Should().BeTrue();
            decision.BestModifier.Should().Be("A");
            decision.BestPriority.Should().Be(1);
            decision.Saturation.HasSaturatedChoice.Should().BeTrue();
        }

        [TestMethod]
        public void Resolve_ReturnsEmptyBestChoice_WhenCandidateListIsEmpty()
        {
            UltimatumGruelingPanelDecision decision = UltimatumGruelingPanelDecisionEngine.Resolve(
                [],
                isGruelingGauntletActive: true,
                _ => false,
                canClickTakeReward: true);

            decision.HasBestChoice.Should().BeFalse();
            decision.BestChoiceElement.Should().BeNull();
            decision.BestModifier.Should().BeEmpty();
            decision.BestPriority.Should().Be(int.MaxValue);
            decision.Saturation.Action.Should().Be(GruelingGauntletAction.ConfirmOnly);
        }
    }
}