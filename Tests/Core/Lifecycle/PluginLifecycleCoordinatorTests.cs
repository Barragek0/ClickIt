namespace ClickIt.Tests.Core.Lifecycle
{
    [TestClass]
    [DoNotParallelize]
    public class PluginLifecycleCoordinatorTests
    {
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
        public void Initialise_WhenGameControllerMissing_ThrowsAfterPrimingLifecycleState()
        {
            var plugin = new ClickIt();
            var settings = new ClickItSettings();

            Action act = () => PluginLifecycleCoordinator.Initialise(plugin, settings);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*GameController is null during plugin initialization.*");
            plugin.State.Runtime.IsShuttingDown.Should().BeFalse();
        }
    }
}