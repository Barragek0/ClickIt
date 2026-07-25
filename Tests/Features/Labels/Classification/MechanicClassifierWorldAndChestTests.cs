namespace ClickIt.Tests.Features.Labels.Classification
{
    [TestClass]
    public class MechanicClassifierWorldAndChestTests
    {
        private static readonly MethodInfo GetNamedInteractableMechanicIdMethod = typeof(MechanicClassifier)
            .GetMethod("GetNamedInteractableMechanicId", BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly MethodInfo GetAltarMechanicIdMethod = typeof(MechanicClassifier)
            .GetMethod("GetAltarMechanicId", BindingFlags.Static | BindingFlags.NonPublic)!;

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

        [DataTestMethod]
        [DataRow(true, EntityType.WorldItem, null, true)]
        [DataRow(true, EntityType.WorldItem, "Metadata/Items/Currency/CurrencyRerollRare", true)]
        [DataRow(true, EntityType.WorldItem, "Metadata/Chests/Strongbox/Arcanist", false)]
        [DataRow(false, EntityType.WorldItem, "Metadata/Items/Currency/CurrencyRerollRare", false)]
        [DataRow(true, EntityType.Chest, "Metadata/Items/Currency/CurrencyRerollRare", false)]
        public void ShouldClickWorldItemCore_ReturnsExpected(
            bool clickItems,
            EntityType type,
            string? itemPath,
            bool expected)
        {
            bool result = MechanicClassifier.ShouldClickWorldItemCore(clickItems, type, itemPath);

            result.Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow("Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron", true, MechanicIds.SettlersCrimsonIron)]
        [DataRow("Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/Verisium", true, MechanicIds.SettlersVerisium)]
        [DataRow("Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/Verisium/VerisiumBossSubAreaTransition", false, null)]
        [DataRow("Metadata/Terrain/Random/Node/Objects/NodeTypes/CrimsonIron", false, null)]
        public void TryGetSettlersOreMechanicId_ReturnsExpected(string path, bool expectedResolved, string? expectedMechanicId)
        {
            bool resolved = MechanicClassifier.TryGetSettlersOreMechanicId(path, out string? mechanicId);

            resolved.Should().Be(expectedResolved);
            mechanicId.Should().Be(expectedMechanicId);
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

        [TestMethod]
        public void GetChestMechanicIdFromConfiguredRules_ReturnsNull_ForNonChestStrongboxAndDisabledLeagueCases()
        {
            InvokeChestMechanicFromConfiguredRules(
                clickBasicChests: true,
                clickLeagueChests: true,
                clickLeagueChestsOther: true,
                clickMirageGoldenDjinnCache: true,
                clickMirageSilverDjinnCache: true,
                clickMirageBronzeDjinnCache: true,
                clickHeistSecureLocker: true,
                clickHeistSecureRepository: true,
                clickHeistHazards: true,
                clickBlightCyst: true,
                clickBreachGraspingCoffers: true,
                clickSynthesisSynthesisedStash: true,
                type: EntityType.WorldItem,
                path: "Metadata/Chests/LeagueHeist/MilitaryChests/HeistChestPathMilitary",
                renderName: "Military Supplies").Should().BeNull();

            InvokeChestMechanicFromConfiguredRules(
                clickBasicChests: true,
                clickLeagueChests: true,
                clickLeagueChestsOther: true,
                clickMirageGoldenDjinnCache: true,
                clickMirageSilverDjinnCache: true,
                clickMirageBronzeDjinnCache: true,
                clickHeistSecureLocker: true,
                clickHeistSecureRepository: true,
                clickHeistHazards: true,
                clickBlightCyst: true,
                clickBreachGraspingCoffers: true,
                clickSynthesisSynthesisedStash: true,
                type: EntityType.Chest,
                path: "Metadata/Chests/Strongbox/Arcanist",
                renderName: "Arcanist Strongbox").Should().BeNull();

            InvokeChestMechanicFromConfiguredRules(
                clickBasicChests: false,
                clickLeagueChests: false,
                clickLeagueChestsOther: true,
                clickMirageGoldenDjinnCache: true,
                clickMirageSilverDjinnCache: true,
                clickMirageBronzeDjinnCache: true,
                clickHeistSecureLocker: true,
                clickHeistSecureRepository: true,
                clickHeistHazards: true,
                clickBlightCyst: true,
                clickBreachGraspingCoffers: true,
                clickSynthesisSynthesisedStash: true,
                type: EntityType.Chest,
                path: "Metadata/Chests/Mirage",
                renderName: "Golden Djinn's Cache").Should().BeNull();
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

        [DataTestMethod]
        [DataRow("Metadata/Terrain/Leagues/Harvest/Objects/Harvest/Irrigator", true)]
        [DataRow("Metadata/Terrain/Leagues/Harvest/Objects/Harvest/Extractor", true)]
        [DataRow("Metadata/Terrain/Leagues/Harvest/Objects/Collector", false)]
        public void IsHarvestPath_ReturnsExpected(string path, bool expected)
        {
            MechanicClassifier.IsHarvestPath(path).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow("Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/PetrifiedWood", true)]
        [DataRow("Metadata/Terrain/Leagues/Settlers/Node/Objects/NodeTypes/CrimsonIron", false)]
        [DataRow("Metadata/Monsters/LeagueKalguur/PetrifiedWood/SmallGrowthMaps@83", false)]
        public void IsSettlersPetrifiedWoodPath_ReturnsExpected(string path, bool expected)
        {
            MechanicClassifier.IsSettlersPetrifiedWoodPath(path).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(true, false, false, false, "Metadata/Terrain/Objects/CleansingFireAltar/Altar", true)]
        [DataRow(false, true, false, false, "Metadata/Terrain/Objects/TangleAltar/Altar", true)]
        [DataRow(false, false, true, false, "Metadata/Terrain/Objects/CleansingFireAltar/Altar", true)]
        [DataRow(false, false, false, true, "Metadata/Terrain/Objects/TangleAltar/Altar", true)]
        [DataRow(false, false, false, false, "Metadata/Terrain/Objects/CleansingFireAltar/Altar", false)]
        [DataRow(true, false, false, false, "Metadata/Terrain/Objects/OtherAltar/Altar", false)]
        [DataRow(true, false, false, false, "", false)]
        public void ShouldClickAltar_ReturnsExpected(
            bool highlightEater,
            bool highlightExarch,
            bool clickEater,
            bool clickExarch,
            string path,
            bool expected)
        {
            bool result = MechanicClassifier.ShouldClickAltar(highlightEater, highlightExarch, clickEater, clickExarch, path);

            result.Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(true, false, false, false, Constants.CleansingFireAltar, MechanicIds.AltarsSearingExarch)]
        [DataRow(false, true, false, false, Constants.TangleAltar, MechanicIds.AltarsEaterOfWorlds)]
        [DataRow(false, false, true, false, Constants.CleansingFireAltar, MechanicIds.AltarsSearingExarch)]
        [DataRow(false, false, false, true, Constants.TangleAltar, MechanicIds.AltarsEaterOfWorlds)]
        [DataRow(false, false, false, false, Constants.CleansingFireAltar, null)]
        [DataRow(true, false, false, false, "Metadata/Terrain/Random/Altar", null)]
        [DataRow(false, false, false, false, "", null)]
        public void GetAltarMechanicId_ReturnsExpected(
            bool highlightExarch,
            bool highlightEater,
            bool clickExarch,
            bool clickEater,
            string path,
            string? expected)
        {
            var settings = new ClickSettings
            {
                HighlightExarch = highlightExarch,
                HighlightEater = highlightEater,
                ClickExarch = clickExarch,
                ClickEater = clickEater
            };

            InvokePrivateAltarMechanic(settings, path).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(true, true, false, "Metadata/Heist/Objects/Level/Door_Automatic", MechanicIds.HeistDoors)]
        [DataRow(true, false, false, "Metadata/MiscellaneousObjects/Door/WoodenDoor", MechanicIds.Doors)]
        [DataRow(false, false, true, "Metadata/Terrain/Objects/Switch_Once_Lever", MechanicIds.Levers)]
        [DataRow(true, true, true, "Metadata/Heist/Objects/Level/Door_Basic", MechanicIds.Doors)]
        [DataRow(false, true, false, "Metadata/MiscellaneousObjects/Lights/Torch", null)]
        [DataRow(false, false, false, "Metadata/Terrain/Objects/Switch_Once_Lever", null)]
        public void GetNamedInteractableMechanicId_ReturnsExpected(
            bool clickDoors,
            bool clickHeistDoors,
            bool clickLevers,
            string metadataPath,
            string? expected)
        {
            InvokePrivateNamedInteractable(clickDoors, clickHeistDoors, clickLevers, renderName: string.Empty, metadataPath)
                .Should().Be(expected);
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
        public void GetAreaTransitionMechanicId_ReturnsNull_ForNonTransitionPath()
        {
            string? mechanicId = MechanicClassifier.GetAreaTransitionMechanicId(
                clickAreaTransitions: true,
                clickLabyrinthTrials: true,
                type: EntityType.Chest,
                path: "Metadata/Terrain/Leagues/Delve/Objects/Encounter");

            mechanicId.Should().BeNull();
        }

        [DataTestMethod]
        [DataRow("Metadata/Terrain/Labyrinth/Objects/LabyrinthTrialPortalAreaTransition", true)]
        [DataRow("Metadata/Terrain/Labyrinth/Trial/Portal", true)]
        [DataRow("Metadata/Terrain/Somewhere/TrialPortal", true)]
        [DataRow("Metadata/Terrain/Leagues/Delve/Objects/SomeAreaTransition", false)]
        [DataRow("", false)]
        public void IsLabyrinthTrialTransitionPath_ReturnsExpected(string path, bool expected)
        {
            TransitionMechanicClassifier.IsLabyrinthTrialTransitionPath(path).Should().Be(expected);
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

        private static string? InvokePrivateNamedInteractable(
            bool clickDoors,
            bool clickHeistDoors,
            bool clickLevers,
            string? renderName,
            string? metadataPath)
            => (string?)GetNamedInteractableMechanicIdMethod.Invoke(null, [clickDoors, clickHeistDoors, clickLevers, renderName, metadataPath]);

        private static string? InvokePrivateAltarMechanic(ClickSettings settings, string path)
            => (string?)GetAltarMechanicIdMethod.Invoke(null, [settings, path]);

        private static ClickSettings CreateSettings(Action<ClickSettings>? configure = null)
        {
            ClickSettings settings = new();
            configure?.Invoke(settings);
            return settings;
        }

    }
}