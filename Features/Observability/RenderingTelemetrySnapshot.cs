namespace ClickIt.Features.Observability
{
    internal sealed record RenderingTelemetrySnapshot(
        bool ServiceAvailable,
        int PendingTextCount,
        int PendingFrameCount)
    {
        public static readonly RenderingTelemetrySnapshot Empty = new(
            ServiceAvailable: false,
            PendingTextCount: 0,
            PendingFrameCount: 0);
    }
}