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
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                string message = exception?.Message ?? "Unknown exception";
                _logError($"[Global] Unhandled exception: {message}", 10);

                if (e.IsTerminating)
                {
                    _logError("[Global] Runtime is terminating", 10);
                }

                // Attempt safe cleanup
                try
                {
                    _forceUnblockInput("Global exception handler");
                }
                catch
                {
                    // Ignore cleanup failures
                }
            };

            // Register unobserved task exception handler
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                _logError($"[Global] Unobserved task exception: {e.Exception?.Message ?? "Unknown task exception"}", 10);
                e.SetObserved(); // Mark as observed to prevent re-throwing

                // Attempt safe cleanup
                try
                {
                    _forceUnblockInput("Unobserved task exception handler");
                }
                catch
                {
                    // Ignore cleanup failures
                }
            };
        }


    }
}