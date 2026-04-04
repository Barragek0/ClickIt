namespace ClickIt.Tests.UI
{
    [TestClass]
    public class ClickItSettingsScreenTests
    {
        [TestMethod]
        public void ApplyTo_AssignsAllScreenNodes_ToSettingsSurface()
        {
            var settings = new ClickItSettings();
            var nodes = new ClickItSettingsScreenNodes(
                DebugTestingPanel: new CustomNode(),
                ControlsPanel: new CustomNode(),
                ControlsSliderWidthStart: new CustomNode(),
                ControlsSliderWidthEnd: new CustomNode(),
                PathfindingSliderWidthStart: new CustomNode(),
                PathfindingSliderWidthEnd: new CustomNode(),
                LazyModeSliderWidthStart: new CustomNode(),
                LazyModeSliderWidthEnd: new CustomNode(),
                LazyModeNearbyMonsterRulesPanel: new CustomNode(),
                PrioritiesSliderWidthStart: new CustomNode(),
                PrioritiesSliderWidthEnd: new CustomNode(),
                DelveSliderWidthStart: new CustomNode(),
                DelveSliderWidthEnd: new CustomNode(),
                AltarsPanel: new CustomNode(),
                AltarModWeights: new CustomNode(),
                ItemTypeFiltersPanel: new CustomNode(),
                MechanicPriorityTablePanel: new CustomNode(),
                EssenceCorruptionTablePanel: new CustomNode(),
                StrongboxFilterTablePanel: new CustomNode(),
                MechanicsTablePanel: new CustomNode(),
                UltimatumModifierTablePanel: new CustomNode(),
                UltimatumTakeRewardModifierTablePanel: new CustomNode());

            nodes.ApplyTo(settings);

            settings.DebugTestingPanel.Should().BeSameAs(nodes.DebugTestingPanel);
            settings.ControlsPanel.Should().BeSameAs(nodes.ControlsPanel);
            settings.ControlsSliderWidthStart.Should().BeSameAs(nodes.ControlsSliderWidthStart);
            settings.ControlsSliderWidthEnd.Should().BeSameAs(nodes.ControlsSliderWidthEnd);
            settings.PathfindingSliderWidthStart.Should().BeSameAs(nodes.PathfindingSliderWidthStart);
            settings.PathfindingSliderWidthEnd.Should().BeSameAs(nodes.PathfindingSliderWidthEnd);
            settings.LazyModeSliderWidthStart.Should().BeSameAs(nodes.LazyModeSliderWidthStart);
            settings.LazyModeSliderWidthEnd.Should().BeSameAs(nodes.LazyModeSliderWidthEnd);
            settings.LazyModeNearbyMonsterRulesPanel.Should().BeSameAs(nodes.LazyModeNearbyMonsterRulesPanel);
            settings.PrioritiesSliderWidthStart.Should().BeSameAs(nodes.PrioritiesSliderWidthStart);
            settings.PrioritiesSliderWidthEnd.Should().BeSameAs(nodes.PrioritiesSliderWidthEnd);
            settings.DelveSliderWidthStart.Should().BeSameAs(nodes.DelveSliderWidthStart);
            settings.DelveSliderWidthEnd.Should().BeSameAs(nodes.DelveSliderWidthEnd);
            settings.AltarsPanel.Should().BeSameAs(nodes.AltarsPanel);
            settings.AltarModWeights.Should().BeSameAs(nodes.AltarModWeights);
            settings.ItemTypeFiltersPanel.Should().BeSameAs(nodes.ItemTypeFiltersPanel);
            settings.MechanicPriorityTablePanel.Should().BeSameAs(nodes.MechanicPriorityTablePanel);
            settings.EssenceCorruptionTablePanel.Should().BeSameAs(nodes.EssenceCorruptionTablePanel);
            settings.StrongboxFilterTablePanel.Should().BeSameAs(nodes.StrongboxFilterTablePanel);
            settings.MechanicsTablePanel.Should().BeSameAs(nodes.MechanicsTablePanel);
            settings.UltimatumModifierTablePanel.Should().BeSameAs(nodes.UltimatumModifierTablePanel);
            settings.UltimatumTakeRewardModifierTablePanel.Should().BeSameAs(nodes.UltimatumTakeRewardModifierTablePanel);
        }
    }
}