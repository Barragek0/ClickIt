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
                DebugShowStatus = new ToggleNode(true),
                DebugShowGameState = new ToggleNode(false),
                DebugShowWindowDebug = new ToggleNode(true),
                DebugShowPerformance = new ToggleNode(true),
                DebugShowClickFrequencyTarget = new ToggleNode(false),
                DebugShowAltarDetection = new ToggleNode(false),
                DebugShowAltarService = new ToggleNode(false),
                DebugShowLabels = new ToggleNode(false),
                DebugShowInventoryPickup = new ToggleNode(false),
                DebugShowHoveredItemMetadata = new ToggleNode(false),
                DebugShowPathfinding = new ToggleNode(false),
                DebugShowUltimatum = new ToggleNode(false),
                DebugShowClicking = new ToggleNode(false),
                DebugShowRuntimeDebugLogOverlay = new ToggleNode(false),
                DebugShowRecentErrors = new ToggleNode(true)
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

            sections.Should().HaveCount(15);
            sections[0].Enabled.Should().BeTrue();
            sections[1].Enabled.Should().BeFalse();
            sections[2].Enabled.Should().BeTrue();
            sections[14].Enabled.Should().BeTrue();
        }
    }
}