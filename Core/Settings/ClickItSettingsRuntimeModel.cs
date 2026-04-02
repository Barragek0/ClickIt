using ExileCore.Shared.Nodes;
using ImGuiNET;

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

        public bool IsAnyUltimatumClickEnabled()
            => ClickItSettingsRuntimeService.IsAnyUltimatumClickEnabled(this);

        public bool IsUltimatumTakeRewardButtonClickEnabled()
            => ClickItSettingsRuntimeService.IsUltimatumTakeRewardButtonClickEnabled(this);

        public bool IsAnyDetailedDebugSectionEnabled()
            => ClickItSettingsRuntimeService.IsAnyDetailedDebugSectionEnabled(this);

        public bool IsOnlyPathfindingDetailedDebugSectionEnabled()
            => ClickItSettingsRuntimeService.IsOnlyPathfindingDetailedDebugSectionEnabled(this);

        public IReadOnlyList<string> GetMechanicPriorityOrder()
            => ClickItSettingsRuntimeService.GetMechanicPriorityOrder(this);

        public IReadOnlyCollection<string> GetMechanicPriorityIgnoreDistanceIds()
            => ClickItSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceIds(this);

        public IReadOnlyDictionary<string, int> GetMechanicPriorityIgnoreDistanceWithinById()
            => ClickItSettingsRuntimeService.GetMechanicPriorityIgnoreDistanceWithinById(this);
    }
}