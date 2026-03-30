using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Reflection;
using ClickIt.Services;
using ClickIt.Definitions;
using ExileCore;
using ExileCore.Shared.Enums;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class LabelFilterServiceHelpersTests
    {
        public sealed class FakeTargetableEntity
        {
            public bool IsTargetable { get; set; }
        }


        [TestMethod]
        public void GetChestMechanicIdFromConfiguredRules_Behaves_Correctly_ForVariousNames()
        {
            static IReadOnlySet<string> BuildEnabledLeagueChestSpecificIds(
                bool clickMirageGoldenDjinnCache,
                bool clickMirageSilverDjinnCache,
                bool clickMirageBronzeDjinnCache,
                bool clickHeistSecureLocker,
                bool clickBlightCyst,
                bool clickBreachGraspingCoffers,
                bool clickSynthesisSynthesisedStash)
            {
                HashSet<string> enabled = new(StringComparer.OrdinalIgnoreCase);
                if (clickMirageGoldenDjinnCache) enabled.Add(MechanicIds.MirageGoldenDjinnCache);
                if (clickMirageSilverDjinnCache) enabled.Add(MechanicIds.MirageSilverDjinnCache);
                if (clickMirageBronzeDjinnCache) enabled.Add(MechanicIds.MirageBronzeDjinnCache);
                if (clickHeistSecureLocker) enabled.Add(MechanicIds.HeistSecureLocker);
                if (clickBlightCyst) enabled.Add(MechanicIds.BlightCyst);
                if (clickBreachGraspingCoffers) enabled.Add(MechanicIds.BreachGraspingCoffers);
                if (clickSynthesisSynthesisedStash) enabled.Add(MechanicIds.SynthesisSynthesisedStash);
                return enabled;
            }

            string? ResolveChestMechanic(
                bool clickBasicChests,
                bool clickLeagueChests,
                bool clickLeagueChestsOther,
                bool clickMirageGoldenDjinnCache,
                bool clickMirageSilverDjinnCache,
                bool clickMirageBronzeDjinnCache,
                bool clickHeistSecureLocker,
                bool clickBlightCyst,
                bool clickBreachGraspingCoffers,
                bool clickSynthesisSynthesisedStash,
                EntityType type,
                string? path,
                string renderName)
            {
                IReadOnlySet<string> enabledSpecificLeagueChestIds = BuildEnabledLeagueChestSpecificIds(
                    clickMirageGoldenDjinnCache,
                    clickMirageSilverDjinnCache,
                    clickMirageBronzeDjinnCache,
                    clickHeistSecureLocker,
                    clickBlightCyst,
                    clickBreachGraspingCoffers,
                    clickSynthesisSynthesisedStash);

                return LabelFilterService.GetChestMechanicIdFromConfiguredRules(
                    clickBasicChests,
                    clickLeagueChests,
                    clickLeagueChestsOther,
                    enabledSpecificLeagueChestIds,
                    type,
                    path,
                    renderName);
            }

            var res1 = ResolveChestMechanic(true, true, true, true, true, true, true, true, true, true, EntityType.Monster, "some/path", "chest");
            res1.Should().BeNull();

            var res2 = ResolveChestMechanic(true, false, true, true, true, true, true, true, true, true, EntityType.Chest, null, "Chest");
            res2.Should().Be("basic-chests");

            var res3 = ResolveChestMechanic(false, true, true, true, true, true, true, true, true, true, EntityType.Chest, null, "Some League");
            res3.Should().Be("league-chests");

            var res4 = ResolveChestMechanic(true, true, true, true, true, true, true, true, true, true, EntityType.Chest, "StrongBoxes/Strongbox", "strongbox");
            res4.Should().BeNull();

            var mirageDisabled = ResolveChestMechanic(false, true, false, false, false, false, false, true, true, true, EntityType.Chest, null, "Golden Djinn's Cache");
            mirageDisabled.Should().BeNull();

            var mirageEnabled = ResolveChestMechanic(false, true, false, true, false, false, false, true, true, true, EntityType.Chest, null, "Golden Djinn's Cache");
            mirageEnabled.Should().Be("league-chests");

            var heistDisabled = ResolveChestMechanic(false, true, false, true, true, true, false, true, true, true, EntityType.Chest, null, "Secure Locker");
            heistDisabled.Should().BeNull();

            var heistEnabled = ResolveChestMechanic(false, true, false, true, true, true, true, true, true, true, EntityType.Chest, null, "Secure Locker");
            heistEnabled.Should().Be("league-chests");

            const string blightCystPath = "Metadata/Chests/Blight/BlightChestObject";
            var blightDisabled = ResolveChestMechanic(false, true, false, true, true, true, true, false, true, true, EntityType.Chest, blightCystPath, "Blight Cyst");
            blightDisabled.Should().BeNull();

            var blightEnabled = ResolveChestMechanic(false, true, false, true, true, true, true, true, true, true, EntityType.Chest, blightCystPath, "Blight Cyst");
            blightEnabled.Should().Be("league-chests");

            const string breachGraspingCoffersPath = "Metadata/Chests/Breach/BreachBoxChest02";
            var breachDisabled = ResolveChestMechanic(false, true, false, true, true, true, true, true, false, true, EntityType.Chest, breachGraspingCoffersPath, "Grasping Coffers");
            breachDisabled.Should().BeNull();

            var breachEnabled = ResolveChestMechanic(false, true, false, true, true, true, true, true, true, true, EntityType.Chest, breachGraspingCoffersPath, "Grasping Coffers");
            breachEnabled.Should().Be("league-chests");

            const string synthesisStashPath = "Metadata/Chests/SynthesisChests/SynthesisChest";
            var synthesisDisabled = ResolveChestMechanic(false, true, false, true, true, true, true, true, true, false, EntityType.Chest, synthesisStashPath, "Synthesised Stash");
            synthesisDisabled.Should().BeNull();

            var synthesisEnabled = ResolveChestMechanic(false, true, false, true, true, true, true, true, true, true, EntityType.Chest, synthesisStashPath, "Synthesised Stash");
            synthesisEnabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void PathHelpers_DetectHarvestAndSettlersOrePaths()
        {
            var rh1 = LabelFilterService.IsHarvestPath("Some/Harvest/Irrigator/Path");
            rh1.Should().BeTrue();
            var rh2 = LabelFilterService.IsHarvestPath("Nothing/Here");
            rh2.Should().BeFalse();

            var rs1 = LabelFilterService.IsSettlersOrePath("Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/PetrifiedWood");
            rs1.Should().BeTrue();
            var rs2 = LabelFilterService.IsSettlersOrePath("Random/Path");
            rs2.Should().BeFalse();

            var verisiumPath = "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/Verisium";
            var rs3 = LabelFilterService.IsSettlersVerisiumPath(verisiumPath);
            rs3.Should().BeTrue();

            var rs4 = LabelFilterService.IsSettlersVerisiumPath(verisiumPath.ToLowerInvariant());
            rs4.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldAllowHarvestRootElementVisibility_OnlyAppliesToHarvestPaths()
        {
            LabelFilterService.ShouldAllowHarvestRootElementVisibility(
                "Metadata/MiscellaneousObjects/Harvest/Irrigator",
                harvestRootElementVisible: false).Should().BeFalse();

            LabelFilterService.ShouldAllowHarvestRootElementVisibility(
                "Metadata/MiscellaneousObjects/Harvest/Extractor",
                harvestRootElementVisible: true).Should().BeTrue();

            LabelFilterService.ShouldAllowHarvestRootElementVisibility(
                "Metadata/MiscellaneousObjects/Leagues/Ritual/Something",
                harvestRootElementVisible: false).Should().BeTrue();

            LabelFilterService.ShouldAllowHarvestRootElementVisibility(
                null,
                harvestRootElementVisible: false).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickAltar_RequiresFlagAndPathPatterns()
        {
            var ra1 = LabelFilterService.ShouldClickAltar(false, false, false, false, string.Empty);
            ra1.Should().BeFalse();

            var ra2 = LabelFilterService.ShouldClickAltar(false, false, false, false, "CleansingFireAltar");
            ra2.Should().BeFalse();

            var ra3 = LabelFilterService.ShouldClickAltar(true, false, false, false, "Some/CleansingFireAltar/Here");
            ra3.Should().BeTrue();
            var ra4 = LabelFilterService.ShouldClickAltar(false, true, false, false, "This/TangleAltar");
            ra4.Should().BeTrue();
        }

        [TestMethod]
        public void IsBasicChestName_AcceptsExpectedNames_IgnoresCase()
        {
            var cb1 = LabelFilterService.IsBasicChestName("chest");
            cb1.Should().BeTrue();
            var cb2 = LabelFilterService.IsBasicChestName("Golden Chest");
            cb2.Should().BeTrue();
            var cb3 = LabelFilterService.IsBasicChestName("weapon rack");
            cb3.Should().BeTrue();
            var cb4 = LabelFilterService.IsBasicChestName("mystery");
            cb4.Should().BeFalse();
            var cb5 = LabelFilterService.IsBasicChestName(null);
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
            var settlersOre = LabelFilterService.RequiresTargetabilityGate("Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/PetrifiedWood");
            settlersOre.Should().BeTrue();

            var nonSettlersPath = LabelFilterService.RequiresTargetabilityGate("Metadata/Terrain/Leagues/Delve/Objects/AzuriteVein");
            nonSettlersPath.Should().BeFalse();

            var emptyPath = LabelFilterService.RequiresTargetabilityGate(string.Empty);
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
            LabelFilterService.ShouldBlockLazyModeForNearbyMonsters(
                0,
                0,
                2,
                3,
                1,
                1,
                0,
                1).Should().BeTrue();

            LabelFilterService.ShouldBlockLazyModeForNearbyMonsters(
                1,
                1,
                0,
                3,
                0,
                1,
                0,
                1).Should().BeTrue();

            LabelFilterService.ShouldBlockLazyModeForNearbyMonsters(
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
            LabelFilterService.ShouldBlockLazyModeForNearbyMonsters(
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
            string reason = LabelFilterService.BuildNearbyMonsterBlockReason(
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
            string reason = LabelFilterService.BuildNearbyMonsterBlockReason(
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
