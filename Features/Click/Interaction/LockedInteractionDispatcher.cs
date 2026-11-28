namespace ClickIt.Features.Click.Interaction
{
    internal sealed class LockedInteractionDispatcher(InteractionExecutor interactionExecutor)
    {
        private readonly InteractionExecutor _interactionExecutor = interactionExecutor;

        internal object ElementLock { get; } = new();

        internal bool PerformGuardedClick(Vector2 point, GameController? controller, IntervalKind interval, Func<Element?, bool> validate, bool allowWhenHotkeyInactive)
        {
            using (LockManager.AcquireStatic(ElementLock))
                return _interactionExecutor.PerformClick(point, gameController: controller, allowWhenHotkeyInactive: allowWhenHotkeyInactive, interval: interval, validateSettledHover: validate);
        }

        internal bool PerformClick(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false,
            IntervalKind interval = IntervalKind.Click,
            Func<Element?, bool>? validateSettledHover = null,
            bool allowWhenPaused = false,
            Func<bool>? canDispatchClick = null)
        {
            using (LockManager.AcquireStatic(ElementLock))
            {
                return _interactionExecutor.PerformClick(clickPos, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove, interval, validateSettledHover, allowWhenPaused, canDispatchClick);
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
