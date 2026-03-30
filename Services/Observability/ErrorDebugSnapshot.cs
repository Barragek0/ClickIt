namespace ClickIt.Services.Observability
{
    internal sealed record ErrorDebugSnapshot(
        bool HasData,
        string Message,
        long Sequence,
        long TimestampMs)
    {
        public static readonly ErrorDebugSnapshot Empty = new(
            HasData: false,
            Message: string.Empty,
            Sequence: 0,
            TimestampMs: 0);
    }
}