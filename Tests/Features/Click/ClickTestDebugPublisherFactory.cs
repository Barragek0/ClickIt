namespace ClickIt.Tests.Features.Click
{
    internal static class ClickTestDebugPublisherFactory
    {
        internal static ClickDebugPublicationService Create(
            Func<bool>? shouldCaptureClickDebug = null,
            Action<ClickDebugSnapshot>? setLatestClickDebug = null,
            Func<Vector2, string, bool>? isClickableInEitherSpace = null,
            Func<Vector2, bool>? isInsideWindowInEitherSpace = null)
        {
            return new ClickDebugPublicationService(new ClickDebugPublicationServiceDependencies(
                GameController: null!,
                ShouldCaptureClickDebug: shouldCaptureClickDebug ?? (static () => false),
                SetLatestClickDebug: setLatestClickDebug ?? (static _ => { }),
                IsClickableInEitherSpace: isClickableInEitherSpace ?? (static (_, _) => false),
                IsInsideWindowInEitherSpace: isInsideWindowInEitherSpace ?? (static _ => false)));
        }
    }
}