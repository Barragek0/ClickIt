namespace ClickIt.Tests.Features.Area
{
    [TestClass]
    public class BlockedAreaHitTestEngineTests
    {
        [TestMethod]
        public void PointInBlockedUiRectangle_UsesWindowOffset_WhenWindowHasTopLeftOffset()
        {
            RectangleF fullScreen = new(100f, 200f, 1280f, 720f);
            RectangleF clientRect = new(10f, 20f, 50f, 50f);
            Vector2 absolutePoint = new(115f, 225f);

            bool blocked = BlockedAreaHitTestEngine.PointInBlockedUiRectangle(absolutePoint, clientRect, fullScreen);

            blocked.Should().BeTrue();
        }

        [TestMethod]
        public void PointInBlockedUiRectangle_DoesNotApplyOffset_WhenWindowTopLeftIsZero()
        {
            RectangleF fullScreen = new(0f, 0f, 1280f, 720f);
            RectangleF clientRect = new(10f, 20f, 50f, 50f);
            Vector2 absolutePoint = new(115f, 225f);

            bool blocked = BlockedAreaHitTestEngine.PointInBlockedUiRectangle(absolutePoint, clientRect, fullScreen);

            blocked.Should().BeFalse();
        }

        [TestMethod]
        public void PointInAnyBlockedUiRectangle_ReturnsTrue_WhenAnyRectangleMatches()
        {
            RectangleF fullScreen = new(0f, 0f, 1280f, 720f);
            List<RectangleF> blockedRects =
            [
                new RectangleF(10f, 10f, 20f, 20f),
                new RectangleF(50f, 50f, 30f, 30f)
            ];
            Vector2 point = new(60f, 60f);

            bool blocked = BlockedAreaHitTestEngine.PointInAnyBlockedUiRectangle(point, blockedRects, fullScreen);

            blocked.Should().BeTrue();
        }
    }
}