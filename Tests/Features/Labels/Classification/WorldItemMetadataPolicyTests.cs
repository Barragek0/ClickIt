namespace ClickIt.Tests.Features.Labels.Classification
{
    [TestClass]
    public class WorldItemMetadataPolicyTests
    {
        [DataTestMethod]
        [DataRow("Metadata/MiscellaneousObjects/WorldItem", "Metadata/Items/Currency/CurrencyModValues", "Metadata/Items/Currency/CurrencyModValues")]
        [DataRow("Metadata/Items/Currency/CurrencyModValues", "Metadata/Items/Currency/Alternate", "Metadata/Items/Currency/CurrencyModValues")]
        [DataRow("", "Metadata/Items/Currency/CurrencyModValues", "Metadata/Items/Currency/CurrencyModValues")]
        [DataRow("Metadata/Items/Currency/CurrencyModValues", "", "Metadata/Items/Currency/CurrencyModValues")]
        [DataRow("", "", "")]
        public void SelectBestWorldItemMetadataPath_ReturnsExpectedPath(string resolvedMetadata, string componentMetadata, string expected)
        {
            string result = WorldItemMetadataPolicy.SelectBestWorldItemMetadataPath(resolvedMetadata, componentMetadata);

            result.Should().Be(expected);
        }

        [TestMethod]
        public void GetWorldItemMetadataPath_ReturnsEmpty_WhenItemIsNull()
        {
            var policy = new WorldItemMetadataPolicy();

            string result = policy.GetWorldItemMetadataPath(null!);

            result.Should().BeEmpty();
        }

        [TestMethod]
        public void GetWorldItemBaseName_ReturnsEmpty_WhenItemIsNull()
        {
            var policy = new WorldItemMetadataPolicy();

            string result = policy.GetWorldItemBaseName(null!);

            result.Should().BeEmpty();
        }

        [TestMethod]
        public void ShouldAllowWorldItemByMetadata_UsesInventoryDelegate_WhenFiltersPass()
        {
            var policy = new WorldItemMetadataPolicy();
            var settings = new ClickSettings
            {
                ItemTypeWhitelistMetadata = [],
                ItemTypeBlacklistMetadata = []
            };

            Entity? capturedItem = null;
            GameController? capturedController = null;
            bool delegateCalled = false;

            bool result = policy.ShouldAllowWorldItemByMetadata(
                settings,
                item: null!,
                gameController: null,
                label: null,
                shouldAllowWhenInventoryFull: (item, gameController) =>
                {
                    delegateCalled = true;
                    capturedItem = item;
                    capturedController = gameController;
                    return true;
                });

            result.Should().BeTrue();
            delegateCalled.Should().BeTrue();
            capturedItem.Should().BeNull();
            capturedController.Should().BeNull();
        }

        [TestMethod]
        public void ShouldAllowWorldItemByMetadata_ReturnsFalse_WithoutCallingDelegate_WhenWhitelistRejects()
        {
            var policy = new WorldItemMetadataPolicy();
            var settings = new ClickSettings
            {
                ItemTypeWhitelistMetadata = ["Items/Currency/"],
                ItemTypeBlacklistMetadata = []
            };

            bool delegateCalled = false;

            bool result = policy.ShouldAllowWorldItemByMetadata(
                settings,
                item: null!,
                gameController: ExileCoreOpaqueFactory.CreateOpaqueGameController(),
                label: ExileCoreOpaqueFactory.CreateOpaqueLabel(),
                shouldAllowWhenInventoryFull: (_, _) =>
                {
                    delegateCalled = true;
                    return true;
                });

            result.Should().BeFalse();
            delegateCalled.Should().BeFalse();
        }
    }
}