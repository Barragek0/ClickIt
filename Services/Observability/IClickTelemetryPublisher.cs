namespace ClickIt.Services.Observability
{
    internal interface IClickTelemetryPublisher
    {
        void PublishClickSnapshot(ClickService.ClickDebugSnapshot snapshot);

        void PublishUltimatumSnapshot(ClickService.UltimatumDebugSnapshot snapshot);

        void PublishRuntimeLog(string message);
    }
}