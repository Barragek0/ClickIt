namespace ClickIt.Services.Observability
{
    internal interface IClickTelemetryPublisher
    {
        void PublishClickSnapshot(ClickDebugSnapshot snapshot);

        void PublishUltimatumSnapshot(UltimatumDebugSnapshot snapshot);

        void PublishRuntimeLog(string message);
    }
}