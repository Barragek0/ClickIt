namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class LostShipmentTargetSelectorTests
    {
        [TestMethod]
        public void ResolveNextLostShipmentCandidate_ReturnsNull_WhenFeatureDisabled()
        {
            var selector = new LostShipmentTargetSelector(new LostShipmentTargetSelectorDependencies(
                Settings: new ClickItSettings
                {
                    ClickLostShipmentCrates = new ToggleNode(false),
                    ClickSettlersOre = new ToggleNode(true),
                    ClickDistance = new RangeNode<int>(600, 0, 1000)
                },
                GameController: null!,
                DebugLog: static _ => { },
                IsInsideWindowInEitherSpace: static _ => false,
                IsClickableInEitherSpace: static (_, _) => false));

            selector.ResolveNextLostShipmentCandidate().Should().BeNull();
        }
    }
}