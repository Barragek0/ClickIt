using SharpDX;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.UI.Debug.Sections
{
    internal sealed class LabelDebugOverlaySection(Debug.DebugOverlayRenderContext context)
    {
        private readonly Debug.DebugOverlayRenderContext _context = context;

        public int RenderAltarDebug(int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Altar Detection ---", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;
            var altarComps = _context.AltarService?.GetAltarComponentsReadOnly() ?? [];
            Color altarCountColor = altarComps.Count > 0 ? Color.LightGreen : Color.Gray;
            _context.DeferredTextQueue.Enqueue($"Altar Components: {altarComps.Count}", new Vector2(xPos, yPos), altarCountColor, 16);
            yPos += lineHeight;
            if (altarComps.Count > 0)
            {
                _context.DeferredTextQueue.Enqueue("Active Altars:", new Vector2(xPos, yPos), Color.Cyan, 16);
                yPos += lineHeight;
                for (int i = 0; i < Math.Min(altarComps.Count, 2); i++)
                {
                    var altar = altarComps[i];
                    yPos = RenderSingleAltarDebug(xPos, yPos, lineHeight, altar, i + 1);
                }
            }

            return yPos + lineHeight;
        }

        public int RenderAltarServiceDebug(int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Altar Service ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            var debugInfo = _context.AltarService?.DebugInfo;
            if (debugInfo == null)
            {
                _context.DeferredTextQueue.Enqueue("  Altar Service: NULL", new Vector2(xPos, yPos), Color.Red, 16);
                return yPos + lineHeight;
            }

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

            if (_context.Plugin is not ClickIt clickIt)
            {
                _context.DeferredTextQueue.Enqueue("Label filter service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            DebugTelemetrySnapshot telemetry = _context.DebugTelemetrySource.GetSnapshot();
            if (!telemetry.Label.ServiceAvailable)
            {
                _context.DeferredTextQueue.Enqueue("Label filter service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            var gameController = _context.Plugin.GameController;
            var labelsCollection = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labelsCollection == null)
            {
                _context.DeferredTextQueue.Enqueue("Labels collection: null", new Vector2(xPos, yPos), Color.Red, 14);
                return yPos + lineHeight;
            }

            int totalLabels = labelsCollection.Count;
            _context.DeferredTextQueue.Enqueue($"Total Visible: {totalLabels}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            int validLabels = 0;
            foreach (var label in labelsCollection)
            {
                if (label?.ItemOnGround?.Path != null)
                    validLabels++;
            }

            _context.DeferredTextQueue.Enqueue($"Valid Labels: {validLabels}", new Vector2(xPos, yPos), Color.White, 14);
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

            var gameController = _context.Plugin.GameController;
            var labels = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labels == null || labels.Count == 0)
            {
                _context.DeferredTextQueue.Enqueue("No ground labels available", new Vector2(xPos, yPos), Color.Gray, 16);
                return yPos + lineHeight;
            }

            RectangleF winRect = gameController?.Window.GetWindowRectangleTimeCache ?? RectangleF.Empty;
            var cursorPos = Mouse.GetCursorPosition();

            if (!Debug.DebugOverlayRenderContext.IsCursorInsideWindow(winRect, cursorPos.X, cursorPos.Y))
            {
                _context.DeferredTextQueue.Enqueue("Hover a ground-item label to inspect metadata", new Vector2(xPos, yPos), Color.Gray, 16);
                return yPos + lineHeight;
            }

            LabelOnGround? hovered = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < labels.Count; i++)
            {
                LabelOnGround? label = labels[i];
                if (label?.Label?.IsValid != true)
                    continue;

                object? rectObj = label.Label.GetClientRect();
                if (rectObj is not RectangleF labelRect)
                    continue;

                if (!Debug.DebugOverlayRenderContext.IsCursorOverLabelRect(labelRect, winRect, cursorPos.X, cursorPos.Y))
                    continue;

                float dist = label.ItemOnGround?.DistancePlayer ?? float.MaxValue;
                if (dist < bestDistance)
                {
                    hovered = label;
                    bestDistance = dist;
                }
            }

            if (hovered == null)
            {
                _context.DeferredTextQueue.Enqueue("Hover a ground-item label to inspect metadata", new Vector2(xPos, yPos), Color.Gray, 16);
                return yPos + lineHeight;
            }

            string name = hovered.ItemOnGround?.RenderName ?? "<unknown>";
            string entityPath = hovered.ItemOnGround?.Path ?? string.Empty;
            string metadata = Debug.DebugOverlayRenderContext.ResolveHoveredItemMetadataPath(hovered);

            _context.DeferredTextQueue.Enqueue($"Name: {name}", new Vector2(xPos, yPos), Color.LightGreen, 16);
            yPos += lineHeight;
            yPos = _context.RenderWrappedText($"Entity Path: {entityPath}", new Vector2(xPos, yPos), Color.White, 14, lineHeight, 60);
            yPos = _context.RenderWrappedText($"Item Metadata: {metadata}", new Vector2(xPos, yPos), Color.Cyan, 14, lineHeight, 60);

            return yPos;
        }

        private int RenderSingleAltarDebug(int xPos, int yPos, int lineHeight, PrimaryAltarComponent altar, int altarNumber)
        {
            _context.DeferredTextQueue.Enqueue($"Altar {altarNumber}:", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;

            AltarWeights? weights = null;
            if (_context.WeightCalculator != null)
            {
                weights = altar.GetCachedWeights(pc => _context.WeightCalculator.CalculateAltarWeights(pc));
            }

            if (altar?.TopMods != null)
            {
                decimal[]? topUpsideWeights = null;
                decimal[]? topDownsideWeights = null;
                if (weights.HasValue)
                {
                    var localWeights = weights.Value;
                    topUpsideWeights = localWeights.GetTopUpsideWeights();
                    topDownsideWeights = localWeights.GetTopDownsideWeights();
                }

                yPos = RenderModsSection(xPos, yPos, lineHeight, "Top", altar.TopMods, topUpsideWeights, topDownsideWeights);
            }

            if (altar?.BottomMods != null)
            {
                decimal[]? bottomUpsideWeights = null;
                decimal[]? bottomDownsideWeights = null;
                if (weights.HasValue)
                {
                    var localWeights = weights.Value;
                    bottomUpsideWeights = localWeights.GetBottomUpsideWeights();
                    bottomDownsideWeights = localWeights.GetBottomDownsideWeights();
                }

                yPos = RenderModsSection(xPos, yPos, lineHeight, "Bottom", altar.BottomMods, bottomUpsideWeights, bottomDownsideWeights);
            }

            return yPos;
        }

        private int RenderModsSection(
            int xPos,
            int yPos,
            int lineHeight,
            string sectionName,
            SecondaryAltarComponent mods,
            decimal[]? upsideWeights,
            decimal[]? downsideWeights)
        {
            int upsideCount = mods.Upsides?.Count ?? 0;
            int downsideCount = mods.Downsides?.Count ?? 0;
            _context.DeferredTextQueue.Enqueue($"  {sectionName} Mods (Upsides: {upsideCount}, Downsides: {downsideCount}):", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            yPos = RenderModsList(xPos, yPos, lineHeight, mods.Upsides, upsideWeights, Color.LightBlue);
            yPos = RenderModsList(xPos, yPos, lineHeight, mods.Downsides, downsideWeights, Color.LightCoral);

            return yPos;
        }

        private int RenderModsList(
            int xPos,
            int yPos,
            int lineHeight,
            IReadOnlyList<string>? mods,
            decimal[]? weights,
            Color color)
        {
            int count = Math.Min(mods?.Count ?? 0, 8);
            for (int i = 0; i < count; i++)
            {
                string mod = mods?[i] ?? string.Empty;
                if (!string.IsNullOrEmpty(mod))
                {
                    decimal weight = weights != null && i < weights.Length ? weights[i] : 0m;
                    string weightText = weights != null ? $" ({weight})" : string.Empty;
                    yPos = _context.RenderWrappedText($"    {i + 1}: {mod}{weightText}", new Vector2(xPos, yPos), color, 12, lineHeight, 45);
                }
            }

            return yPos;
        }
    }
}