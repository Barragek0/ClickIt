namespace ClickIt.Tests.Behavior.Click
{
    [TestClass]
    public class ClickSafetyPolicyTests
    {
        [DataTestMethod]
        [DataRow(10f, 10f, true)]
        [DataRow(110f, 110f, true)]
        [DataRow(60f, 60f, true)]
        [DataRow(9f, 10f, false)]
        [DataRow(10f, 111f, false)]
        [DataRow(500f, 500f, false)]
        public void IsCursorInsideWindow_ReturnsExpected_ForInclusiveBounds(float x, float y, bool expected)
        {
            var policy = new ClickSafetyPolicy();
            var window = new RectangleF(10, 10, 100, 100);

            bool result = policy.IsCursorInsideWindow(window, new Vector2(x, y));

            result.Should().Be(expected);
        }
    }
}
