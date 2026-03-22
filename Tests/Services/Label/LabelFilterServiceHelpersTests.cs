using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
using ClickIt.Services;
using ExileCore;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceHelpersTests
    {
        public sealed class FakeTargetableEntity
        {
            public bool IsTargetable { get; set; }
        }


        private static T InvokePrivateStatic<T>(string methodName, params object?[] args)
        {
            var method = typeof(LabelFilterService).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();
            var value = method!.Invoke(null, args);
            if (value == null)
                return default!;

            value.Should().BeAssignableTo<T>();
            return (T)value;
        }

        [TestMethod]
        public void GetChestMechanicIdInternal_Behaves_Correctly_ForVariousNames()
        {
            var res1 = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", true, true, true, true, true, true, true, true, ExileCore.Shared.Enums.EntityType.Monster, "some/path", "chest");
            res1.Should().BeNull();

            var res2 = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", true, false, true, true, true, true, true, true, ExileCore.Shared.Enums.EntityType.Chest, null, "Chest");
            res2.Should().Be("basic-chests");

            var res3 = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", false, true, true, true, true, true, true, true, ExileCore.Shared.Enums.EntityType.Chest, null, "Some League");
            res3.Should().Be("league-chests");

            var res4 = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", true, true, true, true, true, true, true, true, ExileCore.Shared.Enums.EntityType.Chest, "StrongBoxes/Strongbox", "strongbox");
            res4.Should().BeNull();

            var mirageDisabled = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", false, true, false, false, false, false, false, true, ExileCore.Shared.Enums.EntityType.Chest, null, "Golden Djinn's Cache");
            mirageDisabled.Should().BeNull();

            var mirageEnabled = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", false, true, false, true, false, false, false, true, ExileCore.Shared.Enums.EntityType.Chest, null, "Golden Djinn's Cache");
            mirageEnabled.Should().Be("league-chests");

            var heistDisabled = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", false, true, false, true, true, true, false, true, ExileCore.Shared.Enums.EntityType.Chest, null, "Secure Locker");
            heistDisabled.Should().BeNull();

            var heistEnabled = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", false, true, false, true, true, true, true, true, ExileCore.Shared.Enums.EntityType.Chest, null, "Secure Locker");
            heistEnabled.Should().Be("league-chests");

            const string breachGraspingCoffersPath = "Metadata/Chests/Breach/BreachBoxChest02";
            var breachDisabled = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", false, true, false, true, true, true, true, false, ExileCore.Shared.Enums.EntityType.Chest, breachGraspingCoffersPath, "Grasping Coffers");
            breachDisabled.Should().BeNull();

            var breachEnabled = InvokePrivateStatic<string?>("GetChestMechanicIdInternal", false, true, false, true, true, true, true, true, ExileCore.Shared.Enums.EntityType.Chest, breachGraspingCoffersPath, "Grasping Coffers");
            breachEnabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void PathHelpers_DetectHarvestAndSettlersOrePaths()
        {
            var rh1 = InvokePrivateStatic<bool>("IsHarvestPath", "Some/Harvest/Irrigator/Path");
            rh1.Should().BeTrue();
            var rh2 = InvokePrivateStatic<bool>("IsHarvestPath", "Nothing/Here");
            rh2.Should().BeFalse();

            var rs1 = InvokePrivateStatic<bool>("IsSettlersOrePath", "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/PetrifiedWood");
            rs1.Should().BeTrue();
            var rs2 = InvokePrivateStatic<bool>("IsSettlersOrePath", "Random/Path");
            rs2.Should().BeFalse();

            var verisiumPath = "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/Verisium";
            var rs3 = InvokePrivateStatic<bool>("IsSettlersVerisiumPath", verisiumPath);
            rs3.Should().BeTrue();

            var rs4 = InvokePrivateStatic<bool>("IsSettlersVerisiumPath", verisiumPath.ToLowerInvariant());
            rs4.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickAltar_RequiresFlagAndPathPatterns()
        {
            var ra1 = InvokePrivateStatic<bool>("ShouldClickAltar", false, false, false, false, string.Empty);
            ra1.Should().BeFalse();

            var ra2 = InvokePrivateStatic<bool>("ShouldClickAltar", false, false, false, false, "CleansingFireAltar");
            ra2.Should().BeFalse();

            var ra3 = InvokePrivateStatic<bool>("ShouldClickAltar", true, false, false, false, "Some/CleansingFireAltar/Here");
            ra3.Should().BeTrue();
            var ra4 = InvokePrivateStatic<bool>("ShouldClickAltar", false, true, false, false, "This/TangleAltar");
            ra4.Should().BeTrue();
        }

        [TestMethod]
        public void IsBasicChestName_AcceptsExpectedNames_IgnoresCase()
        {
            var cb1 = InvokePrivateStatic<bool>("IsBasicChestName", "chest");
            cb1.Should().BeTrue();
            var cb2 = InvokePrivateStatic<bool>("IsBasicChestName", "Golden Chest");
            cb2.Should().BeTrue();
            var cb3 = InvokePrivateStatic<bool>("IsBasicChestName", "weapon rack");
            cb3.Should().BeTrue();
            var cb4 = InvokePrivateStatic<bool>("IsBasicChestName", "mystery");
            cb4.Should().BeFalse();
            var cb5 = InvokePrivateStatic<bool>("IsBasicChestName", (string?)null);
            cb5.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSkipUntargetableEntity_RespectsLabelEntityAndItemTargetableState()
        {
            LabelFilterService.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: true,
                labelEntityTargetable: false,
                itemIsTargetable: true).Should().BeTrue();

            LabelFilterService.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: true,
                labelEntityTargetable: true,
                itemIsTargetable: false).Should().BeTrue();

            LabelFilterService.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: false,
                labelEntityTargetable: true,
                itemIsTargetable: false).Should().BeTrue();

            LabelFilterService.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: false,
                labelEntityTargetable: true,
                itemIsTargetable: false,
                allowNullEntityFallback: true).Should().BeFalse();

            LabelFilterService.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: true,
                labelEntityTargetable: true,
                itemIsTargetable: true).Should().BeFalse();
        }

        [TestMethod]
        public void ResolveLabelEntityTargetableFromRaw_ReadsDynamicIsTargetable_WhenEntityIsNotMemoryObjectEntity()
        {
            object? rawLabelEntity = new FakeTargetableEntity { IsTargetable = false };

            LabelFilterService.ResolveLabelEntityTargetableFromRaw(rawLabelEntity, out bool hasTargetable, out bool targetable);
            hasTargetable.Should().BeTrue();
            targetable.Should().BeFalse();

            LabelFilterService.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: true,
                labelEntityTargetable: false,
                itemIsTargetable: true).Should().BeTrue();

            LabelFilterService.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: false,
                labelEntityTargetable: true,
                itemIsTargetable: false,
                allowNullEntityFallback: true).Should().BeFalse();
        }

        [TestMethod]
        public void RequiresTargetabilityGate_IsLimitedToSettlersOrePaths()
        {
            var settlersOre = InvokePrivateStatic<bool>("RequiresTargetabilityGate", "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/PetrifiedWood");
            settlersOre.Should().BeTrue();

            var nonSettlersPath = InvokePrivateStatic<bool>("RequiresTargetabilityGate", "Metadata/Terrain/Leagues/Delve/Objects/AzuriteVein");
            nonSettlersPath.Should().BeFalse();

            var emptyPath = InvokePrivateStatic<bool>("RequiresTargetabilityGate", string.Empty);
            emptyPath.Should().BeFalse();
        }

        [TestMethod]
        public void SelectionDebugSummary_ToCompactString_IncludesExpectedCounters()
        {
            var summary = new LabelFilterService.SelectionDebugSummary(
                Start: 0,
                End: 5,
                Total: 5,
                NullLabel: 1,
                NullEntity: 1,
                OutOfDistance: 1,
                Untargetable: 1,
                NoMechanic: 1,
                WorldItem: 3,
                WorldItemMetadataRejected: 2,
                SettlersPathSeen: 2,
                SettlersMechanicMatched: 1,
                SettlersMechanicDisabled: 1);

            summary.ToCompactString().Should().Be("r:0-5 t:5 nl:1 ne:1 d:1 u:1 nm:1 wi:3/2 sp:2 sm:1 sd:1");
        }

        [TestMethod]
        public void ClearInventoryProbeCacheForShutdown_ResetsStaticCacheState()
        {
            var t = typeof(LabelFilterService);
            var gameController = (GameController)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            t.GetField("_inventoryProbeCacheTimestampMs", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, 123L);
            t.GetField("_inventoryProbeCacheController", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, gameController);
            t.GetField("_inventoryProbeCacheHasValue", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, true);

            t.GetField("_inventoryItemsCacheTimestampMs", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, 456L);
            t.GetField("_inventoryItemsCacheController", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, gameController);
            t.GetField("_inventoryItemsCacheHasValue", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, true);

            LabelFilterService.ClearInventoryProbeCacheForShutdown();

            ((long)t.GetField("_inventoryProbeCacheTimestampMs", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!).Should().Be(0L);
            t.GetField("_inventoryProbeCacheController", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null).Should().BeNull();
            ((bool)t.GetField("_inventoryProbeCacheHasValue", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!).Should().BeFalse();

            ((long)t.GetField("_inventoryItemsCacheTimestampMs", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!).Should().Be(0L);
            t.GetField("_inventoryItemsCacheController", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null).Should().BeNull();
            ((bool)t.GetField("_inventoryItemsCacheHasValue", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowPetrifiedWoodTargetability_UsesEntityWhenPresent_AndAllowsWhenMissing()
        {
            LabelFilterService.ShouldAllowPetrifiedWoodTargetability(
                hasLabelEntityTargetable: false,
                labelEntityTargetable: false).Should().BeTrue();

            LabelFilterService.ShouldAllowPetrifiedWoodTargetability(
                hasLabelEntityTargetable: true,
                labelEntityTargetable: false).Should().BeFalse();

            LabelFilterService.ShouldAllowPetrifiedWoodTargetability(
                hasLabelEntityTargetable: true,
                labelEntityTargetable: true).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldApplyPetrifiedWoodEntityTargetabilityGate_IsTrueOnlyForPetrifiedWood()
        {
            LabelFilterService.ShouldApplyPetrifiedWoodEntityTargetabilityGate(
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/PetrifiedWood").Should().BeTrue();

            LabelFilterService.ShouldApplyPetrifiedWoodEntityTargetabilityGate(
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/Copper").Should().BeFalse();

            LabelFilterService.ShouldApplyPetrifiedWoodEntityTargetabilityGate(
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron").Should().BeFalse();

            LabelFilterService.ShouldApplyPetrifiedWoodEntityTargetabilityGate(
                "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/Verisium").Should().BeFalse();
        }

        [TestMethod]
        public void ShouldBlockLazyModeForNearbyMonsters_ReturnsTrue_WhenAnyEnabledThresholdIsMet()
        {
            InvokePrivateStatic<bool>(
                "ShouldBlockLazyModeForNearbyMonsters",
                0,
                0,
                2,
                3,
                1,
                1,
                0,
                1).Should().BeTrue();

            InvokePrivateStatic<bool>(
                "ShouldBlockLazyModeForNearbyMonsters",
                1,
                1,
                0,
                3,
                0,
                1,
                0,
                1).Should().BeTrue();

            InvokePrivateStatic<bool>(
                "ShouldBlockLazyModeForNearbyMonsters",
                0,
                0,
                0,
                3,
                0,
                1,
                1,
                1).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldBlockLazyModeForNearbyMonsters_ReturnsFalse_WhenNoEnabledThresholdIsMet()
        {
            InvokePrivateStatic<bool>(
                "ShouldBlockLazyModeForNearbyMonsters",
                5,
                0,
                2,
                3,
                0,
                1,
                0,
                1).Should().BeFalse();
        }

        [TestMethod]
        public void BuildNearbyMonsterBlockReason_IncludesOnlyTriggeredThresholds()
        {
            string reason = InvokePrivateStatic<string>(
                "BuildNearbyMonsterBlockReason",
                0,
                2,
                50,
                false,
                3,
                3,
                50,
                true,
                2,
                1,
                50,
                true,
                0,
                1,
                50,
                false);

            reason.Should().Contain("Magic 3/3 within 50");
            reason.Should().Contain("Rare 2/1 within 50");
            reason.Should().NotContain("Normal");
            reason.Should().NotContain("Unique");
        }

        [TestMethod]
        public void BuildNearbyMonsterBlockReason_UsesFallback_WhenNoTriggeredThresholds()
        {
            string reason = InvokePrivateStatic<string>(
                "BuildNearbyMonsterBlockReason",
                0,
                2,
                50,
                false,
                0,
                3,
                50,
                false,
                0,
                1,
                50,
                false,
                0,
                1,
                50,
                false);

            reason.Should().Be("Nearby monster threshold reached");
        }

    }
}
