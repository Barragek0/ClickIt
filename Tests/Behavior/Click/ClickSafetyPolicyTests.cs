using ClickIt.Services.Click.Safety;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Behavior.Click
{
    [TestClass]
    public class ClickSafetyPolicyTests
    {
        [TestMethod]
        public void IsPointClickableInEitherSpace_ReturnsTrue_WhenClientSpaceMatches()
        {
            var policy = new ClickSafetyPolicy();

            bool result = policy.IsPointClickableInEitherSpace(
                clientPoint: new Vector2(100, 100),
                windowTopLeft: new Vector2(20, 20),
                clickabilityCheck: static (point, _) => point.X == 100 && point.Y == 100,
                path: "metadata/path");

            result.Should().BeTrue();
        }

        [TestMethod]
        public void IsPointClickableInEitherSpace_ReturnsTrue_WhenAbsoluteSpaceMatches()
        {
            var policy = new ClickSafetyPolicy();

            bool result = policy.IsPointClickableInEitherSpace(
                clientPoint: new Vector2(100, 100),
                windowTopLeft: new Vector2(20, 20),
                clickabilityCheck: static (point, _) => point.X == 120 && point.Y == 120,
                path: "metadata/path");

            result.Should().BeTrue();
        }

        [TestMethod]
        public void IsCursorInsideWindow_ReturnsFalse_WhenCursorOutsideBounds()
        {
            var policy = new ClickSafetyPolicy();
            var window = new RectangleF(10, 10, 100, 100);

            bool result = policy.IsCursorInsideWindow(window, new Vector2(500, 500));

            result.Should().BeFalse();
        }
    }
}
