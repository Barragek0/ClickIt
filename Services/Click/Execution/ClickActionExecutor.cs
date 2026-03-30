using ExileCore;
using ExileCore.PoEMemory;
using SharpDX;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        private sealed class ClickActionExecutor(ClickService owner)
        {
            public bool ExecuteLabelInteraction(
                Vector2 clickPos,
                Element? expectedElement,
                GameController? controller,
                bool useHoldClick,
                int holdDurationMs = 0,
                bool forceUiHoverVerification = false,
                bool allowWhenHotkeyInactive = false,
                bool avoidCursorMove = false)
            {
                return useHoldClick
                    ? owner.PerformLabelHoldClick(
                        clickPos,
                        expectedElement,
                        controller,
                        holdDurationMs,
                        forceUiHoverVerification,
                        allowWhenHotkeyInactive,
                        avoidCursorMove)
                    : owner.PerformLabelClick(
                        clickPos,
                        expectedElement,
                        controller,
                        forceUiHoverVerification,
                        allowWhenHotkeyInactive,
                        avoidCursorMove);
            }
        }
    }
}