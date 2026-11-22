namespace ClickIt.Tests.Features.Altars
{
    [TestClass]
    public class AltarOverlayTests
    {
        private static readonly RectangleF Window = new(100f, 100f, 1280f, 720f);

        [TestMethod]
        [TestCategory("Regression")]
        public void SourceLabelVisibility_RejectsStreamedOutLabelEvenWhenRootIsStillVisible()
        {
            var root = new AltarProbeElement(new RectangleF(200, 200, 120, 40));
            var label = new SourceLabelProbe { IsVisible = true, Label = root };

            AltarOverlay.IsSourceLabelVisible(label).Should().BeTrue();
            label.IsVisible = false;
            AltarOverlay.IsSourceLabelVisible(label).Should().BeFalse();
            label.IsVisible = true;
            root.IsVisible = false;
            AltarOverlay.IsSourceLabelVisible(label).Should().BeFalse();
            label.Label = null;
            AltarOverlay.IsSourceLabelVisible(label).Should().BeFalse();
        }

        public sealed class SourceLabelProbe
        {
            public bool IsVisible { get; set; }
            public AltarProbeElement? Label { get; set; }
        }

        [TestMethod]
        public void SourceProjectionRejectsDistantAltarWithOptionIntersectingTheCorner()
        {
            RectangleF top = new(-201, -149, 492, 90);
            RectangleF bottom = new(-229, 56, 549, 73);

            LabelGeometry.IsRectOnScreen(bottom, Window).Should().BeTrue();
            AltarOverlay.IsSourceNearOptions(new Vector2(-2310, -5215), top, bottom).Should().BeFalse();
            AltarOverlay.IsSourceNearOptions(new Vector2(0, 0), top, bottom).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void SourceProjectionRejectsStreamOutRectanglesThatJumpBelowTheAltar()
        {
            RectangleF top = new(-199.8f, -137.7f, 490.7f, 124.2f);
            RectangleF bottom = new(-137.0f, 56.0f, 365.2f, 174.8f);

            LabelGeometry.IsRectOnScreen(bottom, new RectangleF(0, 0, 1920, 1080)).Should().BeTrue();
            AltarOverlay.IsSourceNearOptions(new Vector2(182.9f, -458.2f), top, bottom).Should().BeFalse();
            AltarOverlay.IsSourceNearOptions(new Vector2(1199f, 483f),
                new RectangleF(1017, 54, 491, 124), new RectangleF(1080, 293, 365, 175)).Should().BeTrue();
        }

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

        [TestMethod]
        public void Draw_DoesNotRenderWhenOneModBecomesHiddenWhileStreamingOut()
        {
            var queue = new DeferredDrawQueue();
            var top = new AltarProbeElement(new RectangleF(200, 200, 120, 40));
            var bottom = new AltarProbeElement(new RectangleF(200, 260, 120, 40)) { IsVisible = false };
            CreateOverlay(BuildService(BuildAltar(top, bottom))).Draw(CreateDrawContext(queue));

            queue.GetPendingFrameSnapshot().Should().BeEmpty();
            queue.GetPendingTextSnapshot().Should().BeEmpty();
        }

        [TestMethod]
        public void Draw_DoesNotRenderWhenStreamedOutRectsCollapseToOrigin()
        {
            var queue = new DeferredDrawQueue();
            CreateOverlay(BuildService(BuildAltar(
                new AltarProbeElement(new RectangleF(0, 0, 120, 40)),
                new AltarProbeElement(new RectangleF(0, 0, 120, 40))))).Draw(CreateDrawContext(queue));

            queue.GetPendingFrameSnapshot().Should().BeEmpty();
            queue.GetPendingTextSnapshot().Should().BeEmpty();
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
            public new bool IsVisible { get; set; } = true;

            public override RectangleF GetClientRect() => clientRect;
        }
    }
}
