using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClickIt.Utils;
using System.Windows.Forms;

namespace ClickIt.Tests.Unit
{
    using FluentAssertions;

    [TestClass]
    public class KeyboardTests
    {
        [TestMethod]
        public void KeyDown_Up_Press_DoNotThrow_WhenNativeDisabled()
        {
            // The test harness disables native input at startup so these calls should be no-ops
            Keyboard.KeyDown(Keys.A);
            Keyboard.KeyUp(Keys.A);
            Keyboard.KeyPress(Keys.A);
            Keyboard.KeyPress(Keys.B, 5);
            // If we reach here without error the test has succeeded
            true.Should().BeTrue();
        }

        [TestMethod]
        public void QueryKeyMethods_ReturnFalse_WhenNativeDisabled()
        {
            // When native input is disabled all query methods must return false
            Keyboard.IsKeyDown(Keys.A).Should().BeFalse();
            Keyboard.IsKeyPressed(Keys.A).Should().BeFalse();
            Keyboard.IsKeyToggled(Keys.CapsLock).Should().BeFalse();
        }
    }
}
