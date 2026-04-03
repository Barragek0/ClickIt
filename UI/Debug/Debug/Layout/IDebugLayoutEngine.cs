namespace ClickIt.UI.Debug.Layout
{
    internal readonly record struct DebugLayoutSettings(
        int StartY,
        int LineHeight,
        int LinesPerColumn,
        int MaxColumns,
        int BaseX,
        int ColumnShiftPx);

    internal interface IDebugLayoutEngine
    {
        (int NextColumn, int NextX, int NextY) ResolveNextSectionPlacement(int currentColumn, int currentX, int currentY, DebugLayoutSettings layoutSettings);
        int ResolveColumnFromX(int xPos, DebugLayoutSettings layoutSettings);
    }
}
