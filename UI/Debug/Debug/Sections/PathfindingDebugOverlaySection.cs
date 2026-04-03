namespace ClickIt.UI.Debug.Sections
{
    internal sealed class PathfindingDebugOverlaySection(Debug.DebugOverlayRenderContext context)
    {
        private readonly Debug.DebugOverlayRenderContext _context = context;

        public int RenderPathfindingDebug(int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Pathfinding ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            if (_context.Plugin is not ClickIt clickIt)
            {
                _context.DeferredTextQueue.Enqueue("Pathfinding service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            DebugTelemetrySnapshot telemetry = clickIt.State.GetDebugTelemetrySnapshot();
            if (!telemetry.Pathfinding.ServiceAvailable)
            {
                _context.DeferredTextQueue.Enqueue("Pathfinding service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            if (clickIt.State.TryGetDebugTelemetryFreezeState(out long remainingMs, out string freezeReason))
            {
                string freezeSummary = string.IsNullOrWhiteSpace(freezeReason)
                    ? $"Telemetry Hold Active: {remainingMs}ms remaining"
                    : $"Telemetry Hold Active: {remainingMs}ms remaining | {freezeReason}";
                yPos = _context.RenderWrappedText(freezeSummary, new Vector2(xPos, yPos), Color.Orange, 14, lineHeight, 46);
            }

            var snap = telemetry.Pathfinding.Pathfinding;
            Color terrainColor = snap.TerrainLoaded ? Color.LightGreen : Color.Red;
            _context.DeferredTextQueue.Enqueue($"Terrain Loaded: {snap.TerrainLoaded}", new Vector2(xPos, yPos), terrainColor, 14);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Grid: {snap.AreaWidth} x {snap.AreaHeight}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Expanded Nodes: {snap.LastExpandedNodes}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Path Length: {snap.LastPathLength}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;
            _context.DeferredTextQueue.Enqueue($"Compute: {snap.LastComputeMs} ms", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue(
                $"Goal Mode: {(snap.LastGoalResolutionUsedFallback ? "Fallback" : "Direct")}",
                new Vector2(xPos, yPos),
                snap.LastGoalResolutionUsedFallback ? Color.Yellow : Color.LightGreen,
                14);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue(
                $"Grid Start=({snap.LastStart.X},{snap.LastStart.Y}) Req=({snap.LastRequestedGoal.X},{snap.LastRequestedGoal.Y}) Res=({snap.LastResolvedGoal.X},{snap.LastResolvedGoal.Y})",
                new Vector2(xPos, yPos),
                Color.White,
                14);
            yPos += lineHeight;

            if (!string.IsNullOrWhiteSpace(snap.LastGoalResolutionNote))
            {
                yPos = _context.RenderWrappedText(
                    $"Goal Note: {snap.LastGoalResolutionNote}",
                    new Vector2(xPos, yPos),
                    Color.Yellow,
                    14,
                    lineHeight,
                    46);
            }

            string targetPath = string.IsNullOrWhiteSpace(snap.LastTargetPath) ? "<none>" : snap.LastTargetPath;
            yPos = _context.RenderWrappedText($"Target Path: {targetPath}", new Vector2(xPos, yPos), Color.LightBlue, 14, lineHeight, 46);

            if (!string.IsNullOrWhiteSpace(snap.LastFailureReason))
            {
                yPos = _context.RenderWrappedText($"Failure: {snap.LastFailureReason}", new Vector2(xPos, yPos), Color.OrangeRed, 14, lineHeight, 46);
            }

            var movement = telemetry.Pathfinding.OffscreenMovement;
            if (!movement.HasData)
            {
                _context.DeferredTextQueue.Enqueue("Offscreen Movement: <no data>", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            Vector2 gridDelta = movement.TargetGrid - movement.PlayerGrid;
            Vector2 targetDelta = movement.TargetScreen - movement.WindowCenter;
            Vector2 clickDelta = movement.ClickScreen - movement.WindowCenter;

            _context.DeferredTextQueue.Enqueue(
                $"Offscreen Stage: {movement.Stage} | built={movement.BuiltPath} | fromPath={movement.ResolvedFromPath} | clickPoint={movement.ResolvedClickPoint}",
                new Vector2(xPos, yPos),
                Color.White,
                14);
            yPos += lineHeight;

            if (!string.IsNullOrWhiteSpace(movement.MovementSkillDebug))
            {
                yPos = _context.RenderWrappedText(
                    $"Movement Skill Debug: {movement.MovementSkillDebug}",
                    new Vector2(xPos, yPos),
                    Color.Yellow,
                    14,
                    lineHeight,
                    46);
            }

            yPos = _context.RenderWrappedText(
                $"Offscreen Target: {DebugOverlayRenderContext.TrimPath(movement.TargetPath)}",
                new Vector2(xPos, yPos),
                Color.LightBlue,
                14,
                lineHeight,
                46);

            _context.DeferredTextQueue.Enqueue(
                $"Grid P=({movement.PlayerGrid.X:0},{movement.PlayerGrid.Y:0}) T=({movement.TargetGrid.X:0},{movement.TargetGrid.Y:0}) d=({gridDelta.X:0},{gridDelta.Y:0})",
                new Vector2(xPos, yPos),
                Color.Orange,
                14);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue(
                $"Target Delta=({targetDelta.X:0.0},{targetDelta.Y:0.0}) dir={PathfindingRenderer.ToCompass(targetDelta)}",
                new Vector2(xPos, yPos),
                Color.Cyan,
                14);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue(
                $"Click Delta=({clickDelta.X:0.0},{clickDelta.Y:0.0}) dir={PathfindingRenderer.ToCompass(clickDelta)}",
                new Vector2(xPos, yPos),
                Color.Lime,
                14);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue(
                $"Center=({movement.WindowCenter.X:0.0},{movement.WindowCenter.Y:0.0}) Target=({movement.TargetScreen.X:0.0},{movement.TargetScreen.Y:0.0}) Click=({movement.ClickScreen.X:0.0},{movement.ClickScreen.Y:0.0})",
                new Vector2(xPos, yPos),
                Color.Gray,
                14);
            yPos += lineHeight;

            IReadOnlyList<string> trail = telemetry.Pathfinding.OffscreenMovementTrail;
            yPos = _context.RenderDebugTrailBlock(ref xPos, yPos, lineHeight, trail, maxRows: 6, wrapWidth: 52);

            return yPos;
        }
    }
}