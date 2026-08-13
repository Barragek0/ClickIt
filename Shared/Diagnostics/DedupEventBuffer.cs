namespace ClickIt.Shared.Diagnostics;

// Deduplicating event buffer for debug "Recent Events"/"Recent Stages" lists. Repeated messages collapse into a single entry with an (xN) suffix (timestamp ignored when matching), so hot loops show as one accumulating entry instead of flooding the list.
internal sealed class DedupEventBuffer
{
    private readonly List<string> _events = new(16);
    private readonly int _capacity;

    internal DedupEventBuffer(int capacity = 128)
    {
        _capacity = capacity;
    }

    internal IReadOnlyList<string> Events
    {
        // Snapshot under the same lock the writer holds: the debug overlay reads this from the render thread while the blight refresh / executor writes from the loop thread, and a shared live List would race (collection-modified corruption). The copy is only taken when the overlay actually renders, so the hot write path is unaffected.
        get
        {
            lock (_events)
                return [.. _events];
        }
    }

    internal void Add(string message)
    {
        string ts = DateTime.Now.ToString("HH:mm:ss");
        string formatted = $"{ts} {message}";

        lock (_events)
        {
            // Global dedup: match raw message content, ignoring timestamps and prior (xN) counts.
            for (int i = 0; i < _events.Count; i++)
            {
                string existing = _events[i];
                if (MessageMatches(existing, message))
                {
                    int count = ExtractDedupCount(existing) + 1;
                    _events.RemoveAt(i);
                    _events.Add($"{formatted} (x{count})");
                    return;
                }
            }

            if (_events.Count >= _capacity)
                _events.RemoveAt(0);
            _events.Add(formatted);
        }
    }

    private static bool MessageMatches(string formatted, string message)
    {
        ReadOnlySpan<char> span = formatted.AsSpan(StripTimestamp(formatted));
        if (TryFindDedupSuffix(span, out int parenIdx))
            span = span[..parenIdx];
        return span.SequenceEqual(message);
    }

    private static int StripTimestamp(string formatted)
    {
        if (formatted.Length >= 9 && formatted[2] == ':' && formatted[5] == ':')
        {
            int spaceAfterTs = formatted.IndexOf(' ', 8);
            if (spaceAfterTs > 0)
                return spaceAfterTs + 1;
        }
        return 0;
    }

    private static bool TryFindDedupSuffix(ReadOnlySpan<char> formatted, out int parenIdx)
    {
        parenIdx = formatted.LastIndexOf(" (x", StringComparison.Ordinal);
        if (parenIdx <= 0)
            return false;
        int closeParen = formatted[(parenIdx + 1)..].IndexOf(')');
        return closeParen == formatted.Length - parenIdx - 2;
    }

    private static int ExtractDedupCount(string formatted)
    {
        ReadOnlySpan<char> span = formatted.AsSpan(StripTimestamp(formatted));
        if (TryFindDedupSuffix(span, out int parenIdx))
        {
            ReadOnlySpan<char> numPart = span[(parenIdx + 3)..^1];
            if (int.TryParse(numPart, out int count))
                return count;
        }
        return 1;
    }
}
