using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.UI
{
    [TestClass]
    public class DebugOverlaySectionFactoryTests
    {
        [TestMethod]
        public void CreateSections_MapsSettingsTogglesIntoSectionEnablement()
        {
            var settings = new ClickItSettings
            {
                DebugShowStatus = new ExileCore.Shared.Nodes.ToggleNode(true),
                DebugShowGameState = new ExileCore.Shared.Nodes.ToggleNode(false),
                DebugShowPerformance = new ExileCore.Shared.Nodes.ToggleNode(true),
                DebugShowClickFrequencyTarget = new ExileCore.Shared.Nodes.ToggleNode(false),
                DebugShowAltarDetection = new ExileCore.Shared.Nodes.ToggleNode(false),
                DebugShowAltarService = new ExileCore.Shared.Nodes.ToggleNode(false),
                DebugShowLabels = new ExileCore.Shared.Nodes.ToggleNode(false),
                DebugShowInventoryPickup = new ExileCore.Shared.Nodes.ToggleNode(false),
                DebugShowHoveredItemMetadata = new ExileCore.Shared.Nodes.ToggleNode(false),
                DebugShowPathfinding = new ExileCore.Shared.Nodes.ToggleNode(false),
                DebugShowUltimatum = new ExileCore.Shared.Nodes.ToggleNode(false),
                DebugShowClicking = new ExileCore.Shared.Nodes.ToggleNode(false),
                DebugShowRuntimeDebugLogOverlay = new ExileCore.Shared.Nodes.ToggleNode(false),
                DebugShowRecentErrors = new ExileCore.Shared.Nodes.ToggleNode(true)
            };

            var factory = new DebugOverlaySectionFactory(new DebugOverlaySectionFactoryDependencies(
                ClickingSection: null!,
                LabelSection: null!,
                UltimatumSection: null!,
                PerformanceSection: null!,
                StatusSection: null!,
                PathfindingSection: null!,
                RenderAltarDebug: static (_, _, _) => 0,
                RenderAltarServiceDebug: static (_, _, _) => 0,
                RenderHoveredItemMetadataDebug: static (_, _, _) => 0,
                RenderErrorsDebug: static (_, _, _) => 0));

            DebugOverlaySection[] sections = factory.CreateSections(settings, default(PerformanceMetricsSnapshot));

            sections.Should().HaveCount(14);
            sections[0].Enabled.Should().BeTrue();
            sections[1].Enabled.Should().BeFalse();
            sections[2].Enabled.Should().BeTrue();
            sections[13].Enabled.Should().BeTrue();
        }
    }
}