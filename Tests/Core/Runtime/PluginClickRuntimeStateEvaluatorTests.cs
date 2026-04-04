namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class PluginClickRuntimeStateEvaluatorTests
    {
        [TestMethod]
        public void ResolveHotkeyActive_ReturnsFalse_WhenInputHandlerMissing()
        {
            PluginClickRuntimeStateEvaluator.ResolveHotkeyActive(
                inputHandler: null,
                cachedLabels: null,
                labelFilterPort: null).Should().BeFalse();
        }

        [TestMethod]
        public void BuildSnapshot_UsesRitualAndRestrictedItems_ForLazyTiming()
        {
            PluginClickRuntimeStateSnapshot blockedByRitual = PluginClickRuntimeStateEvaluator.BuildSnapshot(
                lazyModeEnabled: true,
                lazyModeDisableActive: false,
                hasLazyModeRestrictedItems: false,
                isRitualActive: true,
                poeForeground: true);

            PluginClickRuntimeStateSnapshot blockedByRestrictedItems = PluginClickRuntimeStateEvaluator.BuildSnapshot(
                lazyModeEnabled: true,
                lazyModeDisableActive: false,
                hasLazyModeRestrictedItems: true,
                isRitualActive: false,
                poeForeground: true);

            blockedByRitual.UseLazyModeTiming.Should().BeFalse();
            blockedByRitual.ShowLazyModeTarget.Should().BeFalse();
            blockedByRestrictedItems.UseLazyModeTiming.Should().BeFalse();
            blockedByRestrictedItems.ShowLazyModeTarget.Should().BeFalse();
        }

        [TestMethod]
        public void BuildSnapshot_HidesLazyTarget_WhenDisableHeldOrGameInactive()
        {
            PluginClickRuntimeStateSnapshot disabledByHotkey = PluginClickRuntimeStateEvaluator.BuildSnapshot(
                lazyModeEnabled: true,
                lazyModeDisableActive: true,
                hasLazyModeRestrictedItems: false,
                isRitualActive: false,
                poeForeground: true);

            PluginClickRuntimeStateSnapshot hiddenOutOfFocus = PluginClickRuntimeStateEvaluator.BuildSnapshot(
                lazyModeEnabled: true,
                lazyModeDisableActive: false,
                hasLazyModeRestrictedItems: false,
                isRitualActive: false,
                poeForeground: false);

            disabledByHotkey.UseLazyModeTiming.Should().BeTrue();
            disabledByHotkey.ShowLazyModeTarget.Should().BeFalse();
            hiddenOutOfFocus.UseLazyModeTiming.Should().BeTrue();
            hiddenOutOfFocus.ShowLazyModeTarget.Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRunManualUiHoverCoroutine_TwoFlagOverload_ReturnsTrue_OnlyWhenManualEnabledAndNotLazy()
        {
            PluginClickRuntimeStateEvaluator.ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: false)
                .Should().BeTrue();

            PluginClickRuntimeStateEvaluator.ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: true)
                .Should().BeFalse();

            PluginClickRuntimeStateEvaluator.ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: false, lazyModeEnabled: false)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRunManualUiHoverCoroutine_SettingsOverload_UsesRelevantFlags()
        {
            var settings = new ClickItSettings();
            settings.ClickOnManualUiHoverOnly.Value = true;
            settings.LazyMode.Value = false;

            PluginClickRuntimeStateEvaluator.ShouldRunManualUiHoverCoroutine(settings).Should().BeTrue();

            settings.LazyMode.Value = true;

            PluginClickRuntimeStateEvaluator.ShouldRunManualUiHoverCoroutine(settings).Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRunManualUiHoverCoroutine_ThreeFlagOverload_ReturnsTrue_OnlyWhenEnabledNonLazyAndNoHotkeyOverride()
        {
            PluginClickRuntimeStateEvaluator.ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeTrue();

            PluginClickRuntimeStateEvaluator.ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: true, clickHotkeyActive: false)
                .Should().BeFalse();

            PluginClickRuntimeStateEvaluator.ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: true)
                .Should().BeFalse();

            PluginClickRuntimeStateEvaluator.ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: false, lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSuppressRegularClickForManualUiHoverMode_MatchesManualCoroutineRunCondition()
        {
            PluginClickRuntimeStateEvaluator.ShouldSuppressRegularClickForManualUiHoverMode(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeTrue();

            PluginClickRuntimeStateEvaluator.ShouldSuppressRegularClickForManualUiHoverMode(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: true)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldEvaluateRitualState_ReturnsExpectedValues()
        {
            PluginClickRuntimeStateEvaluator.ShouldEvaluateRitualState(lazyModeEnabled: true, clickHotkeyActive: true)
                .Should().BeTrue();

            PluginClickRuntimeStateEvaluator.ShouldEvaluateRitualState(lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeTrue();

            PluginClickRuntimeStateEvaluator.ShouldEvaluateRitualState(lazyModeEnabled: false, clickHotkeyActive: true)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldEvaluateLazyModeRestrictedItems_ReturnsTrue_OnlyWhenLazyModeEnabled()
        {
            PluginClickRuntimeStateEvaluator.ShouldEvaluateLazyModeRestrictedItems(lazyModeEnabled: true)
                .Should().BeTrue();

            PluginClickRuntimeStateEvaluator.ShouldEvaluateLazyModeRestrictedItems(lazyModeEnabled: false)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldCancelOffscreenPathingForInputRelease_ReturnsExpectedValues()
        {
            PluginClickRuntimeStateEvaluator.ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled: false, clickHotkeyHeld: false)
                .Should().BeTrue();

            PluginClickRuntimeStateEvaluator.ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled: true, clickHotkeyHeld: false)
                .Should().BeFalse();

            PluginClickRuntimeStateEvaluator.ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled: false, clickHotkeyHeld: true)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRestartClickTimerAfterSuccessfulClick_ReturnsExpectedValues()
        {
            PluginClickRuntimeStateEvaluator.ShouldRestartClickTimerAfterSuccessfulClick(10, 11)
                .Should().BeTrue();

            PluginClickRuntimeStateEvaluator.ShouldRestartClickTimerAfterSuccessfulClick(10, 10)
                .Should().BeFalse();
        }
    }
}