using ExileCore.Shared.Enums;

namespace ClickIt.Utils.Input
{
    internal sealed class ToggleItemsController(ClickItSettings settings, Action<Keys, int> keyPress)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly Action<Keys, int> _keyPress = keyPress;
        private long _lastToggleItemsTimestampMs;

        internal bool TriggerToggleItems()
        {
            if (!_settings.ToggleItems.Value)
            {
                return false;
            }

            int intervalMs = Math.Max(100, _settings.ToggleItemsIntervalMs.Value);
            long now = Environment.TickCount64;

            if (_lastToggleItemsTimestampMs > 0)
            {
                long elapsed = now - _lastToggleItemsTimestampMs;
                if (elapsed >= 0 && elapsed < intervalMs)
                {
                    return false;
                }
            }

            _keyPress(_settings.ToggleItemsHotkey, 20);
            _keyPress(_settings.ToggleItemsHotkey, 20);
            _lastToggleItemsTimestampMs = now;
            return true;
        }

        internal int GetToggleItemsPostClickBlockMs()
            => Math.Max(0, _settings.ToggleItemsPostToggleClickBlockMs.Value);

        internal bool IsInPostClickBlockWindow()
        {
            int blockMs = GetToggleItemsPostClickBlockMs();
            if (blockMs <= 0 || _lastToggleItemsTimestampMs <= 0)
            {
                return false;
            }

            long elapsed = Environment.TickCount64 - _lastToggleItemsTimestampMs;
            return elapsed >= 0 && elapsed < blockMs;
        }

        internal void SetLastToggleItemsTimestamp(long timestampMs)
            => _lastToggleItemsTimestampMs = timestampMs;
    }
}