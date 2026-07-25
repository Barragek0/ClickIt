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

        [TestMethod]
        public void GetModAlert_ReturnsFalse_WhenModIdIsMissing()
        {
            var settings = new ClickItSettings();

            AltarWeightSettingsService.GetModAlert(settings, string.Empty, ClickItSettings.AltarTypeBoss)
                .Should().BeFalse();
        }

        [TestMethod]
        public void GetModAlert_ReturnsCompositeAlert_WhenPresent()
        {
            var settings = new ClickItSettings();
            string compositeKey = AltarWeightSettingsService.BuildCompositeKey(ClickItSettings.AltarTypeBoss, "mod-id");
            settings.ModAlerts[compositeKey] = true;

            AltarWeightSettingsService.GetModAlert(settings, "mod-id", ClickItSettings.AltarTypeBoss)
                .Should().BeTrue();
        }

        [TestMethod]
        public void GetModAlert_FallsBackToLegacyBareModId_WhenCompositeAlertIsMissing()
        {
            var settings = new ClickItSettings();
            settings.ModAlerts["legacy-mod"] = true;

            AltarWeightSettingsService.GetModAlert(settings, "legacy-mod", ClickItSettings.AltarTypeMinion)
                .Should().BeTrue();
            settings.GetModAlert("legacy-mod", ClickItSettings.AltarTypeMinion)
                .Should().BeTrue();
        }

        [TestMethod]
        public void GetModAlert_ReturnsFalse_WhenNoAlertEntryExists()
        {
            var settings = new ClickItSettings();

            AltarWeightSettingsService.GetModAlert(settings, "missing-mod", ClickItSettings.AltarTypeMinion)
                .Should().BeFalse();
        }
    }
}