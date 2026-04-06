namespace ClickIt.Tests.Features.Click.Interaction
{
    [TestClass]
    public class WorldItemUiHoverPolicyTests
    {
        [DataTestMethod]
        [DataRow("Metadata/Items/Heist/Contracts/ContractWeapons1", "Whatever", true)]
        [DataRow("", "Contract: Smuggler's Den", true)]
        [DataRow(null, "contract: smugglers den", true)]
        [DataRow("Metadata/Items/Currency/CurrencyRerollRare", "Chaos Orb", false)]
        [DataRow("   ", "   ", false)]
        public void IsHeistContractWorldItem_ReturnsExpected(string? itemPath, string? renderName, bool expected)
        {
            WorldItemUiHoverPolicy.IsHeistContractWorldItem(itemPath, renderName).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow("Metadata/Items/Heist/HeistBlueprint/BlueprintGeneric", "Whatever", true)]
        [DataRow("Metadata/Items/Currency/Heist/Blueprint/BlueprintCurrency1", "Whatever", true)]
        [DataRow("", "Blueprint: Smuggler's Den", true)]
        [DataRow(null, "blueprint: smugglers den", true)]
        [DataRow("Metadata/Items/Currency/CurrencyRerollRare", "Chaos Orb", false)]
        [DataRow("   ", "   ", false)]
        public void IsHeistBlueprintWorldItem_ReturnsExpected(string? itemPath, string? renderName, bool expected)
        {
            WorldItemUiHoverPolicy.IsHeistBlueprintWorldItem(itemPath, renderName).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow("Metadata/Items/Heist/HeistCoin/HeistCoin1", "Whatever", true)]
        [DataRow("", "Rogue's Marker", true)]
        [DataRow(null, "rogue's marker", true)]
        [DataRow("Metadata/Items/Currency/CurrencyRerollRare", "Chaos Orb", false)]
        [DataRow("   ", "   ", false)]
        public void IsRoguesMarkerWorldItem_ReturnsExpected(string? itemPath, string? renderName, bool expected)
        {
            WorldItemUiHoverPolicy.IsRoguesMarkerWorldItem(itemPath, renderName).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow("Metadata/Items/Heist/Contracts/ContractGeneric", "Contract: Test", true)]
        [DataRow("Metadata/Items/Heist/HeistBlueprint/BlueprintGeneric", "Blueprint: Test", true)]
        [DataRow("Metadata/Items/Heist/HeistCoin/HeistCoin1", "Rogue's Marker", true)]
        [DataRow("Metadata/Items/Currency/CurrencyRerollRare", "Chaos Orb", false)]
        public void ShouldForceUiHoverVerificationForWorldItem_ReturnsExpected(string? itemPath, string? renderName, bool expected)
        {
            WorldItemUiHoverPolicy.ShouldForceUiHoverVerificationForWorldItem(itemPath, renderName).Should().Be(expected);
        }

        [TestMethod]
        public void ResolvePreferredLabelPoint_UsesLowerLabelArea_ForHeistContracts()
        {
            var rect = new RectangleF(100, 200, 180, 40);

            Vector2 preferred = WorldItemUiHoverPolicy.ResolvePreferredLabelPoint(
                rect,
                EntityType.WorldItem,
                chestHeightOffset: 0,
                "Metadata/Items/Heist/Contracts/ContractGeneric",
                "Contract: Test");

            preferred.Y.Should().BeGreaterThan(rect.Center.Y);
            preferred.Y.Should().BeLessThan(rect.Bottom);
            preferred.X.Should().Be(rect.Center.X);
        }

        [TestMethod]
        public void ResolvePreferredLabelPoint_UsesCenter_ForNonChestNonHeistWorldItems()
        {
            var rect = new RectangleF(100, 200, 180, 40);

            Vector2 preferred = WorldItemUiHoverPolicy.ResolvePreferredLabelPoint(
                rect,
                EntityType.WorldItem,
                chestHeightOffset: 12,
                itemPath: "Metadata/Items/Currency/CurrencyRerollRare",
                renderName: "Chaos Orb");

            preferred.Should().Be(rect.Center);
        }
    }
}