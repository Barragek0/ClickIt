namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumGruelingGauntletPolicyTests
    {
        [TestMethod]
        public void ResolvePanelSaturationSummary_ProjectsSaturatedRewardDecision_AndTakeRewardsAction()
        {
            var candidates = new[]
            {
                new UltimatumPanelChoiceCandidate(null!, "A", 4, false),
                new UltimatumPanelChoiceCandidate(null!, "B", 1, true)
            };

            UltimatumGruelingSaturationSummary summary = UltimatumGruelingGauntletPolicy.ResolvePanelSaturationSummary(
                candidates,
                modifier => modifier == "B",
                canClickTakeReward: true);

            summary.HasSaturatedChoice.Should().BeTrue();
            summary.SaturatedModifier.Should().Be("B");
            summary.ShouldTakeReward.Should().BeTrue();
            summary.SaturatedCandidateCount.Should().Be(1);
            summary.Action.Should().Be(GruelingGauntletAction.TakeRewards);
        }

        [TestMethod]
        public void ResolvePanelSaturationSummary_ProjectsConfirmOnly_WhenRewardClickIsNotAllowed()
        {
            var candidates = new[]
            {
                new UltimatumPanelChoiceCandidate(null!, "B", 1, true)
            };

            UltimatumGruelingSaturationSummary summary = UltimatumGruelingGauntletPolicy.ResolvePanelSaturationSummary(
                candidates,
                modifier => modifier == "B",
                canClickTakeReward: false);

            summary.HasSaturatedChoice.Should().BeTrue();
            summary.ShouldTakeReward.Should().BeTrue();
            summary.Action.Should().Be(GruelingGauntletAction.ConfirmOnly);
        }

    }
}