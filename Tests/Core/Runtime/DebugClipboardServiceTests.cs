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

        [TestMethod]
        public void DeepMemoryDumpCoordinator_OnDeepMemoryDumpProgress_ClampsAndPublishesUiState()
        {
            var settings = new ClickItSettings();
            DebugClipboardService service = CreateService(getSettings: () => settings);
            DeepMemoryDumpCoordinator coordinator = GetDeepMemoryDumpCoordinator(service);

            InvokePrivateVoid(coordinator, "OnDeepMemoryDumpProgress", 250);

            settings.MemoryDumpInProgress.Should().BeTrue();
            settings.MemoryDumpProgressPercent.Should().Be(100);
            settings.MemoryDumpLastRunSucceeded.Should().BeFalse();
            settings.MemoryDumpStatusText.Should().Contain("Writing ");
            settings.MemoryDumpStatusText.Should().EndWith("...");
            settings.MemoryDumpOutputPath.Should().BeEmpty();
        }

        [TestMethod]
        public void DeepMemoryDumpCoordinator_OnDeepMemoryDumpCompleted_PublishesSuccessState()
        {
            var settings = new ClickItSettings();
            DebugClipboardService service = CreateService(getSettings: () => settings);
            DeepMemoryDumpCoordinator coordinator = GetDeepMemoryDumpCoordinator(service);

            InvokePrivateVoid(coordinator, "OnDeepMemoryDumpCompleted", @"C:\temp\memory.dat", null);

            settings.MemoryDumpInProgress.Should().BeFalse();
            settings.MemoryDumpProgressPercent.Should().Be(100);
            settings.MemoryDumpLastRunSucceeded.Should().BeTrue();
            settings.MemoryDumpStatusText.Should().Contain("written successfully");
            settings.MemoryDumpOutputPath.Should().Be(@"C:\temp\memory.dat");
        }

        [TestMethod]
        public void DeepMemoryDumpCoordinator_OnDeepMemoryDumpCompleted_PublishesFailureState()
        {
            var settings = new ClickItSettings();
            DebugClipboardService service = CreateService(getSettings: () => settings);
            DeepMemoryDumpCoordinator coordinator = GetDeepMemoryDumpCoordinator(service);

            InvokePrivateVoid(coordinator, "OnDeepMemoryDumpCompleted", null, "disk full");

            settings.MemoryDumpInProgress.Should().BeFalse();
            settings.MemoryDumpProgressPercent.Should().Be(0);
            settings.MemoryDumpLastRunSucceeded.Should().BeFalse();
            settings.MemoryDumpStatusText.Should().Contain("Memory dump failed: disk full");
            settings.MemoryDumpOutputPath.Should().BeEmpty();
        }

        [TestMethod]
        public void DeepMemoryDumpCoordinator_SetMemoryDumpUiState_Returns_WhenSettingsUnavailable()
        {
            DebugClipboardService service = CreateService(getSettings: static () => null);
            DeepMemoryDumpCoordinator coordinator = GetDeepMemoryDumpCoordinator(service);

            Action act = () => InvokePrivateVoid(
                coordinator,
                "SetMemoryDumpUiState",
                true,
                42,
                false,
                "status",
                "path");

            act.Should().NotThrow();
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

        private static DeepMemoryDumpCoordinator GetDeepMemoryDumpCoordinator(DebugClipboardService service)
            => (DeepMemoryDumpCoordinator)RuntimeMemberAccessor.GetRequiredMemberValue(service, "_deepMemoryDumpCoordinator")!;

        private static void InvokePrivateVoid(object instance, string methodName, params object?[] args)
        {
            MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull($"Expected private method {methodName} to exist.");
            method!.Invoke(instance, args);
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