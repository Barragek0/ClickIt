namespace ClickIt.Features.Observability
{
    public sealed record RuntimeDebugLogSnapshot(
        bool HasData,
        string Message,
        long Sequence,
        long TimestampMs)
    {
        public static readonly RuntimeDebugLogSnapshot Empty = new(
            HasData: false,
            Message: string.Empty,
            Sequence: 0,
            TimestampMs: 0);
    }
}