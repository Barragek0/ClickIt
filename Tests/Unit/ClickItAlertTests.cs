using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.IO;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItAlertTests
    {
        [TestMethod]
        public void TryTriggerAlertForMatchedMod_NoSound_DoesNotAddTimestamp()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());
            var settings = clickIt.__Test_GetSettings();

            settings.ModAlerts.Clear();
            settings.ModAlerts["alpha"] = true;

            var alertService = clickIt.__Test_GetAlertService();
            alertService.SetAlertSoundPathForTests(null);

            alertService.TryTriggerAlertForMatchedMod("alpha");

            var dict = alertService.LastAlertTimes;
            dict.ContainsKey("alpha").Should().BeFalse();
        }

        [TestMethod]
        public void TryTriggerAlertForMatchedMod_WithSound_SetsTimestamp_AndRespectsCooldown()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());
            var settings = clickIt.__Test_GetSettings();

            settings.ModAlerts.Clear();
            settings.ModAlerts["alpha"] = true;

            string tmp = Path.GetTempFileName();
            try
            {
                var alertService = clickIt.__Test_GetAlertService();
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
