namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightMenuInteractionsTests
{
    [TestMethod]
    public void EnlargeRectKeepingCenter_EnlargesByRatio_KeepingCenter()
    {
        RectangleF rect = new(100f, 200f, 200f, 100f); // center (200, 250)

        RectangleF enlarged = BlightMenuInteractions.EnlargeRectKeepingCenter(rect, 1.3f);

        enlarged.Width.Should().BeApproximately(260f, 0.001f);
        enlarged.Height.Should().BeApproximately(130f, 0.001f);
        (enlarged.X + (enlarged.Width / 2f)).Should().BeApproximately(200f, 0.001f, "the center must be preserved");
        (enlarged.Y + (enlarged.Height / 2f)).Should().BeApproximately(250f, 0.001f, "the center must be preserved");
    }

    [TestMethod]
    public void EnlargeRectKeepingCenter_DefaultRatio_IsThirtyPercent()
    {
        RectangleF rect = new(0f, 0f, 100f, 50f);

        RectangleF enlarged = BlightMenuInteractions.EnlargeRectKeepingCenter(rect, BlightMenuInteractions.MenuRegionEnlargeRatio);

        enlarged.Width.Should().BeApproximately(130f, 0.001f);
        enlarged.Height.Should().BeApproximately(65f, 0.001f);
        enlarged.X.Should().BeApproximately(-15f, 0.001f);
        enlarged.Y.Should().BeApproximately(-7.5f, 0.001f);
    }
}
