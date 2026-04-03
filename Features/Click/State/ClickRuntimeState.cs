namespace ClickIt.Features.Click.State
{
    internal sealed class ClickRuntimeState
    {
        public ulong LastLeverKey { get; set; }

        public long LastLeverClickTimestampMs { get; set; }

        public long StickyOffscreenTargetAddress { get; set; }

        public long LastMovementSkillUseTimestampMs { get; set; }

        public long MovementSkillPostCastClickBlockUntilTimestampMs { get; set; }

        public long MovementSkillStatusPollUntilTimestampMs { get; set; }

        public object? LastUsedMovementSkillEntry { get; set; }
    }
}