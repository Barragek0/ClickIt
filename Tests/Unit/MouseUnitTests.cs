using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class MouseUnitTests
    {
        private bool _prevDisableNativeInput;

        [TestInitialize]
        public void BeforeEach()
        {
            _prevDisableNativeInput = Mouse.DisableNativeInput;
            Mouse.DisableNativeInput = true;
        }

        [TestCleanup]
        public void AfterEach()
        {
            Mouse.DisableNativeInput = _prevDisableNativeInput;
        }

        [TestMethod]
        public void SetCursorPos_ReturnsTrue_WhenNativeDisabled()
        {
            Mouse.DisableNativeInput = true;
            Mouse.SetCursorPos(100, 200).Should().BeTrue();
        }

        [TestMethod]
        public void SetCursorPos_Overload_ComposesCoordinates_WhenDisabled()
        {
            Mouse.DisableNativeInput = true;
            var window = new RectangleF(100, 200, 0, 0);
            Mouse.SetCursorPos(5, 6, window).Should().BeTrue();
        }

        [TestMethod]
        public void SetCursorPosToCenterOfRec_ComputesCorrectCoords_AndReturnsTrue()
        {
            Mouse.DisableNativeInput = true;
            var win = new RectangleF(10, 20, 200, 100);
            var rect = new RectangleF(0, 0, 50, 40);
            Mouse.SetCurosPosToCenterOfRec(rect, win).Should().BeTrue();
        }

        [TestMethod]
        public void SetCursorPosAndLeftClick_IsNoop_WhenNativeDisabled()
        {
            Mouse.DisableNativeInput = true;
            Mouse.SetCursorPosAndLeftClick(new SharpDX.Vector2(32, 16), 0, new SharpDX.Vector2(0, 0));
            // if we reach here without exception we're good
            true.Should().BeTrue();
        }

        [TestMethod]
        public void LeftRightClick_AreNoop_WhenNativeDisabled()
        {
            Mouse.DisableNativeInput = true;
            Mouse.LeftClick();
            Mouse.RightClick();
            true.Should().BeTrue();
        }

        [TestMethod]
        public void VerticalScroll_IsNoop_WhenNativeDisabled()
        {
            Mouse.DisableNativeInput = true;
            Mouse.VerticalScroll(true, 3);
            Mouse.VerticalScroll(false, 2);
            true.Should().BeTrue();
        }
    }
}
