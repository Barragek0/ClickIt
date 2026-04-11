namespace ClickIt.Tests.Features.Labels.Selection
{
    [TestClass]
    public class LabelEligibilityEngineTests
    {
        [TestMethod]
        public void TryBuildCandidate_ReturnsFalse_WhenOpaqueLabelCannotResolveRuntimeItem()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            int targetableCallCount = 0;
            int mechanicResolverCallCount = 0;

            bool result = LabelEligibilityEngine.TryBuildCandidate(
                label,
                new ClickSettings { ClickDistance = 999 },
                (_, _) =>
                {
                    targetableCallCount++;
                    return true;
                },
                (_, _, _) =>
                {
                    mechanicResolverCallCount++;
                    return MechanicIds.HeistSecureRepository;
                },
                out _,
                out _,
                out LabelCandidateRejectReason rejectReason);

            result.Should().BeFalse();
            rejectReason.Should().Be(LabelCandidateRejectReason.NullItem);
            targetableCallCount.Should().Be(0);
            mechanicResolverCallCount.Should().Be(0);
        }
    }
}