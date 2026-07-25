namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class PluginRenderHostTests
    {
        [TestMethod]
        public void Render_DoesNotThrow_WhenOptionalRenderersAreUnavailable()
        {
            var state = new PluginContext();
            var settings = new ClickItSettings();

            FluentActions.Invoking(() => PluginRenderHost.Render(
                    state,
                    settings,
                    gameController: null,
                    graphics: null,
                    debugClipboardService: CreateOpaqueDebugClipboardService()))
                .Should().NotThrow();
        }

        [TestMethod]
        public void Render_ClearsDeferredQueues_WhenLazyModeRequiresGameControllerButMissing()
        {
            var state = new PluginContext();
            var settings = new ClickItSettings();
            settings.LazyMode.Value = true;

            state.Rendering.LazyModeRenderer = CreateOpaque<LazyModeRenderer>();
            state.Rendering.DeferredTextQueue = new DeferredTextQueue();
            state.Rendering.DeferredFrameQueue = new DeferredFrameQueue();
            state.Rendering.DeferredTextQueue.Enqueue("queued text", new Vector2(1, 2), Color.White, 14);
            state.Rendering.DeferredFrameQueue.Enqueue(new RectangleF(1, 2, 3, 4), Color.White, 1);

            FluentActions.Invoking(() => PluginRenderHost.Render(
                state,
                settings,
                gameController: null,
                graphics: null,
                debugClipboardService: CreateOpaqueDebugClipboardService()))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("GameController is null during render");

            state.Rendering.DeferredTextQueue.GetPendingCount().Should().Be(0);
            state.Rendering.DeferredFrameQueue.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void Render_ClearsDeferredQueues_WhenInventoryWarningRendererRequiresGameControllerButMissing()
        {
            var state = new PluginContext();
            var settings = new ClickItSettings();

            state.Rendering.InventoryFullWarningRenderer = CreateOpaque<InventoryFullWarningRenderer>();
            state.Rendering.DeferredTextQueue = new DeferredTextQueue();
            state.Rendering.DeferredFrameQueue = new DeferredFrameQueue();
            state.Rendering.DeferredTextQueue.Enqueue("queued text", new Vector2(1, 2), Color.White, 14);
            state.Rendering.DeferredFrameQueue.Enqueue(new RectangleF(1, 2, 3, 4), Color.White, 1);

            FluentActions.Invoking(() => PluginRenderHost.Render(
                state,
                settings,
                gameController: null,
                graphics: null,
                debugClipboardService: CreateOpaqueDebugClipboardService()))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("GameController is null during render");

            state.Rendering.DeferredTextQueue.GetPendingCount().Should().Be(0);
            state.Rendering.DeferredFrameQueue.GetPendingCount().Should().Be(0);
        }

        [TestMethod]
        public void Render_CompletesPendingDebugCopy_WhenDebugRenderingIsEnabled()
        {
            var plugin = new ClickIt();
            var state = plugin.State;
            var settings = new ClickItSettings();
            DebugClipboardService debugClipboardService = plugin.GetDebugClipboardService();

            settings.DebugMode.Value = true;
            settings.RenderDebug.Value = true;
            state.Rendering.DeferredTextQueue = new DeferredTextQueue();
            debugClipboardService.RequestAdditionalDebugInfoCopy();

            PluginRenderHost.Render(
                state,
                settings,
                gameController: null,
                graphics: null,
                debugClipboardService);

            debugClipboardService.HasPendingAdditionalDebugInfoCopyRequest.Should().BeFalse();
        }

        [TestMethod]
        public void Render_LeavesPendingDebugCopy_WhenDebugRenderingIsDisabled()
        {
            var plugin = new ClickIt();
            var state = plugin.State;
            var settings = new ClickItSettings();
            DebugClipboardService debugClipboardService = plugin.GetDebugClipboardService();

            settings.DebugMode.Value = true;
            settings.RenderDebug.Value = false;
            debugClipboardService.RequestAdditionalDebugInfoCopy();

            PluginRenderHost.Render(
                state,
                settings,
                gameController: null,
                graphics: null,
                debugClipboardService);

            debugClipboardService.HasPendingAdditionalDebugInfoCopyRequest.Should().BeTrue();
        }

        private static DebugClipboardService CreateOpaqueDebugClipboardService()
            => CreateOpaque<DebugClipboardService>();

        private static T CreateOpaque<T>() where T : class
            => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }
}