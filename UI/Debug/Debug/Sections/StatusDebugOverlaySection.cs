namespace ClickIt.UI.Debug.Sections
{
    internal sealed class StatusDebugOverlaySection(Debug.DebugOverlayRenderContext context)
    {
        private readonly Debug.DebugOverlayRenderContext _context = context;

        public int RenderPluginStatusDebug(int xPos, int yPos, int lineHeight)
        {
            DebugTelemetrySnapshot telemetry = _context.DebugTelemetrySource.GetSnapshot();
            StatusTelemetrySnapshot status = telemetry.Status;

            _context.DeferredTextQueue.Enqueue("--- ClickIt Status ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            Color gameControllerColor = status.GameControllerAvailable ? Color.LightGreen : Color.Red;
            _context.DeferredTextQueue.Enqueue($"Game Controller: {status.GameControllerAvailable}", new Vector2(xPos, yPos), gameControllerColor, 16);
            yPos += lineHeight;

            Color gameColor = status.InGame ? Color.LightGreen : Color.Red;
            _context.DeferredTextQueue.Enqueue($"In Game: {status.InGame}", new Vector2(xPos, yPos), gameColor, 16);
            yPos += lineHeight;

            Color entityColor = status.EntityListValid ? Color.LightGreen : Color.Red;
            _context.DeferredTextQueue.Enqueue($"Entity List Valid: {status.EntityListValid}", new Vector2(xPos, yPos), entityColor, 16);
            yPos += lineHeight;

            Color playerColor = status.PlayerValid ? Color.LightGreen : Color.Red;
            _context.DeferredTextQueue.Enqueue($"Player Valid: {status.PlayerValid}", new Vector2(xPos, yPos), playerColor, 16);
            yPos += lineHeight;

            return yPos;
        }

        public int RenderGameStateDebug(int xPos, int yPos, int lineHeight)
        {
            DebugTelemetrySnapshot telemetry = _context.DebugTelemetrySource.GetSnapshot();
            StatusTelemetrySnapshot status = telemetry.Status;

            _context.DeferredTextQueue.Enqueue("--- Game State ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Current Area: {status.CurrentAreaName}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;

            Color itemColor = status.VisibleItemCount > 0 ? Color.LightGreen : Color.Gray;
            _context.DeferredTextQueue.Enqueue($"Items on Ground: {status.VisibleItemCount}", new Vector2(xPos, yPos), itemColor, 16);
            yPos += lineHeight;

            if (status.CachedLabelsAvailable)
            {
                _context.DeferredTextQueue.Enqueue($"Cached Labels: {status.CachedLabelCount}", new Vector2(xPos, yPos), Color.White, 16);
                yPos += lineHeight;
            }
            else
            {
                _context.DeferredTextQueue.Enqueue("Cache Info Unavailable", new Vector2(xPos, yPos), Color.Gray, 16);
                yPos += lineHeight;
            }

            Color playerColor = status.PlayerValid ? Color.LightGreen : Color.Red;
            _context.DeferredTextQueue.Enqueue($"Player Valid: {status.PlayerValid}", new Vector2(xPos, yPos), playerColor, 16);
            yPos += lineHeight;

            if (status.PlayerPositionAvailable)
            {
                _context.DeferredTextQueue.Enqueue($"Player Pos: ({status.PlayerPositionX:F1}, {status.PlayerPositionY:F1})", new Vector2(xPos, yPos), Color.White, 14);
                yPos += lineHeight;
            }

            return yPos;
        }
    }
}