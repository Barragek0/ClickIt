namespace ClickIt.UI.Overlays.Ultimatum
{
    public class UltimatumRenderer(ClickItSettings settings, Func<List<UltimatumPanelOptionPreview>?>? getPreview, DeferredFrameQueue? deferredFrameQueue)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly Func<List<UltimatumPanelOptionPreview>?>? _getPreview = getPreview;
        private readonly DeferredFrameQueue? _deferredFrameQueue = deferredFrameQueue;

        public void Render()
        {
            if (_settings.ShowUltimatumOptionOverlay?.Value != true)
                return;

            if (_getPreview == null || _deferredFrameQueue == null)
                return;

            List<UltimatumPanelOptionPreview>? previews = _getPreview();
            if (previews == null || previews.Count == 0)
                return;

            int totalPriorities = SystemMath.Max(1, _settings.GetUltimatumModifierPriority().Count);
            for (int i = 0; i < previews.Count; i++)
            {
                UltimatumPanelOptionPreview preview = previews[i];
                Color color = preview.IsSelected
                    ? Color.LawnGreen
                    : preview.PriorityIndex == int.MaxValue
                        ? new Color(190, 190, 190, 220)
                        : ToSharpDxColor(UltimatumModifiersConstants.GetPriorityGradientColor(preview.PriorityIndex, totalPriorities));
                int thickness = preview.IsSelected ? 4 : 2;
                _deferredFrameQueue.Enqueue(preview.Rect, color, thickness);
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
