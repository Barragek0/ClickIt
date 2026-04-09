namespace ClickIt.UI.Overlays.Common
{
    public class ClickHotkeyToggleRenderer(ClickItSettings settings, DeferredTextQueue deferredTextQueue, InputHandler inputHandler)
    {
        private const string ClickingText = "Clicking";
        private const string NotClickingText = "Not Clicking";
        private const float TitleY = 60f;

        private readonly ClickItSettings _settings = settings;
        private readonly DeferredTextQueue _deferredTextQueue = deferredTextQueue ?? new DeferredTextQueue();
        private readonly InputHandler _inputHandler = inputHandler;

        public void Render(GameController gameController)
        {
            if (!_settings.IsClickHotkeyToggleModeEnabled())
                return;

            var windowRect = gameController.Window.GetWindowRectangleTimeCache;
            float centerX = windowRect.Width / 2f;
            float topY = ResolveTopY(_settings.LazyMode.Value);

            bool clicking = _inputHandler.IsClickHotkeyActiveForCurrentInputState();
            (Color color, string statusText) = BuildStatus(clicking);

            _deferredTextQueue.Enqueue(statusText, new Vector2(centerX, topY + (36f * 1.2f)), color, 24, FontAlign.Center);
        }

        internal static float ResolveTopY(bool lazyModeEnabled)
            => lazyModeEnabled ? 130f : TitleY;

        internal static (Color Color, string StatusText) BuildStatus(bool clicking)
            => clicking
                ? (SharpDX.Color.LawnGreen, ClickingText)
                : (SharpDX.Color.Red, NotClickingText);
    }
}