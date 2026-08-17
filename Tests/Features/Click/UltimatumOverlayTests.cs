namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumOverlayTests
    {
        private sealed class ProbeOptionElement(RectangleF clientRect) : Element
        {
            public RectangleF CurrentRect { get; set; } = clientRect;

            public override RectangleF GetClientRect() => CurrentRect;
        }

        [TestMethod]
        public void Draw_UsesFreshElementRect_NotStaleCachedPreviewRect()
        {
            var element = new ProbeOptionElement(new RectangleF(10f, 20f, 100f, 40f));
            var overlay = new UltimatumOverlay(
                () => [new UltimatumPanelOptionPreview(new RectangleF(1f, 2f, 3f, 4f), element, "mod", 0, false)],
                () => { });

            var queue = new DeferredDrawQueue();
            overlay.Draw(CreateContext(queue));

            var frames = queue.GetPendingFrameSnapshot();
            frames.Should().ContainSingle();
            frames[0].Rectangle.Should().Be(new RectangleF(10f, 20f, 100f, 40f));
        }

        [TestMethod]
        public void Draw_FallsBackToCachedRect_WhenElementRectIsInvalid()
        {
            var element = new ProbeOptionElement(RectangleF.Empty);
            var overlay = new UltimatumOverlay(
                () => [new UltimatumPanelOptionPreview(new RectangleF(5f, 6f, 7f, 8f), element, "mod", 0, false)],
                () => { });

            var queue = new DeferredDrawQueue();
            overlay.Draw(CreateContext(queue));

            var frames = queue.GetPendingFrameSnapshot();
            frames.Should().ContainSingle();
            frames[0].Rectangle.Should().Be(new RectangleF(5f, 6f, 7f, 8f));
        }

        [TestMethod]
        public void Draw_EnqueuesNothing_WhenPreviewMissing()
        {
            var overlay = new UltimatumOverlay(() => null, () => { });

            var queue = new DeferredDrawQueue();
            overlay.Draw(CreateContext(queue));

            queue.GetPendingFrameSnapshot().Should().BeEmpty();
        }

        private static OverlayRenderContext CreateContext(DeferredDrawQueue queue)
            => new(
                new ClickItSettings(),
                GameController: null,
                Graphics: null,
                WindowArea: default,
                Labels: null,
                queue);
    }
}
