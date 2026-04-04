namespace ClickIt.UI.Debug.Sections
{
    internal sealed class LabelDebugOverlaySection(Debug.DebugOverlayRenderContext context)
    {
        private readonly Debug.DebugOverlayRenderContext _context = context;

        public int RenderAltarDebug(int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Altar Detection ---", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;
            DebugTelemetrySnapshot telemetry = _context.DebugTelemetrySource.GetSnapshot();
            AltarTelemetrySnapshot altarTelemetry = telemetry.Altar;
            Color altarCountColor = altarTelemetry.ComponentCount > 0 ? Color.LightGreen : Color.Gray;
            _context.DeferredTextQueue.Enqueue($"Altar Components: {altarTelemetry.ComponentCount}", new Vector2(xPos, yPos), altarCountColor, 16);
            yPos += lineHeight;
            if (altarTelemetry.Components.Count > 0)
            {
                _context.DeferredTextQueue.Enqueue("Active Altars:", new Vector2(xPos, yPos), Color.Cyan, 16);
                yPos += lineHeight;
                for (int i = 0; i < altarTelemetry.Components.Count; i++)
                {
                    AltarComponentTelemetrySnapshot altar = altarTelemetry.Components[i];
                    yPos = RenderSingleAltarDebug(xPos, yPos, lineHeight, altar, i + 1);
                }
            }

            return yPos + lineHeight;
        }

        public int RenderAltarServiceDebug(int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Altar Service ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            DebugTelemetrySnapshot telemetry = _context.DebugTelemetrySource.GetSnapshot();
            if (!telemetry.Altar.ServiceAvailable)
            {
                _context.DeferredTextQueue.Enqueue("  Altar Service: NULL", new Vector2(xPos, yPos), Color.Red, 16);
                return yPos + lineHeight;
            }

            AltarServiceDebugTelemetrySnapshot debugInfo = telemetry.Altar.ServiceDebug;

            _context.DeferredTextQueue.Enqueue($"Last Scan Exarch: {debugInfo.LastScanExarchLabels}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Last Scan Eater: {debugInfo.LastScanEaterLabels}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Elements Found: {debugInfo.ElementsFound}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Components Processed: {debugInfo.ComponentsProcessed}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Components Added: {debugInfo.ComponentsAdded}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Components Duplicated: {debugInfo.ComponentsDuplicated}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Mods Matched: {debugInfo.ModsMatched}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Mods Unmatched: {debugInfo.ModsUnmatched}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Last Altar Type: {debugInfo.LastProcessedAltarType}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;

            if (!string.IsNullOrEmpty(debugInfo.LastError))
            {
                _context.DeferredTextQueue.Enqueue($"Last Error: {debugInfo.LastError}", new Vector2(xPos, yPos), Color.Red, 16);
                yPos += lineHeight;
            }

            if (debugInfo.LastScanTime != DateTime.MinValue)
            {
                _context.DeferredTextQueue.Enqueue($"Last Scan: {debugInfo.LastScanTime:HH:mm:ss}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
            }

            return yPos;
        }

        public int RenderLabelsDebug(ref int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Labels ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            DebugTelemetrySnapshot telemetry = _context.DebugTelemetrySource.GetSnapshot();
            if (!telemetry.Label.ServiceAvailable)
            {
                _context.DeferredTextQueue.Enqueue("Label filter service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            if (!telemetry.Label.LabelsAvailable)
            {
                _context.DeferredTextQueue.Enqueue("Labels collection: null", new Vector2(xPos, yPos), Color.Red, 14);
                return yPos + lineHeight;
            }

            _context.DeferredTextQueue.Enqueue($"Total Visible: {telemetry.Label.TotalVisibleLabels}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Valid Labels: {telemetry.Label.ValidVisibleLabels}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            var snap = telemetry.Label.Label;
            if (!snap.HasData)
            {
                _context.DeferredTextQueue.Enqueue("No label debug data yet", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            Color stageColor = string.Equals(snap.Stage, "SelectionReturned", StringComparison.OrdinalIgnoreCase)
                || string.Equals(snap.Stage, "SelectionScanSelected", StringComparison.OrdinalIgnoreCase)
                ? Color.LightGreen
                : Color.Yellow;
            _context.DeferredTextQueue.Enqueue($"Stage: {snap.Stage}  Seq: {snap.Sequence}", new Vector2(xPos, yPos), stageColor, 14);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Range: {snap.StartIndex}-{snap.EndExclusive}  Total: {snap.TotalLabels}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Considered: {snap.ConsideredCandidates}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Reject ND/U/NM: {snap.NullOrDistanceRejected}/{snap.UntargetableRejected}/{snap.NoMechanicRejected}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Ignored(PriorityDist): {snap.IgnoredByDistanceCandidates}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Selected Mechanic: {snap.SelectedMechanicId}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Selected Distance: {snap.SelectedDistance:0.0}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Selected Path: {snap.SelectedEntityPath}", Color.LightGray, 13, 72);

            yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Note: {snap.Notes}", Color.LightGray, 13, 72);

            var trail = telemetry.Label.LabelTrail;
            yPos = _context.RenderDebugTrailBlock(ref xPos, yPos, lineHeight, trail, maxRows: 8, wrapWidth: 80);

            return yPos;
        }

        public int RenderInventoryPickupDebug(ref int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Inventory Pickup ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            DebugTelemetrySnapshot telemetry = _context.DebugTelemetrySource.GetSnapshot();
            var snap = telemetry.Inventory.Inventory;
            if (!snap.HasData)
            {
                _context.DeferredTextQueue.Enqueue("No inventory pickup debug data yet", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            Color decisionColor = snap.DecisionAllowPickup ? Color.LightGreen : Color.OrangeRed;
            Color fullnessColor = snap.InventoryFull ? Color.OrangeRed : Color.LightGreen;

            _context.DeferredTextQueue.Enqueue($"Stage: {snap.Stage}  Seq: {snap.Sequence}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Allow Pickup: {snap.DecisionAllowPickup}", new Vector2(xPos, yPos), decisionColor, 14);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Inventory Full: {snap.InventoryFull} ({snap.InventoryFullSource})", new Vector2(xPos, yPos), fullnessColor, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Server Inventory Present: {snap.HasPrimaryInventory}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Full Flag Used/Value: {snap.UsedFullFlag}/{snap.FullFlagValue}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Cell Occupancy Used: {snap.UsedCellOccupancy}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Capacity/Occupied Cells: {snap.CapacityCells}/{snap.OccupiedCells}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Inventory Entities/Layout Entries: {snap.InventoryEntityCount}/{snap.LayoutEntryCount}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Ground Item Name: {snap.GroundItemName}", Color.LightGray, 13, 72);

            yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Ground Item Path: {snap.GroundItemPath}", Color.LightGray, 13, 72);

            _context.DeferredTextQueue.Enqueue($"Ground Stackable: {snap.IsGroundStackable}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Path Matches In Inventory: {snap.MatchingPathCount}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Partial Stack Matches: {snap.PartialMatchingStackCount} (Any: {snap.HasPartialMatchingStack})", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Note: {snap.Notes}", Color.LightGray, 13, 72);

            var trail = telemetry.Inventory.InventoryTrail;
            yPos = _context.RenderDebugTrailBlock(ref xPos, yPos, lineHeight, trail, maxRows: 4, wrapWidth: 80);

            return yPos;
        }

        public int RenderHoveredItemMetadataDebug(int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Hovered Item Metadata ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            HoveredItemMetadataTelemetrySnapshot hoveredItem = _context.DebugTelemetrySource.GetSnapshot().HoveredItem;
            if (!hoveredItem.LabelsAvailable)
            {
                _context.DeferredTextQueue.Enqueue("No ground labels available", new Vector2(xPos, yPos), Color.Gray, 16);
                return yPos + lineHeight;
            }

            if (!hoveredItem.CursorInsideWindow || !hoveredItem.HasHoveredItem)
            {
                _context.DeferredTextQueue.Enqueue("Hover a ground-item label to inspect metadata", new Vector2(xPos, yPos), Color.Gray, 16);
                return yPos + lineHeight;
            }

            _context.DeferredTextQueue.Enqueue($"Name: {hoveredItem.GroundItemName}", new Vector2(xPos, yPos), Color.LightGreen, 16);
            yPos += lineHeight;
            yPos = _context.RenderWrappedText($"Entity Path: {hoveredItem.EntityPath}", new Vector2(xPos, yPos), Color.White, 14, lineHeight, 60);
            yPos = _context.RenderWrappedText($"Item Metadata: {hoveredItem.MetadataPath}", new Vector2(xPos, yPos), Color.Cyan, 14, lineHeight, 60);

            return yPos;
        }

        private int RenderSingleAltarDebug(int xPos, int yPos, int lineHeight, AltarComponentTelemetrySnapshot altar, int altarNumber)
        {
            _context.DeferredTextQueue.Enqueue($"Altar {altarNumber}:", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;

            yPos = RenderModsSection(xPos, yPos, lineHeight, altar.Top);
            yPos = RenderModsSection(xPos, yPos, lineHeight, altar.Bottom);

            return yPos;
        }

        private int RenderModsSection(
            int xPos,
            int yPos,
            int lineHeight,
            AltarModSectionTelemetrySnapshot section)
        {
            _context.DeferredTextQueue.Enqueue($"  {section.SectionName} Mods (Upsides: {section.UpsideCount}, Downsides: {section.DownsideCount}):", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            yPos = RenderModsList(xPos, yPos, lineHeight, section.Upsides, Color.LightBlue);
            yPos = RenderModsList(xPos, yPos, lineHeight, section.Downsides, Color.LightCoral);

            return yPos;
        }

        private int RenderModsList(
            int xPos,
            int yPos,
            int lineHeight,
            IReadOnlyList<AltarWeightedModTelemetrySnapshot> mods,
            Color color)
        {
            for (int i = 0; i < mods.Count; i++)
            {
                AltarWeightedModTelemetrySnapshot mod = mods[i];
                if (string.IsNullOrEmpty(mod.Text))
                    continue;

                string weightText = mod.Weight.HasValue ? $" ({mod.Weight.Value})" : string.Empty;
                yPos = _context.RenderWrappedText($"    {i + 1}: {mod.Text}{weightText}", new Vector2(xPos, yPos), color, 12, lineHeight, 45);
            }

            return yPos;
        }
    }
}