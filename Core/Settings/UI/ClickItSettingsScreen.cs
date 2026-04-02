using ExileCore.Shared.Nodes;

namespace ClickIt
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
        CustomNode UltimatumTakeRewardModifierTablePanel);

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