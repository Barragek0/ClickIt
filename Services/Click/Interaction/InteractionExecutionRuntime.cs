using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using SharpDX;

namespace ClickIt.Services.Click.Interaction
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
        Action<Vector2, Element?, GameController?, bool, bool, bool> PerformLockedClick,
        Action<Vector2, int, Element?, GameController?, bool, bool, bool> PerformLockedHoldClick,
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

            if (request.UseHoldClick)
            {
                _dependencies.PerformLockedHoldClick(
                    request.ClickPosition,
                    request.HoldDurationMs,
                    request.ExpectedElement,
                    request.Controller,
                    request.ForceUiHoverVerification,
                    request.AllowWhenHotkeyInactive,
                    request.AvoidCursorMove);
            }
            else
            {
                _dependencies.PerformLockedClick(
                    request.ClickPosition,
                    request.ExpectedElement,
                    request.Controller,
                    request.ForceUiHoverVerification,
                    request.AllowWhenHotkeyInactive,
                    request.AvoidCursorMove);
            }

            _dependencies.RecordClickInterval();
            return true;
        }
    }
}
