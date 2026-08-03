namespace ClickIt.Features.Blight.Debug;

internal sealed class BlightDebugEvents
{
    private readonly List<string> _stages = new(16);

    internal IReadOnlyList<string> Stages => _stages;

    internal void Add(string message)
    {
        string ts = DateTime.Now.ToString("HH:mm:ss");
        string formatted = $"{ts} {message}";

        lock (_stages)
        {
            // Global dedup: match raw message content, ignoring timestamps and prior (xN) counts.
            for (int i = 0; i < _stages.Count; i++)
            {
                string existing = _stages[i];
                if (MessageMatches(existing, message))
                {
                    int count = ExtractDedupCount(existing) + 1;
                    _stages.RemoveAt(i);
                    _stages.Add($"{formatted} (x{count})");
                    return;
                }
            }

            if (_stages.Count >= 128)
                _stages.RemoveAt(0);
            _stages.Add(formatted);
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
