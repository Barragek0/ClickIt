namespace ClickIt.Shared.Game
{
    internal static class GameWindowRectResolver
    {
        private const int GWL_STYLE = -16;

        private const long WS_CAPTION = 0x00C00000L;
        private const long WS_THICKFRAME = 0x00040000L;

        private static readonly TimedValueCache<nint, RectangleF> RectCache = new(50);
        private static readonly TimedValueCache<nint, bool> WindowedModeCache = new(250);

        internal readonly record struct WindowRectDebugInfo(
            string WindowMode,
            bool HandleAvailable,
            bool IsWindowed,
            bool OverrideEligible,
            bool OverrideApplied,
            bool ClientRectReadSucceeded,
            bool ResolvedMatchesNormalizedFallback,
            bool NormalizationAdjustedFallback,
            RectangleF RawCacheRect,
            RectangleF NormalizedCacheRect,
            RectangleF ResolvedRect);

        public static RectangleF Resolve(GameController? gameController)
        {
            if (gameController?.Window == null)
                return RectangleF.Empty;

            RectangleF fallback = NormalizeWindowRectangle(gameController.Window.GetWindowRectangleTimeCache);
            nint handle = ResolveProcessWindowHandle(gameController.Window);
            if (handle == nint.Zero)
                return fallback;

            long nowMs = Environment.TickCount64;
            if (RectCache.TryGetValue(handle, nowMs, out RectangleF cachedRect))
                return cachedRect;

            bool isWindowed = WindowedModeCache.TryGetValue(handle, nowMs, out bool cachedIsWindowed)
                ? cachedIsWindowed
                : ResolveAndCacheWindowedMode(handle, nowMs);

            if (!isWindowed)
            {
                RectCache.SetValue(handle, nowMs, fallback);
                return fallback;
            }

            RectangleF resolved = TryGetAbsoluteClientRectangle(handle, out RectangleF corrected)
                && corrected.Width > 0f
                && corrected.Height > 0f
                ? corrected
                : fallback;

            resolved = NormalizeWindowRectangle(resolved);

            RectCache.SetValue(handle, nowMs, resolved);
            return resolved;
        }

        public static string ResolveWindowModeDebugLabel(GameController? gameController)
        {
            if (gameController?.Window == null)
                return "Unknown (window unavailable)";

            nint handle = ResolveProcessWindowHandle(gameController.Window);
            if (handle == nint.Zero)
                return "Unknown (missing window handle)";

            long nowMs = Environment.TickCount64;
            bool isWindowed = WindowedModeCache.TryGetValue(handle, nowMs, out bool cachedIsWindowed)
                ? cachedIsWindowed
                : ResolveAndCacheWindowedMode(handle, nowMs);

            return isWindowed ? "Windowed" : "Borderless/Fullscreen";
        }

        public static WindowRectDebugInfo ResolveDebugInfo(GameController? gameController)
        {
            if (gameController?.Window == null)
            {
                return new WindowRectDebugInfo(
                    WindowMode: "Unknown (window unavailable)",
                    HandleAvailable: false,
                    IsWindowed: false,
                    OverrideEligible: false,
                    OverrideApplied: false,
                    ClientRectReadSucceeded: false,
                    ResolvedMatchesNormalizedFallback: true,
                    NormalizationAdjustedFallback: false,
                    RawCacheRect: RectangleF.Empty,
                    NormalizedCacheRect: RectangleF.Empty,
                    ResolvedRect: RectangleF.Empty);
            }

            RectangleF rawFallback = gameController.Window.GetWindowRectangleTimeCache;
            RectangleF normalizedFallback = NormalizeWindowRectangle(rawFallback);
            bool normalizationAdjustedFallback = !rawFallback.Equals(normalizedFallback);

            nint handle = ResolveProcessWindowHandle(gameController.Window);
            bool handleAvailable = handle != nint.Zero;
            if (!handleAvailable)
            {
                return new WindowRectDebugInfo(
                    WindowMode: "Unknown (missing window handle)",
                    HandleAvailable: false,
                    IsWindowed: false,
                    OverrideEligible: false,
                    OverrideApplied: false,
                    ClientRectReadSucceeded: false,
                    ResolvedMatchesNormalizedFallback: true,
                    NormalizationAdjustedFallback: normalizationAdjustedFallback,
                    RawCacheRect: rawFallback,
                    NormalizedCacheRect: normalizedFallback,
                    ResolvedRect: normalizedFallback);
            }

            long nowMs = Environment.TickCount64;
            bool isWindowed = WindowedModeCache.TryGetValue(handle, nowMs, out bool cachedIsWindowed)
                ? cachedIsWindowed
                : ResolveAndCacheWindowedMode(handle, nowMs);

            bool clientRectReadSucceeded = TryGetAbsoluteClientRectangle(handle, out RectangleF corrected)
                && corrected.Width > 0f
                && corrected.Height > 0f;

            RectangleF resolved = Resolve(gameController);
            bool overrideEligible = isWindowed;
            bool overrideApplied = overrideEligible && clientRectReadSucceeded;
            bool resolvedMatchesNormalizedFallback = resolved.Equals(normalizedFallback);

            return new WindowRectDebugInfo(
                WindowMode: isWindowed ? "Windowed" : "Borderless/Fullscreen",
                HandleAvailable: true,
                IsWindowed: isWindowed,
                OverrideEligible: overrideEligible,
                OverrideApplied: overrideApplied,
                ClientRectReadSucceeded: clientRectReadSucceeded,
                ResolvedMatchesNormalizedFallback: resolvedMatchesNormalizedFallback,
                NormalizationAdjustedFallback: normalizationAdjustedFallback,
                RawCacheRect: rawFallback,
                NormalizedCacheRect: normalizedFallback,
                ResolvedRect: resolved);
        }

        internal static bool IsLikelyWindowed(long style)
            => (style & WS_CAPTION) != 0 || (style & WS_THICKFRAME) != 0;

        internal static RectangleF NormalizeWindowRectangle(RectangleF raw)
        {
            System.Drawing.Rectangle virtualDesktop = SystemInformation.VirtualScreen;
            RectangleF virtualBounds = new(virtualDesktop.X, virtualDesktop.Y, virtualDesktop.Width, virtualDesktop.Height);
            return NormalizeWindowRectangle(raw, virtualBounds);
        }

        internal static RectangleF NormalizeWindowRectangle(RectangleF raw, RectangleF virtualBounds)
        {
            if (raw.Width <= 0f || raw.Height <= 0f)
                return raw;

            if (!(raw.Width > raw.X && raw.Height > raw.Y))
                return raw;

            float desktopRight = virtualBounds.X + virtualBounds.Width;
            float desktopBottom = virtualBounds.Y + virtualBounds.Height;
            float interpretedRight = raw.X + raw.Width;
            float interpretedBottom = raw.Y + raw.Height;

            const float tolerancePx = 16f;
            bool suspiciousAsSize = interpretedRight > desktopRight + tolerancePx
                || interpretedBottom > desktopBottom + tolerancePx;
            if (!suspiciousAsSize)
                return raw;

            float normalizedWidth = raw.Width - raw.X;
            float normalizedHeight = raw.Height - raw.Y;
            if (normalizedWidth <= 0f || normalizedHeight <= 0f)
                return raw;

            return new RectangleF(raw.X, raw.Y, normalizedWidth, normalizedHeight);
        }

        private static nint ResolveProcessWindowHandle(GameWindow gameWindow)
        {
            try
            {
                Process? process = gameWindow.Process;
                return process?.MainWindowHandle ?? nint.Zero;
            }
            catch
            {
                return nint.Zero;
            }
        }

        private static bool TryGetWindowStyle(nint handle, out long style)
        {
            style = 0;

            try
            {
                style = IntPtr.Size == 8
                    ? GetWindowLongPtr(handle, GWL_STYLE).ToInt64()
                    : GetWindowLong(handle, GWL_STYLE);
                return style != 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool ResolveAndCacheWindowedMode(nint handle, long nowMs)
        {
            bool isWindowed = TryGetWindowStyle(handle, out long style) && IsLikelyWindowed(style);

            WindowedModeCache.SetValue(handle, nowMs, isWindowed);

            return isWindowed;
        }

        private static bool TryGetAbsoluteClientRectangle(nint handle, out RectangleF rect)
        {
            rect = RectangleF.Empty;

            if (!GetClientRect(handle, out RECT clientRect))
                return false;

            POINT topLeft = default;
            if (!ClientToScreen(handle, ref topLeft))
                return false;

            float width = clientRect.Right - clientRect.Left;
            float height = clientRect.Bottom - clientRect.Top;
            if (width <= 0f || height <= 0f)
                return false;

            rect = new RectangleF(topLeft.X, topLeft.Y, width, height);
            return true;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong(nint hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(nint hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClientToScreen(nint hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
    }
}
