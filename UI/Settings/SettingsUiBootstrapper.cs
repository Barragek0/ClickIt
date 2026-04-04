namespace ClickIt.UI.Settings
{
    internal static class SettingsUiBootstrapper
    {
        internal static void InitializeScreenNodes(ClickItSettings settings)
            => CreateScreenNodes(settings).ApplyTo(settings);

        internal static ClickItSettingsScreenNodes CreateScreenNodes(ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var safePanelRenderer = new SettingsPanelSafeRenderer(settings);
            var debugTestingPanelRenderer = new DebugTestingPanelRenderer(settings);
            var controlsPanelRenderer = new ControlsPanelRenderer(settings);
            var itemTypeFiltersPanelRenderer = new ItemTypeFiltersPanelRenderer(settings);
            var essenceCorruptionPanelRenderer = new EssenceCorruptionPanelRenderer(settings);
            var strongboxFilterPanelRenderer = new StrongboxFilterPanelRenderer(settings);
            var lazyModeNearbyMonsterRulesPanelRenderer = new LazyModeNearbyMonsterRulesPanelRenderer(settings);
            var mechanicPriorityPanelRenderer = new MechanicPriorityTablePanelRenderer(settings);
            var altarSettingsPanelRenderer = new AltarSettingsPanelRenderer(settings);
            var ultimatumSettingsPanelRenderer = new UltimatumSettingsPanelRenderer(settings);
            var mechanicsEmbeddedSettingsPanelRenderer = new MechanicsEmbeddedSettingsPanelRenderer(settings, itemTypeFiltersPanelRenderer, essenceCorruptionPanelRenderer, strongboxFilterPanelRenderer, ultimatumSettingsPanelRenderer, altarSettingsPanelRenderer);
            var mechanicsTablePanelRenderer = new MechanicsTablePanelRenderer(settings, mechanicsEmbeddedSettingsPanelRenderer);

            return ClickItSettingsScreen.Compose(new ClickItSettingsScreenBindings(
                debugTestingPanelRenderer.Draw,
                controlsPanelRenderer.Draw,
                lazyModeNearbyMonsterRulesPanelRenderer.Draw,
                () => altarSettingsPanelRenderer.DrawAltarsPanel(),
                altarSettingsPanelRenderer.DrawAltarModWeights,
                () => itemTypeFiltersPanelRenderer.DrawPanel(),
                mechanicPriorityPanelRenderer.Draw,
                () => essenceCorruptionPanelRenderer.DrawPanel(),
                () => strongboxFilterPanelRenderer.DrawPanel(),
                mechanicsTablePanelRenderer.Draw,
                () => ultimatumSettingsPanelRenderer.DrawModifierTablePanel(),
                () => ultimatumSettingsPanelRenderer.DrawTakeRewardModifierTablePanel(),
                SettingsUiRenderHelpers.PushStandardSliderWidth,
                SettingsUiRenderHelpers.PopStandardSliderWidth,
                safePanelRenderer.DrawPanel));
        }
    }
}