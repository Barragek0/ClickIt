using ExileCore;

namespace ClickIt.Features.Observability
{
    internal sealed class PluginDebugTelemetrySource(BaseSettingsPlugin<ClickItSettings> plugin) : IDebugTelemetrySource
    {
        private readonly BaseSettingsPlugin<ClickItSettings> _plugin = plugin;

        public DebugTelemetrySnapshot GetSnapshot()
        {
            if (_plugin is ClickIt clickIt)
                return clickIt.State.GetDebugTelemetrySnapshot();

            return DebugTelemetrySnapshot.Empty;
        }

        public bool TryGetFreezeState(out long remainingMs, out string reason)
        {
            if (_plugin is ClickIt clickIt)
                return clickIt.State.TryGetDebugTelemetryFreezeState(out remainingMs, out reason);

            remainingMs = 0;
            reason = string.Empty;
            return false;
        }
    }
}