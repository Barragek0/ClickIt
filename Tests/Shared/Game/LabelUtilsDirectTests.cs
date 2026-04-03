using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Shared;
using ExileCore.Shared.Enums;

namespace ClickIt.Tests.Common.Game
{
    [TestClass]
    public class LabelUtilsDirectTests
    {
        [TestMethod]
        public void IsValidClickableLabel_NullOrMissingParts_ReturnsFalse()
        {
            LabelUtils.IsValidClickableLabel(null!, (v) => true).Should().BeFalse();
        }

        [TestMethod]
        public void IsValidEntityPath_DetectsClickablePathAndHandlesNull()
        {
            LabelUtils.IsValidEntityPathCore(null).Should().BeFalse();
            LabelUtils.IsValidEntityPathCore("some/thing/PetrifiedWood/abc").Should().BeTrue();
        }

        [TestMethod]
        public void IsValidClickableLabelCore_HarvestRequiresVisibleRootElement()
        {
            var clickableHarvestPath = "Metadata/Terrain/Leagues/Harvest/Irrigator";

            var blocked = LabelUtils.IsValidClickableLabelCore(
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

            var allowed = LabelUtils.IsValidClickableLabelCore(
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
            var nonHarvestPath = "Metadata/Terrain/Leagues/Ritual/SomeObject";

            var result = LabelUtils.IsValidClickableLabelCore(
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

    }
}
