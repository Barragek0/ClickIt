namespace ClickIt.Tests.Features.Labels.Application
{
    [TestClass]
    public class LabelMechanicResolutionServiceTests
    {
        [TestMethod]
        public void GetMechanicIdForLabel_ReturnsNull_WhenLabelIsNull()
        {
            int clickSettingsCallCount = 0;
            var service = new LabelMechanicResolutionService(
                gameController: null,
                createClickSettings: _ =>
                {
                    clickSettingsCallCount++;
                    return new ClickSettings();
                },
                worldItemMetadataPolicy: new WorldItemMetadataPolicy(),
                inventoryInteractionPolicy: InteractionRuleTestFactory.CreateInventoryInteractionPolicy(allowClosedDoorPast: false));

            string? mechanicId = service.GetMechanicIdForLabel(null);

            mechanicId.Should().BeNull();
            clickSettingsCallCount.Should().Be(0);
        }
    }
}