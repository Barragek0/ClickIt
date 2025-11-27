using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using ClickIt.Utils;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class InputHandlerPrivateHelpersTests
    {
        [TestMethod]
        public void IsPOEActiveForTests_ReflectsWindowForeground()
        {
            InputHandler.IsPOEActiveForTests(true).Should().BeTrue();
            InputHandler.IsPOEActiveForTests(false).Should().BeFalse();
        }

        [TestMethod]
        public void IsPanelOpenForTests_DetectsEitherPanelAddressNonZero()
        {
            // neither open
            InputHandler.IsPanelOpenForTests(0, 0).Should().BeFalse();
            // left open
            InputHandler.IsPanelOpenForTests(123L, 0).Should().BeTrue();
            // right open
            InputHandler.IsPanelOpenForTests(0, 456L).Should().BeTrue();
            // both open
            InputHandler.IsPanelOpenForTests(111L, 222L).Should().BeTrue();
        }

        [TestMethod]
        public void IsInTownOrHideoutForTests_ReturnsTrueIfEitherFlagSet()
        {
            InputHandler.IsInTownOrHideoutForTests(false, false).Should().BeFalse();
            InputHandler.IsInTownOrHideoutForTests(true, false).Should().BeTrue();
            InputHandler.IsInTownOrHideoutForTests(false, true).Should().BeTrue();
            InputHandler.IsInTownOrHideoutForTests(true, true).Should().BeTrue();
        }
    }
}
