namespace ClickIt.Features.Click.Interaction
{
    internal sealed class LockedInteractionDispatcher(InputHandler inputHandler)
    {
        private readonly InputHandler _inputHandler = inputHandler;
        private readonly object _elementLock = new();

        internal object ElementLock => _elementLock;

        internal void PerformClick(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
            using (LockManager.AcquireStatic(_elementLock))
            {
                _inputHandler.PerformClick(clickPos, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);
            }
        }

        internal void PerformHoldClick(
            Vector2 clickPos,
            int holdDurationMs,
            Element? expectedElement,
            GameController? controller,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
            using (LockManager.AcquireStatic(_elementLock))
            {
                _inputHandler.PerformClickAndHold(clickPos, holdDurationMs, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);
            }
        }
    }
}