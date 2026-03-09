using ClickIt.Constants;
using ClickIt.Services;
using ClickIt.Utils;
using SharpDX;
using Color = SharpDX.Color;

namespace ClickIt.Rendering
{
    public class UltimatumRenderer(ClickItSettings settings, ClickService? clickService, DeferredFrameQueue? deferredFrameQueue)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly ClickService? _clickService = clickService;
        private readonly DeferredFrameQueue? _deferredFrameQueue = deferredFrameQueue;

        public void Render()
        {
            if (_settings.ShowUltimatumOptionOverlay?.Value != true)
                return;

            if (_clickService == null || _deferredFrameQueue == null)
                return;

            if (!_clickService.TryGetUltimatumOptionPreview(out List<ClickService.UltimatumPanelOptionPreview> previews) || previews.Count == 0)
                return;

            int totalPriorities = Math.Max(1, _settings.GetUltimatumModifierPriority().Count);
            for (int i = 0; i < previews.Count; i++)
            {
                ClickService.UltimatumPanelOptionPreview preview = previews[i];
                Color color = preview.IsSelected
                    ? Color.LawnGreen
                    : preview.PriorityIndex == int.MaxValue
                        ? new Color(190, 190, 190, 220)
                        : ToSharpDxColor(UltimatumModifiersConstants.GetPriorityGradientColor(preview.PriorityIndex, totalPriorities));
                int thickness = preview.IsSelected ? 4 : 2;
                _deferredFrameQueue.Enqueue(preview.Rect, color, thickness);
            }
        }

        private static Color ToSharpDxColor(System.Numerics.Vector4 color)
        {
            byte r = (byte)Math.Clamp((int)(color.X * 255f), 0, 255);
            byte g = (byte)Math.Clamp((int)(color.Y * 255f), 0, 255);
            byte b = (byte)Math.Clamp((int)(color.Z * 255f), 0, 255);
            byte a = (byte)Math.Clamp((int)(color.W * 255f), 0, 255);
            return new Color(r, g, b, a);
        }
    }
}
