namespace ClickIt.Tests.Core.Bootstrap
{
    [TestClass]
    public class DomainAssemblerTests
    {
        [TestMethod]
        public void CoreDomainAssembler_AssembleInternal_CreatesServicesAndRefreshesAreaState_WhenStartupDependenciesAreInjected()
        {
            var owner = new ClickIt();
            var settings = new ClickItSettings();
            RectangleF windowRect = new(100f, 200f, 1280f, 720f);
            GameController gameController = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(windowRect);
            Camera camera = (Camera)RuntimeHelpers.GetUninitializedObject(typeof(Camera));

            CoreDomainServices core = CoreDomainAssembler.Assemble(
                owner,
                settings,
                gameController,
                static (areaService, _) => areaService.ApplyBlockedSnapshot(new AreaBlockedSnapshot
                {
                    FullScreenRectangle = new RectangleF(100f, 200f, 1280f, 720f),
                }),
                _ => camera);

            core.PerformanceMonitor.Should().NotBeNull();
            core.ErrorHandler.Should().NotBeNull();
            core.AreaService.Should().NotBeNull();
            core.LabelReadModelService.Should().NotBeNull();
            core.CachedLabels.Should().NotBeNull();
            core.Camera.Should().BeSameAs(camera);
            core.AltarService.Should().NotBeNull();
            core.LabelFilterPort.Should().NotBeNull();
            core.LabelDebugService.Should().NotBeNull();
            core.LazyModeBlockerService.Should().NotBeNull();
            core.InventoryProbeService.Should().NotBeNull();
            core.InventoryInteractionPolicy.Should().NotBeNull();
            core.ShrineService.Should().NotBeNull();
            core.InputHandler.Should().NotBeNull();
            core.PathfindingService.Should().NotBeNull();
            core.WeightCalculator.Should().NotBeNull();
            core.DeferredTextQueue.Should().NotBeNull();
            core.DeferredFrameQueue.Should().NotBeNull();
            core.AreaService.FullScreenRectangle.Should().Be(new RectangleF(windowRect.X, windowRect.Y, windowRect.Width, windowRect.Height));
        }

        [TestMethod]
        public void CoreDomainAssembler_Assemble_Throws_WhenStartupGraphHasNoCamera()
        {
            var owner = new ClickIt();
            var settings = new ClickItSettings();
            GameController gameController = ExileCoreVisibleObjectBuilder.CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f));

            Action act = () => CoreDomainAssembler.Assemble(owner, settings, gameController);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("Camera is null during plugin initialization.");
        }

        [TestMethod]
        public void RenderingAndClickAssemblers_Assemble_DirectBootstrapOwnersUsingCoreServices()
        {
            var owner = new ClickIt();
            var settings = new ClickItSettings();
            GameController gameController = ExileCoreOpaqueFactory.CreateOpaqueGameController();
            CoreDomainServices core = CreateCoreDomainServices(settings);
            Graphics graphics = (Graphics)RuntimeHelpers.GetUninitializedObject(typeof(Graphics));
            var clipboardCopyAttempts = new List<(long Timestamp, long Sequence)>();
            var frozenTelemetrySnapshots = new List<(string Reason, int HoldDurationMs)>();

            RenderingDomainServices rendering = RenderingDomainAssembler.Assemble(
                owner,
                settings,
                core,
                graphics,
                owner.LogMessage,
                (snapshot, now) =>
                {
                    clipboardCopyAttempts.Add((now, snapshot.Sequence));
                    return true;
                });
            ClickAutomationPort clickAutomationPort = ClickDomainAssembler.Assemble(
                settings,
                gameController,
                core,
                rendering.AltarChoiceEvaluator,
                static (_, _) => true,
                static (_, _) => true,
                (reason, holdDurationMs) => frozenTelemetrySnapshots.Add((reason, holdDurationMs)));
            UltimatumRenderer ultimatumRenderer = RenderingDomainAssembler.CreateUltimatumRenderer(settings, clickAutomationPort, core.DeferredFrameQueue);
            AlertService alertService = CreateOpaque<AlertService>();
            SettingsDomainServices settingsDomain = SettingsDomainAssembler.Assemble(alertService, settings);

            rendering.DebugRenderer.Should().NotBeNull();
            rendering.StrongboxRenderer.Should().NotBeNull();
            rendering.LazyModeRenderer.Should().NotBeNull();
            rendering.ClickHotkeyToggleRenderer.Should().NotBeNull();
            rendering.InventoryFullWarningRenderer.Should().NotBeNull();
            rendering.PathfindingRenderer.Should().NotBeNull();
            rendering.AltarChoiceEvaluator.Should().NotBeNull();
            rendering.AltarDisplayRenderer.Should().NotBeNull();
            clickAutomationPort.Should().NotBeNull();
            clickAutomationPort.ClickAutomationSupport.Should().NotBeNull();
            clickAutomationPort.LockedInteractionDispatcher.Should().NotBeNull();
            ultimatumRenderer.Should().NotBeNull();
            settingsDomain.AlertService.Should().BeSameAs(alertService);
            settingsDomain.EffectiveSettings.Should().BeSameAs(settings);
            clipboardCopyAttempts.Should().BeEmpty();
            frozenTelemetrySnapshots.Should().BeEmpty();
            LockManager.Instance.Should().NotBeNull();
        }

        [TestCleanup]
        public void Cleanup()
        {
            LockManager.Instance = null;
        }

        private static CoreDomainServices CreateCoreDomainServices(ClickItSettings settings)
        {
            var performanceMonitor = new PerformanceMonitor(settings);
            var errorHandler = new ErrorHandler(settings, static (_, _) => { }, static (_, _) => { });

            return new CoreDomainServices(
                PerformanceMonitor: performanceMonitor,
                ErrorHandler: errorHandler,
                AreaService: new AreaService(),
                LabelReadModelService: CreateOpaque<LabelReadModelService>(),
                CachedLabels: new TimeCache<List<LabelOnGround>>(() => [], 50),
                Camera: CreateOpaque<Camera>(),
                AltarService: CreateOpaque<AltarService>(),
                LabelFilterPort: CreateOpaque<LabelFilterPort>(),
                LabelDebugService: CreateOpaque<LabelDebugService>(),
                LazyModeBlockerService: CreateOpaque<LazyModeBlockerService>(),
                InventoryProbeService: CreateOpaque<InventoryProbeService>(),
                InventoryInteractionPolicy: CreateOpaque<InventoryInteractionPolicy>(),
                ShrineService: CreateOpaque<ShrineService>(),
                InputHandler: new InputHandler(settings),
                PathfindingService: CreateOpaque<PathfindingService>(),
                WeightCalculator: CreateOpaque<WeightCalculator>(),
                DeferredTextQueue: new DeferredTextQueue(),
                DeferredFrameQueue: new DeferredFrameQueue());
        }

        private static T CreateOpaque<T>() where T : class
            => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }
}