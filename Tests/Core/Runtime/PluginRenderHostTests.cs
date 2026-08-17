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
                    debugClipboardService: CreateOpaqueDebugClipboardService(),
                    drawStartupSetupFlow: static () => { }))
                .Should().NotThrow();
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
            state.Rendering.DeferredDrawQueue = new DeferredDrawQueue();
            debugClipboardService.RequestAdditionalDebugInfoCopy();

            PluginRenderHost.Render(
                state,
                settings,
                gameController: null,
                graphics: null,
                debugClipboardService,
                drawStartupSetupFlow: static () => { });

            debugClipboardService.HasPendingAdditionalDebugInfoCopyRequest.Should().BeFalse();
        }

        [TestMethod]
        public void Render_ConsumesPendingDebugCopy_EvenWhenDebugRenderingIsDisabled()
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
                debugClipboardService,
                drawStartupSetupFlow: static () => { });

            // The copy button must be consumed regardless of the debug-render gate — leaving it latched would flush a stale backlog whenever debug rendering is later enabled.
            debugClipboardService.HasPendingAdditionalDebugInfoCopyRequest.Should().BeFalse();
        }

        private static DebugClipboardService CreateOpaqueDebugClipboardService()
            => CreateOpaque<DebugClipboardService>();

        private static T CreateOpaque<T>() where T : class
            => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }
}
