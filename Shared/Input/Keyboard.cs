namespace ClickIt.Shared.Input
{
    internal static class Keyboard
    {
        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;
        [DllImport("user32.dll")]
        private static extern uint keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
        public static void KeyDown(Keys key)
        {
            _ = keybd_event((byte)key, 0, KEYEVENTF_EXTENDEDKEY, 0);
        }
        public static void KeyUp(Keys key)
        {
            _ = keybd_event((byte)key, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        }
        public static void KeyPress(Keys key, int delay)
        {
            KeyDown(key);
            Thread.Sleep(delay);
            KeyUp(key);
        }
        public static bool IsKeyDown(Keys key)
        {
            return GetKeyState((int)key) < 0;
        }
    }
}
