namespace ClickIt.Core.Bootstrap
{
    internal readonly record struct SettingsDomainServices(
        AlertService AlertService,
        ClickItSettings EffectiveSettings);

    internal static class SettingsDomainAssembler
    {
        /**
        Keep this thin runtime entry wrapper so the normal lifecycle path still
        assembles settings services from the owner. The injected internal overload
        remains available for direct bootstrap tests and composition-only
        validation, so do not fold this back into one method unless the seam is
        preserved.
         */
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

            registry.Register(() => settings.OpenConfigDirectory.OnPressed -= alertService.OpenConfigDirectory);
            registry.Register(() => settings.ReloadAlertSound.OnPressed -= alertService.ReloadAlertSound);

            if (ReferenceEquals(settings, effectiveSettings))
                return;

            effectiveSettings.OpenConfigDirectory.OnPressed += alertService.OpenConfigDirectory;
            effectiveSettings.ReloadAlertSound.OnPressed += alertService.ReloadAlertSound;

            registry.Register(() => effectiveSettings.OpenConfigDirectory.OnPressed -= alertService.OpenConfigDirectory);
            registry.Register(() => effectiveSettings.ReloadAlertSound.OnPressed -= alertService.ReloadAlertSound);
        }
    }
}