namespace ClickIt.Utils
{
    public static class TextHelpers
    {
        public static string GetLine(string text, int lineNo)
        {
            if (text == null) return string.Empty;
            var lines = text.Replace("\r", string.Empty).Split('\n');
            if (lineNo >= 0 && lineNo < lines.Length) return lines[lineNo];
            return string.Empty;
        }

        public static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return text.Replace("\r", string.Empty).Split('\n').Length;
        }
    }
}
