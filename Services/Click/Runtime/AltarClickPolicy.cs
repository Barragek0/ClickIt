namespace ClickIt.Services.Click.Runtime
{
    internal static class AltarClickPolicy
    {
        internal static bool ShouldEvaluateAltarScan(bool clickEaterEnabled, bool clickExarchEnabled)
            => clickEaterEnabled || clickExarchEnabled;

        internal static bool AreBothAltarOptionsActionable(bool topVisibleAndClickable, bool bottomVisibleAndClickable)
            => topVisibleAndClickable && bottomVisibleAndClickable;
    }
}