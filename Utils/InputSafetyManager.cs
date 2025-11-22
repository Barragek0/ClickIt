using ExileCore;
using ExileCore.Shared;
using System;

namespace ClickIt.Utils
{
    public class InputSafetyManager
    {
        private readonly ClickItSettings _settings;
        private readonly ErrorHandler _errorHandler;
        private readonly PluginContext _state;

        public InputSafetyManager(ClickItSettings settings, PluginContext state, ErrorHandler errorHandler)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
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
                        _errorHandler.LogMessage(false, false, "SafeBlockInput: BlockInput(true) returned false - input may not be blocked", 10);
                    }
                    _state.IsInputCurrentlyBlocked = true;
                    _errorHandler.LogMessage(true, true, "SafeBlockInput: input blocked", 5);
                }
            }
            else
            {
                if (_state.IsInputCurrentlyBlocked)
                {
                    bool result = Mouse.blockInput(false);
                    if (!result)
                    {
                        _errorHandler.LogMessage(false, false, "SafeBlockInput: BlockInput(false) returned false - input may still be blocked", 10);
                    }
                    _state.IsInputCurrentlyBlocked = false;
                    _errorHandler.LogMessage(true, true, "SafeBlockInput: input unblocked", 5);
                }
            }
        }

        public void ForceUnblockInput(string reason)
        {
            Mouse.blockInput(false);
            _state.IsInputCurrentlyBlocked = false;
            _errorHandler.LogError($"CRITICAL: Input forcibly unblocked. Reason: {reason}", 10);
        }
    }
}