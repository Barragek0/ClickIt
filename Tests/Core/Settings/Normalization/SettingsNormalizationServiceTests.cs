namespace ClickIt.Tests.Core.Settings.Normalization
{
    [TestClass]
    public class SettingsNormalizationServiceTests
    {
        [TestMethod]
        public void SanitizeLazyModeNearbyMonsterCount_ClampsToRange()
        {
            SettingsNormalizationService.SanitizeLazyModeNearbyMonsterCount(-5).Should().Be(0);
            SettingsNormalizationService.SanitizeLazyModeNearbyMonsterCount(0).Should().Be(0);
            SettingsNormalizationService.SanitizeLazyModeNearbyMonsterCount(87).Should().Be(87);
            SettingsNormalizationService.SanitizeLazyModeNearbyMonsterCount(200).Should().Be(200);
            SettingsNormalizationService.SanitizeLazyModeNearbyMonsterCount(500).Should().Be(200);
        }

        [TestMethod]
        public void SanitizeLazyModeNearbyMonsterDistance_ClampsToRange()
        {
            SettingsNormalizationService.SanitizeLazyModeNearbyMonsterDistance(0).Should().Be(1);
            SettingsNormalizationService.SanitizeLazyModeNearbyMonsterDistance(1).Should().Be(1);
            SettingsNormalizationService.SanitizeLazyModeNearbyMonsterDistance(42).Should().Be(42);
            SettingsNormalizationService.SanitizeLazyModeNearbyMonsterDistance(300).Should().Be(300);
            SettingsNormalizationService.SanitizeLazyModeNearbyMonsterDistance(1000).Should().Be(300);
        }

        [TestMethod]
        public void EnsureLazyModeNearbyMonsterFiltersInitialized_NormalizesAllRaritySettings()
        {
            var settings = new ClickItSettings
            {
                LazyModeNormalMonsterBlockCount = 500,
                LazyModeNormalMonsterBlockDistance = 0,
                LazyModeMagicMonsterBlockCount = -3,
                LazyModeMagicMonsterBlockDistance = 999,
                LazyModeRareMonsterBlockCount = 250,
                LazyModeRareMonsterBlockDistance = -50,
                LazyModeUniqueMonsterBlockCount = 123,
                LazyModeUniqueMonsterBlockDistance = 42,
            };

            SettingsNormalizationService.EnsureLazyModeNearbyMonsterFiltersInitialized(settings);

            settings.LazyModeNormalMonsterBlockCount.Should().Be(200);
            settings.LazyModeNormalMonsterBlockDistance.Should().Be(1);
            settings.LazyModeMagicMonsterBlockCount.Should().Be(0);
            settings.LazyModeMagicMonsterBlockDistance.Should().Be(300);
            settings.LazyModeRareMonsterBlockCount.Should().Be(200);
            settings.LazyModeRareMonsterBlockDistance.Should().Be(1);
            settings.LazyModeUniqueMonsterBlockCount.Should().Be(123);
            settings.LazyModeUniqueMonsterBlockDistance.Should().Be(42);
        }

        [TestMethod]
        public void Apply_Throws_WhenSettingsNull()
        {
            var service = new SettingsNormalizationService();

            Action act = () => service.Apply(null!);

            act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
        }
    }
}
