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
                if (ExtractMessage(existing) == message)
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

    private static bool TryFindDedupSuffix(string formatted, out int parenIdx)
    {
        parenIdx = formatted.LastIndexOf(" (x", StringComparison.Ordinal);
        if (parenIdx <= 0)
            return false;
        int closeParen = formatted.IndexOf(')', parenIdx);
        return closeParen == formatted.Length - 1;
    }

    private static string ExtractMessage(string formatted)
    {
        if (formatted.Length >= 9 && formatted[2] == ':' && formatted[5] == ':')
        {
            int spaceAfterTs = formatted.IndexOf(' ', 8);
            if (spaceAfterTs > 0)
                formatted = formatted[(spaceAfterTs + 1)..];
        }

        return TryFindDedupSuffix(formatted, out int parenIdx)
            ? formatted[..parenIdx]
            : formatted;
    }

    private static int ExtractDedupCount(string formatted)
    {
        if (TryFindDedupSuffix(formatted, out int parenIdx))
        {
            string numPart = formatted[(parenIdx + 3)..^1];
            if (int.TryParse(numPart, out int count))
                return count;
        }
        return 1;
    }
}
