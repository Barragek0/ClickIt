namespace ClickIt.Features.Click.Selection
{
    // Fresh lock-state check for the label scan: the selection caches can serve a stale "clickable"
    // result for a freshly-locked strongbox, and the scan must skip it to reach the next label.
    internal static class LockedStrongboxLabelSuppression
    {
        internal static bool ShouldSuppress(LabelOnGround? label)
        {
            // Read via DynamicAccess (like TryGetLabelItemOnGround) so the fresh lock-state check
            // works on any label wrapper, not just live game labels.
            Entity? item = DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                ? rawItem as Entity
                : null;
            if (item == null)
                return false;

            if (!DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path)
                || !path.Contains("strongbox", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return MechanicClassifier.IsLockedStrongbox(item);
        }
    }
}
