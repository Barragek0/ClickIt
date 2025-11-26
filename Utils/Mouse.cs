using SharpDX;
using RectangleF = SharpDX.RectangleF;
using SystemDrawingPoint = System.Drawing.Point;
using System.Runtime.InteropServices;
using System.Threading;
namespace ClickIt.Utils
{
    internal class Mouse
    {
        // Under normal runtime we call into user32.dll to change the OS cursor.
        // For unit tests we want to avoid touching native input. Use the wrapper
        // `SetCursorPos` and toggle `DisableNativeInput` to suppress native calls.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1411:Validate platform invocation arguments", Justification = "P/Invoke declaration")]
        [DllImport("user32.dll")]
        private static extern bool NativeSetCursorPos(int x, int y);

        private static volatile bool _disableNativeInput = false;
        public static bool DisableNativeInput
        {
            get => _disableNativeInput;
            set => _disableNativeInput = value;
        }

        public static bool SetCursorPos(int x, int y)
        {
            if (_disableNativeInput)
            {
                // Avoid moving the OS cursor during tests or CI runs.
                return true;
            }
            return NativeSetCursorPos(x, y);
        }
        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        public const int MOUSEEVENTF_MIDDOWN = 0x0020;
        public const int MOUSEEVENTF_MIDUP = 0x0040;
        public const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const int MOUSEEVENTF_RIGHTUP = 0x0010;
        public const int MOUSE_EVENT_WHEEL = 0x800;
        private const int MOVEMENT_DELAY = 10;
        private const int CLICK_DELAY = 1;
        public static bool SetCursorPos(int x, int y, RectangleF gameWindow)
        {
            return SetCursorPos(x + (int)gameWindow.X, y + (int)gameWindow.Y);
        }
        public static bool SetCurosPosToCenterOfRec(RectangleF position, RectangleF gameWindow)
        {
            return SetCursorPos((int)(gameWindow.X + position.Center.X),
                (int)(gameWindow.Y + position.Center.Y));
        }
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
            POINT lpPoint;
            GetCursorPos(out lpPoint);
            return lpPoint;
        }
        public static void LeftMouseDown()
        {
            if (!_disableNativeInput)
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            }
        }
        public static void LeftMouseUp()
        {
            if (!_disableNativeInput)
            {
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            }
        }
        public static void RightMouseDown()
        {
            if (!_disableNativeInput)
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
            }
        }
        public static void RightMouseUp()
        {
            if (!_disableNativeInput)
            {
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
            }
        }
        public static void SetCursorPosAndLeftClick(Vector2 pos, int extraDelay, Vector2 offset)
        {
            var posX = (int)(pos.X + offset.X);
            var posY = (int)(pos.Y + offset.Y);
            SetCursorPos(posX, posY);
            Thread.Sleep(MOVEMENT_DELAY + extraDelay);
            if (!_disableNativeInput)
            {
                LeftClick();
            }
        }
        public static void SetCursorPosAndRightClick(Vector2 pos, int extraDelay, Vector2 offset)
        {
            var posX = (int)(pos.X + offset.X);
            var posY = (int)(pos.Y + offset.Y);
            SetCursorPos(posX, posY);
            Thread.Sleep(MOVEMENT_DELAY + extraDelay);
            if (!_disableNativeInput)
            {
                RightClick();
            }
        }
        public static void VerticalScroll(bool forward, int clicks)
        {
            if (_disableNativeInput) return;
            if (forward)
            {
                mouse_event(MOUSE_EVENT_WHEEL, 0, 0, clicks * 120, 0);
            }
            else
            {
                mouse_event(MOUSE_EVENT_WHEEL, 0, 0, -(clicks * 120), 0);
            }
        }
        public static void LeftClick()
        {
            if (_disableNativeInput)
            {
                return;
            }
            LeftMouseDown();
            Thread.Sleep(CLICK_DELAY);
            LeftMouseUp();
        }
        public static void RightClick()
        {
            if (_disableNativeInput)
            {
                return;
            }
            RightMouseDown();
            Thread.Sleep(CLICK_DELAY);
            RightMouseUp();
        }
    }
}
