namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class SettlersOreTargetSelectorTests
    {
        [TestMethod]
        public void ResolveNextSettlersOreCandidate_ReturnsNull_WithoutCollectingLabels_WhenFeatureDisabled()
        {
            bool collectedAddresses = false;
            var selector = new SettlersOreTargetSelector(new SettlersOreTargetSelectorDependencies(
                Settings: new ClickItSettings
                {
                    ClickLostShipmentCrates = new ToggleNode(true),
                    ClickSettlersOre = new ToggleNode(false),
                    ClickDistance = new RangeNode<int>(600, 0, 1000)
                },
                GameController: null!,
                ClickDebugPublisher: ClickTestDebugPublisherFactory.Create(static () => false, static _ => { }),
                DebugLog: static _ => { },
                IsInsideWindowInEitherSpace: static _ => false,
                IsClickableInEitherSpace: static (_, _) => false,
                GroundLabelEntityAddresses: new GroundLabelEntityAddressProvider(() =>
                {
                    collectedAddresses = true;
                    return [];
                })));

            selector.ResolveNextSettlersOreCandidate().Should().BeNull();
            collectedAddresses.Should().BeFalse();
        }
    }
}