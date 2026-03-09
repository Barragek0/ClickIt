using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class AltarSettingsSerializationTests
    {
        [TestMethod]
        public void AltarWeightingAndAlerts_RoundTrip_PreservesCustomValues()
        {
            var settings = new ClickItSettings();

            const string tierKey = "Player|custom-upside";
            const string alertKey = "Boss|custom-upside-alert";

            settings.ModTiers[tierKey] = 77;
            settings.ModAlerts[alertKey] = true;

            string json = JsonConvert.SerializeObject(settings);
            var restored = JsonConvert.DeserializeObject<ClickItSettings>(json);

            restored.Should().NotBeNull();
            restored!.ModTiers.Should().ContainKey(tierKey);
            restored.ModTiers[tierKey].Should().Be(77);
            restored.ModAlerts.Should().ContainKey(alertKey);
            restored.ModAlerts[alertKey].Should().BeTrue();
        }
    }
}
