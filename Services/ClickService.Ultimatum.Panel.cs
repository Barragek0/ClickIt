using ExileCore.PoEMemory.Elements;
using SharpDX;

#nullable enable

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private bool TryHandleUltimatumPanelUi(Vector2 windowTopLeft)
            => UltimatumAutomation.TryHandlePanelUi(windowTopLeft);
    }
}