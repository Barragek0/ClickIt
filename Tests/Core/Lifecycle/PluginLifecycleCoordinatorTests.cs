namespace ClickIt.Tests.Core.Lifecycle
{
    [TestClass]
    [DoNotParallelize]
    public class PluginLifecycleCoordinatorTests
    {
        private static readonly BindingFlags PrivateStaticFlags = BindingFlags.Static | BindingFlags.NonPublic;

        [TestInitialize]
        public void TestInitialize()
        {
            LabelElementSearch.ClearThreadLocalStorage();
            MovementSkillMath.ClearThreadSkillBarEntriesBuffer();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            LabelElementSearch.ClearThreadLocalStorage();
            MovementSkillMath.ClearThreadSkillBarEntriesBuffer();
        }

        [TestMethod]
        public void Shutdown_ClearsThreadLocalBuffers_AndAltarRuntimeCaches()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();
            var altarService = new AltarService(plugin, settings, null);
            plugin.State.Services.AltarService = altarService;

            LabelElementSearch.AddNullElementToThreadLocal();
            LabelElementSearch.GetThreadLocalElementsCount().Should().BeGreaterThan(0);

            List<object?> primedSkillBarBuffer = MovementSkillMath.GetThreadSkillBarEntriesBuffer(4);
            primedSkillBarBuffer.Add(new object());
            primedSkillBarBuffer.Should().HaveCount(1);

            altarService.RecordUnmatchedMod("mod1", "Players");
            altarService.DebugInfo.RecentUnmatchedMods.Should().NotBeEmpty();

            PluginLifecycleCoordinator.Shutdown(plugin, settings);

            plugin.State.Runtime.IsShuttingDown.Should().BeTrue();
            LabelElementSearch.GetThreadLocalElementsCount().Should().Be(0);

            List<object?> freshSkillBarBuffer = MovementSkillMath.GetThreadSkillBarEntriesBuffer(1);
            freshSkillBarBuffer.Should().BeEmpty();
            freshSkillBarBuffer.Should().NotBeSameAs(primedSkillBarBuffer);

            altarService.DebugInfo.RecentUnmatchedMods.Should().BeEmpty();
        }

        [TestMethod]
        public void Shutdown_DisposesCompositionRoot_AfterRuntimeCleanup()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();

            plugin.State.Services.PerformanceMonitor = new PerformanceMonitor(settings);
            plugin.State.Services.ErrorHandler = new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { });
            plugin.State.Services.AreaService = new AreaService();
            plugin.State.Services.AltarService = new AltarService(plugin, settings, null);
            plugin.State.Rendering.DeferredTextQueue = new DeferredTextQueue();
            plugin.State.Rendering.DeferredFrameQueue = new DeferredFrameQueue();
            LockManager.Instance = new LockManager(settings);

            PluginLifecycleCoordinator.Shutdown(plugin, settings);

            plugin.State.Services.PerformanceMonitor.Should().BeNull();
            plugin.State.Services.ErrorHandler.Should().BeNull();
            plugin.State.Services.AreaService.Should().BeNull();
            plugin.State.Services.AltarService.Should().BeNull();
            plugin.State.Rendering.DeferredTextQueue.Should().BeNull();
            plugin.State.Rendering.DeferredFrameQueue.Should().BeNull();
            LockManager.Instance.Should().BeNull();
        }

        [TestMethod]
        public void Shutdown_ClearsInventoryInteractionPolicyCaches()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();
            var gameController = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            int snapshotBuildCount = 0;

            InventoryProbeService probeService = new(new InventoryProbeServiceDependencies(
                CacheWindowMs: 50,
                DebugTrailCapacity: 8,
                TryBuildInventorySnapshot: _ =>
                {
                    snapshotBuildCount++;
                    return (true, default(InventorySnapshot) with
                    {
                        FullProbe = InventoryFullProbe.Empty with
                        {
                            HasPrimaryInventory = true,
                            IsFull = snapshotBuildCount > 1
                        }
                    });
                },
                LayoutCache: new InventoryLayoutCache(cacheWindowMs: 50)));

            InventoryItemEntityService itemEntityService = new(new InventoryItemEntityServiceDependencies(
                CacheWindowMs: 50,
                TryGetPrimaryServerInventory: _ => (false, null),
                TryGetPrimaryServerInventorySlotItems: _ => (false, null),
                EnumerateObjects: _ => Array.Empty<object?>(),
                TryGetInventoryItemEntityFromEntry: _ => null,
                ClassifyInventoryItemEntity: _ => (false, string.Empty)));

            var pickupPolicy = (InventoryPickupPolicyEngine)RuntimeHelpers.GetUninitializedObject(typeof(InventoryPickupPolicyEngine));
            InventoryInteractionPolicy interactionPolicy = new(probeService, itemEntityService, pickupPolicy, "Incursion/IncursionKey");
            plugin.State.Services.InventoryInteractionPolicy = interactionPolicy;

            probeService.IsInventoryFull(gameController, out InventoryFullProbe firstProbe).Should().BeFalse();
            firstProbe.IsFull.Should().BeFalse();
            snapshotBuildCount.Should().Be(1);

            PluginLifecycleCoordinator.Shutdown(plugin, settings);

            probeService.IsInventoryFull(gameController, out InventoryFullProbe secondProbe).Should().BeTrue();
            secondProbe.IsFull.Should().BeTrue();
            snapshotBuildCount.Should().Be(2);
        }

        [TestMethod]
        public void Shutdown_StopsTrackedCoroutines_ClearsRuntimeReferences_AndOnlyStopsNamedClickItCoroutines()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();
            var altarCoroutine = CoroutineTestHarness.CreateCoroutine("ClickIt.AltarLogic", isDone: false);
            var clickLabelCoroutine = CoroutineTestHarness.CreateCoroutine("ClickIt.ClickLogic", isDone: false);
            var manualUiHoverCoroutine = CoroutineTestHarness.CreateCoroutine("ClickIt.ManualUiHoverLogic", isDone: false);
            var delveFlareCoroutine = CoroutineTestHarness.CreateCoroutine("ClickIt.DelveFlareLogic", isDone: false);
            var deepMemoryDumpCoroutine = CoroutineTestHarness.CreateCoroutine("ClickIt.DeepMemoryDump", isDone: false);
            var namedClickItCoroutine = CoroutineTestHarness.CreateCoroutine("ClickIt.NamedCleanup", isDone: false);
            var namedForeignCoroutine = CoroutineTestHarness.CreateCoroutine("Other.Coroutine", isDone: false);

            plugin.State.Runtime.AltarCoroutine = altarCoroutine;
            plugin.State.Runtime.ClickLabelCoroutine = clickLabelCoroutine;
            plugin.State.Runtime.ManualUiHoverCoroutine = manualUiHoverCoroutine;
            plugin.State.Runtime.DelveFlareCoroutine = delveFlareCoroutine;
            plugin.State.Runtime.DeepMemoryDumpCoroutine = deepMemoryDumpCoroutine;

            using var scope = CoroutineTestHarness.ReplaceParallelRunnerCoroutines(
            [
                namedClickItCoroutine,
                namedForeignCoroutine,
            ]);

            PluginLifecycleCoordinator.Shutdown(plugin, settings);

            altarCoroutine.IsDone.Should().BeTrue();
            clickLabelCoroutine.IsDone.Should().BeTrue();
            manualUiHoverCoroutine.IsDone.Should().BeTrue();
            delveFlareCoroutine.IsDone.Should().BeTrue();
            deepMemoryDumpCoroutine.IsDone.Should().BeTrue();
            namedClickItCoroutine.IsDone.Should().BeTrue();
            namedForeignCoroutine.IsDone.Should().BeFalse();

            plugin.State.Runtime.AltarCoroutine.Should().BeNull();
            plugin.State.Runtime.ClickLabelCoroutine.Should().BeNull();
            plugin.State.Runtime.ManualUiHoverCoroutine.Should().BeNull();
            plugin.State.Runtime.DelveFlareCoroutine.Should().BeNull();
            plugin.State.Runtime.DeepMemoryDumpCoroutine.Should().BeNull();
        }

        [TestMethod]
        public void Initialise_WhenGameControllerMissing_ThrowsAfterPrimingLifecycleState()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();

            Action act = () => PluginLifecycleCoordinator.Initialise(plugin, settings);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*GameController is null during plugin initialization.*");
            plugin.State.Runtime.IsShuttingDown.Should().BeFalse();
        }

        [TestMethod]
        public void Initialise_WhenOwnerIsNull_ThrowsArgumentNullException()
        {
            var settings = new ClickItSettings();

            Action act = () => PluginLifecycleCoordinator.Initialise(null!, settings);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("owner");
        }

        [TestMethod]
        public void Initialise_WhenSettingsAreNull_ThrowsArgumentNullException()
        {
            var plugin = new ClickIt();

            Action act = () => PluginLifecycleCoordinator.Initialise(plugin, null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("settings");
        }

        [TestMethod]
        public void Shutdown_WhenOwnerIsNull_ThrowsArgumentNullException()
        {
            var settings = new ClickItSettings();

            Action act = () => PluginLifecycleCoordinator.Shutdown(null!, settings);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("owner");
        }

        [TestMethod]
        public void Shutdown_WhenSettingsAreNull_ThrowsArgumentNullException()
        {
            var plugin = new ClickIt();

            Action act = () => PluginLifecycleCoordinator.Shutdown(plugin, null!);

            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("runtimeSettings");
        }

        [TestMethod]
        public void WaitForCoroutineShutdown_ReturnsImmediately_WhenCoroutineIsNull()
        {
            Action act = () => InvokePrivateLifecycleMethod("WaitForCoroutineShutdown", null, 0);

            act.Should().NotThrow();
        }

        [TestMethod]
        public void WaitForCoroutineShutdown_ReturnsImmediately_WhenCoroutineAlreadyDone()
        {
            var coroutine = CoroutineTestHarness.CreateCoroutine("ClickIt.Done", isDone: true);

            Action act = () => InvokePrivateLifecycleMethod("WaitForCoroutineShutdown", coroutine, 0);

            act.Should().NotThrow();
            coroutine.IsDone.Should().BeTrue();
        }

        [TestMethod]
        public void WaitForCoroutineShutdown_StopsWaiting_WhenTimeoutExpires()
        {
            var coroutine = CoroutineTestHarness.CreateCoroutine("ClickIt.Active", isDone: false);

            Action act = () => InvokePrivateLifecycleMethod("WaitForCoroutineShutdown", coroutine, 0);

            act.Should().NotThrow();
            coroutine.IsDone.Should().BeFalse();
        }

        [TestMethod]
        public void WaitForCoroutineShutdown_WaitsUntilCoroutineCompletes()
        {
            var coroutine = CoroutineTestHarness.CreateCoroutine("ClickIt.Active", isDone: false);
            using ManualResetEventSlim completionSignal = new(initialState: false);
            Thread worker = new(() =>
            {
                Thread.Sleep(5);
                RuntimeMemberAccessor.SetRequiredMember(coroutine, nameof(Coroutine.IsDone), true);
                completionSignal.Set();
            });

            worker.Start();

            try
            {
                Action act = () => InvokePrivateLifecycleMethod("WaitForCoroutineShutdown", coroutine, 50);

                act.Should().NotThrow();
                completionSignal.Wait(200).Should().BeTrue();
                coroutine.IsDone.Should().BeTrue();
            }
            finally
            {
                worker.Join();
            }
        }

        [TestMethod]
        public void StopNamedClickItCoroutines_DoesNotThrow_WhenParallelRunnerIsUnavailable()
        {
            using var scope = CoroutineTestHarness.ReplaceParallelRunner(null);

            Action act = () => InvokePrivateLifecycleMethod("StopNamedClickItCoroutines");

            act.Should().NotThrow();
        }

        [TestMethod]
        public void WaitForNamedClickItCoroutinesShutdown_DoesNotThrow_WhenParallelRunnerIsUnavailable()
        {
            using var scope = CoroutineTestHarness.ReplaceParallelRunner(null);

            Action act = () => InvokePrivateLifecycleMethod("WaitForNamedClickItCoroutinesShutdown", 0);

            act.Should().NotThrow();
        }

        [TestMethod]
        public void WaitForNamedClickItCoroutinesShutdown_WaitsUntilActiveClickItCoroutineCompletes()
        {
            var activeCoroutine = CoroutineTestHarness.CreateCoroutine("ClickIt.Active", isDone: false);
            using var scope = CoroutineTestHarness.ReplaceParallelRunnerCoroutines([activeCoroutine]);
            using ManualResetEventSlim completionSignal = new(initialState: false);
            Thread worker = new(() =>
            {
                Thread.Sleep(5);
                RuntimeMemberAccessor.SetRequiredMember(activeCoroutine, nameof(Coroutine.IsDone), true);
                completionSignal.Set();
            });

            worker.Start();

            try
            {
                Action act = () => InvokePrivateLifecycleMethod("WaitForNamedClickItCoroutinesShutdown", 50);

                act.Should().NotThrow();
                completionSignal.Wait(200).Should().BeTrue();
                activeCoroutine.IsDone.Should().BeTrue();
            }
            finally
            {
                worker.Join();
            }
        }

        private static void InvokePrivateLifecycleMethod(string methodName, params object?[] arguments)
        {
            MethodInfo method = typeof(PluginLifecycleCoordinator).GetMethod(methodName, PrivateStaticFlags)!;
            method.Invoke(null, arguments);
        }
    }
}