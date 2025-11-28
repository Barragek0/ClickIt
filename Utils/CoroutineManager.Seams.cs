using System;

namespace ClickIt.Utils
{
    public partial class CoroutineManager
    {
        // Test seam to allow unit tests to control key state in a deterministic way.
        internal static Func<System.Windows.Forms.Keys, bool> KeyStateProvider = Keyboard.IsKeyDown;
    }
}
