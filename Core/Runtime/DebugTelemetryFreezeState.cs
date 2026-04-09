namespace ClickIt.Core.Runtime
{
    internal sealed class DebugTelemetryFreezeState
    {
        private readonly Lock _sync = new();
        private DebugTelemetrySnapshot _frozenSnapshot = DebugTelemetrySnapshot.Empty;
        private long _freezeUntilTimestampMs;
        private string _freezeReason = string.Empty;

        internal bool TryGetFrozenSnapshot(long nowTimestampMs, out DebugTelemetrySnapshot snapshot)
        {
            lock (_sync)
            {
                long remainingMs = _freezeUntilTimestampMs - nowTimestampMs;
                if (remainingMs > 0)
                {
                    snapshot = _frozenSnapshot;
                    return true;
                }

                ClearUnsafe();
            }

            snapshot = DebugTelemetrySnapshot.Empty;
            return false;
        }

        internal void Freeze(DebugTelemetrySnapshot snapshot, string? reason, int holdDurationMs, long nowTimestampMs)
        {
            int durationMs = SystemMath.Max(0, holdDurationMs);
            if (durationMs <= 0)
                return;

            lock (_sync)
            {
                _frozenSnapshot = snapshot ?? DebugTelemetrySnapshot.Empty;
                _freezeUntilTimestampMs = nowTimestampMs + durationMs;
                _freezeReason = reason ?? string.Empty;
            }
        }

        internal bool TryGetFreezeState(long nowTimestampMs, out long remainingMs, out string reason)
        {
            lock (_sync)
            {
                remainingMs = _freezeUntilTimestampMs - nowTimestampMs;
                if (remainingMs > 0)
                {
                    reason = _freezeReason;
                    return true;
                }

                ClearUnsafe();
            }

            remainingMs = 0;
            reason = string.Empty;
            return false;
        }

        internal void Clear()
        {
            lock (_sync)
            {
                ClearUnsafe();
            }
        }

        private void ClearUnsafe()
        {
            _frozenSnapshot = DebugTelemetrySnapshot.Empty;
            _freezeUntilTimestampMs = 0;
            _freezeReason = string.Empty;
        }
    }
}