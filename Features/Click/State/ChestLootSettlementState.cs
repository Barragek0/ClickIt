namespace ClickIt.Features.Click.State
{
    internal sealed class ChestLootSettlementState
    {
        internal bool IsWatcherActive;
        internal long InitialDelayUntilTimestampMs;
        internal long NextPollTimestampMs;
        internal long LastNewItemTimestampMs;
        internal int PollIntervalMs;
        internal int QuietWindowMs;
        internal readonly HashSet<long> KnownGroundItemAddresses = [];
        internal bool PendingOpenConfirmationActive;
        internal string? PendingOpenMechanicId;
        internal long PendingOpenItemAddress;
        internal long PendingOpenLabelAddress;
        internal bool SourceGridValid;
        internal Vector2 SourceGrid;
    }
}