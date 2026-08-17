namespace ClickIt.Features.Click
{
    /// <summary>
    /// Owns the ultimatum option-frame overlay: refresh (preview recompute on the host coroutine)
    /// and per-frame draw of the cached preview rects.
    /// </summary>
    public sealed class UltimatumOverlay : IOverlay
    {
        private const int UltimatumRefreshIntervalMs = 50;

        private readonly Func<List<UltimatumPanelOptionPreview>?> _getPreview;
        private readonly Action _refreshPreview;

        public UltimatumOverlay(Func<List<UltimatumPanelOptionPreview>?> getPreview, Action refreshPreview)
        {
            _getPreview = getPreview;
            _refreshPreview = refreshPreview;
        }

        public string Name => "Ultimatum";

        public RenderSection Section => RenderSection.UltimatumOverlay;

        public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.Throttled(UltimatumRefreshIntervalMs);

        public TimingChannel? RefreshTimingChannel => TimingChannel.Ultimatum;

        public ProcessingSection ProcessingSection => ProcessingSection.Ultimatum;

        public bool IsEnabled(ClickItSettings settings)
            => settings.ShowUltimatumOptionOverlay?.Value == true;

        public void Refresh(OverlayRefreshContext ctx)
            => _refreshPreview();

        public void Draw(OverlayRenderContext ctx)
        {
            List<UltimatumPanelOptionPreview>? previews = _getPreview();
            if (previews == null || previews.Count == 0)
                return;

            int totalPriorities = SystemMath.Max(1, ctx.Settings.GetUltimatumModifierPriority().Count);
            for (int i = 0; i < previews.Count; i++)
            {
                UltimatumPanelOptionPreview preview = previews[i];
                Color color = preview.IsSelected
                    ? Color.LawnGreen
                    : preview.PriorityIndex == int.MaxValue
                        ? new Color(190, 190, 190, 220)
                        : ToSharpDxColor(UltimatumModifiersConstants.GetPriorityGradientColor(preview.PriorityIndex, totalPriorities));
                int thickness = preview.IsSelected ? 4 : 2;

                // Re-project the option element's client rect each frame (cached decision data only) so the frames follow the panel/window instead of lagging a refresh cadence behind.
                RectangleF rect = preview.Element.GetClientRect();
                if (rect.Width <= 0 || rect.Height <= 0)
                    rect = preview.Rect;
                ctx.DrawQueue.EnqueueFrame(rect, color, thickness);
            }
        }

        private static Color ToSharpDxColor(Vector4 color)
        {
            byte r = (byte)SystemMath.Clamp((int)(color.X * 255f), 0, 255);
            byte g = (byte)SystemMath.Clamp((int)(color.Y * 255f), 0, 255);
            byte b = (byte)SystemMath.Clamp((int)(color.Z * 255f), 0, 255);
            byte a = (byte)SystemMath.Clamp((int)(color.W * 255f), 0, 255);
            return new Color(r, g, b, a);
        }
    }
}
