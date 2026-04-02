using System.Collections.Generic;
using ClickIt.Services.Label.Inventory;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Label
{
    [TestClass]
    public class LabelFilterServiceInventoryTests
    {
        public sealed class FakeBaseInfo
        {
            public int ItemCellsSizeX { get; set; }
            public int ItemCellsSizeY { get; set; }
            public int SizeX { get; set; }
            public int SizeY { get; set; }
        }

        public sealed class FakeBaseWithInfo
        {
            public FakeBaseInfo Info { get; set; } = new FakeBaseInfo();
        }

        [TestMethod]
        public void ShouldPickupWhenInventoryFullCore_OnlyAllowsMatchingPartialStacks()
        {
            InventoryCoreLogic.ShouldPickupWhenInventoryFull(true, false, false).Should().BeFalse();
            InventoryCoreLogic.ShouldPickupWhenInventoryFull(true, true, false).Should().BeFalse();
            InventoryCoreLogic.ShouldPickupWhenInventoryFull(true, true, true).Should().BeTrue();
            InventoryCoreLogic.ShouldPickupWhenInventoryFull(false, false, false).Should().BeTrue();
        }

        [TestMethod]
        public void IsPartialStackCore_ReturnsTrue_OnlyForStrictlyPartialStacks()
        {
            InventoryCoreLogic.IsPartialStack(11, 20).Should().BeTrue();
            InventoryCoreLogic.IsPartialStack(0, 20).Should().BeFalse();
            InventoryCoreLogic.IsPartialStack(20, 20).Should().BeFalse();
            InventoryCoreLogic.IsPartialStack(25, 20).Should().BeFalse();
            InventoryCoreLogic.IsPartialStack(5, 0).Should().BeFalse();
        }

        [TestMethod]
        public void IsPartialServerStackCore_ReturnsTrue_OnlyWhenNotFullAndSizePositive()
        {
            InventoryCoreLogic.IsPartialServerStack(false, 1).Should().BeTrue();
            InventoryCoreLogic.IsPartialServerStack(false, 10).Should().BeTrue();
            InventoryCoreLogic.IsPartialServerStack(true, 10).Should().BeFalse();
            InventoryCoreLogic.IsPartialServerStack(false, 0).Should().BeFalse();
        }

        [TestMethod]
        public void IsInventoryCellUsageFullCore_ReturnsTrue_WhenOccupiedCellsMeetOrExceedCapacity()
        {
            InventoryCoreLogic.IsInventoryCellUsageFull(59, 60).Should().BeFalse();
            InventoryCoreLogic.IsInventoryCellUsageFull(60, 60).Should().BeTrue();
            InventoryCoreLogic.IsInventoryCellUsageFull(61, 60).Should().BeTrue();
            InventoryCoreLogic.IsInventoryCellUsageFull(10, 0).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowPickupWhenPrimaryInventoryMissingCore_ReturnsTrue_OnlyForMissingPrimaryInventorySignal()
        {
            InventoryCoreLogic.ShouldAllowPickupWhenPrimaryInventoryMissing(false, "Primary server inventory missing").Should().BeTrue();
            InventoryCoreLogic.ShouldAllowPickupWhenPrimaryInventoryMissing(true, "Primary server inventory missing").Should().BeFalse();
            InventoryCoreLogic.ShouldAllowPickupWhenPrimaryInventoryMissing(false, "Unable to resolve inventory capacity").Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowPickupWhenGroundItemEntityMissingCore_AllowsOnlyWhenInventoryNotFull()
        {
            InventoryCoreLogic.ShouldAllowPickupWhenGroundItemEntityMissing(false, null).Should().BeTrue();
            InventoryCoreLogic.ShouldAllowPickupWhenGroundItemEntityMissing(true, null).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowPickupWhenGroundItemIdentityMissingCore_AllowsOnlyWhenInventoryNotFullAndIdentityMissing()
        {
            InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing(false, string.Empty, string.Empty).Should().BeTrue();
            InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing(true, string.Empty, string.Empty).Should().BeFalse();
            InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing(false, "Metadata/Items/Test", string.Empty).Should().BeFalse();
            InventoryCoreLogic.ShouldAllowPickupWhenGroundItemIdentityMissing(false, string.Empty, "ItemName").Should().BeFalse();
        }

        [TestMethod]
        public void IsInventoryLayoutUnreliableNotesCore_DetectsExpectedPrefix()
        {
            InventoryCoreLogic.IsInventoryLayoutUnreliableNotes("Inventory layout unreliable from inventory slots (raw:4 parsed:0)").Should().BeTrue();
            InventoryCoreLogic.IsInventoryLayoutUnreliableNotes("Unable to resolve inventory dimensions").Should().BeFalse();
            InventoryCoreLogic.IsInventoryLayoutUnreliableNotes(string.Empty).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowClosedDoorPastMechanicCore_RequiresStoneUnlessLayoutUnreliable()
        {
            InventoryCoreLogic.ShouldAllowClosedDoorPastMechanic(true, "").Should().BeTrue();
            InventoryCoreLogic.ShouldAllowClosedDoorPastMechanic(false, "Inventory layout unreliable from inventory slots (raw:5 parsed:0)").Should().BeTrue();
            InventoryCoreLogic.ShouldAllowClosedDoorPastMechanic(false, "Primary server inventory missing").Should().BeFalse();
            InventoryCoreLogic.ShouldAllowClosedDoorPastMechanic(false, "").Should().BeFalse();
        }

        [TestMethod]
        public void HasSpaceForItemFootprintCore_ReturnsFalse_WhenFreeCellsAreFragmentedForTwoByFour()
        {
            const int inventoryWidth = 3;
            const int inventoryHeight = 5;

            var occupied = new List<InventoryLayoutEntry>
            {
                new(0, 0, 1, 4),
                new(1, 2, 1, 1),
                new(1, 3, 1, 1),
                new(2, 4, 1, 1)
            };

            bool hasSpace = InventoryFitEvaluator.HasSpaceForItemFootprint(
                inventoryWidth,
                inventoryHeight,
                occupied,
                requiredWidth: 2,
                requiredHeight: 4);

            hasSpace.Should().BeFalse();
        }

        [TestMethod]
        public void HasSpaceForItemFootprintCore_ReturnsTrue_WhenContiguousTwoByFourSpaceExists()
        {
            const int inventoryWidth = 3;
            const int inventoryHeight = 5;

            var occupied = new List<InventoryLayoutEntry>
            {
                new(0, 0, 1, 5)
            };

            bool hasSpace = InventoryFitEvaluator.HasSpaceForItemFootprint(
                inventoryWidth,
                inventoryHeight,
                occupied,
                requiredWidth: 2,
                requiredHeight: 4);

            hasSpace.Should().BeTrue();
        }

        [TestMethod]
        public void TryResolveInventoryItemSizeFromBase_PrefersInfoItemCellsSize()
        {
            var fakeBase = new FakeBaseWithInfo
            {
                Info = new FakeBaseInfo
                {
                    ItemCellsSizeX = 2,
                    ItemCellsSizeY = 4
                }
            };

            bool resolved = InventoryCoreLogic.TryResolveInventoryItemSizeFromBase(fakeBase, out int width, out int height);

            resolved.Should().BeTrue();
            width.Should().Be(2);
            height.Should().Be(4);
        }

        [TestMethod]
        public void TryResolveFallbackInventoryItemSizeFromPathCore_ReturnsOneByOne_ForCurrencyAndCardsAndMaps()
        {
            InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath("Metadata/Items/Currency/CurrencyRerollRare", out _, out _).Should().BeTrue();
            InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath("Metadata/Items/DivinationCards/DivinationCardDeck", out _, out _).Should().BeTrue();
            InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath("Metadata/Items/Maps/Atlas2Maps/SomeMap", out _, out _).Should().BeTrue();
        }

        [TestMethod]
        public void TryResolveFallbackInventoryItemSizeFromPathCore_ReturnsFalse_ForNonFallbackPaths()
        {
            InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath("Metadata/Items/Weapons/OneHandWeapons/OneHandSword", out _, out _).Should().BeFalse();
            InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath(string.Empty, out _, out _).Should().BeFalse();
            InventoryCoreLogic.TryResolveFallbackInventoryItemSizeFromPath(null, out _, out _).Should().BeFalse();
        }

    }
}