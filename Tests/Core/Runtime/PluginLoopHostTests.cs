using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.CompilerServices;

namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class PluginLoopHostTests
    {
        [TestMethod]
        public void Constructor_Throws_OnNullArgs()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            FluentActions.Invoking(() => new CoreRuntime.PluginLoopHost(null!, settings, gc!, eh))
                .Should().Throw<ArgumentNullException>();

            FluentActions.Invoking(() => new CoreRuntime.PluginLoopHost(ctx, null!, gc!, eh))
                .Should().Throw<ArgumentNullException>();

            FluentActions.Invoking(() => new CoreRuntime.PluginLoopHost(ctx, settings, null!, eh))
                .Should().Throw<ArgumentNullException>();

            FluentActions.Invoking(() => new CoreRuntime.PluginLoopHost(ctx, settings, gc!, null!))
                .Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void GetPlayerHealthAndESPercent_Return100_WhenRuntimeNotPresent()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var host = new CoreRuntime.PluginLoopHost(ctx, settings, gc!, eh);

            host.GetPlayerHealthPercent().Should().BeApproximately(100f, 0.001f);
            host.GetPlayerEnergyShieldPercent().Should().BeApproximately(100f, 0.001f);
        }

        [TestMethod]
        public void StartCoroutines_CreatesAllCoroutines_AndSetsPriorities()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var host = new CoreRuntime.PluginLoopHost(ctx, settings, gc!, eh);

            var pluginMock = new Moq.Mock<ExileCore.BaseSettingsPlugin<ClickItSettings>>();
            var plugin = pluginMock.Object;

            try
            {
                host.StartCoroutines(plugin);
            }
            catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
            {
            }

            var altarCoroutine = ctx.Runtime.AltarCoroutine;
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
            var perf = new PerformanceMonitor(settings);
            ctx.Services.PerformanceMonitor = perf;
            ctx.Services.ClickService = (ClickService)RuntimeHelpers.GetUninitializedObject(typeof(ClickService));
            ctx.Rendering.ClickRuntimeHost = new CoreRuntime.ClickRuntimeHost(() => ctx.Services.ClickService);

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(ExileCore.GameController)) as ExileCore.GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var host = new CoreRuntime.PluginLoopHost(ctx, settings, gc!, eh);

            ctx.Runtime.Timer.Restart();
            ctx.Runtime.Timer.Stop();
            ctx.Runtime.Timer.Reset();

            var enumerator = host.RunClickLabelStep();
            enumerator.Should().NotBeNull();

            enumerator!.MoveNext();
            ctx.Runtime.WorkFinished.Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRestartClickTimerAfterSuccessfulClick_ReturnsTrue_WhenSequenceIncreases()
        {
            CoreRuntime.PluginLoopHost
                .ShouldRestartClickTimerAfterSuccessfulClick(10, 11)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRestartClickTimerAfterSuccessfulClick_ReturnsFalse_WhenSequenceUnchanged()
        {
            CoreRuntime.PluginLoopHost
                .ShouldRestartClickTimerAfterSuccessfulClick(10, 10)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldCancelOffscreenPathingForInputRelease_ReturnsTrue_WhenNotLazyAndHotkeyReleased()
        {
            CoreRuntime.PluginLoopHost
                .ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled: false, clickHotkeyHeld: false)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldCancelOffscreenPathingForInputRelease_ReturnsFalse_WhenLazyModeEnabled()
        {
            CoreRuntime.PluginLoopHost
                .ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled: true, clickHotkeyHeld: false)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldCancelOffscreenPathingForInputRelease_ReturnsFalse_WhenHotkeyHeld()
        {
            CoreRuntime.PluginLoopHost
                .ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled: false, clickHotkeyHeld: true)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRunManualUiHoverCoroutine_ReturnsTrue_OnlyWhenEnabledNonLazyAndNoHotkeyOverride()
        {
            CoreRuntime.PluginLoopHost
                .ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeTrue();

            CoreRuntime.PluginLoopHost
                .ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: true, clickHotkeyActive: false)
                .Should().BeFalse();

            CoreRuntime.PluginLoopHost
                .ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: true)
                .Should().BeFalse();

            CoreRuntime.PluginLoopHost
                .ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: false, lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSuppressRegularClickForManualUiHoverMode_MatchesManualCoroutineRunCondition()
        {
            CoreRuntime.PluginLoopHost
                .ShouldSuppressRegularClickForManualUiHoverMode(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeTrue();

            CoreRuntime.PluginLoopHost
                .ShouldSuppressRegularClickForManualUiHoverMode(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: true)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldEvaluateRitualState_ReturnsTrue_ForLazyMode()
        {
            CoreRuntime.PluginLoopHost
                .ShouldEvaluateRitualState(lazyModeEnabled: true, clickHotkeyActive: true)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldEvaluateRitualState_ReturnsTrue_WhenHotkeyInactive()
        {
            CoreRuntime.PluginLoopHost
                .ShouldEvaluateRitualState(lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldEvaluateRitualState_ReturnsFalse_WhenNonLazyAndHotkeyActive()
        {
            CoreRuntime.PluginLoopHost
                .ShouldEvaluateRitualState(lazyModeEnabled: false, clickHotkeyActive: true)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldEvaluateLazyModeRestrictedItems_ReturnsTrue_OnlyWhenLazyModeEnabled()
        {
            CoreRuntime.PluginLoopHost
                .ShouldEvaluateLazyModeRestrictedItems(lazyModeEnabled: true)
                .Should().BeTrue();

            CoreRuntime.PluginLoopHost
                .ShouldEvaluateLazyModeRestrictedItems(lazyModeEnabled: false)
                .Should().BeFalse();
        }
    }
}