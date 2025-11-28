using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Reflection;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItPrivateTests
    {
        // We use the plugin's internal test seam __Test_SetSettings to initialize Settings for tests

        [TestMethod]
        public void ResolveCompositeKey_FindsMatchingSuffixKey_WhenNeeded()
        {
            var clickIt = new ClickIt();
            // set via internal test seam (avoids brittle reflection into base types)
            clickIt.__Test_SetSettings(new ClickItSettings());
            var settings = clickIt.__Test_GetSettings();

            settings.Should().NotBeNull();

            settings.ModAlerts.Clear();
            settings.ModAlerts["upsides|foo"] = true;

            var mi = clickIt.GetType().GetMethod("ResolveCompositeKey", BindingFlags.NonPublic | BindingFlags.Instance);
            mi.Should().NotBeNull();

            var key = mi!.Invoke(clickIt, ["foo"]) as string;
            key.Should().Be("upsides|foo");

            settings.ModAlerts["foo"] = true;
            var key2 = (string?)mi.Invoke(clickIt, ["foo"]);
            key2.Should().Be("foo");
        }

        [TestMethod]
        public void IsAlertEnabledForKey_And_CanTriggerForKey_BehaveAsExpected()
        {
            var clickIt = new ClickIt();
            clickIt.__Test_SetSettings(new ClickItSettings());
            var settings = clickIt.__Test_GetSettings();

            settings.Should().NotBeNull();
            settings.ModAlerts.Clear();
            settings.ModAlerts["alpha"] = true;

            var isEnabledMi = clickIt.GetType().GetMethod("IsAlertEnabledForKey", BindingFlags.NonPublic | BindingFlags.Instance);
            isEnabledMi.Should().NotBeNull();

            ((bool)isEnabledMi!.Invoke(clickIt, ["alpha"])).Should().BeTrue();
            ((bool)isEnabledMi.Invoke(clickIt, ["unknown"])).Should().BeFalse();

            var canTriggerMi = clickIt.GetType().GetMethod("CanTriggerForKey", BindingFlags.NonPublic | BindingFlags.Instance);
            canTriggerMi.Should().NotBeNull();

            var lastField = clickIt.GetType().GetField("_lastAlertTimes", BindingFlags.NonPublic | BindingFlags.Instance);
            lastField.Should().NotBeNull();

            var dict = new System.Collections.Generic.Dictionary<string, System.DateTime>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["alpha"] = System.DateTime.UtcNow
            };
            lastField!.SetValue(clickIt, dict);

            ((bool)canTriggerMi!.Invoke(clickIt, ["alpha"])).Should().BeFalse();

            dict["alpha"] = System.DateTime.UtcNow.AddSeconds(-60);
            lastField.SetValue(clickIt, dict);
            ((bool)canTriggerMi.Invoke(clickIt, ["alpha"])).Should().BeTrue();
        }
    }
}
