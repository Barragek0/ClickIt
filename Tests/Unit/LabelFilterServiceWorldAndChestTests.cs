using System;
using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceWorldAndChestTests
    {
        private static void SetMemberValue(object obj, string memberName, object? value)
        {
            if (obj == null) return;
            var type = obj.GetType();

            var prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, value);
                return;
            }

            var backingField = type.GetField($"<{memberName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (backingField != null)
            {
                backingField.SetValue(obj, value);
                return;
            }

            var fuzzy = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in fuzzy)
            {
                if (f.Name.IndexOf(memberName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    f.SetValue(obj, value);
                    return;
                }
            }

            if (value != null)
            {
                foreach (var f in fuzzy)
                {
                    if (f.FieldType.IsInstanceOfType(value))
                    {
                        f.SetValue(obj, value);
                        return;
                    }
                }
            }

            // Last resort: cannot set this member via reflection - fail with clearer message
            throw new InvalidOperationException($"Unable to set member '{memberName}' on type '{type.FullName}' in test.");
        }
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
        public void ShouldClickWorldItemCore_ReturnsFalse_WhenClickItemsDisabled()
        {
            var res = (bool)InvokePrivateStatic("ShouldClickWorldItemCore", false, EntityType.WorldItem, null)!;
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickWorldItemCore_ReturnsFalse_WhenPathContainsStrongbox()
        {
            var ent = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            SetMemberValue(ent, "Path", "some/StrongBoxes/Strongbox/x");

            var res = (bool)InvokePrivateStatic("ShouldClickWorldItemCore", true, EntityType.WorldItem, ent)!;
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickWorldItemCore_ReturnsTrue_WhenEnabledAndNotStrongbox()
        {
            var ent = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            SetMemberValue(ent, "Path", "some/Item/Name");

            var res = (bool)InvokePrivateStatic("ShouldClickWorldItemCore", true, EntityType.WorldItem, ent)!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void IsBasicChest_DetectsSimpleNames_CaseInsensitive()
        {
            var res = (bool)InvokePrivateStatic("IsBasicChestName", "chest")!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickChest_RecognizesBasicChest_WhenSettingsAllow()
        {
            // Call internal helper directly - pass primitive path and renderName to avoid mutating ExileCore objects
            var res = (string?)InvokePrivateStatic("GetChestMechanicIdInternal", true, false, EntityType.Chest, "content/some/chest", "Tribal Chest")!;
            res.Should().Be("basic-chests");
        }

        [TestMethod]
        public void IsSettlersOrePath_UsesStrictSettlersNodeMarkers()
        {
            var settlersNodePath = "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron";
            var monsterPath = "Metadata/Monsters/LeagueKalguur/CrimsonIron/SmallGrowthMaps@83";

            var settlersMatch = (bool)InvokePrivateStatic("IsSettlersOrePath", settlersNodePath)!;
            settlersMatch.Should().BeTrue();

            var monsterMatch = (bool)InvokePrivateStatic("IsSettlersOrePath", monsterPath)!;
            monsterMatch.Should().BeFalse();
        }

        [TestMethod]
        public void GetAreaTransitionMechanicId_UsesLabyrinthToggleForTrialPortals()
        {
            string path = "Metadata/Terrain/Labyrinth/Objects/LabyrinthTrialPortalAreaTransition";

            var disabled = (string?)InvokePrivateStatic(
                "GetAreaTransitionMechanicId",
                true,
                false,
                EntityType.AreaTransition,
                path);

            var enabled = (string?)InvokePrivateStatic(
                "GetAreaTransitionMechanicId",
                false,
                true,
                EntityType.AreaTransition,
                path);

            disabled.Should().BeNull();
            enabled.Should().Be("labyrinth-trials");
        }

        [TestMethod]
        public void GetAreaTransitionMechanicId_UsesAreaTransitionToggleForNonLabyrinthTransitions()
        {
            string path = "Metadata/Terrain/Leagues/Delve/Objects/SomeAreaTransition";

            var disabled = (string?)InvokePrivateStatic(
                "GetAreaTransitionMechanicId",
                false,
                true,
                EntityType.AreaTransition,
                path);

            var enabled = (string?)InvokePrivateStatic(
                "GetAreaTransitionMechanicId",
                true,
                false,
                EntityType.AreaTransition,
                path);

            disabled.Should().BeNull();
            enabled.Should().Be("area-transitions");
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
        public void ShouldAllowIncubatorStackMatchCore_RequiresMatchingLevels_WhenIncubatorPathRuleApplies()
        {
            ((bool)InvokePrivateStatic("ShouldAllowIncubatorStackMatchCore", true, true, 68, true, 69)!).Should().BeFalse();
            ((bool)InvokePrivateStatic("ShouldAllowIncubatorStackMatchCore", true, true, 68, true, 68)!).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldAllowIncubatorStackMatchCore_Rejects_WhenEitherLevelIsMissing()
        {
            ((bool)InvokePrivateStatic("ShouldAllowIncubatorStackMatchCore", true, false, 68, true, 68)!).Should().BeFalse();
            ((bool)InvokePrivateStatic("ShouldAllowIncubatorStackMatchCore", true, true, 68, false, 68)!).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowIncubatorStackMatchCore_AllowsAllLevels_WhenRuleDoesNotApply()
        {
            ((bool)InvokePrivateStatic("ShouldAllowIncubatorStackMatchCore", false, false, 0, false, 0)!).Should().BeTrue();
            ((bool)InvokePrivateStatic("ShouldAllowIncubatorStackMatchCore", false, true, 1, true, 999)!).Should().BeTrue();
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

        [TestMethod]
        public void SelectBestWorldItemMetadataPath_PrefersComponentMetadata_ForMiscObjectsFallback()
        {
            string selected = (string)InvokePrivateStatic(
                "SelectBestWorldItemMetadataPath",
                "Metadata/MiscellaneousObjects/Monolith",
                "Metadata/Items/Currency/CurrencyQuality/Catalyst/ImbuedCatalyst")!;

            selected.Should().Be("Metadata/Items/Currency/CurrencyQuality/Catalyst/ImbuedCatalyst");
        }

        [TestMethod]
        public void SelectBestWorldItemMetadataPath_KeepsResolvedMetadata_WhenAlreadySpecific()
        {
            string selected = (string)InvokePrivateStatic(
                "SelectBestWorldItemMetadataPath",
                "Metadata/Items/Currency/StackableCurrency/ChaosOrb",
                "Metadata/Items/Currency/CurrencyQuality/Catalyst/ImbuedCatalyst")!;

            selected.Should().Be("Metadata/Items/Currency/StackableCurrency/ChaosOrb");
        }
    }
}
