namespace ClickIt.Tests.Features.Mechanics
{
    [TestClass]
    public class InteractionMechanicRuleCatalogTests
    {
        private static readonly LabelOnGround DummyLabel =
            (LabelOnGround)RuntimeHelpers.GetUninitializedObject(typeof(LabelOnGround));

        [DataTestMethod]
        [DataRow("Metadata/Terrain/Harvest/Irrigator", MechanicIds.Harvest)]
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
                "Metadata/Harvest/Irrigator/DelveMineral",
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