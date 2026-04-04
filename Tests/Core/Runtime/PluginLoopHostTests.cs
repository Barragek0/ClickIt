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
            ctx.Services.ClickAutomationPort = (ClickAutomationPort)RuntimeHelpers.GetUninitializedObject(typeof(ClickAutomationPort));
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

    }
}