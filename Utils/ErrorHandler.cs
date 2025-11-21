using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ClickIt.Utils
{
    /// <summary>
    /// Centralized error handling and logging system for the ClickIt plugin.
    /// Provides consistent error management, logging, and safety mechanisms.
    /// </summary>
    public class ErrorHandler
    {
        private readonly ClickItSettings _settings;
        private readonly Action<string, int> _logError;
        private readonly Action<string, int> _logMessage;
        private readonly Action<bool> _safeBlockInput;
        private readonly Action<string> _forceUnblockInput;

        private readonly List<string> _recentErrors = new List<string>();
        private const int MAX_ERRORS_TO_TRACK = 10;

        public IReadOnlyList<string> RecentErrors => _recentErrors.AsReadOnly();

        public ErrorHandler(
            ClickItSettings settings,
            Action<string, int> logError,
            Action<string, int> logMessage,
            Action<bool> safeBlockInput,
            Action<string> forceUnblockInput)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logError = logError ?? throw new ArgumentNullException(nameof(logError));
            _logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
            _safeBlockInput = safeBlockInput ?? throw new ArgumentNullException(nameof(safeBlockInput));
            _forceUnblockInput = forceUnblockInput ?? throw new ArgumentNullException(nameof(forceUnblockInput));
        }

        /// <summary>
        /// Registers global exception handlers to improve crash visibility and safe cleanup.
        /// </summary>
        public void RegisterGlobalExceptionHandlers()
        {
            // Register global unhandled exception handler
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;

            // Register unobserved task exception handler
            TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;
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

            // Attempt safe cleanup
            SafeForceUnblockInput("Global exception handler");
        }

        private void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            string taskMessage = $"[Global] Unobserved task exception: {e.Exception?.Message ?? "Unknown task exception"}";
            LogWithFallback(taskMessage);
            e.SetObserved(); // Mark as observed to prevent re-throwing

            // Attempt safe cleanup
            SafeForceUnblockInput("Unobserved task exception handler");
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
                    System.IO.File.AppendAllText("ClickIt_Crash.log", $"{DateTime.Now}: {message}\n");
                }
                catch { }
            }
        }

        private void SafeForceUnblockInput(string reason)
        {
            try
            {
                _forceUnblockInput(reason);
            }
            catch
            {
                // Ignore cleanup failures
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
            _recentErrors.Add(message);
            if (_recentErrors.Count > MAX_ERRORS_TO_TRACK)
            {
                _recentErrors.RemoveAt(0);
            }
        }

        /// <summary>
        /// Handles input blocking with safety checks.
        /// </summary>
        public void SafeBlockInput(bool block)
        {
            _safeBlockInput(block);
        }

        /// <summary>
        /// Forces input unblocking with logging.
        /// </summary>
        public void ForceUnblockInput(string reason)
        {
            _forceUnblockInput(reason);
        }
    }
}