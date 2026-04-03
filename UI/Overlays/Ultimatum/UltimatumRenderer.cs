using ClickIt.Features.Click.Application;
using ClickIt.Features.Click.Runtime;
using ClickIt.Shared;
using Color = SharpDX.Color;

namespace ClickIt.UI.Overlays.Ultimatum
{
    public class UltimatumRenderer(ClickItSettings settings, IClickAutomationService? clickAutomationService, DeferredFrameQueue? deferredFrameQueue)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly IClickAutomationService? _clickAutomationService = clickAutomationService;
        private readonly DeferredFrameQueue? _deferredFrameQueue = deferredFrameQueue;

        public void Render()
        {
            if (_settings.ShowUltimatumOptionOverlay?.Value != true)
                return;

            if (_clickAutomationService == null || _deferredFrameQueue == null)
                return;

            if (!_clickAutomationService.TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews) || previews.Count == 0)
                return;

            int totalPriorities = Math.Max(1, _settings.GetUltimatumModifierPriority().Count);
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
