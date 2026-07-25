namespace ClickIt.UI.Debug.Layout
{
    internal sealed class DebugTextBlockRenderer(
        DeferredTextQueue deferredTextQueue,
        IDebugLayoutEngine layoutEngine,
        DebugLayoutSettings layoutSettings)
    {
        private readonly DeferredTextQueue _deferredTextQueue = deferredTextQueue;
        private readonly IDebugLayoutEngine _layoutEngine = layoutEngine;
        private readonly DebugLayoutSettings _layoutSettings = layoutSettings;

        internal int RenderTrailBlock(ref int xPos, int yPos, int lineHeight, IReadOnlyList<string> trail, int maxRows, int wrapWidth)
        {
            if (trail == null || trail.Count == 0 || lineHeight <= 0)
                return yPos;

            if (!EnsureLineCapacity(ref xPos, ref yPos, lineHeight))
                return yPos;

            _deferredTextQueue.Enqueue("Recent Stages:", new Vector2(xPos, yPos), Color.LightBlue, 13);
            yPos += lineHeight;

            int rowsToRender = SystemMath.Min(SystemMath.Max(1, maxRows), trail.Count);
            int start = SystemMath.Max(0, trail.Count - rowsToRender);
            for (int i = start; i < trail.Count; i++)
                yPos = EnqueueWrappedLine(ref xPos, yPos, lineHeight, $"  {trail[i]}", Color.LightGray, 12, wrapWidth);


            return yPos;
        }

        internal int EnqueueWrappedLine(ref int xPos, int yPos, int lineHeight, string text, Color color, int fontSize, int maxCharsPerLine = 72)
        {
            if (lineHeight <= 0)
                return yPos;

            if (string.IsNullOrEmpty(text))
            {
                if (!EnsureLineCapacity(ref xPos, ref yPos, lineHeight))
                    return yPos;

                _deferredTextQueue.Enqueue(string.Empty, new Vector2(xPos, yPos), color, fontSize);
                return yPos + lineHeight;
            }

            int safeWrap = SystemMath.Max(20, maxCharsPerLine);
            foreach (string wrappedLine in DebugTextLayoutEngine.WrapDebugText(text, safeWrap))
            {
                if (!EnsureLineCapacity(ref xPos, ref yPos, lineHeight))
                    break;

                _deferredTextQueue.Enqueue(wrappedLine, new Vector2(xPos, yPos), color, fontSize);
                yPos += lineHeight;
            }

            return yPos;
        }

        internal int RenderWrappedText(string text, Vector2 position, Color color, int fontSize, int lineHeight, int maxCharsPerLine)
        {
            if (string.IsNullOrEmpty(text))
                return (int)(position.Y + lineHeight);

            int currentY = (int)position.Y;
            List<string> lines = DebugTextLayoutEngine.WrapDebugText(text, maxCharsPerLine);
            for (int i = 0; i < lines.Count; i++)
            {
                _deferredTextQueue.Enqueue(lines[i], new Vector2(position.X, currentY), color, fontSize);
                currentY += lineHeight;
            }

            return currentY;
        }

        internal bool EnsureLineCapacity(ref int xPos, ref int yPos, int lineHeight)
            => EnsureLineCapacityCore(ref xPos, ref yPos, lineHeight);

        private bool EnsureLineCapacityCore(ref int xPos, ref int yPos, int lineHeight)
        {
            if (lineHeight <= 0)
                return false;

            int usedLines = SystemMath.Max(0, (yPos - _layoutSettings.StartY) / lineHeight);
            if (usedLines < _layoutSettings.LinesPerColumn)
                return true;

            int currentColumn = _layoutEngine.ResolveColumnFromX(xPos, _layoutSettings);
            if (currentColumn >= _layoutSettings.MaxColumns - 1)
                return false;

            int nextColumn = currentColumn + 1;
            xPos = _layoutSettings.BaseX + (nextColumn * _layoutSettings.ColumnShiftPx);
            yPos = _layoutSettings.StartY;
            return true;
        }
    }
}