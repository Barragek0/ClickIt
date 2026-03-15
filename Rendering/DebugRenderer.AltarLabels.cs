using ClickIt.Components;
using ClickIt.Utils;
using ExileCore.PoEMemory.Elements;
using SharpDX;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;

#nullable enable

namespace ClickIt.Rendering
{
    public partial class DebugRenderer
    {
        public int RenderAltarDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue("--- Altar Detection ---", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;
            var altarComps = _altarService?.GetAltarComponentsReadOnly() ?? [];
            Color altarCountColor = altarComps.Count > 0 ? Color.LightGreen : Color.Gray;
            _deferredTextQueue.Enqueue($"Altar Components: {altarComps.Count}", new Vector2(xPos, yPos), altarCountColor, 16);
            yPos += lineHeight;
            if (altarComps.Count > 0)
            {
                _deferredTextQueue.Enqueue("Active Altars:", new Vector2(xPos, yPos), Color.Cyan, 16);
                yPos += lineHeight;
                for (int i = 0; i < Math.Min(altarComps.Count, 2); i++)
                {
                    var altar = altarComps[i];
                    yPos = RenderSingleAltarDebug(xPos, yPos, lineHeight, altar, i + 1);
                }
            }

            return yPos + lineHeight;
        }

        public int RenderSingleAltarDebug(int xPos, int yPos, int lineHeight, PrimaryAltarComponent altar, int altarNumber)
        {
            _deferredTextQueue.Enqueue($"Altar {altarNumber}:", new Vector2(xPos, yPos), Color.Yellow, 16);
            yPos += lineHeight;

            AltarWeights? weights = null;
            if (_weightCalculator != null)
            {
                weights = altar.GetCachedWeights(pc => _weightCalculator.CalculateAltarWeights(pc));
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
            _deferredTextQueue.Enqueue($"  {sectionName} Mods (Upsides: {upsideCount}, Downsides: {downsideCount}):", new Vector2(xPos, yPos), Color.White, 14);
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
                    yPos = RenderWrappedText($"    {i + 1}: {mod}{weightText}", new Vector2(xPos, yPos), color, 12, lineHeight, 45);
                }
            }

            return yPos;
        }

        public int RenderAltarServiceDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue("--- Altar Service ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            var debugInfo = _altarService?.DebugInfo;
            if (debugInfo == null)
            {
                _deferredTextQueue.Enqueue("  Altar Service: NULL", new Vector2(xPos, yPos), Color.Red, 16);
                return yPos + lineHeight;
            }

            _deferredTextQueue.Enqueue($"Last Scan Exarch: {debugInfo.LastScanExarchLabels}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Last Scan Eater: {debugInfo.LastScanEaterLabels}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Elements Found: {debugInfo.ElementsFound}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Components Processed: {debugInfo.ComponentsProcessed}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Components Added: {debugInfo.ComponentsAdded}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Components Duplicated: {debugInfo.ComponentsDuplicated}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Mods Matched: {debugInfo.ModsMatched}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Mods Unmatched: {debugInfo.ModsUnmatched}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;
            _deferredTextQueue.Enqueue($"Last Altar Type: {debugInfo.LastProcessedAltarType}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;

            if (!string.IsNullOrEmpty(debugInfo.LastError))
            {
                _deferredTextQueue.Enqueue($"Last Error: {debugInfo.LastError}", new Vector2(xPos, yPos), Color.Red, 16);
                yPos += lineHeight;
            }

            if (debugInfo.LastScanTime != DateTime.MinValue)
            {
                _deferredTextQueue.Enqueue($"Last Scan: {debugInfo.LastScanTime:HH:mm:ss}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
            }

            return yPos;
        }

        public int RenderLabelsDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue("--- Labels ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            if (_plugin is not ClickIt clickIt || clickIt.State.LabelFilterService == null)
            {
                _deferredTextQueue.Enqueue("Label filter service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            var gameController = _plugin.GameController;
            var labelsCollection = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labelsCollection == null)
            {
                _deferredTextQueue.Enqueue("Labels collection: null", new Vector2(xPos, yPos), Color.Red, 14);
                return yPos + lineHeight;
            }

            int totalLabels = labelsCollection.Count;
            _deferredTextQueue.Enqueue($"Total Visible: {totalLabels}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            int validLabels = 0;
            foreach (var label in labelsCollection)
            {
                if (label?.ItemOnGround?.Path != null)
                    validLabels++;
            }

            _deferredTextQueue.Enqueue($"Valid Labels: {validLabels}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            var snap = clickIt.State.LabelFilterService.GetLatestLabelDebug();
            if (!snap.HasData)
            {
                _deferredTextQueue.Enqueue("No label debug data yet", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            Color stageColor = string.Equals(snap.Stage, "SelectionReturned", StringComparison.OrdinalIgnoreCase)
                || string.Equals(snap.Stage, "SelectionScanSelected", StringComparison.OrdinalIgnoreCase)
                ? Color.LightGreen
                : Color.Yellow;
            _deferredTextQueue.Enqueue($"Stage: {snap.Stage}  Seq: {snap.Sequence}", new Vector2(xPos, yPos), stageColor, 14);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Range: {snap.StartIndex}-{snap.EndExclusive}  Total: {snap.TotalLabels}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Considered: {snap.ConsideredCandidates}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Reject ND/U/NM: {snap.NullOrDistanceRejected}/{snap.UntargetableRejected}/{snap.NoMechanicRejected}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Ignored(PriorityDist): {snap.IgnoredByDistanceCandidates}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Selected Mechanic: {snap.SelectedMechanicId}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Selected Distance: {snap.SelectedDistance:0.0}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Selected Path: {TrimForDebug(snap.SelectedEntityPath, 56)}", new Vector2(xPos, yPos), Color.LightGray, 13);
            yPos += lineHeight;

            _deferredTextQueue.Enqueue($"Note: {TrimForDebug(snap.Notes, 56)}", new Vector2(xPos, yPos), Color.LightGray, 13);
            yPos += lineHeight;

            var trail = clickIt.State.LabelFilterService.GetLatestLabelDebugTrail();
            if (trail.Count > 0)
            {
                _deferredTextQueue.Enqueue("Recent Stages:", new Vector2(xPos, yPos), Color.LightBlue, 13);
                yPos += lineHeight;

                int start = Math.Max(0, trail.Count - 8);
                for (int i = start; i < trail.Count; i++)
                {
                    _deferredTextQueue.Enqueue($"  {TrimForDebug(trail[i], 80)}", new Vector2(xPos, yPos), Color.LightGray, 12);
                    yPos += lineHeight;
                }
            }

            return yPos;
        }

        public int RenderHoveredItemMetadataDebug(int xPos, int yPos, int lineHeight)
        {
            _deferredTextQueue.Enqueue("--- Hovered Item Metadata ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            var gameController = _plugin.GameController;
            var labels = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;
            if (labels == null || labels.Count == 0)
            {
                _deferredTextQueue.Enqueue("No ground labels available", new Vector2(xPos, yPos), Color.Gray, 16);
                return yPos + lineHeight;
            }

            RectangleF winRect = gameController?.Window.GetWindowRectangleTimeCache ?? RectangleF.Empty;
            var cursorPos = Mouse.GetCursorPosition();

            if (!IsCursorInsideWindow(winRect, cursorPos.X, cursorPos.Y))
            {
                _deferredTextQueue.Enqueue("Hover a ground-item label to inspect metadata", new Vector2(xPos, yPos), Color.Gray, 16);
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

                if (!IsCursorOverLabelRect(labelRect, winRect, cursorPos.X, cursorPos.Y))
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
                _deferredTextQueue.Enqueue("Hover a ground-item label to inspect metadata", new Vector2(xPos, yPos), Color.Gray, 16);
                return yPos + lineHeight;
            }

            string name = hovered.ItemOnGround?.RenderName ?? "<unknown>";
            string entityPath = hovered.ItemOnGround?.Path ?? string.Empty;
            string metadata = ResolveHoveredItemMetadataPath(hovered);

            _deferredTextQueue.Enqueue($"Name: {name}", new Vector2(xPos, yPos), Color.LightGreen, 16);
            yPos += lineHeight;
            yPos = RenderWrappedText($"Entity Path: {entityPath}", new Vector2(xPos, yPos), Color.White, 14, lineHeight, 60);
            yPos = RenderWrappedText($"Item Metadata: {metadata}", new Vector2(xPos, yPos), Color.Cyan, 14, lineHeight, 60);

            return yPos;
        }
    }
}