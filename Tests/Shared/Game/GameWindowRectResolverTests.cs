namespace ClickIt.Tests.Shared.Game
{
    [TestClass]
    public class GameWindowRectResolverTests
    {
        private const long WS_CAPTION = 0x00C00000L;
        private const long WS_THICKFRAME = 0x00040000L;

        [DataTestMethod]
        [DataRow(0L, false)]
        [DataRow(WS_CAPTION, true)]
        [DataRow(WS_THICKFRAME, true)]
        [DataRow(WS_CAPTION | WS_THICKFRAME, true)]
        [DataRow(0x80000000L, false)]
        public void IsLikelyWindowed_ReturnsExpectedValue(long style, bool expected)
        {
            bool actual = GameWindowRectResolver.IsLikelyWindowed(style);

            actual.Should().Be(expected);
        }

        [TestMethod]
        public void NormalizeWindowRectangle_ConvertsLtrbStyle_WhenRightBottomStoredInWidthHeight()
        {
            RectangleF raw = new(600, 300, 2200, 1200);
            RectangleF virtualBounds = new(0, 0, 2560, 1440);

            RectangleF normalized = GameWindowRectResolver.NormalizeWindowRectangle(raw, virtualBounds);

            normalized.Should().Be(new RectangleF(600, 300, 1600, 900));
        }

        [TestMethod]
        public void NormalizeWindowRectangle_LeavesWidthHeightStyleUnchanged()
        {
            RectangleF raw = new(600, 300, 1600, 900);
            RectangleF virtualBounds = new(0, 0, 2560, 1440);

            RectangleF normalized = GameWindowRectResolver.NormalizeWindowRectangle(raw, virtualBounds);

            normalized.Should().Be(raw);
        }
    }
}
