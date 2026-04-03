namespace ClickIt.UI.Debug.Layout
{
    internal sealed class DebugLayoutEngine : IDebugLayoutEngine
    {
        public (int NextColumn, int NextX, int NextY) ResolveNextSectionPlacement(
            int currentColumn,
            int currentX,
            int currentY,
            DebugLayoutSettings layoutSettings)
        {
            if (layoutSettings.LineHeight <= 0 || layoutSettings.LinesPerColumn <= 0 || layoutSettings.MaxColumns <= 0)
                return (currentColumn, currentX, currentY);

            int usedLines = Math.Max(0, (currentY - layoutSettings.StartY) / layoutSettings.LineHeight);
            if (usedLines < layoutSettings.LinesPerColumn || currentColumn >= layoutSettings.MaxColumns - 1)
                return (currentColumn, currentX, currentY);

            int nextColumn = currentColumn + 1;
            int nextX = layoutSettings.BaseX + (nextColumn * layoutSettings.ColumnShiftPx);
            return (nextColumn, nextX, layoutSettings.StartY);
        }

        public int ResolveColumnFromX(int xPos, DebugLayoutSettings layoutSettings)
        {
            if (layoutSettings.ColumnShiftPx <= 0 || layoutSettings.MaxColumns <= 0)
                return 0;

            int raw = (xPos - layoutSettings.BaseX) / layoutSettings.ColumnShiftPx;
            return Math.Clamp(raw, 0, layoutSettings.MaxColumns - 1);
        }
    }
}
