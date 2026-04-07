namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class PluginClickRuntimeStateEvaluatorTests
    {
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

        [DataTestMethod]
        [DataRow(true, false, true)]
        [DataRow(false, false, true)]
        [DataRow(false, true, false)]
        public void ShouldEvaluateRitualState_ReturnsExpected(bool lazyModeEnabled, bool clickHotkeyActive, bool expected)
        {
            bool result = PluginClickRuntimeStateEvaluator.ShouldEvaluateRitualState(lazyModeEnabled, clickHotkeyActive);

            result.Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(true, true, false, false)]
        [DataRow(true, false, false, true)]
        [DataRow(true, false, true, false)]
        [DataRow(false, false, false, false)]
        public void ShouldRunManualUiHoverCoroutine_WithHotkeyState_ReturnsExpected(
            bool manualUiHoverEnabled,
            bool lazyModeEnabled,
            bool clickHotkeyActive,
            bool expected)
        {
            bool result = PluginClickRuntimeStateEvaluator.ShouldRunManualUiHoverCoroutine(
                manualUiHoverEnabled,
                lazyModeEnabled,
                clickHotkeyActive);

            result.Should().Be(expected);
            PluginClickRuntimeStateEvaluator.ResolveManualUiHoverMode(
                manualUiHoverEnabled,
                lazyModeEnabled,
                clickHotkeyActive).ShouldRunCoroutine.Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(false, false, true)]
        [DataRow(false, true, false)]
        [DataRow(true, false, false)]
        [DataRow(true, true, false)]
        public void ShouldCancelOffscreenPathingForInputRelease_ReturnsExpected(bool lazyModeEnabled, bool clickHotkeyHeld, bool expected)
        {
            bool result = PluginClickRuntimeStateEvaluator.ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled, clickHotkeyHeld);

            result.Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(10L, 11L, true)]
        [DataRow(10L, 10L, false)]
        [DataRow(11L, 10L, false)]
        public void ShouldRestartClickTimerAfterSuccessfulClick_ReturnsExpected(long clickSequenceBefore, long clickSequenceAfter, bool expected)
        {
            bool result = PluginClickRuntimeStateEvaluator.ShouldRestartClickTimerAfterSuccessfulClick(clickSequenceBefore, clickSequenceAfter);

            result.Should().Be(expected);
        }

        [TestMethod]
        public void ResolveHasLazyModeRestrictedItems_ReturnsFalse_WhenServiceMissing()
        {
            bool result = PluginClickRuntimeStateEvaluator.ResolveHasLazyModeRestrictedItems(
                lazyModeBlockerService: null,
                labels: []);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ResolveHotkeyActive_ReturnsTrue_WhenLazyModeCanDriveInputWithoutRestrictions()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            var handler = new InputHandler(settings);
            List<LabelOnGround> labels = [];
            var cachedLabels = new TimeCache<List<LabelOnGround>>(() => labels, 50);

            bool result = PluginClickRuntimeStateEvaluator.ResolveHotkeyActive(
                handler,
                cachedLabels,
                lazyModeBlockerService: null);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ResolveHotkeyActive_ReturnsFalse_WhenLazyModeRestrictionsExist()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            var handler = new InputHandler(settings);
            var cachedLabels = new TimeCache<List<LabelOnGround>>(() => [], 50);
            var blocker = new LazyModeBlockerService(settings, gameController: null, logRestriction: static _ => { }, nowProvider: static () => 1000);

            SeedNearbyMonsterCache(blocker, settings, now: 1000, cachedResult: true, cachedReason: "cached restriction");

            bool result = PluginClickRuntimeStateEvaluator.ResolveHotkeyActive(
                handler,
                cachedLabels,
                blocker);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ResolvePoeForeground_ReturnsFalse_WhenGameControllerMissing()
        {
            PluginClickRuntimeStateEvaluator.ResolvePoeForeground(gameController: null).Should().BeFalse();
        }

        [TestMethod]
        public void ResolveLazyModeDisableActive_UsesInputHandlerState_WhenHandlerPresent()
        {
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;
            settings.LazyModeDisableKeyToggleMode.Value = true;
            var handler = new InputHandler(settings);

            SeedInputHotkeyState(handler, lazyModeDisableToggled: true);

            PluginClickRuntimeStateEvaluator.ResolveLazyModeDisableActive(settings, handler).Should().BeTrue();
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(true, true)]
        public void ShouldEvaluateLazyModeRestrictedItems_ReturnsExpected(bool lazyModeEnabled, bool expected)
        {
            PluginClickRuntimeStateEvaluator.ShouldEvaluateLazyModeRestrictedItems(lazyModeEnabled).Should().Be(expected);
        }

        [DataTestMethod]
        [DataRow(false, false, false)]
        [DataRow(true, false, true)]
        [DataRow(true, true, false)]
        public void ShouldRunManualUiHoverCoroutine_SettingsOverload_ReturnsExpected(bool manualUiHoverEnabled, bool lazyModeEnabled, bool expected)
        {
            var settings = new ClickItSettings();
            settings.ClickOnManualUiHoverOnly.Value = manualUiHoverEnabled;
            settings.LazyMode.Value = lazyModeEnabled;

            PluginClickRuntimeStateEvaluator.ShouldRunManualUiHoverCoroutine(settings).Should().Be(expected);
        }

        private static void SeedNearbyMonsterCache(
            LazyModeBlockerService service,
            ClickItSettings settings,
            long now,
            bool cachedResult,
            string? cachedReason)
        {
            int settingsSignature = HashCode.Combine(
                settings.LazyModeNormalMonsterBlockCount,
                settings.LazyModeNormalMonsterBlockDistance,
                settings.LazyModeMagicMonsterBlockCount,
                settings.LazyModeMagicMonsterBlockDistance,
                settings.LazyModeRareMonsterBlockCount,
                settings.LazyModeRareMonsterBlockDistance,
                settings.LazyModeUniqueMonsterBlockCount,
                settings.LazyModeUniqueMonsterBlockDistance);

            RuntimeMemberAccessor.SetRequiredMember(
                service,
                "_cachedNearbyMonsterRestrictionCacheState",
                new NearbyMonsterRestrictionCacheState(
                    now - 10,
                    settingsSignature,
                    new LazyModeRestrictionResult(cachedResult, cachedReason)));
        }

        private static void SeedInputHotkeyState(
            InputHandler handler,
            bool? lazyModeDisableToggled = null,
            bool? lazyModeDisableKeyWasDown = null)
        {
            object hotkeyStateService = RuntimeMemberAccessor.GetRequiredMemberValue(handler, "_hotkeyStateService")!;

            if (lazyModeDisableToggled.HasValue)
                RuntimeMemberAccessor.SetRequiredMember(hotkeyStateService, "_lazyModeDisableToggled", lazyModeDisableToggled.Value);

            if (lazyModeDisableKeyWasDown.HasValue)
                RuntimeMemberAccessor.SetRequiredMember(hotkeyStateService, "_lazyModeDisableKeyWasDown", lazyModeDisableKeyWasDown.Value);
        }

    }
}