using ClickIt.Services.Click.Interaction;
using ClickIt.Services.Click.Safety;
using ClickIt.Utils;
using ExileCore;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services.Click.Runtime
{
    internal sealed class ClickFacadeSupport(
        ClickItSettings settings,
        GameController gameController,
        ErrorHandler errorHandler,
        Func<Vector2, string, bool> pointIsInClickableArea,
        IClickSafetyPolicy clickSafetyPolicy,
        LockedInteractionDispatcher lockedInteractionDispatcher,
        PerformanceMonitor performanceMonitor,
        Action<string> publishRuntimeLog,
        Action<string, int>? freezeDebugTelemetrySnapshot)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly GameController _gameController = gameController;
        private readonly ErrorHandler _errorHandler = errorHandler;
        private readonly Func<Vector2, string, bool> _pointIsInClickableArea = pointIsInClickableArea;
        private readonly IClickSafetyPolicy _clickSafetyPolicy = clickSafetyPolicy;
        private readonly LockedInteractionDispatcher _lockedInteractionDispatcher = lockedInteractionDispatcher;
        private readonly PerformanceMonitor _performanceMonitor = performanceMonitor;
        private readonly Action<string> _publishRuntimeLog = publishRuntimeLog;
        private readonly Action<string, int>? _freezeDebugTelemetrySnapshot = freezeDebugTelemetrySnapshot;
        private IInteractionExecutionRuntime? _interactionExecutionRuntime;

        public IInteractionExecutionRuntime InteractionExecutionRuntime => _interactionExecutionRuntime ??= new InteractionExecutionRuntime(
            new InteractionExecutionRuntimeDependencies(
                EnsureCursorInsideGameWindowForClick,
                _lockedInteractionDispatcher.PerformClick,
                _lockedInteractionDispatcher.PerformHoldClick,
                _performanceMonitor.RecordClickInterval));

        public void DebugLog(string message)
        {
            if (_settings.DebugMode?.Value != true)
                return;

            _publishRuntimeLog(message);

            if (_settings.LogMessages?.Value == true)
                _errorHandler.LogMessage(message);
        }

        public bool IsClickableInEitherSpace(Vector2 clientPoint, string path)
        {
            RectangleF windowArea = _gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            return _clickSafetyPolicy.IsPointClickableInEitherSpace(clientPoint, windowTopLeft, _pointIsInClickableArea, path);
        }

        public bool IsInsideWindowInEitherSpace(Vector2 point)
        {
            RectangleF windowArea = _gameController.Window.GetWindowRectangleTimeCache;
            return ClickLabelSelectionMath.IsInsideWindowInEitherSpace(point, windowArea);
        }

        public bool EnsureCursorInsideGameWindowForClick(string outsideWindowLogMessage)
        {
            if (_settings.VerifyCursorInGameWindowBeforeClick?.Value == true && !IsCursorInsideGameWindow())
            {
                DebugLog(outsideWindowLogMessage);
                return false;
            }

            return true;
        }

        public void HoldDebugTelemetryAfterSuccessfulInteraction(string reason)
        {
            if (_settings.DebugMode?.Value != true || _settings.RenderDebug?.Value != true)
                return;

            int holdDurationMs = Math.Max(0, _settings.DebugFreezeSuccessfulInteractionMs?.Value ?? 0);
            if (holdDurationMs <= 0)
                return;

            _freezeDebugTelemetrySnapshot?.Invoke(reason, holdDurationMs);
        }

        private bool IsCursorInsideGameWindow()
        {
            try
            {
                var winRect = _gameController?.Window.GetWindowRectangleTimeCache;
                if (winRect == null)
                    return true;

                var cursor = Mouse.GetCursorPosition();
                return _clickSafetyPolicy.IsCursorInsideWindow(winRect.Value, new Vector2(cursor.X, cursor.Y));
            }
            catch
            {
                return true;
            }
        }
    }
}