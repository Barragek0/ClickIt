namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    public class ClickableLabelPolicyTests
    {
        [TestMethod]
        public void IsValidEntityPath_DetectsClickablePathAndHandlesNull()
        {
            ClickableLabelPolicy.IsValidEntityPathCore(null).Should().BeFalse();
            ClickableLabelPolicy.IsValidEntityPathCore("some/thing/PetrifiedWood/abc").Should().BeTrue();
        }

        [TestMethod]
        public void IsValidClickableLabelCore_HarvestRequiresVisibleRootElement()
        {
            const string clickableHarvestPath = "Metadata/Terrain/Leagues/Harvest/Irrigator";

            bool blocked = ClickableLabelPolicy.IsValidClickableLabelCore(
                labelNotNull: true,
                itemNotNull: true,
                isVisible: true,
                labelElementValid: true,
                inClickableArea: true,
                type: EntityType.WorldItem,
                path: clickableHarvestPath,
                chestOpenOnDamage: false,
                hasEssenceImprisonment: false,
                harvestRootElementVisible: false);

            blocked.Should().BeFalse();

            bool allowed = ClickableLabelPolicy.IsValidClickableLabelCore(
                labelNotNull: true,
                itemNotNull: true,
                isVisible: true,
                labelElementValid: true,
                inClickableArea: true,
                type: EntityType.WorldItem,
                path: clickableHarvestPath,
                chestOpenOnDamage: false,
                hasEssenceImprisonment: false,
                harvestRootElementVisible: true);

            allowed.Should().BeTrue();
        }

        [TestMethod]
        public void IsValidClickableLabelCore_NonHarvestIgnoresRootElementVisibility()
        {
            const string nonHarvestPath = "Metadata/Terrain/Leagues/Ritual/SomeObject";

            bool result = ClickableLabelPolicy.IsValidClickableLabelCore(
                labelNotNull: true,
                itemNotNull: true,
                isVisible: true,
                labelElementValid: true,
                inClickableArea: true,
                type: EntityType.WorldItem,
                path: nonHarvestPath,
                chestOpenOnDamage: false,
                hasEssenceImprisonment: false,
                harvestRootElementVisible: false);

            result.Should().BeTrue();
        }

        [DataTestMethod]
        [DataRow("DelveMineral/col1", true)]
        [DataRow("some/Delve/Objects/Encounter/abc", true)]
        [DataRow("CleansingFireAltar/something", true)]
        [DataRow("copper_altar", true)]
        [DataRow("Leagues/Ritual/blah", true)]
        [DataRow("not/a/match", false)]
        [DataRow("", false)]
        [DataRow("DELVeMINERAL", false)]
        public void IsPathForClickableObject_VariousPatterns(string path, bool expected)
        {
            ClickableLabelPolicy.IsPathForClickableObject(path).Should().Be(expected);
        }
    }
}