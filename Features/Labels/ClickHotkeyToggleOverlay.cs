namespace ClickIt.Features.Labels
{
    /// <summary>
    /// Owns the click-hotkey-toggle status text overlay (Clicking / Not Clicking). Pure per-frame.
    /// </summary>
    public sealed class ClickHotkeyToggleOverlay : IOverlay
    {
        private const string ClickingText = "Clicking";
        private const string NotClickingText = "Not Clicking";
        private const float TitleY = 60f;

        private readonly InputHandler _inputHandler;

        public ClickHotkeyToggleOverlay(InputHandler inputHandler)
        {
            _inputHandler = inputHandler;
        }

        public string Name => "ClickHotkeyToggle";

        public RenderSection Section => RenderSection.ClickHotkeyToggle;

        public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.None;

        public TimingChannel? RefreshTimingChannel => null;

        public ProcessingSection ProcessingSection => ProcessingSection.Unknown;

        public bool IsEnabled(ClickItSettings settings)
            => settings.IsClickHotkeyToggleModeEnabled();

        public void Refresh(OverlayRefreshContext ctx)
        {
        }

        public void Draw(OverlayRenderContext ctx)
        {
            RectangleF windowRect = ctx.WindowArea;
            float centerX = windowRect.X + (windowRect.Width / 2f);
            float topY = ResolveTopY(ctx.Settings.LazyMode.Value);

            bool clicking = _inputHandler.IsClickHotkeyActiveForCurrentInputState();
            (Color color, string statusText) = BuildStatus(clicking);

            ctx.TextQueue.Enqueue(statusText, new Vector2(centerX, topY + (36f * 1.2f)), color, 24, FontAlign.Center);
        }

        internal static float ResolveTopY(bool lazyModeEnabled)
            => lazyModeEnabled ? 130f : TitleY;

        internal static (Color Color, string StatusText) BuildStatus(bool clicking)
            => clicking
                ? (Color.LawnGreen, ClickingText)
                : (Color.Red, NotClickingText);
    }
}
