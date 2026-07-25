namespace ClickIt.Features.Click.Label
{
    internal static class LabelClickPointResolutionPolicy
    {
        public static bool ShouldRetryWithoutClickableArea(string? mechanicId)
            => SettlersMechanicPolicy.IsSettlersMechanicId(mechanicId);

        public static bool ShouldAllowSettlersRelaxedFallback(bool hasBackingEntity, bool worldProjectionInWindow)
        {
            if (!hasBackingEntity)
                return false;

            return !worldProjectionInWindow;
        }
    }
}
