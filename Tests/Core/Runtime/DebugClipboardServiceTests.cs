namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class DebugClipboardServiceTests
    {
        [TestMethod]
        public void RequestAdditionalDebugInfoCopy_SetsPendingFlag()
        {
            DebugClipboardService service = CreateService();

            service.RequestAdditionalDebugInfoCopy();

            service.HasPendingAdditionalDebugInfoCopyRequest.Should().BeTrue();
        }

        [TestMethod]
        public void CompleteAdditionalDebugInfoCopy_ClearsPendingFlag_WhenDebugLinesAreEmpty()
        {
            DebugClipboardService service = CreateService();
            service.RequestAdditionalDebugInfoCopy();

            service.CompleteAdditionalDebugInfoCopy([]);

            service.HasPendingAdditionalDebugInfoCopyRequest.Should().BeFalse();
        }

        [TestMethod]
        public void CompleteAdditionalDebugInfoCopy_ClearsPendingFlag_WhenDebugLinesAreNull()
        {
            DebugClipboardService service = CreateService();
            service.RequestAdditionalDebugInfoCopy();

            service.CompleteAdditionalDebugInfoCopy(null!);

            service.HasPendingAdditionalDebugInfoCopyRequest.Should().BeFalse();
        }

        [TestMethod]
        public void TryAutoCopyInventoryWarningDebugSnapshot_ReturnsFalse_WhenSettingsUnavailable()
        {
            DebugClipboardService service = CreateService(getSettings: static () => null);

            bool copied = service.TryAutoCopyInventoryWarningDebugSnapshot(CreateInventorySnapshot(), now: 1000, debugLines: ["Debug"]);

            copied.Should().BeFalse();
        }

        [TestMethod]
        public void TryAutoCopyInventoryWarningDebugSnapshot_ReturnsFalse_WhenAutoCopyDisabled()
        {
            var settings = new ClickItSettings();
            settings.AutoCopyInventoryWarningDebug.Value = false;
            DebugClipboardService service = CreateService(getSettings: () => settings);

            bool copied = service.TryAutoCopyInventoryWarningDebugSnapshot(CreateInventorySnapshot(), now: 1000, debugLines: ["Debug"]);

            copied.Should().BeFalse();
        }

        [TestMethod]
        public void QueueDeepMemoryDumpCoroutine_LeavesRuntimeCoroutineUnset_WhenFeatureDisabled()
        {
            PluginContext state = new PluginContext();
            DebugClipboardService service = CreateService(state: state);

            service.QueueDeepMemoryDumpCoroutine();

            state.Runtime.DeepMemoryDumpCoroutine.Should().BeNull();
        }

        [TestMethod]
        public void DeepMemoryDumpCoordinator_StatusMessage_IsEmpty_WhenFeatureDisabled()
        {
            DebugClipboardService service = CreateService();
            DeepMemoryDumpCoordinator coordinator = (DeepMemoryDumpCoordinator)RuntimeMemberAccessor.GetRequiredMemberValue(service, "_deepMemoryDumpCoordinator")!;

            string status = coordinator.GetDeepMemoryDumpStatusMessage();

            status.Should().BeEmpty();
        }

        private static DebugClipboardService CreateService(
            PluginContext? state = null,
            ClickIt? owner = null,
            Func<ClickItSettings?>? getSettings = null,
            Func<GameController?>? getGameController = null)
        {
            return new DebugClipboardService(new DebugClipboardServiceDependencies(
                state ?? new PluginContext(),
                owner ?? new ClickIt(),
                getSettings ?? (() => new ClickItSettings()),
                getGameController ?? (() => null)));
        }

        private static InventoryDebugSnapshot CreateInventorySnapshot()
        {
            return new InventoryDebugSnapshot(
                HasData: true,
                Stage: "InventoryFullDecision",
                InventoryFull: true,
                InventoryFullSource: "CellOccupancy",
                HasPrimaryInventory: true,
                UsedFullFlag: false,
                FullFlagValue: false,
                UsedCellOccupancy: true,
                CapacityCells: 60,
                OccupiedCells: 60,
                InventoryEntityCount: 24,
                LayoutEntryCount: 24,
                GroundItemPath: "Metadata/Items/Test",
                GroundItemName: "Test Item",
                IsGroundStackable: false,
                MatchingPathCount: 1,
                PartialMatchingStackCount: 0,
                HasPartialMatchingStack: false,
                DecisionAllowPickup: false,
                Notes: string.Empty,
                Sequence: 1,
                TimestampMs: 1234);
        }
    }
}