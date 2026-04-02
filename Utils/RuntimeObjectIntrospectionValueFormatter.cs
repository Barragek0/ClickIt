namespace ClickIt.Utils
{
    internal static class RuntimeObjectIntrospectionValueFormatter
    {
        public static string FormatValue(object? value, int maxLen = 120)
        {
            if (value == null)
                return "null";

            string text = value switch
            {
                string s => s,
                _ => value.ToString() ?? string.Empty
            };

            if (maxLen > 0 && text.Length > maxLen)
                text = text[..maxLen] + "...";

            return text;
        }
    }
}