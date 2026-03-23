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
        public void ShouldSkipWorldItemAllocatedToSomeoneElse_ReturnsTrue_WhenAllocatedFlagIsTrue()
        {
            var res = (bool)InvokePrivateStatic("ShouldSkipWorldItemAllocatedToSomeoneElse", true)!;
            res.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldSkipWorldItemAllocatedToSomeoneElse_ReturnsFalse_WhenAllocatedFlagIsFalse()
        {
            var res = (bool)InvokePrivateStatic("ShouldSkipWorldItemAllocatedToSomeoneElse", false)!;
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSkipWorldItemAllocatedToSomeoneElse_ReturnsFalse_WhenAllocatedFlagIsNull()
        {
            var res = (bool)InvokePrivateStatic("ShouldSkipWorldItemAllocatedToSomeoneElse", (bool?)null)!;
            res.Should().BeFalse();
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
            var res = (string?)InvokePrivateStatic("GetChestMechanicIdInternal", true, false, true, true, true, true, true, true, true, true, EntityType.Chest, "content/some/chest", "Tribal Chest")!;
            res.Should().Be("basic-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesOtherLeagueToggle_ForNonMirageLeagueChests()
        {
            var disabled = (string?)InvokePrivateStatic(
                "GetChestMechanicIdInternal",
                false,
                true,
                false,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                EntityType.Chest,
                "content/some/chest",
                "Some League Chest")!;

            var enabled = (string?)InvokePrivateStatic(
                "GetChestMechanicIdInternal",
                false,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                EntityType.Chest,
                "content/some/chest",
                "Some League Chest")!;

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesHeistToggle_ForSecureLockerRenderName()
        {
            var disabled = (string?)InvokePrivateStatic(
                "GetChestMechanicIdInternal",
                false,
                true,
                false,
                true,
                true,
                true,
                false,
                true,
                true,
                true,
                EntityType.Chest,
                "content/heist/chest",
                "Secure Locker")!;

            var enabled = (string?)InvokePrivateStatic(
                "GetChestMechanicIdInternal",
                false,
                true,
                false,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                EntityType.Chest,
                "content/heist/chest",
                "Secure Locker")!;

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesHeistToggle_ForLeagueHeistMetadataPath()
        {
            const string heistPath = "Metadata/Chests/LeagueHeist/MilitaryChests/HeistChestPathMilitary";

            var disabled = (string?)InvokePrivateStatic(
                "GetChestMechanicIdInternal",
                false,
                true,
                true,
                true,
                true,
                true,
                false,
                true,
                true,
                true,
                EntityType.Chest,
                heistPath,
                "Military Supplies")!;

            var enabled = (string?)InvokePrivateStatic(
                "GetChestMechanicIdInternal",
                false,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                EntityType.Chest,
                heistPath,
                "Military Supplies")!;

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesBlightCystToggle_ForBlightChestMetadataPath()
        {
            const string blightPath = "Metadata/Chests/Blight/BlightChestObject";

            var disabled = (string?)InvokePrivateStatic(
                "GetChestMechanicIdInternal",
                false,
                true,
                false,
                true,
                true,
                true,
                true,
                false,
                true,
                true,
                EntityType.Chest,
                blightPath,
                "Blight Cyst")!;

            var enabled = (string?)InvokePrivateStatic(
                "GetChestMechanicIdInternal",
                false,
                true,
                false,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                EntityType.Chest,
                blightPath,
                "Blight Cyst")!;

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesBreachToggle_ForGraspingCoffersMetadataPath()
        {
            const string breachPath = "Metadata/Chests/Breach/BreachBoxChest02";

            var disabled = (string?)InvokePrivateStatic(
                "GetChestMechanicIdInternal",
                false,
                true,
                false,
                true,
                true,
                true,
                true,
                true,
                false,
                true,
                EntityType.Chest,
                breachPath,
                "Grasping Coffers")!;

            var enabled = (string?)InvokePrivateStatic(
                "GetChestMechanicIdInternal",
                false,
                true,
                false,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                EntityType.Chest,
                breachPath,
                "Grasping Coffers")!;

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesSynthesisToggle_ForSynthesisedStashMetadataPath()
        {
            const string synthesisPath = "Metadata/Chests/SynthesisChests/SynthesisChest";

            var disabled = (string?)InvokePrivateStatic(
                "GetChestMechanicIdInternal",
                false,
                true,
                false,
                true,
                true,
                true,
                true,
                true,
                true,
                false,
                EntityType.Chest,
                synthesisPath,
                "Synthesised Stash")!;

            var enabled = (string?)InvokePrivateStatic(
                "GetChestMechanicIdInternal",
                false,
                true,
                false,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                EntityType.Chest,
                synthesisPath,
                "Synthesised Stash")!;

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
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
