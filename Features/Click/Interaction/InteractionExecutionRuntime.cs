namespace ClickIt.Features.Click.Interaction
{
    internal readonly record struct InteractionExecutionRequest(
        Vector2 ClickPosition,
        Element? ExpectedElement,
        GameController? Controller,
        bool UseHoldClick,
        bool ForceUiHoverVerification,
        bool AllowWhenHotkeyInactive,
        bool AvoidCursorMove,
        string OutsideWindowLogMessage,
        IntervalKind Interval = IntervalKind.Click);

    internal readonly record struct InteractionExecutionRuntimeDependencies(
        Func<string, bool> EnsureCursorInsideGameWindowForClick,
        Func<Vector2, bool> IsClickPositionAllowed,
        Action<string> DebugLog,
        Func<Vector2, Element?, GameController?, bool, bool, bool, IntervalKind, bool> PerformLockedClick,
        Func<Vector2, Element?, GameController?, bool, bool, bool, bool> PerformLockedHoldClick,
        Action RecordClickInterval);

    internal interface IInteractionExecutionRuntime
    {
        /// <summary>
        /// Executes one click interaction through the locked dispatcher. Returns true only when the click
        /// was actually sent to the OS (internal rejections return false so callers skip the success aftermath).
        /// </summary>
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

            // Only treat the interaction as executed when the executor actually sent the click — internal rejections must not run the success aftermath.
            bool clicked = request.UseHoldClick
                ? _dependencies.PerformLockedHoldClick(
                    request.ClickPosition,
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
                    request.AvoidCursorMove,
                    request.Interval);
            if (!clicked)
                return false;

            _dependencies.RecordClickInterval();
            return true;
        }
    }
}
