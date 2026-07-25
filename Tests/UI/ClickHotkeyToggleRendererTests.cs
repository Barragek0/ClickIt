namespace ClickIt.Tests.UI
{
    [TestClass]
    public class ClickHotkeyToggleRendererTests
    {
        [TestMethod]
        public void Constructor_UsesFallbackDeferredTextQueue_WhenNullIsProvided()
        {
            var settings = new ClickItSettings();
            var renderer = new ClickHotkeyToggleRenderer(settings, null!, new InputHandler(settings));

            object queue = typeof(ClickHotkeyToggleRenderer)
                .GetField("_deferredTextQueue", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(renderer)!;

            queue.Should().NotBeNull();
            queue.Should().BeOfType<DeferredTextQueue>();
        }

        [TestMethod]
        public void Constructor_PreservesProvidedDeferredTextQueue_Instance()
        {
            var settings = new ClickItSettings();
            var queue = new DeferredTextQueue();
            var renderer = new ClickHotkeyToggleRenderer(settings, queue, new InputHandler(settings));

            object storedQueue = typeof(ClickHotkeyToggleRenderer)
                .GetField("_deferredTextQueue", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(renderer)!;

            storedQueue.Should().BeSameAs(queue);
        }
    }
}