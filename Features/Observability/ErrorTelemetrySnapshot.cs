namespace ClickIt.Features.Observability
{
    internal sealed record ErrorTelemetrySnapshot(
        bool ServiceAvailable,
        IReadOnlyList<string> RecentErrors)
    {
        private static readonly IReadOnlyList<string> EmptyTrail = [];

        public static readonly ErrorTelemetrySnapshot Empty = new(
            ServiceAvailable: false,
            RecentErrors: EmptyTrail);
    }
}