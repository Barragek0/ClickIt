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
        /// Registers global exception handlers for unhandled exceptions and unobserved task exceptions.
        /// </summary>
        public void RegisterGlobalExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    LogError($"Unhandled exception ({ex.GetType().Name}): {ex.Message}", 10);
                    LogError($"Stack: {ex.StackTrace}", 10);
                }
                LogError($"UnhandledException Event: IsTerminating={e.IsTerminating}", 10);
                _forceUnblockInput("Unhandled exception");
            };

            TaskScheduler.UnobservedTaskException += (s, evt) =>
            {
                evt.SetObserved();
                var ex = evt.Exception;
                if (ex != null)
                {
                    LogError($"Unobserved Task Exception: {ex.GetType().Name}: {ex.Message}", 10);
                    LogError($"Stack: {ex.StackTrace}", 10);
                }
                _forceUnblockInput("Unobserved task exception");
            };
        }

        /// <summary>
        /// Logs an error message with automatic wrapping for long messages.
        /// </summary>
        public void LogError(string message, int frame = 0)
        {
            if (_settings.DebugMode)
            {
                LogErrorWithWrapping(message, frame);
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

        /// <summary>
        /// Executes an action with error handling and optional recovery.
        /// </summary>
        public bool TryExecute(Action action, string operationName, Action? onError = null)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                LogError($"{operationName} failed: {ex.GetType().Name}: {ex.Message}", 10);
                LogError($"Stack: {ex.StackTrace}", 10);
                onError?.Invoke();
                return false;
            }
        }

        /// <summary>
        /// Executes an action with error handling and optional recovery, returning a result.
        /// </summary>
        public T? TryExecute<T>(Func<T> action, string operationName, Action? onError = null)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                LogError($"{operationName} failed: {ex.GetType().Name}: {ex.Message}", 10);
                LogError($"Stack: {ex.StackTrace}", 10);
                onError?.Invoke();
                return default;
            }
        }

        private void LogErrorWithWrapping(string message, int frame = 0)
        {
            const int maxLineLength = 100; // Maximum characters per line to avoid going off screen

            if (message.Length <= maxLineLength)
            {
                _logError(message, frame);
                return;
            }

            // Split long message into multiple lines
            int startIndex = 0;
            int lineNumber = 1;

            while (startIndex < message.Length)
            {
                int remainingLength = message.Length - startIndex;
                int currentLineLength = Math.Min(maxLineLength, remainingLength);

                // Try to break at a space to avoid splitting words
                if (currentLineLength < remainingLength)
                {
                    int lastSpaceIndex = message.LastIndexOf(' ', startIndex + currentLineLength, currentLineLength);
                    if (lastSpaceIndex > startIndex)
                    {
                        currentLineLength = lastSpaceIndex - startIndex;
                    }
                }

                string line = message.Substring(startIndex, currentLineLength).TrimEnd();

                // Add line number prefix for continuation lines
                if (lineNumber == 1)
                {
                    _logError(line, frame);
                }
                else
                {
                    _logError($"  [{lineNumber}] {line}", frame);
                }

                startIndex += currentLineLength;
                // Skip leading spaces on continuation lines
                while (startIndex < message.Length && message[startIndex] == ' ')
                {
                    startIndex++;
                }

                lineNumber++;
            }
        }

        private void TrackError(string errorMessage)
        {
            string timestampedError = $"[{DateTime.Now:HH:mm:ss}] {errorMessage}";
            lock (_recentErrors)
            {
                _recentErrors.Add(timestampedError);
                if (_recentErrors.Count > MAX_ERRORS_TO_TRACK)
                {
                    _recentErrors.RemoveAt(0);
                }
            }
        }
    }
}