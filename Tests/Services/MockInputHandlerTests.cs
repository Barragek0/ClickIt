using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

namespace ClickIt.Tests.Services
{
    [TestClass]
    public class MockInputHandlerTests
    {
        [TestMethod]
        public void CanPerformClickOn_ReturnsFalseForEmptyPath()
        {
            var handler = new MockInputHandler();
            var label = new MockLabel { Path = "" };
            handler.CanPerformClickOn(label).Should().BeFalse();

            label.Path = "Some/Path";
            handler.CanPerformClickOn(label).Should().BeTrue();
        }

        [TestMethod]
        public void CalculateClickPosition_IsWithinSmallOffset()
        {
            var handler = new MockInputHandler();
            var label = TestFactories.CreateMockLabel(200, 150, "DelveMineral");
            var offset = new MockVector2(10, 20);

            var pos = handler.CalculateClickPosition(label, offset);
            // deterministic seed in MockInputHandler uses random.Next(-2,3)
            pos.X.Should().BeInRange(label.Position.X + offset.X - 2, label.Position.X + offset.X + 2);
            pos.Y.Should().BeInRange(label.Position.Y + offset.Y - 2, label.Position.Y + offset.Y + 2);
        }

        [TestMethod]
        public void IsValidClickPosition_UsesScreenAreas()
        {
            var handler = new MockInputHandler();
            var window = new MockRectangle(0, 0, 1024, 768);
            // Position well inside the screen and not in top UI
            var inside = new MockVector2(512, 384);
            handler.IsValidClickPosition(inside, window).Should().BeTrue();

            // Position outside right edge
            var outside = new MockVector2(2000, 2000);
            handler.IsValidClickPosition(outside, window).Should().BeFalse();
        }
    }
}
