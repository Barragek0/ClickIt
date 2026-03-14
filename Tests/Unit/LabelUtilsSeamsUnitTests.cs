using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using ExileCore.Shared.Enums;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelUtilsSeamsUnitTests
    {
        [TestMethod]
        public void IsValidEntityTypeForTests_WorldItemAndAreaTransitionAndChestBehaviors()
        {
            LabelUtils.IsValidEntityTypeForTests(EntityType.WorldItem, null, false).Should().BeTrue();

            LabelUtils.IsValidEntityTypeForTests(EntityType.AreaTransition, null, false).Should().BeTrue();

            LabelUtils.IsValidEntityTypeForTests(EntityType.Monster, "Some/AreaTransition/Path", false).Should().BeTrue();

            LabelUtils.IsValidEntityTypeForTests(EntityType.Chest, null, true).Should().BeFalse();

            LabelUtils.IsValidEntityTypeForTests(EntityType.Chest, null, false).Should().BeTrue();

            LabelUtils.IsValidEntityTypeForTests(EntityType.Monster, "", false).Should().BeFalse();
        }

        [TestMethod]
        public void IsValidClickableLabelForTests_RejectsWhenInvalidOrNotInAreaAnd_AcceptsOnAnyMatch()
        {
            LabelUtils.IsValidClickableLabelForTests(false, true, true, true, true, EntityType.WorldItem, null, false, false).Should().BeFalse();
            LabelUtils.IsValidClickableLabelForTests(true, false, true, true, true, EntityType.WorldItem, null, false, false).Should().BeFalse();
            LabelUtils.IsValidClickableLabelForTests(true, true, false, true, true, EntityType.WorldItem, null, false, false).Should().BeFalse();
            LabelUtils.IsValidClickableLabelForTests(true, true, true, false, true, EntityType.WorldItem, null, false, false).Should().BeFalse();

            LabelUtils.IsValidClickableLabelForTests(true, true, true, true, false, EntityType.WorldItem, null, false, false).Should().BeFalse();

            // Valid because of entity type
            LabelUtils.IsValidClickableLabelForTests(true, true, true, true, true, EntityType.WorldItem, null, false, false).Should().BeTrue();

            // Valid because path is a clickable object (even if not world item)
            LabelUtils.IsValidClickableLabelForTests(true, true, true, true, true, EntityType.Monster, "CleansingFireAltar", false, false).Should().BeTrue();

            // Valid because of essence imprisonment text
            LabelUtils.IsValidClickableLabelForTests(true, true, true, true, true, EntityType.Monster, "", false, true).Should().BeTrue();

            LabelUtils.IsValidClickableLabelForTests(true, true, true, true, true, EntityType.Monster, "", false, false).Should().BeFalse();
        }

        [TestMethod]
        public void ClearThreadLocalStorage_ForTests_ClearsExistingList()
        {
            var before = LabelUtils.GetThreadLocalElementsCountForTests();

            LabelUtils.AddNullElementToThreadLocalForTests();
            LabelUtils.GetThreadLocalElementsCountForTests().Should().BeGreaterThanOrEqualTo(before + 1);

            LabelUtils.ClearThreadLocalStorage();
            LabelUtils.GetThreadLocalElementsCountForTests().Should().Be(0);
        }
    }
}
