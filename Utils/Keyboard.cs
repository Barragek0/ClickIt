using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
namespace ClickIt.Utils
{
    internal static partial class Keyboard
    {
        // The DisableNativeInput flag lives in Keyboard.Seams.cs for test-only configuration
        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const int KEY_PRESSED = 0x8000;
        private const int KEY_TOGGLED = 0x0001;
        [DllImport("user32.dll")]
        private static extern uint keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
        public static void KeyDown(Keys key)
        {
            if (_disableNativeInput) return;
            keybd_event((byte)key, 0, KEYEVENTF_EXTENDEDKEY, 0);
        }
        public static void KeyUp(Keys key)
        {
            if (_disableNativeInput) return;
            keybd_event((byte)key, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        }
        public static void KeyPress(Keys key)
        {
            if (_disableNativeInput)
            {
                return;
            }
            KeyDown(key);
            Thread.Sleep(50);
            KeyUp(key);
        }
        public static void KeyPress(Keys key, int delay)
        {
            if (_disableNativeInput)
            {
                return;
            }
            KeyDown(key);
            Thread.Sleep(delay);
            KeyUp(key);
        }
        public static bool IsKeyDown(Keys key)
        {
            if (_disableNativeInput) return false;
            return GetKeyState((int)key) < 0;
        }
        public static bool IsKeyPressed(Keys key)
        {
            if (_disableNativeInput) return false;
            return Convert.ToBoolean(GetKeyState((int)key) & KEY_PRESSED);
        }
        public static bool IsKeyToggled(Keys key)
        {
            if (_disableNativeInput) return false;
            return Convert.ToBoolean(GetKeyState((int)key) & KEY_TOGGLED);
        }
    }
}
