namespace ClickIt.Services.Observability
{
    internal interface IDebugTelemetrySource
    {
        DebugTelemetrySnapshot GetSnapshot();

        bool TryGetFreezeState(out long remainingMs, out string reason);
    }
}