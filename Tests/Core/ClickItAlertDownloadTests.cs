using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.IO;
using System;
using ClickIt.Services;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItAlertDownloadTests
    {
        [TestMethod]
        public void ReloadAlertSound_NoFileAndAutoDownloadDisabled_DoesNotSetPath()
        {
            var settings = new ClickItSettings();

            settings.AutoDownloadAlertSound.Value = false;

            string configDir = Path.Combine(Path.GetTempPath(), "clickit_alert_download_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(configDir);

            var alertService = new AlertService(
                () => settings,
                () => settings,
                () => configDir,
                () => null,
                (_, _) => { },
                (_, _) => { });

            alertService.ReloadAlertSound();

            var val = alertService.CurrentAlertSoundPath;
            val.Should().BeNull();

            try { Directory.Delete(configDir, true); } catch { }
        }

        [TestMethod]
        public void ReloadAlertSound_FilePresent_SetsPath()
        {
            var settings = new ClickItSettings();

            var configDir = Path.Combine(Path.GetTempPath(), "clickit_test_config");
            Directory.CreateDirectory(configDir);
            var target = Path.Combine(configDir, "alert.wav");
            File.WriteAllText(target, "empty");

            var alertService = new AlertService(
                () => settings,
                () => settings,
                () => configDir,
                () => null,
                (_, _) => { },
                (_, _) => { });

            alertService.ReloadAlertSound();

            var val = alertService.CurrentAlertSoundPath;
            val.Should().NotBeNullOrEmpty();
            val!.Should().Be(target);

            File.Delete(target);
            try { Directory.Delete(configDir); } catch { }
        }

    }
}
