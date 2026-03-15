using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class ClickItSettingsInputSafetyTests
    {
        [TestMethod]
        public void AvoidOverlappingLabelClickPoints_DefaultsToEnabled()
        {
            var settings = new ClickItSettings();

            settings.AvoidOverlappingLabelClickPoints.Value.Should().BeTrue();
        }

        [TestMethod]
        public void LazyModeDisableKeyToggleMode_DefaultsToDisabled()
        {
            var settings = new ClickItSettings();

            settings.LazyModeDisableKeyToggleMode.Value.Should().BeFalse();
        }

        [TestMethod]
        public void LazyModeRestoreCursorDelayMs_DefaultsToTwenty()
        {
            var settings = new ClickItSettings();

            settings.LazyModeRestoreCursorDelayMs.Value.Should().Be(20);
        }

        [TestMethod]
        public void ToggleItemsIntervalMs_DefaultsToFifteenHundred()
        {
            var settings = new ClickItSettings();

            settings.ToggleItemsIntervalMs.Value.Should().Be(1500);
        }

        [TestMethod]
        public void ToggleItemsPostToggleClickBlockMs_DefaultsToTwenty()
        {
            var settings = new ClickItSettings();

            settings.ToggleItemsPostToggleClickBlockMs.Value.Should().Be(20);
        }

        [TestMethod]
        public void UseMovementSkillsForOffscreenPathfinding_DefaultsToDisabled()
        {
            var settings = new ClickItSettings();

            settings.UseMovementSkillsForOffscreenPathfinding.Value.Should().BeFalse();
        }

        [TestMethod]
        public void OffscreenMovementSkillMinPathSubsectionLength_DefaultsToEight()
        {
            var settings = new ClickItSettings();

            settings.OffscreenMovementSkillMinPathSubsectionLength.Value.Should().Be(8);
        }

        [TestMethod]
        public void OffscreenShieldChargePostCastClickDelayMs_DefaultsToOneHundred()
        {
            var settings = new ClickItSettings();

            settings.OffscreenShieldChargePostCastClickDelayMs.Value.Should().Be(100);
        }

        [TestMethod]
        public void CorruptAllEssences_DefaultsToDisabled()
        {
            var settings = new ClickItSettings();

            settings.CorruptAllEssences.Value.Should().BeFalse();
        }
    }
}
