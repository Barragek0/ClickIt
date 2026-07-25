namespace ClickIt.Features.Observability
{
    internal interface IClickTelemetryPublisher
    {
        void PublishClickSnapshot(ClickDebugSnapshot snapshot);

        void PublishUltimatumSnapshot(UltimatumDebugSnapshot snapshot);

        void PublishRuntimeLog(string message);
    }
}