namespace ClickIt.Features.Click.Interaction
{
    internal readonly record struct InteractionExecutionRequest(
        Vector2 ClickPosition,
        Element? ExpectedElement,
        GameController? Controller,
        bool UseHoldClick,
        int HoldDurationMs,
        bool ForceUiHoverVerification,
        bool AllowWhenHotkeyInactive,
        bool AvoidCursorMove,
        string OutsideWindowLogMessage);

    internal readonly record struct InteractionExecutionRuntimeDependencies(
        Func<string, bool> EnsureCursorInsideGameWindowForClick,
        Func<Vector2, bool> IsClickPositionAllowed,
        Action<string> DebugLog,
        Func<Vector2, Element?, GameController?, bool, bool, bool, bool> PerformLockedClick,
        Func<Vector2, int, Element?, GameController?, bool, bool, bool, bool> PerformLockedHoldClick,
        Action RecordClickInterval);

    internal interface IInteractionExecutionRuntime
    {
        bool Execute(InteractionExecutionRequest request);
    }

    internal sealed class InteractionExecutionRuntime(InteractionExecutionRuntimeDependencies dependencies) : IInteractionExecutionRuntime
    {
        private readonly InteractionExecutionRuntimeDependencies _dependencies = dependencies;

        public bool Execute(InteractionExecutionRequest request)
        {
            if (!_dependencies.EnsureCursorInsideGameWindowForClick(request.OutsideWindowLogMessage))
                return false;

            if (!_dependencies.IsClickPositionAllowed(request.ClickPosition))
            {
                _dependencies.DebugLog($"[InteractionExecutionRuntime] Skipping interaction inside blocked UI rectangle at {request.ClickPosition}.");
                return false;
            }

            // Only treat the interaction as executed when the executor actually sent the click — internal
            // rejections (lazy limiter, hotkey inactive, UIHover mismatch, invalid point) must not run
            // the success aftermath (path/sticky clearing, pending-chest arming, lever cooldowns) or
            // record a click interval for a click that never happened.
            bool clicked = request.UseHoldClick
                ? _dependencies.PerformLockedHoldClick(
                    request.ClickPosition,
                    request.HoldDurationMs,
                    request.ExpectedElement,
                    request.Controller,
                    request.ForceUiHoverVerification,
                    request.AllowWhenHotkeyInactive,
                    request.AvoidCursorMove)
                : _dependencies.PerformLockedClick(
                    request.ClickPosition,
                    request.ExpectedElement,
                    request.Controller,
                    request.ForceUiHoverVerification,
                    request.AllowWhenHotkeyInactive,
                    request.AvoidCursorMove);
            if (!clicked)
                return false;

            _dependencies.RecordClickInterval();
            return true;
        }
    }
}
