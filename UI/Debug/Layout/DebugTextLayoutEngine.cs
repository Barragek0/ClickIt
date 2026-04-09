namespace ClickIt.UI.Debug.Layout
{
    internal static class DebugTextLayoutEngine
    {
        internal static List<string> WrapOverlayText(string? text, int maxLength)
        {
            List<string> lines = [];
            if (string.IsNullOrWhiteSpace(text))
                return lines;

            string normalized = text.Replace("\r\n", "\n");
            string[] baseLines = normalized.Split('\n');
            for (int i = 0; i < baseLines.Length; i++)
            {
                string segment = baseLines[i].Trim();
                if (segment.Length == 0)
                    continue;

                int start = 0;
                while (start < segment.Length)
                {
                    int remaining = segment.Length - start;
                    if (remaining <= maxLength)
                    {
                        lines.Add(segment[start..]);
                        break;
                    }

                    int wrapAt = FindWrapIndex(segment, start, maxLength);
                    int length = SystemMath.Max(1, wrapAt - start);
                    string chunk = segment.Substring(start, length).TrimEnd();
                    if (chunk.Length > 0)
                        lines.Add(chunk);

                    start = wrapAt;
                    while (start < segment.Length && segment[start] == ' ')
                        start++;
                }
            }

            return lines;
        }

        internal static List<string> WrapDebugText(string text, int maxCharsPerLine)
        {
            List<string> lines = new(8);
            if (string.IsNullOrEmpty(text))
            {
                lines.Add(string.Empty);
                return lines;
            }

            int safeWrap = SystemMath.Max(20, maxCharsPerLine);
            int leadingSpaces = 0;
            while (leadingSpaces < text.Length && text[leadingSpaces] == ' ')
                leadingSpaces++;

            string indentation = new(' ', leadingSpaces);
            string content = text[leadingSpaces..];
            int contentLength = content.Length;
            int startIndex = 0;

            while (startIndex < contentLength)
            {
                int endIndex = SystemMath.Min(startIndex + safeWrap, contentLength);
                if (endIndex < contentLength)
                {
                    string segment = content[startIndex..endIndex];
                    int lastSpaceOffset = segment.LastIndexOf(' ');
                    if (lastSpaceOffset > 0)
                        endIndex = startIndex + lastSpaceOffset;
                }

                string line = content[startIndex..endIndex].TrimEnd();
                lines.Add(indentation + line);

                startIndex = endIndex;
                if (startIndex < contentLength && content[startIndex] == ' ')
                    startIndex++;
            }

            return lines;
        }

        private static int FindWrapIndex(string value, int start, int maxLength)
        {
            int hardStop = SystemMath.Min(start + maxLength, value.Length);
            if (hardStop >= value.Length)
                return value.Length;

            int wordBoundary = value.LastIndexOf(' ', hardStop - 1, hardStop - start);
            return wordBoundary > start ? wordBoundary : hardStop;
        }
    }
}
