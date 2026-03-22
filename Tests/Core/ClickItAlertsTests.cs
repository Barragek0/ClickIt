using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItAlertsTests
    {
        [TestMethod]
        public void ResolveCompositeKey_FindsCompositeKeyFromSettings()
        {
            var plugin = (ClickIt)RuntimeHelpers.GetUninitializedObject(typeof(ClickIt));
            var settings = new ClickItSettings();
            settings.ModAlerts.Clear();
            settings.ModAlerts["downside|xmod"] = true;

            var setMethod = typeof(ClickIt).GetMethod("__Test_SetSettings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            setMethod.Invoke(plugin, [settings]);

            var res = plugin.__Test_GetAlertService().ResolveCompositeKey("xmod");
            Assert.AreEqual("downside|xmod", res);
        }

        [TestMethod]
        public void TryTriggerAlertForMatchedMod_SetsLastAlertTime_WhenFileExistsAndEnabled()
        {
            var plugin = (ClickIt)RuntimeHelpers.GetUninitializedObject(typeof(ClickIt));
            var settings = new ClickItSettings();
            settings.ModAlerts.Clear();
            settings.ModAlerts["mymod"] = true;

            var setMethod = typeof(ClickIt).GetMethod("__Test_SetSettings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            setMethod.Invoke(plugin, [settings]);

            string tmp = Path.Combine(Path.GetTempPath(), "ClickItTestTemp");
            Directory.CreateDirectory(tmp);
            string soundPath = Path.Combine(tmp, "alert.wav");
            File.WriteAllText(soundPath, "");

            var alertService = plugin.__Test_GetAlertService();
            alertService.SetAlertSoundPathForTests(soundPath);

            var dict = alertService.LastAlertTimes;
            dict.Clear();

            alertService.TryTriggerAlertForMatchedMod("mymod");

            Assert.IsTrue(dict.ContainsKey("mymod"));

            try { File.Delete(soundPath); Directory.Delete(tmp); } catch { }
        }
    }
}
