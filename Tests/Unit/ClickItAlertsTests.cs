using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using ClickIt;

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
            setMethod.Invoke(plugin, new object[] { settings });

            var method = typeof(ClickIt).GetMethod("ResolveCompositeKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var res = method.Invoke(plugin, new object[] { "xmod" });
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
            setMethod.Invoke(plugin, new object[] { settings });

            string tmp = Path.Combine(Path.GetTempPath(), "ClickItTestTemp");
            Directory.CreateDirectory(tmp);
            string soundPath = Path.Combine(tmp, "alert.wav");
            File.WriteAllText(soundPath, "");

            // Set the private _alertSoundPath field directly so EnsureAlertLoaded will find our file
            var soundField = typeof(ClickIt).GetField("_alertSoundPath", BindingFlags.Instance | BindingFlags.NonPublic);
            soundField!.SetValue(plugin, soundPath);

            // Ensure any previous alert time isn't present
            var lastField = typeof(ClickIt).GetField("_lastAlertTimes", BindingFlags.Instance | BindingFlags.NonPublic);
            var dict = (System.Collections.IDictionary?)lastField!.GetValue(plugin);
            if (dict == null)
            {
                // initialize if not present
                var newDict = new System.Collections.Generic.Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                lastField.SetValue(plugin, newDict);
                dict = newDict as System.Collections.IDictionary;
            }
            dict.Clear();

            // Call the public API
            typeof(ClickIt).GetMethod("TryTriggerAlertForMatchedMod", BindingFlags.Instance | BindingFlags.Public)!
                .Invoke(plugin, new object[] { "mymod" });

            // After invocation, the lastAlertTimes should contain the key
            Assert.IsTrue(dict.Contains("mymod"));

            // Cleanup
            try { File.Delete(soundPath); Directory.Delete(tmp); } catch { }
        }
    }
}
