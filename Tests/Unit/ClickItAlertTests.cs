using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;
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

            // ensure no _alertSoundPath set
            var field = clickIt.GetType().GetField("_alertSoundPath", BindingFlags.Instance | BindingFlags.NonPublic);
            field!.SetValue(clickIt, null);

            clickIt.TryTriggerAlertForMatchedMod("alpha");

            var lastField = clickIt.GetType().GetField("_lastAlertTimes", BindingFlags.Instance | BindingFlags.NonPublic);
            var dict = (System.Collections.Generic.Dictionary<string, System.DateTime>)lastField!.GetValue(clickIt)!;
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

            // create a temporary empty file to act as an alert.wav
            string tmp = Path.GetTempFileName();
            try
            {
                var fieldPath = clickIt.GetType().GetField("_alertSoundPath", BindingFlags.Instance | BindingFlags.NonPublic);
                fieldPath!.SetValue(clickIt, tmp);

                clickIt.TryTriggerAlertForMatchedMod("alpha");

                var lastField = clickIt.GetType().GetField("_lastAlertTimes", BindingFlags.Instance | BindingFlags.NonPublic);
                var dict = (System.Collections.Generic.Dictionary<string, System.DateTime>)lastField!.GetValue(clickIt)!;
                dict.ContainsKey("alpha").Should().BeTrue();

                var first = dict["alpha"];

                // second immediate call should respect cooldown and not update the timestamp
                clickIt.TryTriggerAlertForMatchedMod("alpha");
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
