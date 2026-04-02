using ClickIt.Services.Click.Runtime;
using ExileCore.PoEMemory;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Tests.Services.Click
{
    [TestClass]
    public class UltimatumElementClickExecutorTests
    {
        [TestMethod]
        public void TryClickElement_ReturnsFalse_WhenCursorIsOutsideWindow()
        {
            bool clicked = false;
            bool recorded = false;

            bool result = UltimatumElementClickExecutor.TryClickElement(
                new RectangleF(10, 10, 20, 20),
                null!,
                new Vector2(100, 200),
                "outside-window",
                "reject",
                "click",
                _ => false,
                (_, _) => true,
                _ => { },
                (_, _) => clicked = true,
                () => recorded = true);

            result.Should().BeFalse();
            clicked.Should().BeFalse();
            recorded.Should().BeFalse();
        }

        [TestMethod]
        public void TryClickElement_ReturnsFalse_WhenCenterIsNotClickable()
        {
            string? debugLine = null;

            bool result = UltimatumElementClickExecutor.TryClickElement(
                new RectangleF(10, 10, 20, 20),
                null!,
                new Vector2(0, 0),
                "outside-window",
                "[reject]",
                "[click]",
                _ => true,
                (_, _) => false,
                msg => debugLine = msg,
                (_, _) => { },
                () => { });

            result.Should().BeFalse();
            debugLine.Should().NotBeNull();
            debugLine!.Should().Contain("[reject]");
            debugLine.Should().Contain("center=");
        }

        [TestMethod]
        public void TryClickElement_ClicksCenterOffsetAndRecordsInterval_WhenAllowed()
        {
            Vector2 clickedPos = default;
            bool recorded = false;

            bool result = UltimatumElementClickExecutor.TryClickElement(
                new RectangleF(10, 20, 30, 40),
                null!,
                new Vector2(100, 200),
                "outside-window",
                "[reject]",
                "[click]",
                _ => true,
                (_, _) => true,
                _ => { },
                (pos, _) => clickedPos = pos,
                () => recorded = true);

            result.Should().BeTrue();
            clickedPos.Should().Be(new Vector2(125, 240));
            recorded.Should().BeTrue();
        }
    }
}