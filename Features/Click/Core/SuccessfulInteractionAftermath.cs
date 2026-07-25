namespace ClickIt.Features.Click.Core
{
    internal readonly record struct SuccessfulInteractionAftermath(
        string Reason,
        bool ShouldClearStickyTarget = false,
        bool ShouldClearPath = false,
        bool ShouldInvalidateShrineCache = false,
        string? PendingChestMechanicId = null,
        LabelOnGround? PendingChestLabel = null,
        bool ShouldRecordLeverClick = false);

    internal static class SuccessfulInteractionAftermathApplier
    {
        internal static void Apply(
            SuccessfulInteractionAftermath aftermath,
            Action<string> holdDebugTelemetryAfterSuccess,
            Action? clearStickyTarget = null,
            Action? clearPath = null,
            Action? invalidateShrineCache = null,
            Action<string, LabelOnGround>? markPendingChestOpenConfirmation = null,
            Action<LabelOnGround>? recordLeverClick = null)
        {
            holdDebugTelemetryAfterSuccess(aftermath.Reason);

            if (aftermath.ShouldClearStickyTarget)
                clearStickyTarget?.Invoke();

            if (aftermath.ShouldClearPath)
                clearPath?.Invoke();

            if (aftermath.ShouldInvalidateShrineCache)
                invalidateShrineCache?.Invoke();

            if (aftermath.PendingChestLabel != null && !string.IsNullOrWhiteSpace(aftermath.PendingChestMechanicId))
                markPendingChestOpenConfirmation?.Invoke(aftermath.PendingChestMechanicId, aftermath.PendingChestLabel);

            if (aftermath.ShouldRecordLeverClick && aftermath.PendingChestLabel != null)
                recordLeverClick?.Invoke(aftermath.PendingChestLabel);
        }
    }
}