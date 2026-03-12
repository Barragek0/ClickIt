using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

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

            var alertService = clickIt.__Test_GetAlertService();
            var key = alertService.ResolveCompositeKey("foo");
            key.Should().Be("upsides|foo");

            settings.ModAlerts["foo"] = true;
            var key2 = alertService.ResolveCompositeKey("foo");
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
            var alertService = clickIt.__Test_GetAlertService();

            alertService.IsAlertEnabledForKey("alpha").Should().BeTrue();
            alertService.IsAlertEnabledForKey("unknown").Should().BeFalse();

            alertService.LastAlertTimes["alpha"] = System.DateTime.UtcNow;

            alertService.CanTriggerForKey("alpha").Should().BeFalse();

            alertService.LastAlertTimes["alpha"] = System.DateTime.UtcNow.AddSeconds(-60);
            alertService.CanTriggerForKey("alpha").Should().BeTrue();
        }
    }
}
