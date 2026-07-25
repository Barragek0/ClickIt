namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class ClickItSettingsRuntimeServiceTests
    {
        [TestMethod]
        public void GetMechanicPriorityOrder_RebuildsSnapshotWhenSelectionChanges()
        {
            var settings = new ClickItSettings
            {
                MechanicPriorityOrder = new List<string> { "essences", "items" },
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(ClickItSettings.PriorityComparer),
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(ClickItSettings.PriorityComparer)
            };

            IReadOnlyList<string> initial = ClickItSettingsRuntimeService.GetMechanicPriorityOrder(settings);
            settings.MechanicPriorityOrder = new List<string> { "items", "essences" };

            IReadOnlyList<string> updated = ClickItSettingsRuntimeService.GetMechanicPriorityOrder(settings);

            updated.Should().ContainInOrder("items", "essences");
            updated.Should().NotEqual(initial);
        }

        [TestMethod]
        public void IsOnlyPathfindingDetailedDebugSectionEnabled_RequiresPathfindingAndNoOtherSections()
        {
            var settings = new ClickItSettings();
            settings.DebugShowStatus.Value = false;
            settings.DebugShowGameState.Value = false;
            settings.DebugShowWindowDebug.Value = false;
            settings.DebugShowPerformance.Value = false;
            settings.DebugShowClickFrequencyTarget.Value = false;
            settings.DebugShowAltarDetection.Value = false;
            settings.DebugShowAltarService.Value = false;
            settings.DebugShowLabels.Value = false;
            settings.DebugShowInventoryPickup.Value = false;
            settings.DebugShowHoveredItemMetadata.Value = false;
            settings.DebugShowUltimatum.Value = false;
            settings.DebugShowClicking.Value = false;
            settings.DebugShowRuntimeDebugLogOverlay.Value = false;
            settings.DebugShowRecentErrors.Value = false;
            settings.DebugShowPathfinding.Value = true;

            ClickItSettingsRuntimeService.IsOnlyPathfindingDetailedDebugSectionEnabled(settings).Should().BeTrue();

            settings.DebugShowLabels.Value = true;
            ClickItSettingsRuntimeService.IsOnlyPathfindingDetailedDebugSectionEnabled(settings).Should().BeFalse();
        }

        [TestMethod]
        public void IsAnyDetailedDebugSectionEnabled_ReturnsTrue_WhenOnlyWindowDebugIsEnabled()
        {
            var settings = new ClickItSettings();
            settings.DebugShowStatus.Value = false;
            settings.DebugShowGameState.Value = false;
            settings.DebugShowWindowDebug.Value = true;
            settings.DebugShowPerformance.Value = false;
            settings.DebugShowClickFrequencyTarget.Value = false;
            settings.DebugShowAltarDetection.Value = false;
            settings.DebugShowAltarService.Value = false;
            settings.DebugShowLabels.Value = false;
            settings.DebugShowInventoryPickup.Value = false;
            settings.DebugShowHoveredItemMetadata.Value = false;
            settings.DebugShowPathfinding.Value = false;
            settings.DebugShowUltimatum.Value = false;
            settings.DebugShowClicking.Value = false;
            settings.DebugShowRuntimeDebugLogOverlay.Value = false;
            settings.DebugShowRecentErrors.Value = false;

            ClickItSettingsRuntimeService.IsAnyDetailedDebugSectionEnabled(settings).Should().BeTrue();
        }

        [TestMethod]
        public void IsOnlyPathfindingDetailedDebugSectionEnabled_ReturnsFalse_WhenWindowDebugAlsoEnabled()
        {
            var settings = new ClickItSettings();
            settings.DebugShowStatus.Value = false;
            settings.DebugShowGameState.Value = false;
            settings.DebugShowWindowDebug.Value = true;
            settings.DebugShowPerformance.Value = false;
            settings.DebugShowClickFrequencyTarget.Value = false;
            settings.DebugShowAltarDetection.Value = false;
            settings.DebugShowAltarService.Value = false;
            settings.DebugShowLabels.Value = false;
            settings.DebugShowInventoryPickup.Value = false;
            settings.DebugShowHoveredItemMetadata.Value = false;
            settings.DebugShowPathfinding.Value = true;
            settings.DebugShowUltimatum.Value = false;
            settings.DebugShowClicking.Value = false;
            settings.DebugShowRuntimeDebugLogOverlay.Value = false;
            settings.DebugShowRecentErrors.Value = false;

            ClickItSettingsRuntimeService.IsOnlyPathfindingDetailedDebugSectionEnabled(settings).Should().BeFalse();
        }
    }
}