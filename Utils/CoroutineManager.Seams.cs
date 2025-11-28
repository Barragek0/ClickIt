namespace ClickIt.Utils
{
    public partial class CoroutineManager
    {
        // Test seam to allow unit tests to control key state in a deterministic way.
        internal static Func<Keys, bool> KeyStateProvider = Keyboard.IsKeyDown;
    }
}
