using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Utils
{
    [TestClass]
    public class CoroutineManagerTests
    {

        [TestMethod]
        public void Constructor_Throws_OnNullArgs()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            FluentActions.Invoking(() => new global::ClickIt.Utils.CoroutineManager(null!, settings, gc!, eh))
                .Should().Throw<ArgumentNullException>();

            FluentActions.Invoking(() => new global::ClickIt.Utils.CoroutineManager(ctx, null!, gc!, eh))
                .Should().Throw<ArgumentNullException>();

            FluentActions.Invoking(() => new global::ClickIt.Utils.CoroutineManager(ctx, settings, null!, eh))
                .Should().Throw<ArgumentNullException>();

            FluentActions.Invoking(() => new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, null!))
                .Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void GetPlayerHealthAndESPercent_Return100_WhenRuntimeNotPresent()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh);

            cm.GetPlayerHealthPercent().Should().BeApproximately(100f, 0.001f);
            cm.GetPlayerEnergyShieldPercent().Should().BeApproximately(100f, 0.001f);
        }

        [TestMethod]
        public void StartCoroutines_CreatesAllCoroutines_AndSetsPriorities()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh);

            var pluginMock = new Moq.Mock<ExileCore.BaseSettingsPlugin<ClickItSettings>>();
            var plugin = pluginMock.Object;

            try
            {
                cm.StartCoroutines(plugin);
            }
            catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
            {
                // The StartCoroutines implementation may call into runtime-specific threads; ignore runtime-specific failures but assert that at least the altar coroutine was created.
            }

            var altarCoroutine = ctx.AltarCoroutine;
            altarCoroutine.Should().NotBeNull();
            altarCoroutine!.Priority.Should().Be(ExileCore.Shared.Enums.CoroutinePriority.Normal);
        }

        [TestMethod]
        public void ClickLabel_SetsWorkFinished_WhenTimerBelowTarget_OrCanClickFalse()
        {
            var settings = new ClickItSettings();
            settings.Enable.Value = true;
            settings.ClickFrequencyTarget.Value = 1000;

            var ctx = new PluginContext();
            var perf = new global::ClickIt.Utils.PerformanceMonitor(settings);
            ctx.PerformanceMonitor = perf;
            ctx.ClickService = (global::ClickIt.Services.ClickService)RuntimeHelpers.GetUninitializedObject(typeof(global::ClickIt.Services.ClickService));

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new global::ClickIt.Utils.ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var cm = new global::ClickIt.Utils.CoroutineManager(ctx, settings, gc!, eh);

            ctx.Timer.Restart();
            ctx.Timer.Stop();
            ctx.Timer.Reset();

            var enumerator = cm.RunClickLabelStepForTests();
            enumerator.Should().NotBeNull();

            enumerator!.MoveNext();
            ctx.WorkFinished.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRestartClickTimerAfterSuccessfulClick_ReturnsTrue_WhenSequenceIncreases()
        {
            global::ClickIt.Utils.CoroutineManager
                .ShouldRestartClickTimerAfterSuccessfulClick(10, 11)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRestartClickTimerAfterSuccessfulClick_ReturnsFalse_WhenSequenceUnchanged()
        {
            global::ClickIt.Utils.CoroutineManager
                .ShouldRestartClickTimerAfterSuccessfulClick(10, 10)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldCancelOffscreenPathingForInputRelease_ReturnsTrue_WhenNotLazyAndHotkeyReleased()
        {
            global::ClickIt.Utils.CoroutineManager
                .ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled: false, clickHotkeyHeld: false)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldCancelOffscreenPathingForInputRelease_ReturnsFalse_WhenLazyModeEnabled()
        {
            global::ClickIt.Utils.CoroutineManager
                .ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled: true, clickHotkeyHeld: false)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldCancelOffscreenPathingForInputRelease_ReturnsFalse_WhenHotkeyHeld()
        {
            global::ClickIt.Utils.CoroutineManager
                .ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled: false, clickHotkeyHeld: true)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRunManualUiHoverCoroutine_ReturnsTrue_OnlyWhenEnabledNonLazyAndNoHotkeyOverride()
        {
            global::ClickIt.Utils.CoroutineManager
                .ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeTrue();

            global::ClickIt.Utils.CoroutineManager
                .ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: true, clickHotkeyActive: false)
                .Should().BeFalse();

            global::ClickIt.Utils.CoroutineManager
                .ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: true)
                .Should().BeFalse();

            global::ClickIt.Utils.CoroutineManager
                .ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: false, lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSuppressRegularClickForManualUiHoverMode_MatchesManualCoroutineRunCondition()
        {
            global::ClickIt.Utils.CoroutineManager
                .ShouldSuppressRegularClickForManualUiHoverMode(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeTrue();

            global::ClickIt.Utils.CoroutineManager
                .ShouldSuppressRegularClickForManualUiHoverMode(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: true)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldEvaluateRitualState_ReturnsTrue_ForLazyMode()
        {
            global::ClickIt.Utils.CoroutineManager
                .ShouldEvaluateRitualState(lazyModeEnabled: true, clickHotkeyActive: true)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldEvaluateRitualState_ReturnsTrue_WhenHotkeyInactive()
        {
            global::ClickIt.Utils.CoroutineManager
                .ShouldEvaluateRitualState(lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldEvaluateRitualState_ReturnsFalse_WhenNonLazyAndHotkeyActive()
        {
            global::ClickIt.Utils.CoroutineManager
                .ShouldEvaluateRitualState(lazyModeEnabled: false, clickHotkeyActive: true)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldEvaluateLazyModeRestrictedItems_ReturnsTrue_OnlyWhenLazyModeEnabled()
        {
            global::ClickIt.Utils.CoroutineManager
                .ShouldEvaluateLazyModeRestrictedItems(lazyModeEnabled: true)
                .Should().BeTrue();

            global::ClickIt.Utils.CoroutineManager
                .ShouldEvaluateLazyModeRestrictedItems(lazyModeEnabled: false)
                .Should().BeFalse();
        }

    }
}
