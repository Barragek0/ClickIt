using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.IO;
using System;
using ClickIt.Services;

namespace ClickIt.Tests.Core
{
    [TestClass]
    public class ClickItAlertTests
    {
        [TestMethod]
        public void TryTriggerAlertForMatchedMod_NoSound_DoesNotAddTimestamp()
        {
            var settings = new ClickItSettings();
            settings.AutoDownloadAlertSound.Value = false;

            settings.ModAlerts.Clear();
            settings.ModAlerts["alpha"] = true;

            string configDir = Path.Combine(Path.GetTempPath(), "clickit_alert_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(configDir);

            var alertService = new AlertService(
                () => settings,
                () => settings,
                () => configDir,
                () => null,
                (_, _) => { },
                (_, _) => { });

            alertService.SetAlertSoundPathForTests(null);

            alertService.TryTriggerAlertForMatchedMod("alpha");

            var dict = alertService.LastAlertTimes;
            dict.ContainsKey("alpha").Should().BeFalse();

            try { Directory.Delete(configDir, true); } catch { }
        }

        [TestMethod]
        public void TryTriggerAlertForMatchedMod_WithSound_SetsTimestamp_AndRespectsCooldown()
        {
            var settings = new ClickItSettings();

            settings.ModAlerts.Clear();
            settings.ModAlerts["alpha"] = true;

            string tmp = Path.GetTempFileName();
            try
            {
                var alertService = new AlertService(
                    () => settings,
                    () => settings,
                    Path.GetTempPath,
                    () => null,
                    (_, _) => { },
                    (_, _) => { });

                alertService.SetAlertSoundPathForTests(tmp);

                alertService.TryTriggerAlertForMatchedMod("alpha");

                var dict = alertService.LastAlertTimes;
                dict.ContainsKey("alpha").Should().BeTrue();

                var first = dict["alpha"];

                alertService.TryTriggerAlertForMatchedMod("alpha");
                var second = dict["alpha"];
                second.Should().Be(first);
            }
            finally
            {
                File.Delete(tmp);
            }
        }
    }
}
