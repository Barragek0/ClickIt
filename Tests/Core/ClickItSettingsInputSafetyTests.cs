using ExileCore.Shared.Attributes;
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
        public void ClickOnManualUiHoverOnly_DefaultsToDisabled()
        {
            var settings = new ClickItSettings();

            settings.ClickOnManualUiHoverOnly.Value.Should().BeFalse();
        }

        [TestMethod]
        public void UseMovementSkillsForOffscreenPathfinding_DefaultsToDisabled()
        {
            var settings = new ClickItSettings();

            settings.UseMovementSkillsForOffscreenPathfinding.Value.Should().BeFalse();
        }

        [TestMethod]
        public void DebugFreezeSuccessfulInteractionMs_DefaultsToTenSeconds()
        {
            var settings = new ClickItSettings();

            settings.DebugFreezeSuccessfulInteractionMs.Value.Should().Be(10000);
        }

        [TestMethod]
        public void DebugFreezeSuccessfulInteractionMs_IsHiddenFromRawSettingsTree()
        {
            var property = typeof(ClickItSettings).GetProperty(nameof(ClickItSettings.DebugFreezeSuccessfulInteractionMs));

            property.Should().NotBeNull();
            property!
                .GetCustomAttributes(typeof(ConditionalDisplayAttribute), inherit: false)
                .Should()
                .NotBeEmpty();
        }

        [TestMethod]
        public void ShowEssenceCorruptionTablePanel_DisabledWhenCorruptAllEnabled()
        {
            var settings = new ClickItSettings();

            settings.ShowEssenceCorruptionTablePanel.Should().BeTrue();
            settings.CorruptAllEssences.Value = true;
            settings.ShowEssenceCorruptionTablePanel.Should().BeFalse();
        }
    }
}
