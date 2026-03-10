using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Tests.TestUtils;

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

            var key = PrivateMethodAccessor.Invoke<string?>(clickIt, "ResolveCompositeKey", "foo");
            key.Should().Be("upsides|foo");

            settings.ModAlerts["foo"] = true;
            var key2 = PrivateMethodAccessor.Invoke<string?>(clickIt, "ResolveCompositeKey", "foo");
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

            PrivateMethodAccessor.Invoke<bool>(clickIt, "IsAlertEnabledForKey", "alpha").Should().BeTrue();
            PrivateMethodAccessor.Invoke<bool>(clickIt, "IsAlertEnabledForKey", "unknown").Should().BeFalse();

            var dict = new System.Collections.Generic.Dictionary<string, System.DateTime>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["alpha"] = System.DateTime.UtcNow
            };
            PrivateFieldAccessor.Set(clickIt, "_lastAlertTimes", dict);

            PrivateMethodAccessor.Invoke<bool>(clickIt, "CanTriggerForKey", "alpha").Should().BeFalse();

            dict["alpha"] = System.DateTime.UtcNow.AddSeconds(-60);
            PrivateFieldAccessor.Set(clickIt, "_lastAlertTimes", dict);
            PrivateMethodAccessor.Invoke<bool>(clickIt, "CanTriggerForKey", "alpha").Should().BeTrue();
        }
    }
}
