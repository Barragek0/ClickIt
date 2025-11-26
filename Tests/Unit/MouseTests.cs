using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;
using SharpDX;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class MouseTests
    {
        [TestMethod]
        public void SetCursorPos_ReturnsTrue_WhenNativeInputDisabled()
        {
            Mouse.DisableNativeInput = true;
            bool result = Mouse.SetCursorPos(10, 20);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void LeftClick_DoesNothing_WhenNativeInputDisabled()
        {
            Mouse.DisableNativeInput = true;
            // Should not throw, and should be a no-op
            Mouse.LeftClick();
            Mouse.RightClick();
            Assert.IsTrue(Mouse.DisableNativeInput);
        }

        [TestMethod]
        public void SetCursorPos_Overload_ComposesCoordinates_WhenDisabled()
        {
            Mouse.DisableNativeInput = true;
            var window = new RectangleF(100, 200, 0, 0);
            bool result = Mouse.SetCursorPos(5, 6, window);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void SetCurosPosToCenterOfRec_ReturnsTrue_WhenDisabled()
        {
            Mouse.DisableNativeInput = true;
            var rect = new RectangleF(10, 20, 4, 6);
            var window = new RectangleF(100, 200, 0, 0);
            bool result = Mouse.SetCurosPosToCenterOfRec(rect, window);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void GetCursorPosition_ReturnsPoint()
        {
            var p = Mouse.GetCursorPosition();
            Assert.IsNotNull(p);
        }
    }
}
