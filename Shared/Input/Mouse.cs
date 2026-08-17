namespace ClickIt.Shared.Input
{
    internal class Mouse
    {
        public static bool DisableNativeInput;

        [DllImport("user32.dll")]
        private static extern bool NativeSetCursorPos(int x, int y);

        public static bool SetCursorPos(int x, int y)
        {
            if (DisableNativeInput) return true;
            return NativeSetCursorPos(x, y);
        }
        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        public const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const int MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int MOVEMENT_DELAY = 10;
        private const int CLICK_DELAY = 1;
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
            public static implicit operator SystemDrawingPoint(POINT point)
            {
                return new SystemDrawingPoint(point.X, point.Y);
            }
        }
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);
        public static SystemDrawingPoint GetCursorPosition()
        {
            GetCursorPos(out POINT lpPoint);
            return lpPoint;
        }
        public static void LeftMouseDown()
        {
            if (DisableNativeInput) return;
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        }
        public static void LeftMouseUp()
        {
            if (DisableNativeInput) return;
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }
        public static void RightMouseDown()
        {
            if (DisableNativeInput) return;
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
        }
        public static void RightMouseUp()
        {
            if (DisableNativeInput) return;
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
        }
        public static void LeftClick()
        {
            if (DisableNativeInput) return;
            LeftMouseDown();
            Thread.Sleep(CLICK_DELAY);
            LeftMouseUp();
        }
        public static void RightClick()
        {
            if (DisableNativeInput) return;
            RightMouseDown();
            Thread.Sleep(CLICK_DELAY);
            RightMouseUp();
        }
    }
}
