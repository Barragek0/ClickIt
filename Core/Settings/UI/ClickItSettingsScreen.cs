namespace ClickIt
{
    public partial class ClickItSettings
    {
        private void ComposeScreenNodes()
        {
            DebugTestingPanel = SettingsScreenComposer.CreateSafePanelNode("DebugTestingPanel", DrawDebugTestingPanel, DrawPanelSafe);
            ControlsSliderWidthStart = SettingsScreenComposer.CreateSliderWidthBoundaryNode(PushStandardSliderWidth);
            ControlsSliderWidthEnd = SettingsScreenComposer.CreateSliderWidthBoundaryNode(PopStandardSliderWidth);
            PathfindingSliderWidthStart = SettingsScreenComposer.CreateSliderWidthBoundaryNode(PushStandardSliderWidth);
            PathfindingSliderWidthEnd = SettingsScreenComposer.CreateSliderWidthBoundaryNode(PopStandardSliderWidth);
            LazyModeSliderWidthStart = SettingsScreenComposer.CreateSliderWidthBoundaryNode(PushStandardSliderWidth);
            LazyModeSliderWidthEnd = SettingsScreenComposer.CreateSliderWidthBoundaryNode(PopStandardSliderWidth);
            LazyModeNearbyMonsterRulesPanel = SettingsScreenComposer.CreateSafePanelNode("LazyModeNearbyMonsterRulesPanel", DrawLazyModeNearbyMonsterRulesPanel, DrawPanelSafe);
            PrioritiesSliderWidthStart = SettingsScreenComposer.CreateSliderWidthBoundaryNode(PushStandardSliderWidth);
            PrioritiesSliderWidthEnd = SettingsScreenComposer.CreateSliderWidthBoundaryNode(PopStandardSliderWidth);
            DelveSliderWidthStart = SettingsScreenComposer.CreateSliderWidthBoundaryNode(PushStandardSliderWidth);
            DelveSliderWidthEnd = SettingsScreenComposer.CreateSliderWidthBoundaryNode(PopStandardSliderWidth);
            AltarsPanel = SettingsScreenComposer.CreateSafePanelNode("AltarsPanel", DrawAltarsPanel, DrawPanelSafe);
            AltarModWeights = SettingsScreenComposer.CreateSafePanelNode("AltarModWeights", DrawAltarModWeights, DrawPanelSafe);
            ItemTypeFiltersPanel = SettingsScreenComposer.CreateSafePanelNode("ItemTypeFiltersPanel", DrawItemTypeFiltersPanel, DrawPanelSafe);
            MechanicPriorityTablePanel = SettingsScreenComposer.CreateSafePanelNode("MechanicPriorityTablePanel", DrawMechanicPriorityTablePanel, DrawPanelSafe);
            EssenceCorruptionTablePanel = SettingsScreenComposer.CreateSafePanelNode("EssenceCorruptionTablePanel", DrawEssenceCorruptionTablePanel, DrawPanelSafe);
            StrongboxFilterTablePanel = SettingsScreenComposer.CreateSafePanelNode("StrongboxFilterTablePanel", DrawStrongboxFilterTablePanel, DrawPanelSafe);
            MechanicsTablePanel = SettingsScreenComposer.CreateSafePanelNode("MechanicsTablePanel", DrawMechanicsTablePanel, DrawPanelSafe);
            UltimatumModifierTablePanel = SettingsScreenComposer.CreateSafePanelNode("UltimatumModifierTablePanel", DrawUltimatumModifierTablePanel, DrawPanelSafe);
            UltimatumTakeRewardModifierTablePanel = SettingsScreenComposer.CreateSafePanelNode("UltimatumTakeRewardModifierTablePanel", DrawUltimatumTakeRewardModifierTablePanel, DrawPanelSafe);
        }
    }
}