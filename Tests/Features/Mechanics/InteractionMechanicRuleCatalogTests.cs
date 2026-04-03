using ClickIt.Features.Labels.Classification;
using ClickIt.Features.Mechanics.Rules;
using ExileCore.PoEMemory.Elements;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;

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
                NearestHarvest = true,
                ClickBetrayal = true,
                ClickBlight = true,
                ClickDelveSpawners = true
            };

            string? mechanicId = InteractionMechanicRuleCatalog.TryResolve(
                settings,
                path,
                DummyLabel,
                gameController: null,
                CreateDependencies());

            mechanicId.Should().Be(expectedMechanicId);
        }

        [TestMethod]
        public void TryResolve_PrioritizesHarvest_WhenMultipleRulesMatch()
        {
            ClickSettings settings = new()
            {
                NearestHarvest = true,
                ClickSulphite = true
            };

            string? mechanicId = InteractionMechanicRuleCatalog.TryResolve(
                settings,
                "Metadata/Harvest/Irrigator/DelveMineral",
                DummyLabel,
                gameController: null,
                CreateDependencies());

            mechanicId.Should().Be(MechanicIds.Harvest);
        }

        [TestMethod]
        public void TryResolve_RespectsStrongboxMetadataToggleBeforeDependencyDelegate()
        {
            ClickSettings disabledSettings = new()
            {
                StrongboxClickMetadata = []
            };

            string? disabledResult = InteractionMechanicRuleCatalog.TryResolve(
                disabledSettings,
                "Metadata/StrongBoxes/Strongbox",
                DummyLabel,
                gameController: null,
                CreateDependencies(shouldClickStrongbox: true));

            disabledResult.Should().BeNull();

            ClickSettings enabledSettings = new()
            {
                StrongboxClickMetadata = ["StrongBoxes/Strongbox"]
            };

            string? enabledResult = InteractionMechanicRuleCatalog.TryResolve(
                enabledSettings,
                "Metadata/StrongBoxes/Strongbox",
                DummyLabel,
                gameController: null,
                CreateDependencies(shouldClickStrongbox: true));

            enabledResult.Should().Be(MechanicIds.Strongboxes);
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
                CreateDependencies(allowClosedDoorPast: false));

            blockedResult.Should().BeNull();

            string? allowedResult = InteractionMechanicRuleCatalog.TryResolve(
                settings,
                path,
                DummyLabel,
                gameController: null,
                CreateDependencies(allowClosedDoorPast: true));

            allowedResult.Should().Be(MechanicIds.AlvaTempleDoors);
        }

        private static MechanicClassifierDependencies CreateDependencies(
            bool shouldClickStrongbox = false,
            bool allowClosedDoorPast = false)
            => new(
                static _ => string.Empty,
                static (_, _, _, _) => true,
                (_, _, _) => shouldClickStrongbox,
                static (_, _) => false,
                static (_, _, _, _) => null,
                _ => allowClosedDoorPast);
    }
}