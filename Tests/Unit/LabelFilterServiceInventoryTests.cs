using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ExileCore.PoEMemory.MemoryObjects;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceInventoryTests
    {
        private static object? InvokePrivateStatic(string name, params object?[] args)
        {
            var method = typeof(Services.LabelFilterService).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();
            return method!.Invoke(null, args);
        }

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
            ((bool)InvokePrivateStatic("ShouldPickupWhenInventoryFullCore", true, false, false)!).Should().BeFalse();
            ((bool)InvokePrivateStatic("ShouldPickupWhenInventoryFullCore", true, true, false)!).Should().BeFalse();
            ((bool)InvokePrivateStatic("ShouldPickupWhenInventoryFullCore", true, true, true)!).Should().BeTrue();
            ((bool)InvokePrivateStatic("ShouldPickupWhenInventoryFullCore", false, false, false)!).Should().BeTrue();
        }

        [TestMethod]
        public void IsPartialStackCore_ReturnsTrue_OnlyForStrictlyPartialStacks()
        {
            ((bool)InvokePrivateStatic("IsPartialStackCore", 11, 20)!).Should().BeTrue();
            ((bool)InvokePrivateStatic("IsPartialStackCore", 0, 20)!).Should().BeFalse();
            ((bool)InvokePrivateStatic("IsPartialStackCore", 20, 20)!).Should().BeFalse();
            ((bool)InvokePrivateStatic("IsPartialStackCore", 25, 20)!).Should().BeFalse();
            ((bool)InvokePrivateStatic("IsPartialStackCore", 5, 0)!).Should().BeFalse();
        }

        [TestMethod]
        public void IsPartialServerStackCore_ReturnsTrue_OnlyWhenNotFullAndSizePositive()
        {
            ((bool)InvokePrivateStatic("IsPartialServerStackCore", false, 1)!).Should().BeTrue();
            ((bool)InvokePrivateStatic("IsPartialServerStackCore", false, 10)!).Should().BeTrue();
            ((bool)InvokePrivateStatic("IsPartialServerStackCore", true, 10)!).Should().BeFalse();
            ((bool)InvokePrivateStatic("IsPartialServerStackCore", false, 0)!).Should().BeFalse();
        }

        [TestMethod]
        public void IsInventoryCellUsageFullCore_ReturnsTrue_WhenOccupiedCellsMeetOrExceedCapacity()
        {
            ((bool)InvokePrivateStatic("IsInventoryCellUsageFullCore", 59, 60)!).Should().BeFalse();
            ((bool)InvokePrivateStatic("IsInventoryCellUsageFullCore", 60, 60)!).Should().BeTrue();
            ((bool)InvokePrivateStatic("IsInventoryCellUsageFullCore", 61, 60)!).Should().BeTrue();
            ((bool)InvokePrivateStatic("IsInventoryCellUsageFullCore", 10, 0)!).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowPickupWhenPrimaryInventoryMissingCore_ReturnsTrue_OnlyForMissingPrimaryInventorySignal()
        {
            ((bool)InvokePrivateStatic("ShouldAllowPickupWhenPrimaryInventoryMissingCore", false, "Primary server inventory missing")!).Should().BeTrue();
            ((bool)InvokePrivateStatic("ShouldAllowPickupWhenPrimaryInventoryMissingCore", true, "Primary server inventory missing")!).Should().BeFalse();
            ((bool)InvokePrivateStatic("ShouldAllowPickupWhenPrimaryInventoryMissingCore", false, "Unable to resolve inventory capacity")!).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowPickupWhenGroundItemEntityMissingCore_AllowsOnlyWhenInventoryNotFull()
        {
            ((bool)InvokePrivateStatic("ShouldAllowPickupWhenGroundItemEntityMissingCore", false, null)!).Should().BeTrue();
            ((bool)InvokePrivateStatic("ShouldAllowPickupWhenGroundItemEntityMissingCore", true, null)!).Should().BeFalse();
            ((bool)InvokePrivateStatic("ShouldAllowPickupWhenGroundItemEntityMissingCore", false, new Entity())!).Should().BeFalse();
        }

        [TestMethod]
        public void HasSpaceForItemFootprintCore_ReturnsFalse_WhenFreeCellsAreFragmentedForTwoByFour()
        {
            const int inventoryWidth = 3;
            const int inventoryHeight = 5;

            var occupied = new List<Services.LabelFilterService.InventoryLayoutEntry>
            {
                new(0, 0, 1, 4),
                new(1, 2, 1, 1),
                new(1, 3, 1, 1),
                new(2, 4, 1, 1)
            };

            bool hasSpace = Services.LabelFilterService.HasSpaceForItemFootprintCore(
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

            var occupied = new List<Services.LabelFilterService.InventoryLayoutEntry>
            {
                new(0, 0, 1, 5)
            };

            bool hasSpace = Services.LabelFilterService.HasSpaceForItemFootprintCore(
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
            var method = typeof(Services.LabelFilterService).GetMethod(
                "TryResolveInventoryItemSizeFromBase",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var fakeBase = new FakeBaseWithInfo
            {
                Info = new FakeBaseInfo
                {
                    ItemCellsSizeX = 2,
                    ItemCellsSizeY = 4
                }
            };

            object?[] args = [fakeBase, 0, 0];
            bool resolved = (bool)method!.Invoke(null, args)!;

            resolved.Should().BeTrue();
            ((int)args[1]!).Should().Be(2);
            ((int)args[2]!).Should().Be(4);
        }

    }
}