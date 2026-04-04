namespace ClickIt.UI.Settings
{
    internal sealed record ClickItSettingsScreenNodes(
        CustomNode DebugTestingPanel,
        CustomNode ControlsSliderWidthStart,
        CustomNode ControlsSliderWidthEnd,
        CustomNode PathfindingSliderWidthStart,
        CustomNode PathfindingSliderWidthEnd,
        CustomNode LazyModeSliderWidthStart,
        CustomNode LazyModeSliderWidthEnd,
        CustomNode LazyModeNearbyMonsterRulesPanel,
        CustomNode PrioritiesSliderWidthStart,
        CustomNode PrioritiesSliderWidthEnd,
        CustomNode DelveSliderWidthStart,
        CustomNode DelveSliderWidthEnd,
        CustomNode AltarsPanel,
        CustomNode AltarModWeights,
        CustomNode ItemTypeFiltersPanel,
        CustomNode MechanicPriorityTablePanel,
        CustomNode EssenceCorruptionTablePanel,
        CustomNode StrongboxFilterTablePanel,
        CustomNode MechanicsTablePanel,
        CustomNode UltimatumModifierTablePanel,
        CustomNode UltimatumTakeRewardModifierTablePanel)
    {
        internal void ApplyTo(ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            settings.DebugTestingPanel = DebugTestingPanel;
            settings.ControlsSliderWidthStart = ControlsSliderWidthStart;
            settings.ControlsSliderWidthEnd = ControlsSliderWidthEnd;
            settings.PathfindingSliderWidthStart = PathfindingSliderWidthStart;
            settings.PathfindingSliderWidthEnd = PathfindingSliderWidthEnd;
            settings.LazyModeSliderWidthStart = LazyModeSliderWidthStart;
            settings.LazyModeSliderWidthEnd = LazyModeSliderWidthEnd;
            settings.LazyModeNearbyMonsterRulesPanel = LazyModeNearbyMonsterRulesPanel;
            settings.PrioritiesSliderWidthStart = PrioritiesSliderWidthStart;
            settings.PrioritiesSliderWidthEnd = PrioritiesSliderWidthEnd;
            settings.DelveSliderWidthStart = DelveSliderWidthStart;
            settings.DelveSliderWidthEnd = DelveSliderWidthEnd;
            settings.AltarsPanel = AltarsPanel;
            settings.AltarModWeights = AltarModWeights;
            settings.ItemTypeFiltersPanel = ItemTypeFiltersPanel;
            settings.MechanicPriorityTablePanel = MechanicPriorityTablePanel;
            settings.EssenceCorruptionTablePanel = EssenceCorruptionTablePanel;
            settings.StrongboxFilterTablePanel = StrongboxFilterTablePanel;
            settings.MechanicsTablePanel = MechanicsTablePanel;
            settings.UltimatumModifierTablePanel = UltimatumModifierTablePanel;
            settings.UltimatumTakeRewardModifierTablePanel = UltimatumTakeRewardModifierTablePanel;
        }
    }

    internal sealed record ClickItSettingsScreenBindings(
        Action DrawDebugTestingPanel,
        Action DrawLazyModeNearbyMonsterRulesPanel,
        Action DrawAltarsPanel,
        Action DrawAltarModWeights,
        Action DrawItemTypeFiltersPanel,
        Action DrawMechanicPriorityTablePanel,
        Action DrawEssenceCorruptionTablePanel,
        Action DrawStrongboxFilterTablePanel,
        Action DrawMechanicsTablePanel,
        Action DrawUltimatumModifierTablePanel,
        Action DrawUltimatumTakeRewardModifierTablePanel,
        Action PushStandardSliderWidth,
        Action PopStandardSliderWidth,
        Action<string, Action> DrawPanelSafe);

    internal static class ClickItSettingsScreen
    {
        internal static ClickItSettingsScreenNodes Compose(ClickItSettingsScreenBindings bindings)
            => new(
                SettingsScreenComposer.CreateSafePanelNode("DebugTestingPanel", bindings.DrawDebugTestingPanel, bindings.DrawPanelSafe),
                SettingsScreenComposer.CreateSliderWidthBoundaryNode(bindings.PushStandardSliderWidth),
                SettingsScreenComposer.CreateSliderWidthBoundaryNode(bindings.PopStandardSliderWidth),
                SettingsScreenComposer.CreateSliderWidthBoundaryNode(bindings.PushStandardSliderWidth),
                SettingsScreenComposer.CreateSliderWidthBoundaryNode(bindings.PopStandardSliderWidth),
                SettingsScreenComposer.CreateSliderWidthBoundaryNode(bindings.PushStandardSliderWidth),
                SettingsScreenComposer.CreateSliderWidthBoundaryNode(bindings.PopStandardSliderWidth),
                SettingsScreenComposer.CreateSafePanelNode("LazyModeNearbyMonsterRulesPanel", bindings.DrawLazyModeNearbyMonsterRulesPanel, bindings.DrawPanelSafe),
                SettingsScreenComposer.CreateSliderWidthBoundaryNode(bindings.PushStandardSliderWidth),
                SettingsScreenComposer.CreateSliderWidthBoundaryNode(bindings.PopStandardSliderWidth),
                SettingsScreenComposer.CreateSliderWidthBoundaryNode(bindings.PushStandardSliderWidth),
                SettingsScreenComposer.CreateSliderWidthBoundaryNode(bindings.PopStandardSliderWidth),
                SettingsScreenComposer.CreateSafePanelNode("AltarsPanel", bindings.DrawAltarsPanel, bindings.DrawPanelSafe),
                SettingsScreenComposer.CreateSafePanelNode("AltarModWeights", bindings.DrawAltarModWeights, bindings.DrawPanelSafe),
                SettingsScreenComposer.CreateSafePanelNode("ItemTypeFiltersPanel", bindings.DrawItemTypeFiltersPanel, bindings.DrawPanelSafe),
                SettingsScreenComposer.CreateSafePanelNode("MechanicPriorityTablePanel", bindings.DrawMechanicPriorityTablePanel, bindings.DrawPanelSafe),
                SettingsScreenComposer.CreateSafePanelNode("EssenceCorruptionTablePanel", bindings.DrawEssenceCorruptionTablePanel, bindings.DrawPanelSafe),
                SettingsScreenComposer.CreateSafePanelNode("StrongboxFilterTablePanel", bindings.DrawStrongboxFilterTablePanel, bindings.DrawPanelSafe),
                SettingsScreenComposer.CreateSafePanelNode("MechanicsTablePanel", bindings.DrawMechanicsTablePanel, bindings.DrawPanelSafe),
                SettingsScreenComposer.CreateSafePanelNode("UltimatumModifierTablePanel", bindings.DrawUltimatumModifierTablePanel, bindings.DrawPanelSafe),
                SettingsScreenComposer.CreateSafePanelNode("UltimatumTakeRewardModifierTablePanel", bindings.DrawUltimatumTakeRewardModifierTablePanel, bindings.DrawPanelSafe));
    }
}