namespace ClickIt.Shared.Diagnostics;

// Parses the trailing " (xN)" dedup-count suffix shared by the debug trail and the event buffer. A suffix-less entry means count 1; callers default accordingly (DebugSnapshotStore -> 0, DedupEventBuffer -> 1).
internal static class DedupSuffix
{
    internal static bool TryGetCount(string formatted, out int count)
    {
        count = 0;
        int parenIdx = formatted.LastIndexOf(" (x", StringComparison.Ordinal);
        if (parenIdx <= 0)
            return false;
        int closeParen = formatted.IndexOf(')', parenIdx);
        if (closeParen != formatted.Length - 1)
            return false;
        return int.TryParse(formatted.AsSpan(parenIdx + 3, closeParen - parenIdx - 3), out count);
    }
}
