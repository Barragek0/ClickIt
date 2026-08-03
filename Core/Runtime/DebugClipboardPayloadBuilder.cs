namespace ClickIt.Core.Runtime
{
    internal static class DebugClipboardPayloadBuilder
    {
        internal static string BuildDebugClipboardPayload(string[] lines)
        {
            StringBuilder sb = new(lines.Length * 32);
            sb.AppendLine("=== ClickIt Additional Debug Information ===");
            sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            for (int i = 0; i < lines.Length; i++)
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    sb.AppendLine(lines[i]);


            return sb.ToString().TrimEnd();
        }
    }
}