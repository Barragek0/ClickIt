namespace ClickIt.Tests.Features.Labels.Classification
{
    [TestClass]
    public class MechanicClassificationContractTests
    {
        private static readonly LabelOnGround DummyLabel =
            (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));

        [DataTestMethod]
        [DataRow("Metadata/Terrain/Leagues/Harvest/Objects/HarvestEncounterTree", MechanicIds.Harvest)]
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
                CreateInventoryInteractionPolicy(allowClosedDoorPast: false));

            mechanicId.Should().Be(expectedMechanicId);
        }

        [TestMethod]
        public void InteractionRuleCatalog_ResolvesStrongboxes_WhenMetadataAndDependencyAllow()
        {
            ClickSettings settings = CreateFullyEnabledInteractionSettings();
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemProbe
            {
                ChestComponent = new ChestProbe { IsLocked = false },
                Rarity = MonsterRarity.White,
                RenderName = "Arcanist's Strongbox"
            });

            string? mechanicId = InteractionMechanicRuleCatalog.TryResolve(
                settings,
                "Metadata/StrongBoxes/Strongbox",
                label,
                gameController: null,
                CreateInventoryInteractionPolicy(allowClosedDoorPast: false));

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
                CreateInventoryInteractionPolicy(allowClosedDoorPast: true));

            mechanicId.Should().Be(MechanicIds.AlvaTempleDoors);
        }

        [DataTestMethod]
        [DataRow("Metadata/Chests/Blight/BlightChestObject", "Blight Cyst", MechanicIds.BlightCyst)]
        [DataRow("Metadata/Chests/LegionChests/Warhoard", "Legion Chest", MechanicIds.LegionChest)]
        [DataRow("Metadata/Chests/Breach/BreachBoxChest02", "Grasping Coffers", MechanicIds.BreachGraspingCoffers)]
        [DataRow("Metadata/Chests/DelveChests/Resonator", "Resonator Stash", MechanicIds.DelveResonatorStash)]
        [DataRow("Metadata/Chests/DelveChests/FossilisedRemains", "Fossilised Remains", MechanicIds.DelveFossil)]
        [DataRow("Metadata/Chests/DelveChests/DelveChestSpecialArmour", "Special Armour", MechanicIds.DelveSpecialArmour)]
        [DataRow("Metadata/Chests/DelveChests/DelveChestSpecialWeapon", "Special Weapon", MechanicIds.DelveSpecialWeapon)]
        [DataRow("Metadata/Chests/DelveChests/DelveChestSpecialTrinket", "Special Trinket", MechanicIds.DelveSpecialTrinket)]
        [DataRow("Metadata/Chests/DelveChests/DynamiteCurrency", "Currency", MechanicIds.DelveCurrency)]
        [DataRow("Metadata/Chests/DelveChest/UnknownCurrency", "Currency", MechanicIds.DelveCurrency)]
        [DataRow("Metadata/Chests/DelveChest/UnknownResonator", "Resonator", MechanicIds.DelveResonatorStash)]
        [DataRow("Metadata/Chests/DelveChests/SomeMapChest", "Maps", MechanicIds.DelveMaps)]
        [DataRow("Metadata/Chests/DelveChests/SuppliesFlares", "Flare Cache", MechanicIds.DelveFlareChests)]
        [DataRow("Metadata/Chests/DelveChests/SuppliesDynamite", "Dynamite Cache", MechanicIds.DelveDynamiteChests)]
        [DataRow("Metadata/Chests/SynthesisChests/SynthesisChest", "Synthesised Stash", MechanicIds.SynthesisSynthesisedStash)]
        [DataRow("Metadata/LeagueDeepwater/CursedTreasure/Chest", "Cursed Treasure", MechanicIds.AllflameCursedTreasure)]
        [DataRow("Metadata/LeagueDeepwater/BrinerotStores/Chest", "Brinerot Plunder", MechanicIds.AllflameBrinerotPlunder)]
        [DataRow("Metadata/LeagueDeepwater/GiantCoralChest", "Coral Nest", MechanicIds.AllflameCoralNest)]
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
                MechanicIds.LegionChest,
                MechanicIds.BreachGraspingCoffers,
                MechanicIds.DelveResonatorStash,
                MechanicIds.DelveFossil,
                MechanicIds.DelveMaps,
                MechanicIds.DelveFlareChests,
                MechanicIds.DelveDynamiteChests,
                MechanicIds.DelveSpecialArmour,
                MechanicIds.DelveSpecialWeapon,
                MechanicIds.DelveSpecialTrinket,
                MechanicIds.DelveCurrency,
                MechanicIds.SynthesisSynthesisedStash,
                MechanicIds.AllflameCursedTreasure,
                MechanicIds.AllflameBrinerotPlunder,
                MechanicIds.AllflameCoralNest
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

        [DataTestMethod]
        [DataRow("Metadata/Chests/DelveChests/Resonator", "Resonator Stash", MechanicIds.DelveResonatorStash)]
        [DataRow("Metadata/Chests/DelveChests/FossilisedRemains", "Fossilised Remains", MechanicIds.DelveFossil)]
        [DataRow("Metadata/Chests/DelveChests/DelveChestSpecialArmour", "Special Armour", MechanicIds.DelveSpecialArmour)]
        [DataRow("Metadata/Chests/DelveChests/DelveChestSpecialWeapon", "Special Weapon", MechanicIds.DelveSpecialWeapon)]
        [DataRow("Metadata/Chests/DelveChests/DelveChestSpecialTrinket", "Special Trinket", MechanicIds.DelveSpecialTrinket)]
        [DataRow("Metadata/Chests/DelveChests/DynamiteCurrency", "Currency", MechanicIds.DelveCurrency)]
        [DataRow("Metadata/Chests/DelveChest/UnknownCurrency", "Currency", MechanicIds.DelveCurrency)]
        [DataRow("Metadata/Chests/DelveChest/UnknownResonator", "Resonator", MechanicIds.DelveResonatorStash)]
        [DataRow("Metadata/Chests/DelveChests/SomeMapChest", "Maps", MechanicIds.DelveMaps)]
        [DataRow("Metadata/Chests/DelveChests/SuppliesFlares", "Flare Cache", MechanicIds.DelveFlareChests)]
        [DataRow("Metadata/Chests/DelveChests/SuppliesDynamite", "Dynamite Cache", MechanicIds.DelveDynamiteChests)]
        public void DelveChestSpecificToggle_DoesNotFallBackToOther(string path, string name, string id)
        {
            var settings = new ClickItSettings();
            settings.ClickDelveSealedLockbox.Value = true;
            settings.ClickDelveSpecialWeapon.Value = true;
            settings.ClickDelveMaps.Value = true;
            settings.ClickDelveFallbackJewellery.Value = true;
            settings.ClickDelveFallbackArmour.Value = true;
            var enabled = ClickSettingsFactory.BuildEnabledLeagueChestSpecificIds(settings, true);
            MechanicClassifier.GetChestMechanicIdFromConfiguredRules(false, true, true, enabled, EntityType.Chest, path, name).Should().Be(id);
            settings.ClickDelveResonatorStash.Value = false;
            settings.ClickDelveFossil.Value = false;
            settings.ClickDelveMaps.Value = false;
            settings.ClickDelveFlareChests.Value = false;
            settings.ClickDelveDynamiteChests.Value = false;
            settings.ClickDelveSealedLockbox.Value = false;
            settings.ClickDelveSpecialArmour.Value = false;
            settings.ClickDelveSpecialWeapon.Value = false;
            settings.ClickDelveSpecialTrinket.Value = false;
            settings.ClickDelveCurrency.Value = false;
            enabled = ClickSettingsFactory.BuildEnabledLeagueChestSpecificIds(settings, true);
            MechanicClassifier.GetChestMechanicIdFromConfiguredRules(false, true, true, enabled, EntityType.Chest, path, name).Should().BeNull();
            MechanicClassifier.GetChestMechanicIdFromConfiguredRules(false, false, true, enabled, EntityType.Chest, path, name).Should().BeNull();
        }
        [DataTestMethod]
        [DataRow("Metadata/Chests/DelveChests/UnknownTrinket", MechanicIds.DelveFallbackJewellery)]
        [DataRow("Metadata/Chests/DelveChests/UnknownArmour", MechanicIds.DelveFallbackArmour)]
        [DataRow("Metadata/Chests/DelveChest/UnknownWeapon", MechanicIds.DelveFallbackWeapons)]
        public void DelveFallbacks_RespectSettingsAndSpecificRules(string path, string fallbackId)
        {
            var settings = new ClickItSettings();
            settings.ClickDelveSealedLockbox.Value = true;
            settings.ClickDelveSpecialWeapon.Value = true;
            settings.ClickDelveFallbackJewellery.Value = true;
            settings.ClickDelveFallbackArmour.Value = true;
            settings.ClickDelveFallbackWeapons.Value = true;
            var enabled = ClickSettingsFactory.BuildEnabledLeagueChestSpecificIds(settings, true);
            MechanicClassifier.GetChestMechanicIdFromConfiguredRules(false, true, true, enabled, EntityType.Chest, path, "Reward").Should().Be(fallbackId);
            settings.ClickDelveSpecialTrinket.Value = false;
            enabled = ClickSettingsFactory.BuildEnabledLeagueChestSpecificIds(settings, true);
            MechanicClassifier.GetChestMechanicIdFromConfiguredRules(false, true, true, enabled, EntityType.Chest,
                "Metadata/Chests/DelveChests/DelveChestSpecialTrinket", "Reward").Should().BeNull();
            settings.ClickDelveSpecialWeapon.Value = false;
            enabled = ClickSettingsFactory.BuildEnabledLeagueChestSpecificIds(settings, true);
            MechanicClassifier.GetChestMechanicIdFromConfiguredRules(false, true, true, enabled, EntityType.Chest,
                "Metadata/Chests/DelveChests/DelveChestSpecialWeapon", "Reward").Should().BeNull();
            settings.ClickDelveFallbackJewellery.Value = false;
            settings.ClickDelveFallbackArmour.Value = false;
            settings.ClickDelveFallbackWeapons.Value = false;
            settings.ClickDelveFallbackCurrency.Value = false;
            enabled = ClickSettingsFactory.BuildEnabledLeagueChestSpecificIds(settings, true);
            MechanicClassifier.GetChestMechanicIdFromConfiguredRules(false, true, true, enabled, EntityType.Chest, path, "Reward").Should().BeNull();
        }

        [DataTestMethod]
        [DataRow("Metadata/Chests/DelveChests/Flares")]
        [DataRow("Metadata/Chests/DelveChests/Dynamite")]
        [DataRow("Metadata/Chests/Other/SuppliesFlares")]
        [DataRow("Metadata/Chests/Other/SuppliesDynamite")]
        public void DelveSupplies_RequireAllPathFragments(string path)
        {
            var settings = new ClickItSettings();
            var enabled = ClickSettingsFactory.BuildEnabledLeagueChestSpecificIds(settings, true);
            MechanicClassifier.GetChestMechanicIdFromConfiguredRules(false, true, false, enabled, EntityType.Chest, path, "Cache").Should().BeNull();
        }

        [TestMethod]
        public void DelveCurrencyPrimary_DisabledDoesNotFallBack()
        {
            var settings = new ClickItSettings();
            settings.ClickDelveCurrency.Value = false;
            var enabled = ClickSettingsFactory.BuildEnabledLeagueChestSpecificIds(settings, true);
            MechanicClassifier.GetChestMechanicIdFromConfiguredRules(false, true, true, enabled, EntityType.Chest,
                "Metadata/Chests/DelveChests/DynamiteCurrency", "Reward").Should().BeNull();
        }
        private static ClickSettings CreateFullyEnabledInteractionSettings()
            => new()
            {
                ClickHarvest = true,
                ClickSulphite = true,
                ClickStrongboxes = true,
                StrongboxClickMetadata = ["StrongBoxes/Strongbox"],
                ClickSanctum = true,
                ClickBetrayal = true,
                ClickBlight = true,
                ClickBlightPump = true,
                ClickAlvaTempleDoors = true,
                ClickLegionPillars = true,
                ClickAzurite = true,
                ClickInitialUltimatum = true,
                ClickDelveSpawners = true,
                ClickCrafting = true,
                ClickBreach = true
            };

        private static InventoryInteractionPolicy CreateInventoryInteractionPolicy(bool allowClosedDoorPast = false)
            => InteractionRuleTestFactory.CreateInventoryInteractionPolicy(allowClosedDoorPast);

        private static LabelOnGround CreateStrongboxLabel(object item)
            => new LabelProbe { ItemOnGround = item };
    }
}
