namespace ClickIt.Features.Click.Interaction
{
    internal sealed class LockedInteractionDispatcher(InteractionExecutor interactionExecutor)
    {
        private readonly InteractionExecutor _interactionExecutor = interactionExecutor;

        internal object ElementLock { get; } = new();

        internal bool PerformClick(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false,
            IntervalKind interval = IntervalKind.Click)
        {
            using (LockManager.AcquireStatic(ElementLock))
            {
                return _interactionExecutor.PerformClick(clickPos, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove, interval);
            }
        }

        internal bool PerformHoldClick(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
            using (LockManager.AcquireStatic(ElementLock))
            {
                return _interactionExecutor.PerformClickAndHold(clickPos, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);
            }
        }
    }
}