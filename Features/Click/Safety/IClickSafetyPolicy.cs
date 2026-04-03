namespace ClickIt.Features.Click.Safety
{
    internal interface IClickSafetyPolicy
    {
        bool IsPointClickableInEitherSpace(Vector2 clientPoint, Vector2 windowTopLeft, Func<Vector2, string, bool> clickabilityCheck, string path);

        bool IsCursorInsideWindow(RectangleF windowArea, Vector2 cursorAbsolute);
    }
}
