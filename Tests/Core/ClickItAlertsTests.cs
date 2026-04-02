using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using ClickIt.Services;

namespace ClickIt.Tests.Core
{
    [TestClass]
    public class ClickItAlertsTests
    {
        [TestMethod]
        public void ResolveCompositeKey_FindsCompositeKeyFromSettings()
        {
            var settings = new ClickItSettings();
            settings.ModAlerts.Clear();
            settings.ModAlerts["downside|xmod"] = true;

            var alertService = new AlertService(
                () => settings,
                () => settings,
                Path.GetTempPath,
                () => null,
                (_, _) => { },
                (_, _) => { });

            var res = alertService.ResolveCompositeKey("xmod");
            Assert.AreEqual("downside|xmod", res);
        }

        [TestMethod]
        public void TryTriggerAlertForMatchedMod_SetsLastAlertTime_WhenFileExistsAndEnabled()
        {
            var settings = new ClickItSettings();
            settings.ModAlerts.Clear();
            settings.ModAlerts["mymod"] = true;

            string tmp = Path.Combine(Path.GetTempPath(), "ClickItTestTemp");
            Directory.CreateDirectory(tmp);
            string soundPath = Path.Combine(tmp, "alert.wav");
            File.WriteAllText(soundPath, "");

            var alertService = new AlertService(
                () => settings,
                () => settings,
                Path.GetTempPath,
                () => null,
                (_, _) => { },
                (_, _) => { });

            alertService.SetAlertSoundPathOverride(soundPath);

            var dict = alertService.LastAlertTimes;
            dict.Clear();

            alertService.TryTriggerAlertForMatchedMod("mymod");

            Assert.IsTrue(dict.ContainsKey("mymod"));

            try { File.Delete(soundPath); Directory.Delete(tmp); } catch { }
        }
    }
}
