namespace ClickIt.Tests.Core.Settings
{
    [TestClass]
    public class ClickItSettingsInputSafetyTests
    {
        [TestMethod]
        public void WrapperSubmenus_ProxyRootSettingsNodes()
        {
            var settings = new ClickItSettings();
            var debugTesting = new ClickItDebugSettingsSubmenu(settings);
            var controls = new ClickItControlsSettingsSubmenu(settings);

            debugTesting.DebugMode.Should().BeSameAs(settings.DebugMode);
            debugTesting.DebugTestingPanel.Should().BeSameAs(settings.DebugTestingPanel);
            controls.ClickLabelKey.Should().BeSameAs(settings.ClickLabelKey);
            controls.Pathfinding.UseMovementSkillsForOffscreenPathfinding.Should().BeSameAs(settings.UseMovementSkillsForOffscreenPathfinding);
            controls.LazyMode.LazyModeDisableKey.Should().BeSameAs(settings.LazyModeDisableKey);
        }

        [TestMethod]
        public void RootSettingsSurface_DoesNotExposePublicWrapperProperties()
        {
            typeof(ClickItSettings).GetProperty("DebugTesting", BindingFlags.Instance | BindingFlags.Public)
                .Should()
                .BeNull();

            typeof(ClickItSettings).GetProperty("Controls", BindingFlags.Instance | BindingFlags.Public)
                .Should()
                .BeNull();
        }

        [TestMethod]
        public void MovedBackingProperties_AreHiddenFromRawSettingsTree()
        {
            var debugModeProperty = typeof(ClickItSettings).GetProperty(nameof(ClickItSettings.DebugMode));
            var clickLabelKeyProperty = typeof(ClickItSettings).GetProperty(nameof(ClickItSettings.ClickLabelKey));
            var lazyModeDisableKeyProperty = typeof(ClickItSettings).GetProperty(nameof(ClickItSettings.LazyModeDisableKey));

            debugModeProperty.Should().NotBeNull();
            clickLabelKeyProperty.Should().NotBeNull();
            lazyModeDisableKeyProperty.Should().NotBeNull();

            debugModeProperty!
                .GetCustomAttributes(typeof(IgnoreMenuAttribute), inherit: false)
                .Should()
                .NotBeEmpty();

            clickLabelKeyProperty!
                .GetCustomAttributes(typeof(IgnoreMenuAttribute), inherit: false)
                .Should()
                .NotBeEmpty();

            lazyModeDisableKeyProperty!
                .GetCustomAttributes(typeof(IgnoreMenuAttribute), inherit: false)
                .Should()
                .NotBeEmpty();
        }

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