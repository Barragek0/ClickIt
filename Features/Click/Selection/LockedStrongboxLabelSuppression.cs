namespace ClickIt.Features.Click.Selection
{
    // Fresh lock-state check for the label scan: the selection caches can serve a stale "clickable" result for a freshly-locked strongbox, and the scan must skip it to reach the next label.
    internal static class LockedStrongboxLabelSuppression
    {
        internal static bool ShouldSuppress(LabelOnGround? label, string? entityPath = null)
        {
            // Read via DynamicAccess (like TryGetLabelItemOnGround) so the fresh lock-state check works on any label wrapper, not just live game labels.
            if (!DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item))
                return false;

            if (entityPath != null)
            {
                if (!entityPath.Contains("strongbox", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else
            {
                if (!DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path)
                    || !path.Contains("strongbox", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return MechanicClassifier.IsLockedStrongbox(item);
        }
    }
}
