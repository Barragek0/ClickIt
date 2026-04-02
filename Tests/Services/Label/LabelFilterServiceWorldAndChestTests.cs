using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ExileCore.Shared.Enums;
using ClickIt.Definitions;
using ClickIt.Services.Label.Classification;

namespace ClickIt.Tests.Label
{
    [TestClass]
    public class LabelFilterServiceWorldAndChestTests
    {
        private static string? InvokeChestMechanicFromConfiguredRules(
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
            HashSet<string> enabledSpecificLeagueChestIds = new(StringComparer.OrdinalIgnoreCase);
            if (clickMirageGoldenDjinnCache) enabledSpecificLeagueChestIds.Add(MechanicIds.MirageGoldenDjinnCache);
            if (clickMirageSilverDjinnCache) enabledSpecificLeagueChestIds.Add(MechanicIds.MirageSilverDjinnCache);
            if (clickMirageBronzeDjinnCache) enabledSpecificLeagueChestIds.Add(MechanicIds.MirageBronzeDjinnCache);
            if (clickHeistSecureLocker) enabledSpecificLeagueChestIds.Add(MechanicIds.HeistSecureLocker);
            if (clickBlightCyst) enabledSpecificLeagueChestIds.Add(MechanicIds.BlightCyst);
            if (clickBreachGraspingCoffers) enabledSpecificLeagueChestIds.Add(MechanicIds.BreachGraspingCoffers);
            if (clickSynthesisSynthesisedStash) enabledSpecificLeagueChestIds.Add(MechanicIds.SynthesisSynthesisedStash);

            return MechanicClassifier.GetChestMechanicIdFromConfiguredRules(
                clickBasicChests,
                clickLeagueChests,
                clickLeagueChestsOther,
                enabledSpecificLeagueChestIds,
                type,
                path,
                renderName);
        }

        [TestMethod]
        public void ShouldClickWorldItemCore_ReturnsFalse_WhenClickItemsDisabled()
        {
            var res = MechanicClassifier.ShouldClickWorldItemCore(false, EntityType.WorldItem, itemPath: null);
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickWorldItemCore_ReturnsFalse_WhenPathContainsStrongbox()
        {
            var res = MechanicClassifier.ShouldClickWorldItemCore(true, EntityType.WorldItem, "some/StrongBoxes/Strongbox/x");
            res.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldClickWorldItemCore_ReturnsTrue_WhenEnabledAndNotStrongbox()
        {
            var res = MechanicClassifier.ShouldClickWorldItemCore(true, EntityType.WorldItem, "some/Item/Name");
            res.Should().BeTrue();
        }



        [TestMethod]
        public void IsBasicChest_DetectsSimpleNames_CaseInsensitive()
        {
            var res = MechanicClassifier.IsBasicChestName("chest");
            res.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickChest_RecognizesBasicChest_WhenSettingsAllow()
        {
            // Call internal helper directly - pass primitive path and renderName to avoid mutating ExileCore objects
            var res = InvokeChestMechanicFromConfiguredRules(true, false, true, true, true, true, true, true, true, true, EntityType.Chest, "content/some/chest", "Tribal Chest");
            res.Should().Be("basic-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesOtherLeagueToggle_ForNonMirageLeagueChests()
        {
            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, true, true, EntityType.Chest, "content/some/chest", "Some League Chest");

            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, true, true, true, true, true, true, true, true, EntityType.Chest, "content/some/chest", "Some League Chest");

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesHeistToggle_ForSecureLockerRenderName()
        {
            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, false, true, true, true, EntityType.Chest, "content/heist/chest", "Secure Locker");

            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, true, true, EntityType.Chest, "content/heist/chest", "Secure Locker");

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesHeistToggle_ForLeagueHeistMetadataPath()
        {
            const string heistPath = "Metadata/Chests/LeagueHeist/MilitaryChests/HeistChestPathMilitary";

            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, true, true, true, true, false, true, true, true, EntityType.Chest, heistPath, "Military Supplies");

            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, true, true, true, true, true, true, true, true, EntityType.Chest, heistPath, "Military Supplies");

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesBlightCystToggle_ForBlightChestMetadataPath()
        {
            const string blightPath = "Metadata/Chests/Blight/BlightChestObject";

            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, false, true, true, EntityType.Chest, blightPath, "Blight Cyst");

            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, true, true, EntityType.Chest, blightPath, "Blight Cyst");

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesBreachToggle_ForGraspingCoffersMetadataPath()
        {
            const string breachPath = "Metadata/Chests/Breach/BreachBoxChest02";

            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, false, true, EntityType.Chest, breachPath, "Grasping Coffers");

            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, true, true, EntityType.Chest, breachPath, "Grasping Coffers");

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesSynthesisToggle_ForSynthesisedStashMetadataPath()
        {
            const string synthesisPath = "Metadata/Chests/SynthesisChests/SynthesisChest";

            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, true, false, EntityType.Chest, synthesisPath, "Synthesised Stash");

            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, true, true, EntityType.Chest, synthesisPath, "Synthesised Stash");

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void IsSettlersOrePath_UsesStrictSettlersNodeMarkers()
        {
            var settlersNodePath = "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron";
            var monsterPath = "Metadata/Monsters/LeagueKalguur/CrimsonIron/SmallGrowthMaps@83";

            var settlersMatch = MechanicClassifier.IsSettlersOrePath(settlersNodePath);
            settlersMatch.Should().BeTrue();

            var monsterMatch = MechanicClassifier.IsSettlersOrePath(monsterPath);
            monsterMatch.Should().BeFalse();
        }

        [TestMethod]
        public void GetAreaTransitionMechanicId_UsesLabyrinthToggleForTrialPortals()
        {
            string path = "Metadata/Terrain/Labyrinth/Objects/LabyrinthTrialPortalAreaTransition";

            var disabled = MechanicClassifier.GetAreaTransitionMechanicId(
                true,
                false,
                EntityType.AreaTransition,
                path);

            var enabled = MechanicClassifier.GetAreaTransitionMechanicId(
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

            var disabled = MechanicClassifier.GetAreaTransitionMechanicId(
                false,
                true,
                EntityType.AreaTransition,
                path);

            var enabled = MechanicClassifier.GetAreaTransitionMechanicId(
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
            global::ClickIt.Services.Label.Inventory.InventoryStackingEngine.ShouldAllowIncubatorStackMatch(true, true, 68, true, 69).Should().BeFalse();
            global::ClickIt.Services.Label.Inventory.InventoryStackingEngine.ShouldAllowIncubatorStackMatch(true, true, 68, true, 68).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldAllowIncubatorStackMatchCore_Rejects_WhenEitherLevelIsMissing()
        {
            global::ClickIt.Services.Label.Inventory.InventoryStackingEngine.ShouldAllowIncubatorStackMatch(true, false, 68, true, 68).Should().BeFalse();
            global::ClickIt.Services.Label.Inventory.InventoryStackingEngine.ShouldAllowIncubatorStackMatch(true, true, 68, false, 68).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowIncubatorStackMatchCore_AllowsAllLevels_WhenRuleDoesNotApply()
        {
            global::ClickIt.Services.Label.Inventory.InventoryStackingEngine.ShouldAllowIncubatorStackMatch(false, false, 0, false, 0).Should().BeTrue();
            global::ClickIt.Services.Label.Inventory.InventoryStackingEngine.ShouldAllowIncubatorStackMatch(false, true, 1, true, 999).Should().BeTrue();
        }

        [TestMethod]
        public void SelectBestWorldItemMetadataPath_PrefersComponentMetadata_ForMiscObjectsFallback()
        {
            string selected = WorldItemMetadataPolicy.SelectBestWorldItemMetadataPath(
                "Metadata/MiscellaneousObjects/Monolith",
                "Metadata/Items/Currency/CurrencyQuality/Catalyst/ImbuedCatalyst");

            selected.Should().Be("Metadata/Items/Currency/CurrencyQuality/Catalyst/ImbuedCatalyst");
        }

        [TestMethod]
        public void SelectBestWorldItemMetadataPath_KeepsResolvedMetadata_WhenAlreadySpecific()
        {
            string selected = WorldItemMetadataPolicy.SelectBestWorldItemMetadataPath(
                "Metadata/Items/Currency/StackableCurrency/ChaosOrb",
                "Metadata/Items/Currency/CurrencyQuality/Catalyst/ImbuedCatalyst");

            selected.Should().Be("Metadata/Items/Currency/StackableCurrency/ChaosOrb");
        }
    }
}
