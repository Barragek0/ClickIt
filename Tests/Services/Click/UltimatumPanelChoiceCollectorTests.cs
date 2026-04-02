using ClickIt.Services.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Click
{
    [TestClass]
    public class UltimatumPanelChoiceCollectorTests
    {
        [TestMethod]
        public void ResolveGruelingSaturation_ComputesSaturationCountAndRewardDecision()
        {
            var candidates = new[]
            {
                new UltimatumPanelChoiceCandidate(null!, "A", 2, false),
                new UltimatumPanelChoiceCandidate(null!, "B", 5, true),
                new UltimatumPanelChoiceCandidate(null!, "C", 1, true)
            };

            UltimatumPanelChoiceCollector.ResolveGruelingSaturation(
                candidates,
                modifier => modifier == "B",
                out bool hasSaturatedChoice,
                out string saturatedModifier,
                out bool shouldTakeReward,
                out int saturatedCount);

            hasSaturatedChoice.Should().BeTrue();
            saturatedModifier.Should().Be("B");
            shouldTakeReward.Should().BeTrue();
            saturatedCount.Should().Be(2);
        }

        [TestMethod]
        public void ResolveGruelingSaturation_HandlesNoSaturatedCandidates()
        {
            var candidates = new[]
            {
                new UltimatumPanelChoiceCandidate(null!, "A", 2, false),
                new UltimatumPanelChoiceCandidate(null!, "B", 5, false)
            };

            UltimatumPanelChoiceCollector.ResolveGruelingSaturation(
                candidates,
                _ => true,
                out bool hasSaturatedChoice,
                out string saturatedModifier,
                out bool shouldTakeReward,
                out int saturatedCount);

            hasSaturatedChoice.Should().BeFalse();
            saturatedModifier.Should().BeEmpty();
            shouldTakeReward.Should().BeFalse();
            saturatedCount.Should().Be(0);
        }
    }
}