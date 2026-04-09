namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    public class ClickableLabelPolicyTests
    {
        [DataTestMethod]
        [DataRow(EntityType.WorldItem, null, false, true)]
        [DataRow(EntityType.AreaTransition, null, false, true)]
        [DataRow(EntityType.Monster, "Metadata/Terrain/AreaTransition/SomeExit", false, true)]
        [DataRow(EntityType.Chest, "Metadata/Chests/Basic", false, true)]
        [DataRow(EntityType.Chest, "Metadata/Chests/Basic", true, false)]
        [DataRow(EntityType.Monster, "Metadata/Monsters/NotClickable", false, false)]
        public void IsValidEntityTypeCore_ReturnsExpected(EntityType type, string? path, bool chestOpenOnDamage, bool expected)
        {
            ClickableLabelPolicy.IsValidEntityTypeCore(type, path, chestOpenOnDamage).Should().Be(expected);
        }

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
        [DataRow(false, true, true, true, true, false)]
        [DataRow(true, false, true, true, true, false)]
        [DataRow(true, true, false, true, true, false)]
        [DataRow(true, true, true, false, true, false)]
        [DataRow(true, true, true, true, false, false)]
        public void IsValidClickableLabelCore_ReturnsFalse_ForEarlyGuardFailures(
            bool labelNotNull,
            bool itemNotNull,
            bool isVisible,
            bool labelElementValid,
            bool inClickableArea,
            bool expected)
        {
            bool result = ClickableLabelPolicy.IsValidClickableLabelCore(
                labelNotNull,
                itemNotNull,
                isVisible,
                labelElementValid,
                inClickableArea,
                type: EntityType.WorldItem,
                path: "Metadata/Terrain/Leagues/Ritual/SomeObject",
                chestOpenOnDamage: false,
                hasEssenceImprisonment: false,
                harvestRootElementVisible: true);

            result.Should().Be(expected);
        }

        [TestMethod]
        public void IsValidClickableLabelCore_ReturnsFalse_WhenClickableAreaRejectsOtherwiseValidPath()
        {
            bool result = ClickableLabelPolicy.IsValidClickableLabelCore(
                labelNotNull: true,
                itemNotNull: true,
                isVisible: true,
                labelElementValid: true,
                inClickableArea: false,
                type: EntityType.WorldItem,
                path: "Metadata/Terrain/Leagues/Ritual/SomeObject",
                chestOpenOnDamage: false,
                hasEssenceImprisonment: false,
                harvestRootElementVisible: true);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void IsValidClickableLabelCore_AllowsClickableObjectPath_WhenEntityTypeIsOtherwiseInvalid()
        {
            bool result = ClickableLabelPolicy.IsValidClickableLabelCore(
                labelNotNull: true,
                itemNotNull: true,
                isVisible: true,
                labelElementValid: true,
                inClickableArea: true,
                type: EntityType.Monster,
                path: "Metadata/MiscellaneousObjects/Door/TempleDoor",
                chestOpenOnDamage: false,
                hasEssenceImprisonment: false,
                harvestRootElementVisible: true);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void IsValidClickableLabelCore_AllowsEssenceFallback_WhenPathAndEntityTypeDoNotMatch()
        {
            bool result = ClickableLabelPolicy.IsValidClickableLabelCore(
                labelNotNull: true,
                itemNotNull: true,
                isVisible: true,
                labelElementValid: true,
                inClickableArea: true,
                type: EntityType.Monster,
                path: "Metadata/Monsters/NotClickable",
                chestOpenOnDamage: false,
                hasEssenceImprisonment: true,
                harvestRootElementVisible: true);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void IsValidClickableLabelCore_ReturnsFalse_WhenNoFallbacksMatch()
        {
            bool result = ClickableLabelPolicy.IsValidClickableLabelCore(
                labelNotNull: true,
                itemNotNull: true,
                isVisible: true,
                labelElementValid: true,
                inClickableArea: true,
                type: EntityType.Monster,
                path: "Metadata/Monsters/NotClickable",
                chestOpenOnDamage: false,
                hasEssenceImprisonment: false,
                harvestRootElementVisible: true);

            result.Should().BeFalse();
        }

        [DataTestMethod]
        [DataRow("DelveMineral/col1", true)]
        [DataRow("some/Delve/Objects/Encounter/abc", true)]
        [DataRow("CleansingFireAltar/something", true)]
        [DataRow("copper_altar", true)]
        [DataRow("Leagues/Ritual/blah", true)]
        [DataRow("Metadata/Heist/Objects/Level/Door_NPCCatburglar", true)]
        [DataRow("Metadata/Heist/Objects/Level/Door_Basic", true)]
        [DataRow("Metadata/Heist/Objects/Level/DoorSecretLaboratory", true)]
        [DataRow("Metadata/Heist/Objects/Level/Hazards/Strength_SmashMarker", true)]
        [DataRow("Metadata/Terrain/Leagues/Harvest/Extractor", true)]
        [DataRow("Metadata/Terrain/Leagues/Harvest/IrrigatorTierTwo", true)]
        [DataRow("Metadata/Terrain/Leagues/Sanctum/Start", true)]
        [DataRow("Metadata/Terrain/Leagues/Blight/BlightPump", true)]
        [DataRow("Metadata/Leagues/Ultimatum/Objects/UltimatumChallengeInteractable", true)]
        [DataRow("Metadata/Terrain/Leagues/Ritual/RitualRuneInteractable", true)]
        [DataRow("Metadata/Heist/Objects/Level/DoorwayDecoration", true)]
        [DataRow("Metadata/Heist/Objects/Level/SideProp", false)]
        [DataRow("not/a/match", false)]
        [DataRow("", false)]
        [DataRow("DELVeMINERAL", false)]
        public void IsPathForClickableObject_VariousPatterns(string path, bool expected)
        {
            ClickableLabelPolicy.IsPathForClickableObject(path).Should().Be(expected);
        }
    }
}