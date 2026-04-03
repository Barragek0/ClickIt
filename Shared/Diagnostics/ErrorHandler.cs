using ClickIt.Features.Observability;

namespace ClickIt.Shared.Diagnostics
{
    /// <summary>
    /// Centralized error handling and logging system for the ClickIt plugin.
    /// Provides consistent error management, logging, and safety mechanisms.
    /// </summary>
    public class ErrorHandler(
        ClickItSettings settings,
        Action<string, int> logError,
        Action<string, int> logMessage)
    {
        private readonly ClickItSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        private readonly Action<string, int> _logError = logError ?? throw new ArgumentNullException(nameof(logError));
        private readonly Action<string, int> _logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
        private bool _globalHandlersRegistered;
        private const int MAX_ERRORS_TO_TRACK = 10;
        private readonly DebugSnapshotChannel<ErrorDebugSnapshot, string> _errorDebugChannel = new(
            ErrorDebugSnapshot.Empty,
            MAX_ERRORS_TO_TRACK,
            static (snapshot, sequence) => snapshot with { Sequence = sequence },
            static snapshot => $"{snapshot.Sequence:00000} {snapshot.Message}",
            static message => new ErrorDebugSnapshot(
                HasData: true,
                Message: message,
                Sequence: 0,
                TimestampMs: Environment.TickCount64));

        public IReadOnlyList<string> RecentErrors => _errorDebugChannel.GetTrail();

        /// <summary>
        /// Registers global exception handlers to improve crash visibility and safe cleanup.
        /// </summary>
        public void RegisterGlobalExceptionHandlers()
        {
            if (_globalHandlersRegistered)
                return;

            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;

            TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

            _globalHandlersRegistered = true;
        }

        public void UnregisterGlobalExceptionHandlers()
        {
            if (!_globalHandlersRegistered)
                return;

            AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
            TaskScheduler.UnobservedTaskException -= HandleUnobservedTaskException;
            _globalHandlersRegistered = false;
        }

        private void HandleUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            string message = exception?.Message ?? "Unknown exception";
            string fullMessage = $"[Global] Unhandled exception: {message}";

            LogWithFallback(fullMessage);

            if (e.IsTerminating)
            {
                string terminatingMessage = "[Global] Runtime is terminating";
                LogWithFallback(terminatingMessage);
            }
        }

        private void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            string taskMessage = $"[Global] Unobserved task exception: {e.Exception?.Message ?? "Unknown task exception"}";
            LogWithFallback(taskMessage);
            e.SetObserved(); // Mark as observed to prevent re-throwing
        }

        private void LogWithFallback(string message)
        {
            try
            {
                _logError(message, 10);
            }
            catch
            {
                // Fallback to direct file logging if plugin logging fails
                try
                {
                    File.AppendAllText("ClickIt_Crash.log", $"{DateTime.Now}: {message}\n");
                }
                catch { }
            }
        }

        /// <summary>
        /// Logs an error message with automatic wrapping for long messages.
        /// </summary>
        public void LogError(string message, int frame = 0)
        {
            if (_settings.DebugMode)
            {
                _logError(message, frame);
                TrackError(message);
            }
        }

        /// <summary>
        /// Logs a message with optional local debug requirement.
        /// </summary>
        public void LogMessage(string message, int frame = 5)
        {
            LogMessage(false, true, message, frame);
        }

        /// <summary>
        /// Logs a message with local debug control.
        /// </summary>
        public void LogMessage(bool localDebug, string message, int frame = 0)
        {
            LogMessage(true, localDebug, message, frame);
        }

        /// <summary>
        /// Logs a message with full control over debug requirements.
        /// </summary>
        public void LogMessage(bool requireLocalDebug, bool localDebugFlag, string message, int frame)
        {
            if (requireLocalDebug)
            {
                if (localDebugFlag && _settings.DebugMode && _settings.LogMessages)
                {
                    _logMessage(message, frame);
                }
            }
            else
            {
                if (_settings.DebugMode && _settings.LogMessages)
                {
                    _logMessage(message, frame);
                }
            }
        }

        /// <summary>
        /// Tracks recent errors for debugging purposes.
        /// </summary>
        private void TrackError(string message)
        {
            _errorDebugChannel.PublishEvent(message);
        }

    }
}
