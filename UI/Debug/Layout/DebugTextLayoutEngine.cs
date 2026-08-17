namespace ClickIt.UI.Debug.Layout
{
    internal static class DebugTextLayoutEngine
    {
        internal static List<string> WrapOverlayText(string? text, int maxLength)
        {
            List<string> lines = [];
            if (string.IsNullOrWhiteSpace(text))
                return lines;

            int safeWrap = SystemMath.Max(1, maxLength);
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
                    if (remaining <= safeWrap)
                    {
                        lines.Add(segment[start..]);
                        break;
                    }

                    int wrapAt = FindWrapIndex(segment, start, safeWrap);
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
