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
        public void LazyModeRestoreCursorDelayMs_DefaultsToTen()
        {
            var settings = new ClickItSettings();

            settings.LazyModeRestoreCursorDelayMs.Value.Should().Be(10);
        }
    }
}
