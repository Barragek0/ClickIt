namespace ClickIt.UI.Debug.Sections
{
    internal sealed class ClickingDebugOverlaySection(DebugOverlayRenderContext context)
    {
        private readonly DebugOverlayRenderContext _context = context;

        public int RenderRuntimeDebugLogOverlay(ref int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Debug Log Overlay ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            if (_context.Plugin is not ClickIt)
            {
                _context.DeferredTextQueue.Enqueue("Click service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            DebugTelemetrySnapshot telemetry = _context.DebugTelemetrySource.GetSnapshot();
            if (!telemetry.Click.ServiceAvailable)
            {
                _context.DeferredTextQueue.Enqueue("Click service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            RuntimeDebugLogSnapshot latest = telemetry.Click.RuntimeLog;
            if (!latest.HasData)
            {
                _context.DeferredTextQueue.Enqueue("No debug log messages yet", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Latest: {latest.Message}", Color.LightGray, 13, 80);

            IReadOnlyList<string> trail = telemetry.Click.RuntimeLogTrail;
            yPos = _context.RenderDebugTrailBlock(ref xPos, yPos, lineHeight, trail, maxRows: 10, wrapWidth: 80);
            return yPos;
        }

        public int RenderClickingDebug(ref int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Clicking ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            if (_context.Plugin is not ClickIt)
            {
                _context.DeferredTextQueue.Enqueue("Click service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            DebugTelemetrySnapshot telemetry = _context.DebugTelemetrySource.GetSnapshot();
            if (!telemetry.Click.ServiceAvailable)
            {
                _context.DeferredTextQueue.Enqueue("Click service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            if (_context.DebugTelemetrySource.TryGetFreezeState(out long remainingMs, out string freezeReason))
            {
                string freezeSummary = string.IsNullOrWhiteSpace(freezeReason)
                    ? $"Telemetry Hold Active: {remainingMs}ms remaining"
                    : $"Telemetry Hold Active: {remainingMs}ms remaining | {freezeReason}";
                yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, freezeSummary, Color.Orange, 13, 86);
            }

            _context.DeferredTextQueue.Enqueue("Click Settings Snapshot:", new Vector2(xPos, yPos), Color.LightBlue, 14);
            yPos += lineHeight;

            IReadOnlyList<string> clickSettingsLines = telemetry.Click.Settings.SummaryLines;
            for (int i = 0; i < clickSettingsLines.Count; i++)
                yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, clickSettingsLines[i], Color.LightGray, 13, 86);


            ClickDebugSnapshot snap = telemetry.Click.Click;
            if (!snap.HasData)
            {
                _context.DeferredTextQueue.Enqueue("No click data yet", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            Color stageColor = snap.Resolved && snap.ResolvedClickable ? Color.LightGreen : Color.Yellow;
            _context.DeferredTextQueue.Enqueue($"Stage: {snap.Stage}  Seq: {snap.Sequence}", new Vector2(xPos, yPos), stageColor, 14);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Mechanic: {snap.MechanicId}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Distance: {snap.Distance:0.0}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Path: {snap.EntityPath}", Color.LightGray, 13, 72);

            _context.DeferredTextQueue.Enqueue($"World Raw: ({snap.WorldScreenRaw.X:0.0},{snap.WorldScreenRaw.Y:0.0})", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"World Abs: ({snap.WorldScreenAbsolute.X:0.0},{snap.WorldScreenAbsolute.Y:0.0})", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Click Pos: ({snap.ResolvedClickPoint.X:0.0},{snap.ResolvedClickPoint.Y:0.0})", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Center InWnd/Clickable: {snap.CenterInWindow}/{snap.CenterClickable}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Resolved InWnd/Clickable: {snap.ResolvedInWindow}/{snap.ResolvedClickable}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Resolved: {snap.Resolved}  Note: {snap.Notes}", Color.LightGray, 13, 72);

            IReadOnlyList<string> trail = telemetry.Click.ClickTrail;
            yPos = _context.RenderDebugTrailBlock(ref xPos, yPos, lineHeight, trail, maxRows: 8, wrapWidth: 78);

            return yPos;
        }
    }
}