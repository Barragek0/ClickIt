using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Collections.Generic;
using ClickIt.Services;
using ClickIt.Definitions;
using ClickIt.Services.Label.Classification;
using ClickIt.Services.Label.Selection;
using ExileCore;
using ExileCore.Shared.Enums;

namespace ClickIt.Tests.Label.Classification
{
    [TestClass]
    public class LabelClassificationPolicyTests
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

                return MechanicClassifier.GetChestMechanicIdFromConfiguredRules(
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
            var rh1 = MechanicClassifier.IsHarvestPath("Some/Harvest/Irrigator/Path");
            rh1.Should().BeTrue();
            var rh2 = MechanicClassifier.IsHarvestPath("Nothing/Here");
            rh2.Should().BeFalse();

            var rs1 = MechanicClassifier.IsSettlersOrePath("Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/PetrifiedWood");
            rs1.Should().BeTrue();
            var rs2 = MechanicClassifier.IsSettlersOrePath("Random/Path");
            rs2.Should().BeFalse();

            var verisiumPath = "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/Verisium";
            var rs3 = MechanicClassifier.IsSettlersVerisiumPath(verisiumPath);
            rs3.Should().BeTrue();

            var rs4 = MechanicClassifier.IsSettlersVerisiumPath(verisiumPath.ToLowerInvariant());
            rs4.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldAllowHarvestRootElementVisibility_OnlyAppliesToHarvestPaths()
        {
            LabelTargetabilityPolicy.ShouldAllowHarvestRootElementVisibility(
                "Metadata/MiscellaneousObjects/Harvest/Irrigator",
                harvestRootElementVisible: false).Should().BeFalse();

            LabelTargetabilityPolicy.ShouldAllowHarvestRootElementVisibility(
                "Metadata/MiscellaneousObjects/Harvest/Extractor",
                harvestRootElementVisible: true).Should().BeTrue();

            LabelTargetabilityPolicy.ShouldAllowHarvestRootElementVisibility(
                "Metadata/MiscellaneousObjects/Leagues/Ritual/Something",
                harvestRootElementVisible: false).Should().BeTrue();

            LabelTargetabilityPolicy.ShouldAllowHarvestRootElementVisibility(
                null,
                harvestRootElementVisible: false).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickAltar_RequiresFlagAndPathPatterns()
        {
            var ra1 = MechanicClassifier.ShouldClickAltar(false, false, false, false, string.Empty);
            ra1.Should().BeFalse();

            var ra2 = MechanicClassifier.ShouldClickAltar(false, false, false, false, "CleansingFireAltar");
            ra2.Should().BeFalse();

            var ra3 = MechanicClassifier.ShouldClickAltar(true, false, false, false, "Some/CleansingFireAltar/Here");
            ra3.Should().BeTrue();
            var ra4 = MechanicClassifier.ShouldClickAltar(false, true, false, false, "This/TangleAltar");
            ra4.Should().BeTrue();
        }

        [TestMethod]
        public void IsBasicChestName_AcceptsExpectedNames_IgnoresCase()
        {
            var cb1 = MechanicClassifier.IsBasicChestName("chest");
            cb1.Should().BeTrue();
            var cb2 = MechanicClassifier.IsBasicChestName("Golden Chest");
            cb2.Should().BeTrue();
            var cb3 = MechanicClassifier.IsBasicChestName("weapon rack");
            cb3.Should().BeTrue();
            var cb4 = MechanicClassifier.IsBasicChestName("mystery");
            cb4.Should().BeFalse();
            var cb5 = MechanicClassifier.IsBasicChestName(null);
            cb5.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSkipUntargetableEntity_RespectsLabelEntityAndItemTargetableState()
        {
            LabelTargetabilityPolicy.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: true,
                labelEntityTargetable: false,
                itemIsTargetable: true).Should().BeTrue();

            LabelTargetabilityPolicy.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: true,
                labelEntityTargetable: true,
                itemIsTargetable: false).Should().BeTrue();

            LabelTargetabilityPolicy.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: false,
                labelEntityTargetable: true,
                itemIsTargetable: false).Should().BeTrue();

            LabelTargetabilityPolicy.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: false,
                labelEntityTargetable: true,
                itemIsTargetable: false,
                allowNullEntityFallback: true).Should().BeFalse();

            LabelTargetabilityPolicy.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: true,
                labelEntityTargetable: true,
                itemIsTargetable: true).Should().BeFalse();
        }

        [TestMethod]
        public void ResolveLabelEntityTargetableFromRaw_ReadsDynamicIsTargetable_WhenEntityIsNotMemoryObjectEntity()
        {
            object? rawLabelEntity = new FakeTargetableEntity { IsTargetable = false };

            LabelTargetabilityPolicy.ResolveLabelEntityTargetableFromRaw(rawLabelEntity, out bool hasTargetable, out bool targetable);
            hasTargetable.Should().BeTrue();
            targetable.Should().BeFalse();

            LabelTargetabilityPolicy.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: true,
                labelEntityTargetable: false,
                itemIsTargetable: true).Should().BeTrue();

            LabelTargetabilityPolicy.ShouldSkipUntargetableEntity(
                hasLabelEntityTargetable: false,
                labelEntityTargetable: true,
                itemIsTargetable: false,
                allowNullEntityFallback: true).Should().BeFalse();
        }

        [TestMethod]
        public void RequiresTargetabilityGate_IsLimitedToSettlersOrePaths()
        {
            var settlersOre = LabelTargetabilityPolicy.RequiresTargetabilityGate("Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/PetrifiedWood");
            settlersOre.Should().BeTrue();

            var nonSettlersPath = LabelTargetabilityPolicy.RequiresTargetabilityGate("Metadata/Terrain/Leagues/Delve/Objects/AzuriteVein");
            nonSettlersPath.Should().BeFalse();

            var emptyPath = LabelTargetabilityPolicy.RequiresTargetabilityGate(string.Empty);
            emptyPath.Should().BeFalse();
        }
    }
}