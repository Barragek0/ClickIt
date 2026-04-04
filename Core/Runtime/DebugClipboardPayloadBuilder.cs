namespace ClickIt.Core.Runtime
{
    internal static class DebugClipboardPayloadBuilder
    {
        internal static string BuildDebugClipboardPayload(string[] lines)
        {
            var sb = new StringBuilder(lines.Length * 32);
            sb.AppendLine("=== ClickIt Additional Debug Information ===");
            sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    sb.AppendLine(lines[i]);
            }

            return sb.ToString().TrimEnd();
        }

        internal static string BuildInventoryWarningClipboardPayload(
            InventoryDebugSnapshot snapshot,
            long now,
            long lastAutoCopySuccessTimestampMs,
            string[] debugLines)
        {
            string payload = BuildDebugClipboardPayload(debugLines);

            var sb = new StringBuilder(payload.Length + 512);
            if (!string.IsNullOrWhiteSpace(payload))
            {
                sb.AppendLine(payload);
                sb.AppendLine();
            }

            sb.AppendLine("=== Inventory Warning Trigger Snapshot ===");
            sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.AppendLine($"NowMs: {now}");
            sb.AppendLine($"LastAutoCopySuccessMs: {lastAutoCopySuccessTimestampMs}");
            sb.AppendLine($"Sequence: {snapshot.Sequence}");
            sb.AppendLine($"TimestampMs: {snapshot.TimestampMs}");
            sb.AppendLine($"Stage: {snapshot.Stage}");
            sb.AppendLine($"DecisionAllowPickup: {snapshot.DecisionAllowPickup}");
            sb.AppendLine($"InventoryFull: {snapshot.InventoryFull}");
            sb.AppendLine($"InventoryFullSource: {snapshot.InventoryFullSource}");
            sb.AppendLine($"HasPrimaryInventory: {snapshot.HasPrimaryInventory}");
            sb.AppendLine($"UsedFullFlag: {snapshot.UsedFullFlag}");
            sb.AppendLine($"FullFlagValue: {snapshot.FullFlagValue}");
            sb.AppendLine($"UsedCellOccupancy: {snapshot.UsedCellOccupancy}");
            sb.AppendLine($"CapacityCells: {snapshot.CapacityCells}");
            sb.AppendLine($"OccupiedCells: {snapshot.OccupiedCells}");
            sb.AppendLine($"InventoryEntityCount: {snapshot.InventoryEntityCount}");
            sb.AppendLine($"LayoutEntryCount: {snapshot.LayoutEntryCount}");
            sb.AppendLine($"GroundItemName: {snapshot.GroundItemName}");
            sb.AppendLine($"GroundItemPath: {snapshot.GroundItemPath}");
            sb.AppendLine($"IsGroundStackable: {snapshot.IsGroundStackable}");
            sb.AppendLine($"MatchingPathCount: {snapshot.MatchingPathCount}");
            sb.AppendLine($"PartialMatchingStackCount: {snapshot.PartialMatchingStackCount}");
            sb.AppendLine($"HasPartialMatchingStack: {snapshot.HasPartialMatchingStack}");
            sb.AppendLine($"Notes: {snapshot.Notes}");

            return sb.ToString().TrimEnd();
        }
    }
}