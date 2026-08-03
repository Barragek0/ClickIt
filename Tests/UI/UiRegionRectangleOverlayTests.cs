namespace ClickIt.Tests.UI;

[TestClass]
public class UiRegionRectangleOverlayTests
{
    [TestMethod]
    public void TryGetDrawRect_ConvertsLtrbPackedRectToMinMax()
    {
        bool ok = UiRegionRectangleOverlay.TryGetDrawRect(
            new RectangleF(100f, 200f, 500f, 600f), out NumVector2 min, out NumVector2 max);

        ok.Should().BeTrue();
        min.Should().Be(new NumVector2(100f, 200f), "left/top are the draw-list min");
        max.Should().Be(new NumVector2(500f, 600f), "right/bottom are the draw-list max");
    }

    [TestMethod]
    public void TryGetDrawRect_ReturnsFalse_ForEmptyOrDegenerateRect()
    {
        UiRegionRectangleOverlay.TryGetDrawRect(RectangleF.Empty, out _, out _).Should().BeFalse();
        UiRegionRectangleOverlay.TryGetDrawRect(new RectangleF(100f, 100f, 100f, 100f), out _, out _).Should().BeFalse();
    }
}
