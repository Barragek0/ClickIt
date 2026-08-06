namespace ClickIt.Tests.UI;

[TestClass]
public class UiRegionRectangleOverlayTests
{
    [TestMethod]
    public void TryGetDrawRect_ConvertsStandardRectToMinMax()
    {
        bool ok = UiRegionRectangleOverlay.TryGetDrawRect(
            new RectangleF(100f, 200f, 400f, 400f), out NumVector2 min, out NumVector2 max);

        ok.Should().BeTrue();
        min.Should().Be(new NumVector2(100f, 200f), "left/top are the draw-list min");
        max.Should().Be(new NumVector2(500f, 600f), "right/bottom are left/top plus extents");
    }

    [TestMethod]
    public void TryGetDrawRect_ReturnsFalse_ForEmptyOrDegenerateRect()
    {
        UiRegionRectangleOverlay.TryGetDrawRect(RectangleF.Empty, out _, out _).Should().BeFalse();
        UiRegionRectangleOverlay.TryGetDrawRect(new RectangleF(100f, 100f, 0f, 50f), out _, out _).Should().BeFalse();
    }
}
