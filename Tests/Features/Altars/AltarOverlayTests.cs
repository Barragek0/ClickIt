namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarOverlayTests
    {
        private static readonly RectangleF Window = new(100f, 100f, 1280f, 720f);

        [TestMethod]
        public void Draw_EnqueuesFrames_WhenBothModsRectsOnScreen()
        {
            var queue = new DeferredDrawQueue();
            AltarOverlay overlay = CreateOverlay(BuildService(BuildAltar(
                new AltarProbeElement(new RectangleF(200f, 200f, 120f, 40f)),
                new AltarProbeElement(new RectangleF(200f, 260f, 120f, 40f)))));

            overlay.Draw(CreateDrawContext(queue));

            queue.GetPendingFrameSnapshot().Count(item => item.Thickness > 0).Should().Be(2);
        }

        [TestMethod]
        public void Draw_EnqueuesFrames_WhenPartOfAltarIsOnScreen()
        {
            // A partially visible altar (top mods on screen, bottom mods below the window) still draws: the check skips only when BOTH mods rects are fully off-screen.
            var queue = new DeferredDrawQueue();
            AltarOverlay overlay = CreateOverlay(BuildService(BuildAltar(
                new AltarProbeElement(new RectangleF(200f, 200f, 120f, 40f)),
                new AltarProbeElement(new RectangleF(200f, 4000f, 120f, 40f)))));

            overlay.Draw(CreateDrawContext(queue));

            queue.GetPendingFrameSnapshot().Count(item => item.Thickness > 0).Should().Be(2);
        }

        [TestMethod]
        public void Draw_EnqueuesNoFrames_WhenAltarIsFullyOffScreen()
        {
            // Regression: an altar whose mods have left the window must not draw boxes near a screen corner.
            var queue = new DeferredDrawQueue();
            AltarOverlay overlay = CreateOverlay(BuildService(BuildAltar(
                new AltarProbeElement(new RectangleF(9000f, 9000f, 120f, 40f)),
                new AltarProbeElement(new RectangleF(9000f, 9100f, 120f, 40f)))));

            overlay.Draw(CreateDrawContext(queue));

            queue.GetPendingFrameSnapshot().Count(item => item.Thickness > 0).Should().Be(0);
        }

        private static AltarService BuildService(PrimaryAltarComponent component)
        {
            var owner = (ClickIt)RuntimeHelpers.GetUninitializedObject(typeof(ClickIt));
            var service = new AltarService(owner, new ClickItSettings(), cachedLabels: null);
            service.AddAltarComponent(component);
            return service;
        }

        private static PrimaryAltarComponent BuildAltar(AltarProbeElement top, AltarProbeElement bottom)
            => new(
                AltarType.Unknown,
                new SecondaryAltarComponent(top, [], []),
                new AltarButton(null),
                new SecondaryAltarComponent(bottom, [], []),
                new AltarButton(null));

        private static AltarOverlay CreateOverlay(AltarService service)
            => new(new WeightCalculator(new ClickItSettings()), new AltarChoiceEvaluator(new ClickItSettings()), service, logMessage: null);

        private static OverlayRenderContext CreateDrawContext(DeferredDrawQueue queue)
            => new(new ClickItSettings(), GameController: null, Graphics: null, WindowArea: Window, Labels: null, queue);

        public sealed class AltarProbeElement(RectangleF clientRect) : Element
        {
            public new bool IsValid { get; set; } = true;

            public override RectangleF GetClientRect() => clientRect;
        }
    }
}
