using ClickIt.Services;
using ClickIt.Utils;

namespace ClickIt.Composition
{
    internal readonly record struct SettingsDomainServices(
        AlertService AlertService,
        ClickItSettings EffectiveSettings);

    internal static class SettingsDomainAssembler
    {
        public static SettingsDomainServices Assemble(ClickIt owner)
        {
            ArgumentNullException.ThrowIfNull(owner);

            return new SettingsDomainServices(
                owner.GetAlertService(),
                owner.GetEffectiveSettingsForLifecycle());
        }

        public static void WireActions(
            ClickItSettings settings,
            ClickItSettings effectiveSettings,
            AlertService alertService,
            ServiceDisposalRegistry registry)
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