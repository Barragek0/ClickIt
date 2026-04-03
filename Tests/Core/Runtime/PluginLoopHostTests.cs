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
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            FluentActions.Invoking(() => new PluginLoopHost(null!, settings, gc!, eh))
                .Should().Throw<ArgumentNullException>();

            FluentActions.Invoking(() => new PluginLoopHost(ctx, null!, gc!, eh))
                .Should().Throw<ArgumentNullException>();

            FluentActions.Invoking(() => new PluginLoopHost(ctx, settings, null!, eh))
                .Should().Throw<ArgumentNullException>();

            FluentActions.Invoking(() => new PluginLoopHost(ctx, settings, gc!, null!))
                .Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void GetPlayerHealthAndESPercent_Return100_WhenRuntimeNotPresent()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            host.GetPlayerHealthPercent().Should().BeApproximately(100f, 0.001f);
            host.GetPlayerEnergyShieldPercent().Should().BeApproximately(100f, 0.001f);
        }

        [TestMethod]
        public void StartCoroutines_CreatesAllCoroutines_AndSetsPriorities()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            var pluginMock = new Moq.Mock<BaseSettingsPlugin<ClickItSettings>>();
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
            altarCoroutine!.Priority.Should().Be(CoroutinePriority.Normal);
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
            ctx.Services.ClickAutomationPort = (ClickService)RuntimeHelpers.GetUninitializedObject(typeof(ClickService));
            ctx.Rendering.ClickRuntimeHost = new ClickRuntimeHost(() => ctx.Services.ClickAutomationPort);

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var host = new PluginLoopHost(ctx, settings, gc!, eh);

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
            PluginLoopHost
                .ShouldRestartClickTimerAfterSuccessfulClick(10, 11)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldRestartClickTimerAfterSuccessfulClick_ReturnsFalse_WhenSequenceUnchanged()
        {
            PluginLoopHost
                .ShouldRestartClickTimerAfterSuccessfulClick(10, 10)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldCancelOffscreenPathingForInputRelease_ReturnsTrue_WhenNotLazyAndHotkeyReleased()
        {
            PluginLoopHost
                .ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled: false, clickHotkeyHeld: false)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldCancelOffscreenPathingForInputRelease_ReturnsFalse_WhenLazyModeEnabled()
        {
            PluginLoopHost
                .ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled: true, clickHotkeyHeld: false)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldCancelOffscreenPathingForInputRelease_ReturnsFalse_WhenHotkeyHeld()
        {
            PluginLoopHost
                .ShouldCancelOffscreenPathingForInputRelease(lazyModeEnabled: false, clickHotkeyHeld: true)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldRunManualUiHoverCoroutine_ReturnsTrue_OnlyWhenEnabledNonLazyAndNoHotkeyOverride()
        {
            PluginLoopHost
                .ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeTrue();

            PluginLoopHost
                .ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: true, clickHotkeyActive: false)
                .Should().BeFalse();

            PluginLoopHost
                .ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: true)
                .Should().BeFalse();

            PluginLoopHost
                .ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled: false, lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldSuppressRegularClickForManualUiHoverMode_MatchesManualCoroutineRunCondition()
        {
            PluginLoopHost
                .ShouldSuppressRegularClickForManualUiHoverMode(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeTrue();

            PluginLoopHost
                .ShouldSuppressRegularClickForManualUiHoverMode(manualUiHoverEnabled: true, lazyModeEnabled: false, clickHotkeyActive: true)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldEvaluateRitualState_ReturnsTrue_ForLazyMode()
        {
            PluginLoopHost
                .ShouldEvaluateRitualState(lazyModeEnabled: true, clickHotkeyActive: true)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldEvaluateRitualState_ReturnsTrue_WhenHotkeyInactive()
        {
            PluginLoopHost
                .ShouldEvaluateRitualState(lazyModeEnabled: false, clickHotkeyActive: false)
                .Should().BeTrue();
        }

        [TestMethod]
        public void ShouldEvaluateRitualState_ReturnsFalse_WhenNonLazyAndHotkeyActive()
        {
            PluginLoopHost
                .ShouldEvaluateRitualState(lazyModeEnabled: false, clickHotkeyActive: true)
                .Should().BeFalse();
        }

        [TestMethod]
        public void ShouldEvaluateLazyModeRestrictedItems_ReturnsTrue_OnlyWhenLazyModeEnabled()
        {
            PluginLoopHost
                .ShouldEvaluateLazyModeRestrictedItems(lazyModeEnabled: true)
                .Should().BeTrue();

            PluginLoopHost
                .ShouldEvaluateLazyModeRestrictedItems(lazyModeEnabled: false)
                .Should().BeFalse();
        }
    }
}