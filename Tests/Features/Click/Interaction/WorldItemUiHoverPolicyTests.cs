namespace ClickIt.Tests.Features.Click.Interaction
{
    [TestClass]
    public class WorldItemUiHoverPolicyTests
    {
        [TestMethod]
        public void IsHeistContractWorldItem_DetectsByPathAndName()
        {
            WorldItemUiHoverPolicy.IsHeistContractWorldItem(
                "Metadata/Items/Heist/Contracts/ContractWeapons1",
                "Whatever").Should().BeTrue();

            WorldItemUiHoverPolicy.IsHeistContractWorldItem(
                string.Empty,
                "Contract: Smuggler's Den").Should().BeTrue();

            WorldItemUiHoverPolicy.IsHeistContractWorldItem(
                "Metadata/Items/Currency/CurrencyRerollRare",
                "Chaos Orb").Should().BeFalse();
        }

        [TestMethod]
        public void IsHeistBlueprintWorldItem_DetectsByPathAndName()
        {
            WorldItemUiHoverPolicy.IsHeistBlueprintWorldItem(
                "Metadata/Items/Heist/HeistBlueprint/BlueprintGeneric",
                "Whatever").Should().BeTrue();

            WorldItemUiHoverPolicy.IsHeistBlueprintWorldItem(
                "Metadata/Items/Currency/Heist/Blueprint/BlueprintCurrency1",
                "Whatever").Should().BeTrue();

            WorldItemUiHoverPolicy.IsHeistBlueprintWorldItem(
                string.Empty,
                "Blueprint: Smuggler's Den").Should().BeTrue();

            WorldItemUiHoverPolicy.IsHeistBlueprintWorldItem(
                "Metadata/Items/Currency/CurrencyRerollRare",
                "Chaos Orb").Should().BeFalse();
        }

        [TestMethod]
        public void IsRoguesMarkerWorldItem_DetectsByPathAndName()
        {
            WorldItemUiHoverPolicy.IsRoguesMarkerWorldItem(
                "Metadata/Items/Heist/HeistCoin/HeistCoin1",
                "Whatever").Should().BeTrue();

            WorldItemUiHoverPolicy.IsRoguesMarkerWorldItem(
                string.Empty,
                "Rogue's Marker").Should().BeTrue();

            WorldItemUiHoverPolicy.IsRoguesMarkerWorldItem(
                "Metadata/Items/Currency/CurrencyRerollRare",
                "Chaos Orb").Should().BeFalse();
        }

        [TestMethod]
        public void ShouldForceUiHoverVerificationForWorldItem_ReturnsTrue_ForHeistContractsBlueprintsAndMarkers()
        {
            WorldItemUiHoverPolicy.ShouldForceUiHoverVerificationForWorldItem(
                "Metadata/Items/Heist/Contracts/ContractGeneric",
                "Contract: Test").Should().BeTrue();

            WorldItemUiHoverPolicy.ShouldForceUiHoverVerificationForWorldItem(
                "Metadata/Items/Heist/HeistBlueprint/BlueprintGeneric",
                "Blueprint: Test").Should().BeTrue();

            WorldItemUiHoverPolicy.ShouldForceUiHoverVerificationForWorldItem(
                "Metadata/Items/Heist/HeistCoin/HeistCoin1",
                "Rogue's Marker").Should().BeTrue();

            WorldItemUiHoverPolicy.ShouldForceUiHoverVerificationForWorldItem(
                "Metadata/Items/Currency/CurrencyRerollRare",
                "Chaos Orb").Should().BeFalse();
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
    }
}