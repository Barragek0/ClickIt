namespace ClickIt.UI.Debug.Introspection
{
    internal static class RuntimeObjectIntrospectionStreamWriter
    {
        public static bool TryWriteTraversalEvents(StreamWriter writer, IReadOnlyList<RuntimeObjectTraversalEvent> events, int maxValueChars, out string? error)
        {
            for (int i = 0; i < events.Count; i++)
                if (!TryWriteLine(writer, RuntimeObjectIntrospectionEventFormatter.FormatTraversalEvent(events[i], maxValueChars), out error))
                    return false;


            error = null;
            return true;
        }

        public static bool TryWriteLine(StreamWriter writer, string line, out string? error)
        {
            try
            {
                writer.WriteLine(line);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed while writing dump file: {ex.Message}";
                return false;
            }
        }

        public static bool TryFlush(StreamWriter writer, out string? error)
        {
            try
            {
                writer.Flush();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed while flushing dump file: {ex.Message}";
                return false;
            }
        }
    }
}