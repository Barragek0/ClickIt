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
            var settings = new ClickItSettings();

            AltarWeightSettingsService.InitializeDefaultWeights(settings);

            settings.ModAlerts[AltarWeightSettingsService.BuildCompositeKey(ClickItSettings.AltarTypeMinion, "#% chance to drop an additional Divine Orb")]
                .Should().BeTrue();
            settings.ModAlerts[AltarWeightSettingsService.BuildCompositeKey(ClickItSettings.AltarTypeBoss, "Final Boss drops # additional Divine Orbs")]
                .Should().BeTrue();
        }
    }
}