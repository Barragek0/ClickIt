namespace ClickIt.UI.Settings
{
    internal sealed class ClickItSettingsUiState
    {
        internal string ExpandedMechanicPriorityRowId = string.Empty;
        internal string ExpandedMechanicsTableRowId = string.Empty;
        internal string ExpandedItemTypeRowKey = string.Empty;
        internal string ExpandedUltimatumTakeRewardRowKey = string.Empty;
        internal string LastSettingsUiError = string.Empty;
        internal string UpsideSearchFilter = string.Empty;
        internal string DownsideSearchFilter = string.Empty;
        internal string ItemTypeSearchFilter = string.Empty;
        internal string EssenceSearchFilter = string.Empty;
        internal string StrongboxSearchFilter = string.Empty;
        internal string MechanicsSearchFilter = string.Empty;
        internal string UltimatumSearchFilter = string.Empty;
        internal string UltimatumTakeRewardSearchFilter = string.Empty;
        internal bool MechanicsAltarWeightTablesExpanded;
        private long _alertSoundTestEndsAtTimestamp;

        internal bool IsAlertSoundTestPlaying
            => Stopwatch.GetTimestamp() < Interlocked.Read(ref _alertSoundTestEndsAtTimestamp);

        internal bool TryBeginAlertSoundTest()
        {
            long now = Stopwatch.GetTimestamp();
            while (true)
            {
                long current = Interlocked.Read(ref _alertSoundTestEndsAtTimestamp);
                if (current > now)
                    return false;

                if (Interlocked.CompareExchange(ref _alertSoundTestEndsAtTimestamp, long.MaxValue, current) == current)
                    return true;
            }
        }

        internal void CompleteAlertSoundTest(TimeSpan playbackDuration)
        {
            double ticks = Math.Ceiling(playbackDuration.TotalSeconds * Stopwatch.Frequency);
            long durationTicks = ticks >= long.MaxValue ? long.MaxValue : (long)ticks;
            long now = Stopwatch.GetTimestamp();
            long endTimestamp = durationTicks > long.MaxValue - now ? long.MaxValue : now + durationTicks;
            Interlocked.Exchange(ref _alertSoundTestEndsAtTimestamp, endTimestamp);
        }

        internal void CancelAlertSoundTest()
            => Interlocked.Exchange(ref _alertSoundTestEndsAtTimestamp, 0);
    }
}
