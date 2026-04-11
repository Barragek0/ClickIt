namespace ClickIt.Features.Click.Runtime.TestSeams
{
    /** This seam isolates the offscreen owners from the concrete ExileCore player, window, and world-to-screen getter chain because the current harness cannot safely shape GameController.Player without triggering IngameData static offset initialization. Keep the default implementation behavior-equivalent to the underlying runtime getters; tests may replace it only to model that external boundary. */
    internal interface IOffscreenRuntimeSeam
    {
        Entity? GetPlayer(GameController gameController);

        bool TryGetGridPosition(Entity entity, out Vector2 gridPosition);

        RectangleF GetWindowRectangle(GameController gameController);

        bool TryProjectWorldToScreen(GameController gameController, Entity target, out Vector2 targetScreen);
    }

    internal sealed class OffscreenRuntimeSeam : IOffscreenRuntimeSeam
    {
        internal static IOffscreenRuntimeSeam Instance { get; } = new OffscreenRuntimeSeam();

        private OffscreenRuntimeSeam()
        {
        }

        public Entity? GetPlayer(GameController gameController)
            => gameController.Player;

        public bool TryGetGridPosition(Entity entity, out Vector2 gridPosition)
        {
            gridPosition = new Vector2(entity.GridPosNum.X, entity.GridPosNum.Y);
            return true;
        }

        public RectangleF GetWindowRectangle(GameController gameController)
            => gameController.Window.GetWindowRectangleTimeCache;

        public bool TryProjectWorldToScreen(GameController gameController, Entity target, out Vector2 targetScreen)
        {
            NumVector2 raw = gameController.Game.IngameState.Camera.WorldToScreen(target.PosNum);
            targetScreen = new Vector2(raw.X, raw.Y);
            return true;
        }
    }
}