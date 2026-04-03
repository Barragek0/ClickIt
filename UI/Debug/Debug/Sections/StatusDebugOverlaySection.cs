using SharpDX;
using Color = SharpDX.Color;

namespace ClickIt.UI.Debug.Sections
{
    internal sealed class StatusDebugOverlaySection(Debug.DebugOverlayRenderContext context)
    {
        private readonly Debug.DebugOverlayRenderContext _context = context;

        public int RenderPluginStatusDebug(int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- ClickIt Status ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            var gameController = _context.Plugin.GameController;
            bool inGame = gameController?.InGame == true;
            Color gameColor = inGame ? Color.LightGreen : Color.Red;
            _context.DeferredTextQueue.Enqueue($"In Game: {inGame}", new Vector2(xPos, yPos), gameColor, 16);
            yPos += lineHeight;

            bool entityListValid = gameController?.EntityListWrapper?.ValidEntitiesByType != null;
            Color entityColor = entityListValid ? Color.LightGreen : Color.Red;
            _context.DeferredTextQueue.Enqueue($"Entity List Valid: {entityListValid}", new Vector2(xPos, yPos), entityColor, 16);
            yPos += lineHeight;

            bool playerValid = gameController?.Player != null;
            Color playerColor = playerValid ? Color.LightGreen : Color.Red;
            _context.DeferredTextQueue.Enqueue($"Player Valid: {playerValid}", new Vector2(xPos, yPos), playerColor, 16);
            yPos += lineHeight;

            return yPos;
        }

        public int RenderGameStateDebug(int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Game State ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            var gameController = _context.Plugin.GameController;
            var currentArea = gameController?.Area?.CurrentArea;
            string areaName = currentArea?.DisplayName ?? "Unknown";
            _context.DeferredTextQueue.Enqueue($"Current Area: {areaName}", new Vector2(xPos, yPos), Color.White, 16);
            yPos += lineHeight;

            bool hasItems = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible?.Count > 0;
            Color itemColor = hasItems ? Color.LightGreen : Color.Gray;
            int itemCount = gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible?.Count ?? 0;
            _context.DeferredTextQueue.Enqueue($"Items on Ground: {itemCount}", new Vector2(xPos, yPos), itemColor, 16);
            yPos += lineHeight;

            if (_context.Plugin is ClickIt clickItPlugin)
            {
                var cachedLabels = clickItPlugin.State.Services.CachedLabels;
                if (cachedLabels != null)
                {
                    var labels = cachedLabels.Value;
                    int cachedCount = labels?.Count ?? 0;
                    _context.DeferredTextQueue.Enqueue($"Cached Labels: {cachedCount}", new Vector2(xPos, yPos), Color.White, 16);
                    yPos += lineHeight;
                }
                else
                {
                    _context.DeferredTextQueue.Enqueue("Cache Info Unavailable", new Vector2(xPos, yPos), Color.Gray, 16);
                }

                yPos += lineHeight;
            }

            bool playerValid = gameController?.Player != null;
            Color playerColor = playerValid ? Color.LightGreen : Color.Red;
            _context.DeferredTextQueue.Enqueue($"Player Valid: {playerValid}", new Vector2(xPos, yPos), playerColor, 16);
            yPos += lineHeight;

            if (playerValid && gameController?.Player != null)
            {
                var playerPos = gameController.Player.Pos;
                _context.DeferredTextQueue.Enqueue($"Player Pos: ({playerPos.X:F1}, {playerPos.Y:F1})", new Vector2(xPos, yPos), Color.White, 14);
                yPos += lineHeight;
            }

            return yPos;
        }
    }
}