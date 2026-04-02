using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class AltarWeightSettingsServiceTests
    {
        [TestMethod]
        public void InitializeDefaultWeights_SeedsDivineOrbAlerts()
        {
            var settings = new global::ClickIt.ClickItSettings();

            global::ClickIt.AltarWeightSettingsService.InitializeDefaultWeights(settings);

            settings.ModAlerts[global::ClickIt.AltarWeightSettingsService.BuildCompositeKey(global::ClickIt.ClickItSettings.AltarTypeMinion, "#% chance to drop an additional Divine Orb")]
                .Should().BeTrue();
            settings.ModAlerts[global::ClickIt.AltarWeightSettingsService.BuildCompositeKey(global::ClickIt.ClickItSettings.AltarTypeBoss, "Final Boss drops # additional Divine Orbs")]
                .Should().BeTrue();
        }
    }
}