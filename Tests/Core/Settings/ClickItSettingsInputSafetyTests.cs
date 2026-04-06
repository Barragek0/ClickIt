namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class ClickItSettingsInputSafetyTests
    {
        [TestMethod]
        public void Constructor_InitializesSettingsUiOwners_AndScreenNodes()
        {
            var settings = new ClickItSettings();

            settings.DebugTestingPanel.Should().NotBeNull();
            settings.ControlsPanel.Should().NotBeNull();
            settings.ControlsSliderWidthStart.Should().NotBeNull();
            settings.ControlsSliderWidthEnd.Should().NotBeNull();
            settings.PathfindingSliderWidthStart.Should().NotBeNull();
            settings.PathfindingSliderWidthEnd.Should().NotBeNull();
            settings.LazyModeSliderWidthStart.Should().NotBeNull();
            settings.LazyModeSliderWidthEnd.Should().NotBeNull();
            settings.LazyModeNearbyMonsterRulesPanel.Should().NotBeNull();
            settings.PrioritiesSliderWidthStart.Should().NotBeNull();
            settings.PrioritiesSliderWidthEnd.Should().NotBeNull();
            settings.DelveSliderWidthStart.Should().NotBeNull();
            settings.DelveSliderWidthEnd.Should().NotBeNull();
            settings.AltarsPanel.Should().NotBeNull();
            settings.AltarModWeights.Should().NotBeNull();
            settings.ItemTypeFiltersPanel.Should().NotBeNull();
            settings.MechanicPriorityTablePanel.Should().NotBeNull();
            settings.EssenceCorruptionTablePanel.Should().NotBeNull();
            settings.StrongboxFilterTablePanel.Should().NotBeNull();
            settings.MechanicsTablePanel.Should().NotBeNull();
            settings.UltimatumModifierTablePanel.Should().NotBeNull();
            settings.UltimatumTakeRewardModifierTablePanel.Should().NotBeNull();
        }

        [TestMethod]
        public void AvoidOverlappingLabelClickPoints_DefaultsToEnabled()
        {
            var settings = new ClickItSettings();

            settings.AvoidOverlappingLabelClickPoints.Value.Should().BeTrue();
        }

        [TestMethod]
        public void ClickOnManualUiHoverOnly_DefaultsToDisabled()
        {
            var settings = new ClickItSettings();

            settings.ClickOnManualUiHoverOnly.Value.Should().BeFalse();
        }

        [TestMethod]
        public void UseMovementSkillsForOffscreenPathfinding_DefaultsToDisabled()
        {
            var settings = new ClickItSettings();

            settings.UseMovementSkillsForOffscreenPathfinding.Value.Should().BeFalse();
        }

        [TestMethod]
        public void DebugFreezeSuccessfulInteractionMs_DefaultsToTenSeconds()
        {
            var settings = new ClickItSettings();

            settings.DebugFreezeSuccessfulInteractionMs.Value.Should().Be(10000);
        }

        [TestMethod]
        public void LegacySettingsTreeGate_DefaultsToHidden()
        {
            var settings = new ClickItSettings();

            settings.ShowLegacySettingsTreeNodes.Should().BeFalse();
        }

        [TestMethod]
        public void ShowEssenceCorruptionTablePanel_DisabledWhenCorruptAllEnabled()
        {
            var settings = new ClickItSettings();

            settings.ShowEssenceCorruptionTablePanel.Should().BeTrue();
            settings.CorruptAllEssences.Value = true;
            settings.ShowEssenceCorruptionTablePanel.Should().BeFalse();
        }

    }
}