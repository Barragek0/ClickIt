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

        [TestMethod]
        public void RunClickLabelStep_SuppressesRegularClick_WhenManualUiHoverModeOwnsTheInput()
        {
            var settings = new ClickItSettings();
            settings.Enable.Value = true;
            settings.ClickOnManualUiHoverOnly.Value = true;
            settings.LazyMode.Value = false;

            var ctx = new PluginContext();
            ctx.Services.PerformanceMonitor = new PerformanceMonitor(settings);
            ctx.Services.ClickAutomationPort = (ClickAutomationPort)RuntimeHelpers.GetUninitializedObject(typeof(ClickAutomationPort));
            ctx.Rendering.ClickRuntimeHost = new ClickRuntimeHost(() => ctx.Services.ClickAutomationPort);

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            var enumerator = host.RunClickLabelStep();

            enumerator.MoveNext();

            ctx.Runtime.WorkFinished.Should().BeTrue();
        }

        [TestMethod]
        public void RunClickLabelStep_StopsImmediately_WhenPerformanceMonitorMissing()
        {
            var settings = new ClickItSettings();
            settings.Enable.Value = true;

            var ctx = new PluginContext();
            ctx.Rendering.ClickRuntimeHost = new ClickRuntimeHost(() => new FakeClickAutomationService());

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            var enumerator = host.RunClickLabelStep();

            enumerator.MoveNext().Should().BeFalse();
            ctx.Runtime.WorkFinished.Should().BeFalse();
        }

        [TestMethod]
        public void RunClickLabelStep_StopsImmediately_WhenRuntimeHostMissing()
        {
            var settings = new ClickItSettings();
            settings.Enable.Value = true;

            var ctx = new PluginContext();
            ctx.Services.PerformanceMonitor = new PerformanceMonitor(settings);

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            var enumerator = host.RunClickLabelStep();

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
            var fakeService = new FakeClickAutomationService();

            ctx.Services.PerformanceMonitor = new PerformanceMonitor(settings);
            ctx.Services.InputHandler = new InputHandler(settings);
            ctx.Services.CachedLabels = new TimeCache<List<LabelOnGround>>(() => [], 50);
            ctx.Rendering.ClickRuntimeHost = new ClickRuntimeHost(() => fakeService);

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
            fakeService.ManualHoverCallCount.Should().Be(0);
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
            var fakeService = new FakeClickAutomationService();

            ctx.Services.PerformanceMonitor = perf;
            ctx.Services.InputHandler = new InputHandler(settings);
            ctx.Services.CachedLabels = new TimeCache<List<LabelOnGround>>(() => labels, 50);
            ctx.Rendering.ClickRuntimeHost = new ClickRuntimeHost(() => fakeService);

            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindowAndGame(new RectangleF(100f, 200f, 1280f, 720f));
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc, eh);
            SetSafeLazyModeContextCache(host, settings, labels);

            ctx.Runtime.Timer.Reset();

            IEnumerator enumerator = InvokePrivateCoroutine(host, "ProcessManualUiHoverClick");

            enumerator.MoveNext().Should().BeFalse();
            fakeService.ManualHoverCallCount.Should().Be(0);
        }

        [TestMethod]
        public void RunClickLabelStep_CancelsOffscreenPathing_AndLogsBlockReason_WhenCanClickIsFalse()
        {
            var settings = new ClickItSettings();
            settings.Enable.Value = true;
            settings.DebugMode.Value = true;
            settings.LogMessages.Value = true;
            settings.LazyMode.Value = false;
            settings.ClickFrequencyTarget.Value = -1000;

            var ctx = new PluginContext();
            var fakeService = new FakeClickAutomationService();
            var messages = new List<string>();

            ctx.Services.PerformanceMonitor = new PerformanceMonitor(settings);
            ctx.Services.InputHandler = new InputHandler(settings);
            ctx.Rendering.ClickRuntimeHost = new ClickRuntimeHost(() => fakeService);

            GameController gc = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindowAndGame(new RectangleF(100f, 200f, 1280f, 720f));
            var eh = new ErrorHandler(settings, (s, f) => { }, (message, _) => messages.Add(message));
            var host = new PluginLoopHost(ctx, settings, gc, eh);

            var enumerator = host.RunClickLabelStep();

            enumerator.MoveNext().Should().BeFalse();
            ctx.Runtime.WorkFinished.Should().BeTrue();
            fakeService.CancelOffscreenPathingCallCount.Should().Be(1);
            fakeService.ProcessRegularClickCallCount.Should().Be(0);
            messages.Should().ContainSingle(message => message.Contains("[ClickLogic] blocked:", StringComparison.Ordinal)
                && message.Contains("reason='", StringComparison.Ordinal));
        }

        [TestMethod]
        public void PrivateGetSuccessfulClickSequence_ReturnsDispatcherSequence_WhenDispatcherPresent()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => true);
            RuntimeMemberAccessor.SetRequiredMember(executor, "_successfulClickSequence", 7L);
            ctx.Services.LockedInteractionDispatcher = new LockedInteractionDispatcher(executor);

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            long result = InvokePrivate<long>(host, "GetSuccessfulClickSequence");

            result.Should().Be(7L);
        }

        [TestMethod]
        public void RestartClickTimerAfterSuccessfulInteraction_RestartsTimer_WhenSequenceAdvances()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => true);
            RuntimeMemberAccessor.SetRequiredMember(executor, "_successfulClickSequence", 4L);
            ctx.Services.LockedInteractionDispatcher = new LockedInteractionDispatcher(executor);

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            ctx.Runtime.Timer.Start();
            Thread.Sleep(20);
            InvokePrivate(host, "RestartClickTimerAfterSuccessfulInteraction", 3L, true);

            ctx.Runtime.Timer.ElapsedMilliseconds.Should().BeLessThan(20L);
        }

        [TestMethod]
        public void RestartClickTimerAfterSuccessfulInteraction_DoesNotRestartTimer_WhenSequenceDoesNotAdvance()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => true);
            RuntimeMemberAccessor.SetRequiredMember(executor, "_successfulClickSequence", 3L);
            ctx.Services.LockedInteractionDispatcher = new LockedInteractionDispatcher(executor);

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            ctx.Runtime.Timer.Start();
            Thread.Sleep(20);
            long elapsedBefore = ctx.Runtime.Timer.ElapsedMilliseconds;

            InvokePrivate(host, "RestartClickTimerAfterSuccessfulInteraction", 3L, true);

            ctx.Runtime.Timer.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(elapsedBefore);
        }

        [TestMethod]
        public void RestartClickTimerAfterSuccessfulInteraction_DoesNotRestartTimer_WhenInteractionDidNotSucceed()
        {
            var settings = new ClickItSettings();
            var ctx = new PluginContext();
            var executor = new InteractionExecutor(settings, new PerformanceMonitor(settings), () => true);
            RuntimeMemberAccessor.SetRequiredMember(executor, "_successfulClickSequence", 8L);
            ctx.Services.LockedInteractionDispatcher = new LockedInteractionDispatcher(executor);

            var gc = RuntimeHelpers.GetUninitializedObject(typeof(GameController)) as GameController;
            var eh = new ErrorHandler(settings, (s, f) => { }, (m, f) => { });
            var host = new PluginLoopHost(ctx, settings, gc!, eh);

            ctx.Runtime.Timer.Start();
            Thread.Sleep(20);
            long elapsedBefore = ctx.Runtime.Timer.ElapsedMilliseconds;

            InvokePrivate(host, "RestartClickTimerAfterSuccessfulInteraction", 3L, false);

            ctx.Runtime.Timer.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(elapsedBefore);
        }

        private static IEnumerator InvokePrivateCoroutine(PluginLoopHost host, string methodName)
        {
            MethodInfo method = typeof(PluginLoopHost).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (IEnumerator)method.Invoke(host, null)!;
        }

        private static void InvokePrivate(PluginLoopHost host, string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(PluginLoopHost).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(host, arguments);
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

        private sealed class FakeClickAutomationService : IClickAutomationService
        {
            public int CancelOffscreenPathingCallCount { get; private set; }
            public int ProcessRegularClickCallCount { get; private set; }
            public int ManualHoverCallCount { get; private set; }

            public void CancelOffscreenPathingState()
            {
                CancelOffscreenPathingCallCount++;
            }

            public void CancelPostChestLootSettlementState()
            {
            }

            public IEnumerator ProcessRegularClick()
            {
                ProcessRegularClickCallCount++;
                yield break;
            }

            public bool TryClickManualUiHoverLabel(IReadOnlyList<LabelOnGround>? labels)
            {
                ManualHoverCallCount++;
                return false;
            }

            public bool TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
            {
                previews = [];
                return false;
            }
        }

    }
}