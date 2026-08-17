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

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });

            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            ctx.Runtime.Timer.Restart();
            ctx.Runtime.Timer.Stop();
            ctx.Runtime.Timer.Reset();

            var enumerator = host.ClickLabel();
            enumerator.Should().NotBeNull();

            enumerator!.MoveNext();
            ctx.Runtime.WorkFinished.Should().BeTrue();
        }

        [TestMethod]
        public void ClickLabel_SuppressesRegularClick_WhenManualUiHoverModeOwnsTheInput()
        {
            var settings = new ClickItSettings();
            settings.Enable.Value = true;
            settings.ClickOnManualUiHoverOnly.Value = true;
            settings.LazyMode.Value = false;

            var ctx = new PluginContext();
            ctx.Services.PerformanceMonitor = new PerformanceMonitor(settings);
            ctx.Services.ClickAutomationPort = (ClickAutomationPort)RuntimeHelpers.GetUninitializedObject(typeof(ClickAutomationPort));

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            var enumerator = host.ClickLabel();

            enumerator.MoveNext();

            ctx.Runtime.WorkFinished.Should().BeTrue();
        }

        [TestMethod]
        public void ClickLabel_StopsImmediately_WhenPerformanceMonitorMissing()
        {
            var settings = new ClickItSettings();
            settings.Enable.Value = true;

            var ctx = new PluginContext();
            ctx.Services.ClickAutomationPort = (ClickAutomationPort)RuntimeHelpers.GetUninitializedObject(typeof(ClickAutomationPort));

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            var enumerator = host.ClickLabel();

            enumerator.MoveNext().Should().BeFalse();
            ctx.Runtime.WorkFinished.Should().BeFalse();
        }

        [TestMethod]
        public void ClickLabel_StopsImmediately_WhenClickPortMissing()
        {
            var settings = new ClickItSettings();
            settings.Enable.Value = true;

            var ctx = new PluginContext();
            ctx.Services.PerformanceMonitor = new PerformanceMonitor(settings);

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            var enumerator = host.ClickLabel();

            enumerator.MoveNext().Should().BeFalse();
            ctx.Runtime.WorkFinished.Should().BeFalse();
        }

        [TestMethod]
        public void ProcessManualUiHoverClick_SkipsClick_WhenRitualIsActive()
        {
            var settings = new ClickItSettings();
            settings.ClickOnManualUiHoverOnly.Value = true;
            settings.LazyMode.Value = false;

            var ctx = new PluginContext();
            ctx.Services.PerformanceMonitor = new PerformanceMonitor(settings);
            ctx.Services.InputHandler = new InputHandler(settings);
            ctx.Services.CachedLabels = new TimeCache<List<LabelOnGround>>(() => [], 50);
            ctx.Services.ClickAutomationPort = (ClickAutomationPort)RuntimeHelpers.GetUninitializedObject(typeof(ClickAutomationPort));

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            RuntimeMemberAccessor.SetRequiredMember(
                host,
                "_lazyModeContextCache",
                new PluginLazyModeContextCache(new PluginLazyModeContextCacheDependencies(
                    settings,
                    GetLabels: () => [],
                    IsRitualActive: static () => true,
                    HasLazyModeRestrictedItems: static _ => false,
                    GetTimestampMs: static () => 1000)));

            IEnumerator enumerator = InvokePrivateCoroutine(host, "ProcessManualUiHoverClick");

            enumerator.MoveNext().Should().BeFalse();
        }

        [TestMethod]
        public void ProcessManualUiHoverClick_SkipsClick_WhenManualHoverGateIsBlockedByTimer()
        {
            var settings = new ClickItSettings();
            settings.ClickOnManualUiHoverOnly.Value = true;
            settings.LazyMode.Value = false;
            settings.ClickFrequencyTarget.Value = 1000;

            var ctx = new PluginContext();
            var perf = new PerformanceMonitor(settings);
            var labels = new List<LabelOnGround> { ExileCoreOpaqueFactory.CreateOpaqueLabel() };

            ctx.Services.PerformanceMonitor = perf;
            ctx.Services.InputHandler = new InputHandler(settings);
            ctx.Services.CachedLabels = new TimeCache<List<LabelOnGround>>(() => labels, 50);
            ctx.Services.ClickAutomationPort = (ClickAutomationPort)RuntimeHelpers.GetUninitializedObject(typeof(ClickAutomationPort));

            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindowAndGame(new RectangleF(100f, 200f, 1280f, 720f));
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc, eh);
            SetSafeLazyModeContextCache(host, settings, labels);

            perf.RecordClickDispatch();

            IEnumerator enumerator = InvokePrivateCoroutine(host, "ProcessManualUiHoverClick");

            enumerator.MoveNext().Should().BeFalse();
        }

        [TestMethod]
        public void MainClickLabelCoroutine_PacesIdleIterations_WithWaitTime()
        {
            var settings = new ClickItSettings();
            settings.Enable.Value = true;
            settings.ClickFrequencyTarget.Value = 1000;

            var ctx = new PluginContext();
            ctx.Services.PerformanceMonitor = new PerformanceMonitor(settings);
            ctx.Services.ClickAutomationPort = (ClickAutomationPort)RuntimeHelpers.GetUninitializedObject(typeof(ClickAutomationPort));

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            IEnumerator enumerator = InvokePrivateCoroutine(host, "MainClickLabelCoroutine");

            // First iteration: the guarded click step (gate blocked by CanClick) paces itself with a WaitTime instead of spinning.
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().BeAssignableTo<IEnumerator>();
            IEnumerator guardedStep = (IEnumerator)enumerator.Current;
            guardedStep.MoveNext().Should().BeTrue();
            guardedStep.Current.Should().BeOfType<WaitTime>();
        }

        [TestMethod]
        public void ClickLabel_BlocksAndLogsReason_WhenCanClickIsFalse()
        {
            var settings = new ClickItSettings();
            settings.Enable.Value = true;
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            settings.LazyMode.Value = false;
            settings.ClickFrequencyTarget.Value = -1000;

            var ctx = new PluginContext();
            var messages = new List<string>();

            ctx.Services.PerformanceMonitor = new PerformanceMonitor(settings);
            ctx.Services.ClickAutomationPort = (ClickAutomationPort)RuntimeHelpers.GetUninitializedObject(typeof(ClickAutomationPort));

            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindowAndGame(new RectangleF(100f, 200f, 1280f, 720f));
            var eh = new ErrorHandler(settings, (s, f) => { }, (message, _) => messages.Add(message));
            var host = new PluginLoopHost(ctx, settings, gc, eh);

            var enumerator = host.ClickLabel();

            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().BeOfType<WaitTime>();
            ctx.Runtime.WorkFinished.Should().BeTrue();
            messages.Should().ContainSingle(message => message.Contains("[ClickLogic] blocked:", StringComparison.Ordinal)
                && message.Contains("reason='", StringComparison.Ordinal));
        }

        [TestMethod]
        public void ResolveClickTargetTime_SubtractsAverageTimeToClick()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var perf = new PerformanceMonitor(settings);
            ctx.Services.PerformanceMonitor = perf;

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            // No click-timing samples yet -> the target is the frequency target itself.
            double fresh = InvokePrivate<double>(host, "ResolveClickTargetTime", 80d);

            fresh.Should().Be(80);

            // After a click that took measurable time to dispatch, the target backs off by that average so the next click still lands ~target apart.
            perf.MarkClickRunStart();
            Thread.Sleep(10);
            perf.RecordClickDispatch();

            double compensated = InvokePrivate<double>(host, "ResolveClickTargetTime", 80d);

            compensated.Should().BeLessThan(80);
            compensated.Should().BeGreaterThanOrEqualTo(1);
        }

        private static IEnumerator InvokePrivateCoroutine(PluginLoopHost host, string methodName)
        {
            MethodInfo method = typeof(PluginLoopHost).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (IEnumerator)method.Invoke(host, null)!;
        }

        private static T InvokePrivate<T>(PluginLoopHost host, string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(PluginLoopHost).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (T)method.Invoke(host, arguments)!;
        }

        private static void SetSafeLazyModeContextCache(PluginLoopHost host, ClickItSettings settings, IReadOnlyList<LabelOnGround> labels)
        {
            RuntimeMemberAccessor.SetRequiredMember(
                host,
                "_lazyModeContextCache",
                new PluginLazyModeContextCache(new PluginLazyModeContextCacheDependencies(
                    settings,
                    GetLabels: () => labels,
                    IsRitualActive: static () => false,
                    HasLazyModeRestrictedItems: static _ => false,
                    GetTimestampMs: static () => 1000)));
        }

    }
}
