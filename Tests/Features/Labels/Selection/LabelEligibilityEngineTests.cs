namespace ClickIt.Tests.Features.Labels.Selection
{
    [TestClass]
    public class LabelEligibilityEngineTests
    {
        [TestMethod]
        public void TryBuildCandidate_Throws_WhenOpaqueLabelDereferencesRuntimeItem()
        {
            LabelOnGround label = ExileCoreOpaqueFactory.CreateOpaqueLabel();

            int targetableCallCount = 0;
            int mechanicResolverCallCount = 0;

            Action act = () => _ = LabelEligibilityEngine.TryBuildCandidate(
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
                out _);

            act.Should().Throw<NullReferenceException>();
            targetableCallCount.Should().Be(0);
            mechanicResolverCallCount.Should().Be(0);
        }
    }
}