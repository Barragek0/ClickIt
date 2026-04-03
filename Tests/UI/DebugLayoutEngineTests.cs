using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.UI
{
    [TestClass]
    public class DebugLayoutEngineTests
    {
        [TestMethod]
        public void ResolveNextSectionPlacement_MovesToNextColumn_WhenCurrentColumnIsFull()
        {
            var engine = new DebugLayoutEngine();
            var settings = new DebugLayoutSettings(
                StartY: 120,
                LineHeight: 18,
                LinesPerColumn: 2,
                MaxColumns: 4,
                BaseX: 10,
                ColumnShiftPx: 600);

            (int nextColumn, int nextX, int nextY) = engine.ResolveNextSectionPlacement(
                currentColumn: 0,
                currentX: 10,
                currentY: 120 + (2 * 18),
                settings);

            nextColumn.Should().Be(1);
            nextX.Should().Be(610);
            nextY.Should().Be(120);
        }

        [TestMethod]
        public void ResolveColumnFromX_ClampsToMaxColumn()
        {
            var engine = new DebugLayoutEngine();
            var settings = new DebugLayoutSettings(
                StartY: 120,
                LineHeight: 18,
                LinesPerColumn: 34,
                MaxColumns: 4,
                BaseX: 10,
                ColumnShiftPx: 600);

            int column = engine.ResolveColumnFromX(xPos: 5000, settings);

            column.Should().Be(3);
        }
    }
}
