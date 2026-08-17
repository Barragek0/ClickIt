namespace ClickIt.Tests.Features.Harvest
{
    [TestClass]
    public class HarvestOverlayTests
    {
        private static readonly RectangleF Window = new(100f, 100f, 1280f, 720f);

        [TestMethod]
        public void Draw_EnqueuesFrame_WhenLabelBoundsOnScreen()
        {
            var queue = new DeferredDrawQueue();
            HarvestOverlay overlay = CreateOverlay(SetEstimates([
                new HarvestPlotEstimate(ExileCoreOpaqueFactory.CreateOpaqueLabel(), [], 100d, new RectangleF(200f, 200f, 100f, 40f)),
            ]));

            overlay.Draw(CreateDrawContext(queue));

            queue.GetPendingFrameSnapshot().Count(item => item.Thickness > 0).Should().Be(1);
        }

        [TestMethod]
        public void Draw_EnqueuesNoFrame_WhenLabelBoundsFullyOffScreen()
        {
            // Regression: a harvest plot whose label has left the window must not draw a box near a screen corner.
            var queue = new DeferredDrawQueue();
            HarvestOverlay overlay = CreateOverlay(SetEstimates([
                new HarvestPlotEstimate(ExileCoreOpaqueFactory.CreateOpaqueLabel(), [], 100d, new RectangleF(9000f, 9000f, 100f, 40f)),
            ]));

            overlay.Draw(CreateDrawContext(queue));

            queue.GetPendingFrameSnapshot().Count(item => item.Thickness > 0).Should().Be(0);
        }

        private static HarvestOverlay CreateOverlay(HarvestService service)
            => new(service);

        private static HarvestService SetEstimates(IReadOnlyList<HarvestPlotEstimate> estimates)
        {
            var service = new HarvestService(new ClickItSettings());
            typeof(HarvestService)
                .GetProperty(nameof(HarvestService.CurrentEstimates), BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(service, estimates);
            return service;
        }

        private static OverlayRenderContext CreateDrawContext(DeferredDrawQueue queue)
            => new(new ClickItSettings(), GameController: null, Graphics: null, WindowArea: Window, Labels: null, queue);
    }
}
