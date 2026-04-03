using ClickIt.Shared;
using ExileCore;

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
            float topY = _settings.LazyMode.Value ? 130f : TitleY;

            bool clicking = _inputHandler.IsClickHotkeyActiveForCurrentInputState();
            SharpDX.Color color = clicking ? SharpDX.Color.LawnGreen : SharpDX.Color.Red;
            string statusText = clicking ? ClickingText : NotClickingText;

            _deferredTextQueue.Enqueue(statusText, new SharpDX.Vector2(centerX, topY + (36f * 1.2f)), color, 24, ExileCore.Shared.Enums.FontAlign.Center);
        }
    }
}