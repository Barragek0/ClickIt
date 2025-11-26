using System;
namespace ClickIt.Utils
{
    // Test seam: allow tests to disable real keyboard events during unit tests / CI
    internal static partial class Keyboard
    {
        private static volatile bool _disableNativeInput = false;
        public static bool DisableNativeInput
        {
            get => _disableNativeInput;
            set => _disableNativeInput = value;
        }
    }
}
