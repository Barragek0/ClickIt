namespace ClickIt.Tests.Features.Labels.Classification
{
    [TestClass]
    public class MechanicClassifierWorldAndChestTests
    {
        private static string? InvokeChestMechanicFromConfiguredRules(
            bool clickBasicChests,
            bool clickLeagueChests,
            bool clickLeagueChestsOther,
            bool clickMirageGoldenDjinnCache,
            bool clickMirageSilverDjinnCache,
            bool clickMirageBronzeDjinnCache,
            bool clickHeistSecureLocker,
            bool clickHeistSecureRepository,
            bool clickHeistHazards,
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
            if (clickHeistSecureRepository) enabledSpecificLeagueChestIds.Add(MechanicIds.HeistSecureRepository);
            if (clickHeistHazards) enabledSpecificLeagueChestIds.Add(MechanicIds.HeistHazards);
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
        public void IsBasicChest_DetectsSimpleNames_CaseInsensitive()
        {
            var res = MechanicClassifier.IsBasicChestName("chest");
            res.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldClickChest_RecognizesBasicChest_WhenSettingsAllow()
        {
            var res = InvokeChestMechanicFromConfiguredRules(true, false, true, true, true, true, true, true, false, true, true, true, EntityType.Chest, "content/some/chest", "Tribal Chest");
            res.Should().Be("basic-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesOtherLeagueToggle_ForNonMirageLeagueChests()
        {
            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, false, true, true, true, EntityType.Chest, "content/some/chest", "Some League Chest");
            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, true, true, true, true, true, true, false, true, true, true, EntityType.Chest, "content/some/chest", "Some League Chest");

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [DataTestMethod]
        [DataRow("Golden Djinn's Cache", true, false, false, "mirage-golden-djinn-cache")]
        [DataRow("Silver Djinn's Cache", false, true, false, "mirage-silver-djinn-cache")]
        [DataRow("Bronze Djinn's Cache", false, false, true, "mirage-bronze-djinn-cache")]
        public void ShouldClickChest_UsesSpecificMirageMechanicId_WhenMatchingTierIsEnabled(
            string renderName,
            bool goldenEnabled,
            bool silverEnabled,
            bool bronzeEnabled,
            string expectedMechanicId)
        {
            string? disabled = InvokeChestMechanicFromConfiguredRules(
                clickBasicChests: false,
                clickLeagueChests: true,
                clickLeagueChestsOther: false,
                clickMirageGoldenDjinnCache: false,
                clickMirageSilverDjinnCache: false,
                clickMirageBronzeDjinnCache: false,
                clickHeistSecureLocker: true,
                clickHeistSecureRepository: true,
                clickHeistHazards: true,
                clickBlightCyst: true,
                clickBreachGraspingCoffers: true,
                clickSynthesisSynthesisedStash: true,
                type: EntityType.Chest,
                path: "Metadata/Chests/Mirage",
                renderName: renderName);

            string? enabled = InvokeChestMechanicFromConfiguredRules(
                clickBasicChests: false,
                clickLeagueChests: true,
                clickLeagueChestsOther: false,
                clickMirageGoldenDjinnCache: goldenEnabled,
                clickMirageSilverDjinnCache: silverEnabled,
                clickMirageBronzeDjinnCache: bronzeEnabled,
                clickHeistSecureLocker: true,
                clickHeistSecureRepository: true,
                clickHeistHazards: true,
                clickBlightCyst: true,
                clickBreachGraspingCoffers: true,
                clickSynthesisSynthesisedStash: true,
                type: EntityType.Chest,
                path: "Metadata/Chests/Mirage",
                renderName: renderName);

            disabled.Should().BeNull();
            enabled.Should().Be(expectedMechanicId);
        }

        [TestMethod]
        public void ShouldClickChest_UsesHeistToggle_ForSecureLockerRenderName()
        {
            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, false, false, false, true, true, true, EntityType.Chest, "content/heist/chest", "Secure Locker");
            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, false, false, true, true, true, EntityType.Chest, "content/heist/chest", "Secure Locker");

            disabled.Should().BeNull();
            enabled.Should().Be(MechanicIds.HeistSecureLocker);
        }

        [TestMethod]
        public void ShouldClickChest_UsesHeistToggle_ForLeagueHeistMetadataPath()
        {
            const string heistPath = "Metadata/Chests/LeagueHeist/MilitaryChests/HeistChestPathMilitary";

            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, true, true, true, true, false, false, false, true, true, true, EntityType.Chest, heistPath, "Military Supplies");
            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, true, true, true, true, true, false, false, true, true, true, EntityType.Chest, heistPath, "Military Supplies");

            disabled.Should().BeNull();
            enabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesEitherHeistSecureToggle_ForLeagueHeistMetadataPathWithoutSpecificName()
        {
            const string heistPath = "Metadata/Chests/LeagueHeist/MilitaryChests/HeistChestPathMilitary";

            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, true, true, true, true, false, false, false, true, true, true, EntityType.Chest, heistPath, "Military Supplies");
            var lockerEnabled = InvokeChestMechanicFromConfiguredRules(false, true, true, true, true, true, true, false, false, true, true, true, EntityType.Chest, heistPath, "Military Supplies");
            var repositoryEnabled = InvokeChestMechanicFromConfiguredRules(false, true, true, true, true, true, false, true, false, true, true, true, EntityType.Chest, heistPath, "Military Supplies");

            disabled.Should().BeNull();
            lockerEnabled.Should().Be("league-chests");
            repositoryEnabled.Should().Be("league-chests");
        }

        [TestMethod]
        public void ShouldClickChest_UsesHeistToggle_ForSecureRepositoryRenderName()
        {
            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, false, false, false, true, true, EntityType.Chest, "content/heist/chest", "Secure Repository");
            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, false, false, true, true, EntityType.Chest, "content/heist/chest", "Secure Repository");

            disabled.Should().BeNull();
            enabled.Should().Be(MechanicIds.HeistSecureRepository);
        }

        [TestMethod]
        public void ShouldClickChest_UsesBlightCystToggle_ForBlightChestMetadataPath()
        {
            const string blightPath = "Metadata/Chests/Blight/BlightChestObject";

            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, false, false, true, true, EntityType.Chest, blightPath, "Blight Cyst");
            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, false, true, true, true, EntityType.Chest, blightPath, "Blight Cyst");

            disabled.Should().BeNull();
            enabled.Should().Be(MechanicIds.BlightCyst);
        }

        [TestMethod]
        public void ShouldClickChest_UsesBreachToggle_ForGraspingCoffersMetadataPath()
        {
            const string breachPath = "Metadata/Chests/Breach/BreachBoxChest02";

            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, false, true, false, true, EntityType.Chest, breachPath, "Grasping Coffers");
            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, false, true, true, true, EntityType.Chest, breachPath, "Grasping Coffers");

            disabled.Should().BeNull();
            enabled.Should().Be(MechanicIds.BreachGraspingCoffers);
        }

        [TestMethod]
        public void ShouldClickChest_UsesSynthesisToggle_ForSynthesisedStashMetadataPath()
        {
            const string synthesisPath = "Metadata/Chests/SynthesisChests/SynthesisChest";

            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, false, true, true, false, EntityType.Chest, synthesisPath, "Synthesised Stash");
            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, false, true, true, true, EntityType.Chest, synthesisPath, "Synthesised Stash");

            disabled.Should().BeNull();
            enabled.Should().Be(MechanicIds.SynthesisSynthesisedStash);
        }

        [TestMethod]
        public void ShouldClickChest_UsesHeistHazardsToggle_ForHeistHazardsMetadataPath()
        {
            const string hazardsPath = "Metadata/Heist/Objects/Level/Hazards";

            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, false, true, true, true, EntityType.Chest, hazardsPath, "Hazards");
            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, true, true, true, true, EntityType.Chest, hazardsPath, "Hazards");

            disabled.Should().BeNull();
            enabled.Should().Be(MechanicIds.HeistHazards);
        }

        [TestMethod]
        public void ShouldClickChest_UsesHeistHazardsToggle_ForHeistHazardsMetadataPath_WhenTypeIsNotChest()
        {
            const string hazardsPath = "Heist/Objects/Level/Hazards/Strength_SmashMarker";

            var disabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, false, true, true, true, EntityType.Monster, hazardsPath, "Strength Smash Marker");
            var enabled = InvokeChestMechanicFromConfiguredRules(false, true, false, true, true, true, true, true, true, true, true, true, EntityType.Monster, hazardsPath, "Strength Smash Marker");

            disabled.Should().BeNull();
            enabled.Should().Be(MechanicIds.HeistHazards);
        }

        [TestMethod]
        public void IsSettlersOrePath_UsesStrictSettlersNodeMarkers()
        {
            var settlersNodePath = "Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron";
            var monsterPath = "Metadata/Monsters/LeagueKalguur/CrimsonIron/SmallGrowthMaps@83";

            MechanicClassifier.IsSettlersOrePath(settlersNodePath).Should().BeTrue();
            MechanicClassifier.IsSettlersOrePath(monsterPath).Should().BeFalse();
        }

        [TestMethod]
        public void GetAreaTransitionMechanicId_UsesLabyrinthToggleForTrialPortals()
        {
            string path = "Metadata/Terrain/Labyrinth/Objects/LabyrinthTrialPortalAreaTransition";

            var disabled = MechanicClassifier.GetAreaTransitionMechanicId(true, false, EntityType.AreaTransition, path);
            var enabled = MechanicClassifier.GetAreaTransitionMechanicId(false, true, EntityType.AreaTransition, path);

            disabled.Should().BeNull();
            enabled.Should().Be("labyrinth-trials");
        }

        [TestMethod]
        public void GetAreaTransitionMechanicId_UsesAreaTransitionToggleForNonLabyrinthTransitions()
        {
            string path = "Metadata/Terrain/Leagues/Delve/Objects/SomeAreaTransition";

            var disabled = MechanicClassifier.GetAreaTransitionMechanicId(false, true, EntityType.AreaTransition, path);
            var enabled = MechanicClassifier.GetAreaTransitionMechanicId(true, false, EntityType.AreaTransition, path);

            disabled.Should().BeNull();
            enabled.Should().Be("area-transitions");
        }

        [TestMethod]
        public void ShouldAllowIncubatorStackMatchCore_RequiresMatchingLevels_WhenIncubatorPathRuleApplies()
        {
            InventoryStackingEngine.ShouldAllowIncubatorStackMatch(true, true, 68, true, 69).Should().BeFalse();
            InventoryStackingEngine.ShouldAllowIncubatorStackMatch(true, true, 68, true, 68).Should().BeTrue();
        }

        [TestMethod]
        public void ShouldAllowIncubatorStackMatchCore_Rejects_WhenEitherLevelIsMissing()
        {
            InventoryStackingEngine.ShouldAllowIncubatorStackMatch(true, false, 68, true, 68).Should().BeFalse();
            InventoryStackingEngine.ShouldAllowIncubatorStackMatch(true, true, 68, false, 68).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldAllowIncubatorStackMatchCore_AllowsAllLevels_WhenRuleDoesNotApply()
        {
            InventoryStackingEngine.ShouldAllowIncubatorStackMatch(false, false, 0, false, 0).Should().BeTrue();
            InventoryStackingEngine.ShouldAllowIncubatorStackMatch(false, true, 1, true, 999).Should().BeTrue();
        }

    }
}