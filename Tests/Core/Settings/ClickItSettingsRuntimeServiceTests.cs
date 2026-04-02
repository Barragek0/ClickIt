using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class ClickItSettingsRuntimeServiceTests
    {
        [TestMethod]
        public void GetMechanicPriorityOrder_RebuildsSnapshotWhenSelectionChanges()
        {
            var settings = new global::ClickIt.ClickItSettings
            {
                MechanicPriorityOrder = new List<string> { "essences", "items" },
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(global::ClickIt.ClickItSettings.PriorityComparer),
                MechanicPriorityIgnoreDistanceWithinById = new Dictionary<string, int>(global::ClickIt.ClickItSettings.PriorityComparer)
            };

            IReadOnlyList<string> initial = global::ClickIt.ClickItSettingsRuntimeService.GetMechanicPriorityOrder(settings);
            settings.MechanicPriorityOrder = new List<string> { "items", "essences" };

            IReadOnlyList<string> updated = global::ClickIt.ClickItSettingsRuntimeService.GetMechanicPriorityOrder(settings);

            updated.Should().ContainInOrder("items", "essences");
            updated.Should().NotEqual(initial);
        }

        [TestMethod]
        public void IsOnlyPathfindingDetailedDebugSectionEnabled_RequiresPathfindingAndNoOtherSections()
        {
            var settings = new global::ClickIt.ClickItSettings();
            settings.DebugShowStatus.Value = false;
            settings.DebugShowGameState.Value = false;
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

            global::ClickIt.ClickItSettingsRuntimeService.IsOnlyPathfindingDetailedDebugSectionEnabled(settings).Should().BeTrue();

            settings.DebugShowLabels.Value = true;
            global::ClickIt.ClickItSettingsRuntimeService.IsOnlyPathfindingDetailedDebugSectionEnabled(settings).Should().BeFalse();
        }
    }
}