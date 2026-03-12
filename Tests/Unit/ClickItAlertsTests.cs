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

            // Use the test seam __Test_SetSettings to inject test settings (seam exists in ClickIt.TestSeams.cs)
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

            // Set EffectiveSettings
            var setMethod = typeof(ClickIt).GetMethod("__Test_SetSettings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            setMethod.Invoke(plugin, [settings]);

            string tmp = Path.Combine(Path.GetTempPath(), "ClickItTestTemp");
            Directory.CreateDirectory(tmp);
            string soundPath = Path.Combine(tmp, "alert.wav");
            File.WriteAllText(soundPath, "");

            var alertService = plugin.__Test_GetAlertService();
            alertService.SetAlertSoundPathForTests(soundPath);

            // Ensure any previous alert time isn't present
            var dict = alertService.LastAlertTimes;
            dict.Clear();

            alertService.TryTriggerAlertForMatchedMod("mymod");

            // After invocation, the lastAlertTimes should contain the key
            Assert.IsTrue(dict.ContainsKey("mymod"));

            // Cleanup
            try { File.Delete(soundPath); Directory.Delete(tmp); } catch { }
        }
    }
}
