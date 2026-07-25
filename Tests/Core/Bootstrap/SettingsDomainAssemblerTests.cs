namespace ClickIt.Tests.Core.Bootstrap
{
    [TestClass]
    public class SettingsDomainAssemblerTests
    {
        [TestMethod]
        public void WireActions_AttachesAndRemovesHandlers_OnSingleSettingsInstance()
        {
            var settings = new ClickItSettings();
            var registry = new PluginServiceRegistry();
            var logs = new List<string>();
            string configDir = Path.Combine(Path.GetTempPath(), "clickit_settingsdomain_" + Guid.NewGuid().ToString("N"));
            int openConfigHandlersBefore = GetHandlerCount(settings.OpenConfigDirectory);
            int reloadHandlersBefore = GetHandlerCount(settings.ReloadAlertSound);

            Directory.CreateDirectory(configDir);

            try
            {
                var alertService = CreateAlertService(settings, configDir, logs);

                SettingsDomainAssembler.WireActions(settings, settings, alertService, registry);

                GetHandlerCount(settings.OpenConfigDirectory).Should().Be(openConfigHandlersBefore + 1);
                GetHandlerCount(settings.ReloadAlertSound).Should().Be(reloadHandlersBefore + 1);

                SettingsUiRenderHelpers.TriggerButtonNode(settings.ReloadAlertSound);

                logs.Should().ContainSingle(message => message.Contains("Alert sound not found in config directory.", StringComparison.Ordinal));

                registry.DisposeAll();

                GetHandlerCount(settings.OpenConfigDirectory).Should().Be(openConfigHandlersBefore);
                GetHandlerCount(settings.ReloadAlertSound).Should().Be(reloadHandlersBefore);
            }
            finally
            {
                Directory.Delete(configDir, recursive: true);
            }
        }

        [TestMethod]
        public void WireActions_AttachesAndRemovesHandlers_OnRuntimeAndEffectiveSettings_WhenDifferentInstancesProvided()
        {
            var settings = new ClickItSettings();
            var effectiveSettings = new ClickItSettings();
            var registry = new PluginServiceRegistry();
            string configDir = Path.Combine(Path.GetTempPath(), "clickit_settingsdomain_" + Guid.NewGuid().ToString("N"));
            int runtimeOpenHandlersBefore = GetHandlerCount(settings.OpenConfigDirectory);
            int runtimeReloadHandlersBefore = GetHandlerCount(settings.ReloadAlertSound);
            int effectiveOpenHandlersBefore = GetHandlerCount(effectiveSettings.OpenConfigDirectory);
            int effectiveReloadHandlersBefore = GetHandlerCount(effectiveSettings.ReloadAlertSound);

            Directory.CreateDirectory(configDir);

            try
            {
                var alertService = CreateAlertService(settings, configDir, []);

                SettingsDomainAssembler.WireActions(settings, effectiveSettings, alertService, registry);

                GetHandlerCount(settings.OpenConfigDirectory).Should().Be(runtimeOpenHandlersBefore + 1);
                GetHandlerCount(settings.ReloadAlertSound).Should().Be(runtimeReloadHandlersBefore + 1);
                GetHandlerCount(effectiveSettings.OpenConfigDirectory).Should().Be(effectiveOpenHandlersBefore + 1);
                GetHandlerCount(effectiveSettings.ReloadAlertSound).Should().Be(effectiveReloadHandlersBefore + 1);

                registry.DisposeAll();

                GetHandlerCount(settings.OpenConfigDirectory).Should().Be(runtimeOpenHandlersBefore);
                GetHandlerCount(settings.ReloadAlertSound).Should().Be(runtimeReloadHandlersBefore);
                GetHandlerCount(effectiveSettings.OpenConfigDirectory).Should().Be(effectiveOpenHandlersBefore);
                GetHandlerCount(effectiveSettings.ReloadAlertSound).Should().Be(effectiveReloadHandlersBefore);
            }
            finally
            {
                Directory.Delete(configDir, recursive: true);
            }
        }

        private static AlertService CreateAlertService(ClickItSettings settings, string configDir, List<string> logs)
        {
            return new AlertService(
                () => settings,
                () => settings,
                () => configDir,
                () => null,
                (message, _) => logs.Add(message),
                (message, _) => logs.Add(message));
        }

        private static int GetHandlerCount(ButtonNode buttonNode)
            => buttonNode.OnPressed?.GetInvocationList().Length ?? 0;
    }
}