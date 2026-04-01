namespace ClickIt.Services.Click.Label
{
    internal static class LabelClickPointResolutionPolicy
    {
        public static bool ShouldRetryWithoutClickableArea(string? mechanicId)
        {
            return !string.IsNullOrWhiteSpace(mechanicId)
                && mechanicId.StartsWith("settlers-", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldAllowSettlersRelaxedFallback(bool hasBackingEntity, bool worldProjectionInWindow)
        {
            if (!hasBackingEntity)
                return false;

            return !worldProjectionInWindow;
        }
    }
}
