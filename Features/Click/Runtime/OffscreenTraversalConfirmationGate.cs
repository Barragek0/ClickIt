namespace ClickIt.Features.Click.Runtime
{
    internal sealed class OffscreenTraversalConfirmationGate(Func<long>? getTimestampMs = null)
    {
        private const int ConfirmationWindowMs = 120;

        private readonly Func<long> _getTimestampMs = getTimestampMs ?? (() => Environment.TickCount64);
        private long _pendingTargetAddress;
        private string _pendingTargetPath = string.Empty;
        private long _pendingTargetFirstSeenTimestampMs;

        internal bool ShouldDelay(Entity target, string? targetPath, out long remainingDelayMs)
        {
            (bool ShouldDelay, long NextAddress, string NextPath, long NextFirstSeenTimestampMs, long RemainingDelayMs) confirmation = OffscreenPathingMath.EvaluateOffscreenTraversalTargetConfirmation(
                target.Address,
                targetPath,
                _pendingTargetAddress,
                _pendingTargetPath,
                _pendingTargetFirstSeenTimestampMs,
                _getTimestampMs(),
                ConfirmationWindowMs);

            _pendingTargetAddress = confirmation.NextAddress;
            _pendingTargetPath = confirmation.NextPath;
            _pendingTargetFirstSeenTimestampMs = confirmation.NextFirstSeenTimestampMs;
            remainingDelayMs = confirmation.RemainingDelayMs;
            return confirmation.ShouldDelay;
        }

        internal void Reset()
        {
            _pendingTargetAddress = 0;
            _pendingTargetPath = string.Empty;
            _pendingTargetFirstSeenTimestampMs = 0;
        }
    }
}