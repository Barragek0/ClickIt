using ExileCore;
using ExileCore.Shared;
using System;

namespace ClickIt.Utils
{
    public class InputSafetyManager
    {
        private readonly ClickItSettings _settings;
        private readonly Action<bool, string, int> _logMessage;
        private readonly Action<string, int> _logError;
        private readonly PluginContext _state;

        public InputSafetyManager(ClickItSettings settings, PluginContext state, Action<bool, string, int> logMessage, Action<string, int> logError)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
            _logError = logError ?? throw new ArgumentNullException(nameof(logError));
        }

        public void SafeBlockInput(bool block)
        {
            if (block)
            {
                if (!_state.IsInputCurrentlyBlocked)
                {
                    bool result = false;
                    result = Mouse.blockInput(true);
                    if (!result)
                    {
                        _logMessage(false, "SafeBlockInput: BlockInput(true) returned false - input may not be blocked", 10);
                    }
                    _state.IsInputCurrentlyBlocked = true;
                    _logMessage(true, "SafeBlockInput: input blocked", 5);
                }
            }
            else
            {
                if (_state.IsInputCurrentlyBlocked)
                {
                    bool result = Mouse.blockInput(false);
                    if (!result)
                    {
                        _logMessage(false, "SafeBlockInput: BlockInput(false) returned false - input may still be blocked", 10);
                    }
                    _state.IsInputCurrentlyBlocked = false;
                    _logMessage(true, "SafeBlockInput: input unblocked", 5);
                }
            }
        }

        public void ForceUnblockInput(string reason)
        {
            Mouse.blockInput(false);
            _state.IsInputCurrentlyBlocked = false;
            _logError($"CRITICAL: Input forcibly unblocked. Reason: {reason}", 10);
        }
    }
}