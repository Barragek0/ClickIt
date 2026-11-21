namespace ClickIt.Tests.Features.Essence
{
    [TestClass]
    public class EssenceServiceTests
    {
        [TestMethod]
        [TestCategory("Settings")]
        public void SecondCorruptionSafeguards_AreEnabledByDefault()
        {
            var settings = new ClickItSettings();

            settings.EssenceSecondCorruptionOnlyWhenNoDeafening.Value.Should().BeTrue();
            settings.EssenceSecondCorruptionOnlyWhenNoCorruptionEssences.Value.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldCorruptEssence_ReturnsFalse_WhenLabelIsNull()
        {
            var settings = new ClickItSettings();
            var service = new EssenceService(settings);

            bool shouldCorrupt = service.ShouldCorruptEssence(null);

            shouldCorrupt.Should().BeFalse();
        }
    }
}
