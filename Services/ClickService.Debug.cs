using SharpDX;
using ClickIt.Services.Click.Runtime;
using ClickIt.Services.Observability;
using ClickIt.Utils;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        public ClickDebugSnapshot GetLatestClickDebug()
        {
            return _clickTelemetryStore.GetLatestClickDebug();
        }

        public IReadOnlyList<string> GetLatestClickDebugTrail()
        {
            return _clickTelemetryStore.GetLatestClickDebugTrail();
        }

        public RuntimeDebugLogSnapshot GetLatestRuntimeDebugLog()
        {
            return _clickTelemetryStore.GetLatestRuntimeDebugLog();
        }

        public IReadOnlyList<string> GetLatestRuntimeDebugLogTrail()
        {
            return _clickTelemetryStore.GetLatestRuntimeDebugLogTrail();
        }

        public UltimatumDebugSnapshot GetLatestUltimatumDebug()
        {
            return _clickTelemetryStore.GetLatestUltimatumDebug();
        }

        public IReadOnlyList<string> GetLatestUltimatumDebugTrail()
        {
            return _clickTelemetryStore.GetLatestUltimatumDebugTrail();
        }

        private void SetLatestClickDebug(ClickDebugSnapshot snapshot)
        {
            if (!ShouldCaptureClickDebug())
                return;

            _clickTelemetryStore.PublishClickSnapshot(snapshot);
        }

        private bool ShouldCaptureClickDebug()
        {
            return settings.DebugMode.Value && settings.DebugShowClicking.Value;
        }

        private bool ShouldCaptureUltimatumDebug()
        {
            return settings.DebugMode.Value && settings.DebugShowUltimatum.Value;
        }

        private void SetLatestUltimatumDebug(UltimatumDebugSnapshot snapshot)
        {
            if (!ShouldCaptureUltimatumDebug())
                return;

            _clickTelemetryStore.PublishUltimatumSnapshot(snapshot);
        }

        private void PublishUltimatumDebug(UltimatumDebugEvent debugEvent)
        {
            if (!ShouldCaptureUltimatumDebug())
                return;

            _clickTelemetryStore.PublishUltimatumEvent(debugEvent);
        }

        private void SetLatestRuntimeDebugLog(string message)
        {
            _clickTelemetryStore.PublishRuntimeLog(message);
        }

        void IClickTelemetryPublisher.PublishClickSnapshot(ClickDebugSnapshot snapshot)
        {
            SetLatestClickDebug(snapshot);
        }

        void IClickTelemetryPublisher.PublishUltimatumSnapshot(UltimatumDebugSnapshot snapshot)
        {
            SetLatestUltimatumDebug(snapshot);
        }

        void IClickTelemetryPublisher.PublishRuntimeLog(string message)
        {
            SetLatestRuntimeDebugLog(message);
        }
    }
}
