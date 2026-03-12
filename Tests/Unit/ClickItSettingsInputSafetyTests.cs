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
    }
}
