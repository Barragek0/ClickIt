namespace ClickIt.Tests.Features.Mechanics
{
    [TestClass]
    public class InteractionMechanicRuleCatalogTests
    {
        private static readonly LabelOnGround DummyLabel =
            (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));

        [DataTestMethod]
        [TestCategory("Settings")]
        [DataRow(false, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, false, false)]
        [DataRow(true, true, true)]
        public void TryResolve_CorpseSculptureRequiresBothSettings(bool betrayal, bool sculpture, bool expected)
        {
            ClickSettings settings = new() { ClickBetrayal = betrayal, ClickBetrayalCorpseSculpture = sculpture };
            InteractionMechanicRuleCatalog.TryResolve(settings, "Metadata/Terrain/Leagues/Betrayal/BetrayalCorpseSculpture",
                DummyLabel, null, CreateInventoryInteractionPolicy()).Should().Be(expected ? MechanicIds.Betrayal : null);
        }

        [DataTestMethod]
        [TestCategory("Settings")]
        [DataRow("Metadata/Terrain/Leagues/BeyondDemons/BeyondInitiator", false, false, null)]
        [DataRow("Metadata/Terrain/Leagues/BeyondDemons/BeyondInitiator", true, false, MechanicIds.Beyond)]
        [DataRow("Metadata/Terrain/Leagues/Sentinel/SentinelRandomEncounter", false, false, null)]
        [DataRow("Metadata/Terrain/Leagues/Sentinel/SentinelRandomEncounter", false, true, MechanicIds.Sentinel)]
        public void TryResolve_LeagueMechanicsFollowTheirOwnToggles(string path, bool beyond, bool sentinel, string? expected)
        {
            ClickSettings settings = new() { ClickBeyond = beyond, ClickSentinel = sentinel };
            InteractionMechanicRuleCatalog.TryResolve(settings, path, DummyLabel, null, CreateInventoryInteractionPolicy())
                .Should().Be(expected);
        }

        [DataTestMethod]
        [TestCategory("Settings")]
        [DataRow(false, null)]
        [DataRow(true, MechanicIds.Fates)]
        public void TryResolve_FatesFollowsItsToggle(bool clickFates, string? expected)
        {
            ClickSettings settings = new() { ClickFates = clickFates };
            InteractionMechanicRuleCatalog.TryResolve(settings,
                "Metadata/Terrain/Leagues/Augury/Objects/AuguryCardObject", DummyLabel, null, CreateInventoryInteractionPolicy())
                .Should().Be(expected);
        }

        [DataTestMethod]
        [TestCategory("Settings")]
        [DataRow(false, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, false, false)]
        [DataRow(true, true, true)]
        public void TryResolve_HarvestBossSeedRequiresBothSettings(bool harvest, bool bossSeeds, bool expected)
        {
            ClickSettings settings = new() { ClickHarvest = harvest, HarvestBossSeeds = bossSeeds };
            string? result = InteractionMechanicRuleCatalog.TryResolve(settings,
                "Metadata/Terrain/Leagues/Harvest/Objects/BossSeed", DummyLabel, null, CreateInventoryInteractionPolicy());

            result.Should().Be(expected ? MechanicIds.Harvest : null);
        }

        [DataTestMethod]
        [DataRow("Metadata/Terrain/Leagues/Harvest/Objects/HarvestEncounterTree", MechanicIds.Harvest)]
        [DataRow("Metadata/Terrain/BetrayalMakeChoice/Some", MechanicIds.Betrayal)]
        [DataRow("Metadata/Terrain/BlightPump/Some", MechanicIds.Blight)]
        [DataRow("Metadata/Terrain/Delve/Objects/Encounter/Node", MechanicIds.DelveEncounterInitiators)]
        public void TryResolve_ReturnsExpectedMechanic_ForEnabledPathRules(string path, string expectedMechanicId)
        {
            ClickSettings settings = new()
            {
                ClickHarvest = true,
                ClickBetrayal = true,
                ClickBlight = true,
                ClickBlightPump = true,
                ClickDelveSpawners = true
            };

            string? mechanicId = InteractionMechanicRuleCatalog.TryResolve(
                settings,
                path,
                DummyLabel,
                gameController: null,
                CreateInventoryInteractionPolicy());

            mechanicId.Should().Be(expectedMechanicId);
        }

        [TestMethod]
        public void TryResolve_PrioritizesHarvest_WhenMultipleRulesMatch()
        {
            ClickSettings settings = new()
            {
                ClickHarvest = true,
                ClickSulphite = true
            };

            string? mechanicId = InteractionMechanicRuleCatalog.TryResolve(
                settings,
                "Metadata/Harvest/Objects/HarvestEncounterTree/DelveMineral",
                DummyLabel,
                gameController: null,
                CreateInventoryInteractionPolicy());

            mechanicId.Should().Be(MechanicIds.Harvest);
        }

        [TestMethod]
        public void TryResolve_RespectsStrongboxMetadataToggleBeforeDependencyDelegate()
        {
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemProbe
            {
                ChestComponent = new ChestProbe { IsLocked = false },
                Rarity = MonsterRarity.White,
                RenderName = "Arcanist's Strongbox"
            });

            ClickSettings disabledSettings = new()
            {
                ClickStrongboxes = true,
                StrongboxClickMetadata = []
            };

            string? disabledResult = InteractionMechanicRuleCatalog.TryResolve(
                disabledSettings,
                "Metadata/StrongBoxes/Strongbox",
                label,
                gameController: null,
                CreateInventoryInteractionPolicy());

            disabledResult.Should().BeNull();

            ClickSettings enabledSettings = new()
            {
                ClickStrongboxes = true,
                StrongboxClickMetadata = ["StrongBoxes/Strongbox"]
            };

            string? enabledResult = InteractionMechanicRuleCatalog.TryResolve(
                enabledSettings,
                "Metadata/StrongBoxes/Strongbox",
                label,
                gameController: null,
                CreateInventoryInteractionPolicy());

            enabledResult.Should().Be(MechanicIds.Strongboxes);
        }

        [TestMethod]
        public void TryResolve_RequiresStrongboxMechanicToggle()
        {
            ClickSettings settings = new()
            {
                ClickStrongboxes = false,
                StrongboxClickMetadata = ["StrongBoxes/Strongbox"]
            };
            LabelOnGround label = CreateStrongboxLabel(new StrongboxItemProbe
            {
                ChestComponent = new ChestProbe { IsLocked = false },
                Rarity = MonsterRarity.White,
                RenderName = "Arcanist's Strongbox"
            });

            string? result = InteractionMechanicRuleCatalog.TryResolve(
                settings,
                "Metadata/StrongBoxes/Strongbox",
                label,
                gameController: null,
                CreateInventoryInteractionPolicy());

            result.Should().BeNull();
        }

        [TestMethod]
        public void TryResolve_RequiresClosedDoorDependency_ForAlvaTempleDoorRule()
        {
            ClickSettings settings = new()
            {
                ClickAlvaTempleDoors = true
            };

            string path = $"Metadata/{Constants.ClosedDoorPast}/SomeDoor";

            string? blockedResult = InteractionMechanicRuleCatalog.TryResolve(
                settings,
                path,
                DummyLabel,
                gameController: null,
                CreateInventoryInteractionPolicy(allowClosedDoorPast: false));

            blockedResult.Should().BeNull();

            string? allowedResult = InteractionMechanicRuleCatalog.TryResolve(
                settings,
                path,
                DummyLabel,
                gameController: null,
                CreateInventoryInteractionPolicy(allowClosedDoorPast: true));

            allowedResult.Should().Be(MechanicIds.AlvaTempleDoors);
        }

        [TestMethod]
        public void TryResolve_RequiresBlightPumpToggle_ForBlightPumpRule()
        {
            const string pumpPath = "Metadata/Terrain/Leagues/Blight/Objects/BlightPump";

            ClickSettings pumpDisabled = new()
            {
                ClickBlight = true,
                ClickBlightPump = false
            };

            string? disabledResult = InteractionMechanicRuleCatalog.TryResolve(
                pumpDisabled,
                pumpPath,
                DummyLabel,
                gameController: null,
                CreateInventoryInteractionPolicy());

            disabledResult.Should().BeNull();

            ClickSettings pumpEnabled = new()
            {
                ClickBlight = true,
                ClickBlightPump = true
            };

            string? enabledResult = InteractionMechanicRuleCatalog.TryResolve(
                pumpEnabled,
                pumpPath,
                DummyLabel,
                gameController: null,
                CreateInventoryInteractionPolicy());

            enabledResult.Should().Be(MechanicIds.Blight);
        }

        private static InventoryInteractionPolicy CreateInventoryInteractionPolicy(bool allowClosedDoorPast = false)
            => InteractionRuleTestFactory.CreateInventoryInteractionPolicy(allowClosedDoorPast);

        private static LabelOnGround CreateStrongboxLabel(object item)
            => new LabelProbe { ItemOnGround = item };
    }
}
