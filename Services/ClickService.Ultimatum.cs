using ClickIt.Services.Click.Application;
using ClickIt.Services.Click.Runtime;
using ExileCore.PoEMemory.Elements;
using SharpDX;

#nullable enable

namespace ClickIt.Services
{
    public partial class ClickService
    {
        public bool TryGetUltimatumOptionPreview(out List<UltimatumPanelOptionPreview> previews)
            => UltimatumAutomation.TryGetOptionPreview(out previews);

        private bool TryClickPreferredUltimatumModifier(LabelOnGround label, Vector2 windowTopLeft)
            => UltimatumAutomation.TryClickPreferredModifier(label, windowTopLeft);

    }
}
