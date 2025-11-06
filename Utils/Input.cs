
using System.Numerics;
using System.Windows.Forms;

namespace ClickIt.Utils
{
    internal static class Input
    {
        public static void SetCursorPos(Vector2 position)
        {
            Mouse.SetCursorPos((int)position.X, (int)position.Y);
        }

        public static bool GetKeyState(Keys key)
        {
            return Keyboard.IsKeyDown(key);
        }
    }
}