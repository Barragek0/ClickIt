namespace ClickIt.Features.Click.Interaction
{
    internal sealed class LockedInteractionDispatcher(InteractionExecutor interactionExecutor)
    {
        private readonly InteractionExecutor _interactionExecutor = interactionExecutor;
        private readonly object _elementLock = new();

        internal object ElementLock => _elementLock;

        internal long GetSuccessfulClickSequence()
            => _interactionExecutor.GetSuccessfulClickSequence();

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
                _interactionExecutor.PerformClick(clickPos, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);
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
                _interactionExecutor.PerformClickAndHold(clickPos, holdDurationMs, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);
            }
        }
    }
}