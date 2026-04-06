namespace ClickIt.Tests.Features.Labels.Classification
{
    [TestClass]
    public class MechanicClassificationContractTests
    {
        private static readonly LabelOnGround DummyLabel =
            (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));

        [DataTestMethod]
        [DataRow("Metadata/Terrain/Harvest/Irrigator", MechanicIds.Harvest)]
        [DataRow("Metadata/Terrain/DelveMineral/Node", MechanicIds.DelveSulphiteVeins)]
        [DataRow("Metadata/Terrain/Sanctum/Door", MechanicIds.Sanctum)]
        [DataRow("Metadata/Terrain/BetrayalMakeChoice/Some", MechanicIds.Betrayal)]
        [DataRow("Metadata/Terrain/BlightPump/Core", MechanicIds.Blight)]
        [DataRow("Metadata/Terrain/LegionInitiator/Pillar", MechanicIds.LegionPillars)]
        [DataRow("Metadata/Terrain/AzuriteEncounterController/Node", MechanicIds.DelveAzuriteVeins)]
        [DataRow("Metadata/Leagues/Ultimatum/Objects/UltimatumChallengeInteractable", MechanicIds.UltimatumInitialOverlay)]
        [DataRow("Metadata/Terrain/Delve/Objects/Encounter/Node", MechanicIds.DelveEncounterInitiators)]
        [DataRow("Metadata/Terrain/CraftingUnlocks/Something", MechanicIds.CraftingRecipes)]
        [DataRow("Metadata/Terrain/Brequel/Some", MechanicIds.BreachNodes)]
        public void InteractionRuleCatalog_ResolvesExpectedMechanicIds_ForAllRuleOwnedMechanics(string path, string expectedMechanicId)
        {
            ClickSettings settings = CreateFullyEnabledInteractionSettings();

            string? mechanicId = InteractionMechanicRuleCatalog.TryResolve(
                settings,
                path,
                DummyLabel,
                gameController: null,
                CreateDependencies(allowClosedDoorPast: true));

            mechanicId.Should().Be(expectedMechanicId);
        }

        [TestMethod]
        public void InteractionRuleCatalog_ResolvesStrongboxes_WhenMetadataAndDependencyAllow()
        {
            ClickSettings settings = CreateFullyEnabledInteractionSettings();

            string? mechanicId = InteractionMechanicRuleCatalog.TryResolve(
                settings,
                "Metadata/StrongBoxes/Strongbox",
                DummyLabel,
                gameController: null,
                CreateDependencies(allowClosedDoorPast: true));

            mechanicId.Should().Be(MechanicIds.Strongboxes);
        }

        [TestMethod]
        public void InteractionRuleCatalog_ResolvesSpecificAlvaTempleDoorsMechanic()
        {
            ClickSettings settings = CreateFullyEnabledInteractionSettings();

            string? mechanicId = InteractionMechanicRuleCatalog.TryResolve(
                settings,
                $"Metadata/{Constants.ClosedDoorPast}/SomeDoor",
                DummyLabel,
                gameController: null,
                CreateDependencies(allowClosedDoorPast: true));

            mechanicId.Should().Be(MechanicIds.AlvaTempleDoors);
        }

        [DataTestMethod]
        [DataRow("Metadata/Chests/Blight/BlightChestObject", "Blight Cyst", MechanicIds.BlightCyst)]
        [DataRow("Metadata/Chests/Breach/BreachBoxChest02", "Grasping Coffers", MechanicIds.BreachGraspingCoffers)]
        [DataRow("Metadata/Chests/SynthesisChests/SynthesisChest", "Synthesised Stash", MechanicIds.SynthesisSynthesisedStash)]
        [DataRow("Metadata/Heist/Objects/Level/Hazards/Strength_SmashMarker", "Strength Smash Marker", MechanicIds.HeistHazards)]
        [DataRow("Metadata/Chests/LeagueHeist/MilitaryChests/HeistChestPathMilitary", "Secure Repository", MechanicIds.HeistSecureRepository)]
        [DataRow("Metadata/Chests/LeagueHeist/AgilityChests/HeistChestPathAgility", "Secure Locker", MechanicIds.HeistSecureLocker)]
        public void ChestRules_ResolveSpecificMechanicIds_WhenSpecificRulesAreEnabled(string path, string renderName, string expectedMechanicId)
        {
            HashSet<string> enabledSpecificIds =
            [
                MechanicIds.MirageGoldenDjinnCache,
                MechanicIds.MirageSilverDjinnCache,
                MechanicIds.MirageBronzeDjinnCache,
                MechanicIds.HeistSecureLocker,
                MechanicIds.HeistSecureRepository,
                MechanicIds.HeistHazards,
                MechanicIds.BlightCyst,
                MechanicIds.BreachGraspingCoffers,
                MechanicIds.SynthesisSynthesisedStash
            ];

            EntityType type = string.Equals(expectedMechanicId, MechanicIds.HeistHazards, StringComparison.OrdinalIgnoreCase)
                ? EntityType.Monster
                : EntityType.Chest;

            string? mechanicId = MechanicClassifier.GetChestMechanicIdFromConfiguredRules(
                clickBasicChests: false,
                clickLeagueChests: true,
                clickLeagueChestsOther: false,
                enabledSpecificLeagueChestIds: enabledSpecificIds,
                type: type,
                path: path,
                renderName: renderName);

            mechanicId.Should().Be(expectedMechanicId);
        }

        private static ClickSettings CreateFullyEnabledInteractionSettings()
            => new()
            {
                NearestHarvest = true,
                ClickSulphite = true,
                ClickStrongboxes = true,
                StrongboxClickMetadata = ["StrongBoxes/Strongbox"],
                ClickSanctum = true,
                ClickBetrayal = true,
                ClickBlight = true,
                ClickAlvaTempleDoors = true,
                ClickLegionPillars = true,
                ClickAzurite = true,
                ClickInitialUltimatum = true,
                ClickDelveSpawners = true,
                ClickCrafting = true,
                ClickBreach = true
            };

        private static MechanicClassifierDependencies CreateDependencies(bool allowClosedDoorPast)
            => new(
                static _ => string.Empty,
                static (_, _, _, _) => true,
            (_, path, _) => path.Contains("StrongBoxes/Strongbox", StringComparison.OrdinalIgnoreCase),
                static (_, _) => false,
                static (_, _, _, _) => null,
                _ => allowClosedDoorPast);
    }
}