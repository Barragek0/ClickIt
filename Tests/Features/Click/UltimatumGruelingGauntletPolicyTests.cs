using ClickIt.Features.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        [TestMethod]
        public void DetermineAction_ReturnsTakeRewards_OnlyWhenAllRewardConditionsAreMet()
        {
            UltimatumGruelingGauntletPolicy
                .DetermineAction(hasSaturatedChoice: true, shouldTakeReward: true, canClickTakeReward: true)
                .Should().Be(GruelingGauntletAction.TakeRewards);

            UltimatumGruelingGauntletPolicy
                .DetermineAction(hasSaturatedChoice: false, shouldTakeReward: true, canClickTakeReward: true)
                .Should().Be(GruelingGauntletAction.ConfirmOnly);

            UltimatumGruelingGauntletPolicy
                .DetermineAction(hasSaturatedChoice: true, shouldTakeReward: false, canClickTakeReward: true)
                .Should().Be(GruelingGauntletAction.ConfirmOnly);

            UltimatumGruelingGauntletPolicy
                .DetermineAction(hasSaturatedChoice: true, shouldTakeReward: true, canClickTakeReward: false)
                .Should().Be(GruelingGauntletAction.ConfirmOnly);
        }

        [TestMethod]
        public void ShouldSuppressClick_ReturnsTrue_OnlyForRewardWithoutButtonPermission()
        {
            UltimatumGruelingGauntletPolicy.ShouldSuppressClick(shouldTakeReward: true, canClickTakeReward: false).Should().BeTrue();
            UltimatumGruelingGauntletPolicy.ShouldSuppressClick(shouldTakeReward: true, canClickTakeReward: true).Should().BeFalse();
            UltimatumGruelingGauntletPolicy.ShouldSuppressClick(shouldTakeReward: false, canClickTakeReward: false).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldTreatChoiceAsSaturated_UsesReportedStateWhenAvailable_ElseFallsBackToVisibility()
        {
            UltimatumGruelingGauntletPolicy.ShouldTreatChoiceAsSaturated(hasSaturationState: true, isSaturated: true, fallbackVisible: false).Should().BeTrue();
            UltimatumGruelingGauntletPolicy.ShouldTreatChoiceAsSaturated(hasSaturationState: true, isSaturated: false, fallbackVisible: true).Should().BeFalse();
            UltimatumGruelingGauntletPolicy.ShouldTreatChoiceAsSaturated(hasSaturationState: false, isSaturated: false, fallbackVisible: true).Should().BeTrue();
            UltimatumGruelingGauntletPolicy.ShouldTreatChoiceAsSaturated(hasSaturationState: false, isSaturated: true, fallbackVisible: false).Should().BeFalse();
        }

        [TestMethod]
        public void ContainsAtlasPassiveSkillId_ReturnsTrue_WhenTargetIdExistsAcrossConvertibleValues()
        {
            object atlasPassiveIds = new object[]
            {
                1,
                "2",
                3L,
                4.0
            };

            UltimatumGruelingGauntletPolicy.ContainsAtlasPassiveSkillId(atlasPassiveIds, 2).Should().BeTrue();
            UltimatumGruelingGauntletPolicy.ContainsAtlasPassiveSkillId(atlasPassiveIds, 3).Should().BeTrue();
            UltimatumGruelingGauntletPolicy.ContainsAtlasPassiveSkillId(atlasPassiveIds, 4).Should().BeTrue();
        }

        [TestMethod]
        public void ContainsAtlasPassiveSkillId_ReturnsFalse_WhenTargetIdMissingOrValuesNotConvertible()
        {
            object atlasPassiveIds = new object[]
            {
                "foo",
                null!,
                9
            };

            UltimatumGruelingGauntletPolicy.ContainsAtlasPassiveSkillId(atlasPassiveIds, 10).Should().BeFalse();
        }
    }
}