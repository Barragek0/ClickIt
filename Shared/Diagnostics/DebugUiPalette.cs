using NumVec4 = System.Numerics.Vector4;

namespace ClickIt.Shared.Diagnostics
{
    // Shared debug-overlay chrome colors (Info/Error/Warn/Green/Muted/Dim/White/Orange/Header). File-specific semantic colors (plan actions, region rects, perf labels) stay in their owners.
    internal static class DebugUiPalette
    {
        internal static readonly NumVec4 CWarn = Vec4(Color.Yellow);
        internal static readonly NumVec4 CError = Vec4(Color.Red);
        internal static readonly NumVec4 CInfo = Vec4(Color.Cyan);
        internal static readonly NumVec4 CMuted = Vec4(Color.LightGray);
        internal static readonly NumVec4 CDim = Vec4(Color.DarkGray);
        internal static readonly NumVec4 COrange = Vec4(Color.Orange);
        internal static readonly NumVec4 CHeader = Vec4(Color.Orange);
        internal static readonly NumVec4 CWhite = Vec4(Color.White);
        internal static readonly NumVec4 CGreen = Vec4(Color.LightGreen);

        internal static NumVec4 Vec4(Color c)
            => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
    }
}
