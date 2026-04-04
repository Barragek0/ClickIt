namespace ClickIt.Features.Click
{
    internal readonly record struct ClickAutomationSupportDependencies(
        ClickItSettings Settings,
        ClickTelemetryStore TelemetryStore,
        Func<RectangleF> GetWindowRectangle,
        Func<Vector2> GetCursorPosition,
        Func<Vector2, string, bool> PointIsInClickableArea,
        Action<string> LogMessage,
        Action<string, int>? FreezeDebugTelemetrySnapshot);

    internal sealed class ClickAutomationSupport(ClickAutomationSupportDependencies dependencies)
    {
        private readonly ClickAutomationSupportDependencies _dependencies = dependencies;
        private readonly IClickSafetyPolicy _clickSafetyPolicy = new ClickSafetyPolicy();

        internal ClickDebugSnapshot GetLatestClickDebug()
            => _dependencies.TelemetryStore.GetLatestClickDebug();

        internal IReadOnlyList<string> GetLatestClickDebugTrail()
            => _dependencies.TelemetryStore.GetLatestClickDebugTrail();

        internal RuntimeDebugLogSnapshot GetLatestRuntimeDebugLog()
            => _dependencies.TelemetryStore.GetLatestRuntimeDebugLog();

        internal IReadOnlyList<string> GetLatestRuntimeDebugLogTrail()
            => _dependencies.TelemetryStore.GetLatestRuntimeDebugLogTrail();

        internal UltimatumDebugSnapshot GetLatestUltimatumDebug()
            => _dependencies.TelemetryStore.GetLatestUltimatumDebug();

        internal IReadOnlyList<string> GetLatestUltimatumDebugTrail()
            => _dependencies.TelemetryStore.GetLatestUltimatumDebugTrail();

        internal bool ShouldCaptureClickDebug()
            => _dependencies.Settings.DebugMode.Value && _dependencies.Settings.DebugShowClicking.Value;

        internal bool ShouldCaptureUltimatumDebug()
            => _dependencies.Settings.DebugMode.Value && _dependencies.Settings.DebugShowUltimatum.Value;

        internal void PublishClickSnapshot(ClickDebugSnapshot snapshot)
        {
            if (!ShouldCaptureClickDebug())
                return;

            _dependencies.TelemetryStore.PublishClickSnapshot(snapshot);
        }

        internal void PublishUltimatumSnapshot(UltimatumDebugSnapshot snapshot)
        {
            if (!ShouldCaptureUltimatumDebug())
                return;

            _dependencies.TelemetryStore.PublishUltimatumSnapshot(snapshot);
        }

        internal void PublishUltimatumEvent(UltimatumDebugEvent debugEvent)
        {
            if (!ShouldCaptureUltimatumDebug())
                return;

            _dependencies.TelemetryStore.PublishUltimatumEvent(debugEvent);
        }

        internal void PublishRuntimeLog(string message)
            => _dependencies.TelemetryStore.PublishRuntimeLog(message);

        internal void DebugLog(string message)
        {
            if (_dependencies.Settings.DebugMode?.Value != true)
                return;

            _dependencies.TelemetryStore.PublishRuntimeLog(message);

            if (_dependencies.Settings.LogMessages?.Value == true)
                _dependencies.LogMessage(message);
        }

        internal bool IsClickableInEitherSpace(Vector2 clientPoint, string path)
        {
            RectangleF windowArea = _dependencies.GetWindowRectangle();
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            return _clickSafetyPolicy.IsPointClickableInEitherSpace(clientPoint, windowTopLeft, _dependencies.PointIsInClickableArea, path);
        }

        internal bool IsInsideWindowInEitherSpace(Vector2 point)
            => ClickLabelSelectionMath.IsInsideWindowInEitherSpace(point, _dependencies.GetWindowRectangle());

        internal bool EnsureCursorInsideGameWindowForClick(string outsideWindowLogMessage)
        {
            if (_dependencies.Settings.VerifyCursorInGameWindowBeforeClick?.Value == true && !IsCursorInsideGameWindow())
            {
                DebugLog(outsideWindowLogMessage);
                return false;
            }

            return true;
        }

        internal void HoldDebugTelemetryAfterSuccessfulInteraction(string reason)
        {
            if (_dependencies.Settings.DebugMode?.Value != true || _dependencies.Settings.RenderDebug?.Value != true)
                return;

            int holdDurationMs = Math.Max(0, _dependencies.Settings.DebugFreezeSuccessfulInteractionMs?.Value ?? 0);
            if (holdDurationMs <= 0)
                return;

            _dependencies.FreezeDebugTelemetrySnapshot?.Invoke(reason, holdDurationMs);
        }

        internal bool IsCursorInsideGameWindow()
        {
            try
            {
                return _clickSafetyPolicy.IsCursorInsideWindow(_dependencies.GetWindowRectangle(), _dependencies.GetCursorPosition());
            }
            catch
            {
                return true;
            }
        }
    }
}