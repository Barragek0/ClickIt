using SharpDX;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.UI.Debug
{
    internal sealed class DebugOverlayRenderContext(
        BaseSettingsPlugin<ClickItSettings> plugin,
        AltarService? altarService,
        AreaService? areaService,
        WeightCalculator? weightCalculator,
        DeferredTextQueue deferredTextQueue,
        DeferredFrameQueue deferredFrameQueue,
        IDebugTelemetrySource debugTelemetrySource)
    {
        private const int DetailedDebugStartY = 120;
        private const int DetailedDebugLinesPerColumn = 34;
        private const int DetailedDebugBaseX = 10;
        private const int DetailedDebugColumnShiftPx = 600;
        private const int DetailedDebugMaxColumns = 4;

        public BaseSettingsPlugin<ClickItSettings> Plugin { get; } = plugin;
        public AltarService? AltarService { get; } = altarService;
        public AreaService? AreaService { get; } = areaService;
        public WeightCalculator? WeightCalculator { get; } = weightCalculator;
        public DeferredTextQueue DeferredTextQueue { get; } = deferredTextQueue;
        public DeferredFrameQueue DeferredFrameQueue { get; } = deferredFrameQueue;
        public IDebugTelemetrySource DebugTelemetrySource { get; } = debugTelemetrySource;

        public int RenderDebugTrailBlock(ref int xPos, int yPos, int lineHeight, IReadOnlyList<string> trail, int maxRows, int wrapWidth)
        {
            if (trail == null || trail.Count == 0 || lineHeight <= 0)
                return yPos;

            if (!EnsureDebugLineCapacity(ref xPos, ref yPos, lineHeight))
                return yPos;

            DeferredTextQueue.Enqueue("Recent Stages:", new Vector2(xPos, yPos), Color.LightBlue, 13);
            yPos += lineHeight;

            int rowsToRender = Math.Min(Math.Max(1, maxRows), trail.Count);
            int start = Math.Max(0, trail.Count - rowsToRender);
            for (int i = start; i < trail.Count; i++)
            {
                yPos = EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"  {trail[i]}", Color.LightGray, 12, wrapWidth);
            }

            return yPos;
        }

        public int EnqueueWrappedDebugLine(
            ref int xPos,
            int yPos,
            int lineHeight,
            string text,
            Color color,
            int fontSize,
            int maxCharsPerLine = 72)
        {
            if (lineHeight <= 0)
                return yPos;

            if (string.IsNullOrEmpty(text))
            {
                if (!EnsureDebugLineCapacity(ref xPos, ref yPos, lineHeight))
                    return yPos;

                DeferredTextQueue.Enqueue(string.Empty, new Vector2(xPos, yPos), color, fontSize);
                return yPos + lineHeight;
            }

            int safeWrap = Math.Max(20, maxCharsPerLine);
            foreach (string wrappedLine in WrapTextForDebug(text, safeWrap))
            {
                if (!EnsureDebugLineCapacity(ref xPos, ref yPos, lineHeight))
                    break;

                DeferredTextQueue.Enqueue(wrappedLine, new Vector2(xPos, yPos), color, fontSize);
                yPos += lineHeight;
            }

            return yPos;
        }

        public int RenderWrappedText(string text, Vector2 position, Color color, int fontSize, int lineHeight, int maxCharsPerLine)
        {
            if (string.IsNullOrEmpty(text))
                return (int)(position.Y + lineHeight);

            int currentY = (int)position.Y;
            int startIndex = 0;

            int leadingSpaces = 0;
            while (leadingSpaces < text.Length && text[leadingSpaces] == ' ')
            {
                leadingSpaces++;
            }

            string indentation = new(' ', leadingSpaces);
            ReadOnlySpan<char> content = text.AsSpan(leadingSpaces);
            int contentLength = content.Length;

            while (startIndex < contentLength)
            {
                int endIndex = Math.Min(startIndex + maxCharsPerLine, contentLength);
                if (endIndex < contentLength)
                {
                    ReadOnlySpan<char> segment = content.Slice(startIndex, endIndex - startIndex);
                    int lastSpaceOffset = segment.LastIndexOf(' ');
                    if (lastSpaceOffset > 0)
                    {
                        endIndex = startIndex + lastSpaceOffset;
                    }
                }

                ReadOnlySpan<char> lineSpan = content.Slice(startIndex, endIndex - startIndex).TrimEnd();
                string line = lineSpan.ToString();
                DeferredTextQueue.Enqueue(indentation + line, new Vector2(position.X, currentY), color, fontSize);
                currentY += lineHeight;
                startIndex = endIndex;
                if (startIndex < contentLength && content[startIndex] == ' ')
                {
                    startIndex++;
                }
            }

            return currentY;
        }

        public static bool IsCursorInsideWindow(RectangleF windowRect, int cursorX, int cursorY)
            => windowRect != RectangleF.Empty && IsPointInRect(cursorX, cursorY, windowRect);

        public static bool IsCursorOverLabelRect(RectangleF labelRect, RectangleF windowRect, int cursorX, int cursorY)
        {
            if (labelRect.Width <= 0 || labelRect.Height <= 0)
                return false;

            float left = labelRect.Left + windowRect.X;
            float right = labelRect.Right + windowRect.X;
            float top = labelRect.Top + windowRect.Y;
            float bottom = labelRect.Bottom + windowRect.Y;

            return cursorX >= left && cursorX <= right && cursorY >= top && cursorY <= bottom;
        }

        public static string ResolveHoveredItemMetadataPath(LabelOnGround label)
        {
            try
            {
                return EntityHelpers.ResolveWorldItemMetadataPath(
                    label.ItemOnGround,
                    missingItemFallback: "<missing item>",
                    missingItemEntityFallback: "<missing WorldItem.ItemEntity>",
                    missingMetadataFallback: "<missing metadata/path>");
            }
            catch (Exception ex)
            {
                return $"<error: {ex.GetType().Name}>";
            }
        }

        public static string TrimPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "<none>";

            return path.Length <= 80 ? path : path[^80..];
        }

        private static bool IsPointInRect(int x, int y, RectangleF rect)
            => x >= rect.Left && x <= rect.Right && y >= rect.Top && y <= rect.Bottom;

        private static bool EnsureDebugLineCapacity(ref int xPos, ref int yPos, int lineHeight)
        {
            if (lineHeight <= 0)
                return false;

            int usedLines = Math.Max(0, (yPos - DetailedDebugStartY) / lineHeight);
            if (usedLines < DetailedDebugLinesPerColumn)
                return true;

            int currentColumn = ResolveDebugColumnFromX(xPos, DetailedDebugBaseX, DetailedDebugColumnShiftPx, DetailedDebugMaxColumns);
            if (currentColumn >= DetailedDebugMaxColumns - 1)
                return false;

            int nextColumn = currentColumn + 1;
            xPos = DetailedDebugBaseX + (nextColumn * DetailedDebugColumnShiftPx);
            yPos = DetailedDebugStartY;
            return true;
        }

        private static int ResolveDebugColumnFromX(int xPos, int baseX, int columnShiftPx, int maxColumns)
        {
            if (columnShiftPx <= 0 || maxColumns <= 0)
                return 0;

            int raw = (xPos - baseX) / columnShiftPx;
            return Math.Clamp(raw, 0, maxColumns - 1);
        }

        private static List<string> WrapTextForDebug(string text, int maxCharsPerLine)
        {
            var lines = new List<string>(8);
            if (string.IsNullOrEmpty(text))
            {
                lines.Add(string.Empty);
                return lines;
            }

            int safeWrap = Math.Max(20, maxCharsPerLine);
            int leadingSpaces = 0;
            while (leadingSpaces < text.Length && text[leadingSpaces] == ' ')
            {
                leadingSpaces++;
            }

            string indentation = new(' ', leadingSpaces);
            string content = text.Substring(leadingSpaces);
            int contentLength = content.Length;
            int startIndex = 0;

            while (startIndex < contentLength)
            {
                int endIndex = Math.Min(startIndex + safeWrap, contentLength);
                if (endIndex < contentLength)
                {
                    string segment = content.Substring(startIndex, endIndex - startIndex);
                    int lastSpaceOffset = segment.LastIndexOf(' ');
                    if (lastSpaceOffset > 0)
                        endIndex = startIndex + lastSpaceOffset;
                }

                string line = content.Substring(startIndex, endIndex - startIndex).TrimEnd();
                lines.Add(indentation + line);

                startIndex = endIndex;
                if (startIndex < contentLength && content[startIndex] == ' ')
                    startIndex++;
            }

            return lines;
        }
    }
}