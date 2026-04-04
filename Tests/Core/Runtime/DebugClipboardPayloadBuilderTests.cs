namespace ClickIt.Tests.Core.Runtime
{
    [TestClass]
    public class DebugClipboardPayloadBuilderTests
    {
        [TestMethod]
        public void BuildDebugClipboardPayload_SkipsBlankLines_AndPreservesDebugContent()
        {
            string payload = DebugClipboardPayloadBuilder.BuildDebugClipboardPayload([
                "Line A",
                string.Empty,
                "   ",
                "Line B"
            ]);

            payload.Should().Contain("=== ClickIt Additional Debug Information ===");
            payload.Should().Contain("Line A");
            payload.Should().Contain("Line B");
            payload.Should().NotContain("\r\n\r\n\r\n");
        }

        [TestMethod]
        public void BuildInventoryWarningClipboardPayload_IncludesSnapshotAndLastCopyMetadata()
        {
            var snapshot = new InventoryDebugSnapshot(
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
                PartialMatchingStackCount: 2,
                HasPartialMatchingStack: true,
                DecisionAllowPickup: false,
                Notes: "test notes",
                Sequence: 5,
                TimestampMs: 1234);

            string payload = DebugClipboardPayloadBuilder.BuildInventoryWarningClipboardPayload(
                snapshot,
                now: 5000,
                lastAutoCopySuccessTimestampMs: 4000,
                debugLines: ["Debug A"]);

            payload.Should().Contain("=== ClickIt Additional Debug Information ===");
            payload.Should().Contain("=== Inventory Warning Trigger Snapshot ===");
            payload.Should().Contain("NowMs: 5000");
            payload.Should().Contain("LastAutoCopySuccessMs: 4000");
            payload.Should().Contain("Stage: InventoryFullDecision");
            payload.Should().Contain("GroundItemName: Test Item");
            payload.Should().Contain("HasPartialMatchingStack: True");
            payload.Should().Contain("Notes: test notes");
        }
    }
}