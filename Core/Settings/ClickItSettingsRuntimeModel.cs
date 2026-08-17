namespace ClickIt
{
    public partial class ClickItSettings
    {
        public bool IsLazyModeDisableHotkeyToggleModeEnabled()
            => ClickItSettingsRuntimeService.IsLazyModeDisableHotkeyToggleModeEnabled(this);

        public bool IsClickHotkeyToggleModeEnabled()
            => ClickItSettingsRuntimeService.IsClickHotkeyToggleModeEnabled(this);

        public bool IsInitialUltimatumClickEnabled()
            => ClickItSettingsRuntimeService.IsInitialUltimatumClickEnabled(this);

        public bool IsOtherUltimatumClickEnabled()
            => ClickItSettingsRuntimeService.IsOtherUltimatumClickEnabled(this);

        public bool IsUltimatumTakeRewardButtonClickEnabled()
            => ClickItSettingsRuntimeService.IsUltimatumTakeRewardButtonClickEnabled(this);

        public IReadOnlyList<string> GetMechanicPriorityOrder()
            => ClickItSettingsRuntimeService.GetMechanicPriorityOrder(this);

        public IReadOnlyCollection<string> GetMechanicPriorityIgnoreDistanceIds()
            => ClickItSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceIds(this);

        public IReadOnlyDictionary<string, int> GetMechanicPriorityIgnoreDistanceWithinById()
            => ClickItSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceWithinById(this);
    }
}