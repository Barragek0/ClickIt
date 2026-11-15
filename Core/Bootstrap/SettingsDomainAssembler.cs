namespace ClickIt.Core.Bootstrap
{
    internal readonly record struct SettingsDomainServices(
        AlertService AlertService,
        ClickItSettings EffectiveSettings);

    internal static class SettingsDomainAssembler
    {
        // Thin runtime entry wrapper so the injected internal overload stays testable without runtime traversal.
        public static SettingsDomainServices Assemble(ClickIt owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            return Assemble(
                owner.GetAlertService(),
                owner.GetEffectiveSettingsForLifecycle());
        }
        internal static SettingsDomainServices Assemble(
            AlertService alertService,
            ClickItSettings effectiveSettings)
            => new(alertService, effectiveSettings);

        public static void WireActions(
            ClickItSettings settings,
            ClickItSettings effectiveSettings,
            AlertService alertService,
            PluginServiceRegistry registry)
        {
            settings.OpenConfigDirectory.OnPressed += alertService.OpenConfigDirectory;
            settings.ReloadAlertSound.OnPressed += alertService.ReloadAlertSound;
            Action settingsTestAlertSound = () => PlayTestAlertSound(settings, alertService);
            settings.TestAlertSound.OnPressed += settingsTestAlertSound;

            registry.Register(() => settings.OpenConfigDirectory.OnPressed -= alertService.OpenConfigDirectory);
            registry.Register(() => settings.ReloadAlertSound.OnPressed -= alertService.ReloadAlertSound);
            registry.Register(() => settings.TestAlertSound.OnPressed -= settingsTestAlertSound);

            if (ReferenceEquals(settings, effectiveSettings))
                return;

            effectiveSettings.OpenConfigDirectory.OnPressed += alertService.OpenConfigDirectory;
            effectiveSettings.ReloadAlertSound.OnPressed += alertService.ReloadAlertSound;
            Action effectiveTestAlertSound = () => PlayTestAlertSound(effectiveSettings, alertService);
            effectiveSettings.TestAlertSound.OnPressed += effectiveTestAlertSound;

            registry.Register(() => effectiveSettings.OpenConfigDirectory.OnPressed -= alertService.OpenConfigDirectory);
            registry.Register(() => effectiveSettings.ReloadAlertSound.OnPressed -= alertService.ReloadAlertSound);
            registry.Register(() => effectiveSettings.TestAlertSound.OnPressed -= effectiveTestAlertSound);
        }

        private static void PlayTestAlertSound(ClickItSettings settings, AlertService alertService)
        {
            if (!settings.UiState.TryBeginAlertSoundTest())
                return;

            if (alertService.TryPlayTestSound(out TimeSpan playbackDuration))
                settings.UiState.CompleteAlertSoundTest(playbackDuration);
            else
                settings.UiState.CancelAlertSoundTest();
        }
    }
}
