using ClickIt.Services.Click.Runtime;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;

namespace ClickIt.Tests.Services.Click
{
    [TestClass]
    public class ClickableProbeResolverTests
    {
        [TestMethod]
        public void TryResolveNearbyClickablePoint_ReturnsCenter_WhenCenterIsClickable()
        {
            bool ok = ClickableProbeResolver.TryResolveNearbyClickablePoint(
                new Vector2(100, 100),
                "test-path",
                _ => true,
                (point, _) => point == new Vector2(100, 100),
                out Vector2 clickPos);

            ok.Should().BeTrue();
            clickPos.Should().Be(new Vector2(100, 100));
        }

        [TestMethod]
        public void TryResolveNearbyClickablePoint_RespectsWindowAndReturnsFalse_WhenNoProbeIsClickable()
        {
            bool ok = ClickableProbeResolver.TryResolveNearbyClickablePoint(
                new Vector2(100, 100),
                "test-path",
                point => point.X >= 100,
                (_, _) => false,
                out Vector2 clickPos);

            ok.Should().BeFalse();
            clickPos.Should().Be(default(Vector2));
        }
    }
}