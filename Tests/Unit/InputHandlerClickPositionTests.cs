using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;
using System.Collections.Generic;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InputHandlerClickPositionTests
    {
        [TestMethod]
        public void ResolveVisibleClickPoint_WhenPreferredPointIsUnblocked_ReturnsPreferredPoint()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);
            var blocked = new List<RectangleF> { new RectangleF(0, 0, 10, 10) };

            Vector2 result = InputHandler.ResolveVisibleClickPoint(target, preferred, blocked);

            result.Should().Be(preferred);
        }

        [TestMethod]
        public void ResolveVisibleClickPoint_WhenCenterBlocked_ReturnsPointInsideTargetOutsideBlockedArea()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);
            var blocked = new List<RectangleF> { new RectangleF(40, 10, 20, 20) };

            Vector2 result = InputHandler.ResolveVisibleClickPoint(target, preferred, blocked);

            bool insideTarget = result.X >= target.Left && result.X <= target.Right && result.Y >= target.Top && result.Y <= target.Bottom;
            bool insideBlocked = result.X >= blocked[0].Left && result.X <= blocked[0].Right && result.Y >= blocked[0].Top && result.Y <= blocked[0].Bottom;

            insideTarget.Should().BeTrue();
            insideBlocked.Should().BeFalse();
        }

        [TestMethod]
        public void TryResolveVisibleClickPoint_WhenEntireTargetBlocked_ReturnsFalse()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);
            var blocked = new List<RectangleF> { new RectangleF(0, 0, 100, 40) };

            bool hasVisiblePoint = InputHandler.TryResolveVisibleClickPoint(target, preferred, blocked, out Vector2 resolved);

            hasVisiblePoint.Should().BeFalse();
            resolved.Should().Be(preferred);
        }

        [TestMethod]
        public void TryResolveVisibleClickablePoint_UsesClickableSubregion_WhenCenterIsNotClickable()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);
            var blocked = new List<RectangleF>();

            static bool IsClickable(Vector2 p) => p.X < 40 || p.X > 60;

            bool ok = InputHandler.TryResolveVisibleClickablePoint(target, preferred, blocked, IsClickable, out Vector2 resolved);

            ok.Should().BeTrue();
            resolved.X.Should().NotBeInRange(40f, 60f);
            resolved.Y.Should().BeGreaterThanOrEqualTo(target.Top);
            resolved.Y.Should().BeLessThanOrEqualTo(target.Bottom);
        }

        [TestMethod]
        public void TryResolveVisibleClickablePoint_ReturnsFalse_WhenNoClickablePointExists()
        {
            var target = new RectangleF(0, 0, 100, 40);
            var preferred = new Vector2(50, 20);
            var blocked = new List<RectangleF>();

            static bool IsClickable(Vector2 _) => false;

            bool ok = InputHandler.TryResolveVisibleClickablePoint(target, preferred, blocked, IsClickable, out Vector2 resolved);

            ok.Should().BeFalse();
            resolved.Should().Be(preferred);
        }

        [TestMethod]
        public void IsSafeAutomationPoint_ReturnsTrue_WhenInsideWindowAndVirtualScreen()
        {
            var point = new Vector2(200, 200);
            var gameWindow = new RectangleF(100, 100, 500, 400);
            var virtualScreen = new RectangleF(0, 0, 1920, 1080);

            InputHandler.IsSafeAutomationPoint(point, gameWindow, virtualScreen).Should().BeTrue();
        }

        [TestMethod]
        public void IsSafeAutomationPoint_ReturnsFalse_WhenOutsideGameWindow()
        {
            var point = new Vector2(50, 50);
            var gameWindow = new RectangleF(100, 100, 500, 400);
            var virtualScreen = new RectangleF(0, 0, 1920, 1080);

            InputHandler.IsSafeAutomationPoint(point, gameWindow, virtualScreen).Should().BeFalse();
        }

        [TestMethod]
        public void IsSafeAutomationPoint_ReturnsFalse_WhenOutsideVirtualScreen()
        {
            var point = new Vector2(-5000, -5000);
            var gameWindow = new RectangleF(0, 0, 1920, 1080);
            var virtualScreen = new RectangleF(0, 0, 1920, 1080);

            InputHandler.IsSafeAutomationPoint(point, gameWindow, virtualScreen).Should().BeFalse();
        }
    }
}
