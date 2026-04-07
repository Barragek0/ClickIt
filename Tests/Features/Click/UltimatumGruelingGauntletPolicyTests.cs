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

        [DataTestMethod]
        [DataRow(true, true, true, 2)]
        [DataRow(true, true, false, 1)]
        [DataRow(true, false, true, 1)]
        [DataRow(false, true, true, 1)]
        public void DetermineAction_ProjectsExpectedOutcome(
            bool hasSaturatedChoice,
            bool shouldTakeReward,
            bool canClickTakeReward,
            int expected)
        {
            GruelingGauntletAction action = UltimatumGruelingGauntletPolicy.DetermineAction(
                hasSaturatedChoice,
                shouldTakeReward,
                canClickTakeReward);

            action.Should().Be((GruelingGauntletAction)expected);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(true, true)]
        public void ShouldTreatChoiceAsSaturated_UsesExplicitStateWhenPresent(bool isSaturated, bool expected)
        {
            bool actual = UltimatumGruelingGauntletPolicy.ShouldTreatChoiceAsSaturated(
                hasSaturationState: true,
                isSaturated,
                fallbackVisible: !expected);

            actual.Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(true, true)]
        [DataRow(false, false)]
        public void ShouldTreatChoiceAsSaturated_FallsBackToVisibility_WhenNoExplicitStateExists(bool fallbackVisible, bool expected)
        {
            bool actual = UltimatumGruelingGauntletPolicy.ShouldTreatChoiceAsSaturated(
                hasSaturationState: false,
                isSaturated: false,
                fallbackVisible);

            actual.Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(true, false, true)]
        [DataRow(true, true, false)]
        [DataRow(false, false, false)]
        public void ShouldSuppressClick_OnlySuppressesPendingRewardChoice(bool shouldTakeReward, bool canClickTakeReward, bool expected)
        {
            bool suppressed = UltimatumGruelingGauntletPolicy.ShouldSuppressClick(shouldTakeReward, canClickTakeReward);

            suppressed.Should().Be(expected);
        }

        [TestMethod]
        public void TryReadChoiceSaturation_ReturnsFalse_WhenMemberIsMissing()
        {
            bool ok = UltimatumGruelingGauntletPolicy.TryReadChoiceSaturation(new object(), out bool isSaturated);

            ok.Should().BeFalse();
            isSaturated.Should().BeFalse();
        }

        [TestMethod]
        public void TryReadChoiceSaturation_ConvertsConvertibleState()
        {
            dynamic probe = new System.Dynamic.ExpandoObject();
            probe.IsSaturated = "true";

            bool ok = UltimatumGruelingGauntletPolicy.TryReadChoiceSaturation(probe, out bool isSaturated);

            ok.Should().BeTrue();
            isSaturated.Should().BeTrue();
        }

        [TestMethod]
        public void ContainsAtlasPassiveSkillId_MatchesConvertedEntries_AndSkipsInvalidValues()
        {
            bool contains = UltimatumGruelingGauntletPolicy.ContainsAtlasPassiveSkillId(
                new object?[] { null, "7", "bad", 13 },
                targetId: 13);

            contains.Should().BeTrue();
        }

        [TestMethod]
        public void ContainsAtlasPassiveSkillId_ReturnsFalse_WhenTargetIdIsAbsent()
        {
            bool contains = UltimatumGruelingGauntletPolicy.ContainsAtlasPassiveSkillId(
                new object?[] { "2", 5, null },
                targetId: 13);

            contains.Should().BeFalse();
        }
    }
}