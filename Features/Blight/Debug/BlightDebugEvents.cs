namespace ClickIt.Features.Blight.Debug;

// Blight's Recent Stages buffer — backed by the shared dedup buffer so it stays byte-for-byte
// compatible with pathfinding's Recent Events (same timestamp + (xN) dedup + 128 cap).
internal sealed class BlightDebugEvents
{
    private readonly DedupEventBuffer _buffer = new();

    internal IReadOnlyList<string> Stages => _buffer.Events;

    internal void Add(string message) => _buffer.Add(message);
}

