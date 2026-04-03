namespace ClickIt.UI.Settings
{
    internal static class SettingsUiBootstrapper
    {
        internal static ClickItSettingsScreenNodes CreateScreenNodes(ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var safePanelRenderer = new SettingsPanelSafeRenderer(settings);
            var debugTestingPanelRenderer = new DebugTestingPanelRenderer(settings);
            var itemFiltersPanelRenderer = new ItemFiltersPanelRenderer(settings);
            var lazyModeNearbyMonsterRulesPanelRenderer = new LazyModeNearbyMonsterRulesPanelRenderer(settings);
            var mechanicPriorityPanelRenderer = new MechanicPriorityTablePanelRenderer(settings);
            var altarSettingsPanelRenderer = new AltarSettingsPanelRenderer(settings);
            var mechanicsTablePanelRenderer = new MechanicsTablePanelRenderer(settings);
            var ultimatumSettingsPanelRenderer = new UltimatumSettingsPanelRenderer(settings);

            return ClickItSettingsScreen.Compose(new ClickItSettingsScreenBindings(
                debugTestingPanelRenderer.Draw,
                lazyModeNearbyMonsterRulesPanelRenderer.Draw,
                altarSettingsPanelRenderer.DrawAltarsPanel,
                altarSettingsPanelRenderer.DrawAltarModWeights,
                itemFiltersPanelRenderer.DrawItemTypeFiltersPanel,
                mechanicPriorityPanelRenderer.Draw,
                itemFiltersPanelRenderer.DrawEssenceCorruptionTablePanel,
                itemFiltersPanelRenderer.DrawStrongboxFilterTablePanel,
                mechanicsTablePanelRenderer.Draw,
                ultimatumSettingsPanelRenderer.DrawModifierTablePanel,
                ultimatumSettingsPanelRenderer.DrawTakeRewardModifierTablePanel,
                SettingsUiRenderHelpers.PushStandardSliderWidth,
                SettingsUiRenderHelpers.PopStandardSliderWidth,
                safePanelRenderer.DrawPanel));
        }
    }
}