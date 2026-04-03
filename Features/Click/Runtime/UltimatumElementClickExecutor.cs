namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumElementClickExecutor
    {
        internal static bool TryClickElement(
            RectangleF rect,
            Element element,
            Vector2 windowTopLeft,
            string outsideWindowLog,
            string rejectedClickableAreaLogPrefix,
            string clickLog,
            Func<string, bool> ensureCursorInsideGameWindowForClick,
            Func<Vector2, string, bool> isClickableInEitherSpace,
            Action<string> debugLog,
            Action<Vector2, Element> performLockedClick,
            Action recordClickInterval)
        {
            if (!ensureCursorInsideGameWindowForClick(outsideWindowLog))
                return false;

            if (!isClickableInEitherSpace(rect.Center, "Ultimatum"))
            {
                debugLog($"{rejectedClickableAreaLogPrefix} center={rect.Center}");
                return false;
            }

            Vector2 clickPos = rect.Center + windowTopLeft;
            debugLog($"{clickLog} {clickPos}");

            performLockedClick(clickPos, element);
            recordClickInterval();
            return true;
        }
    }
}