namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumLabelMath
    {
        internal static bool IsUltimatumPath(string? path)
            => Constants.IsUltimatumInteractablePath(path);

        internal static bool TryGetLabelItemPath(LabelOnGround? label, out string path)
        {
            path = string.Empty;

            if (!DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                || rawItem == null)
                return false;

            return DynamicAccess.TryReadString(rawItem, DynamicAccessProfiles.Path, out path);
        }

        internal static bool TryGetLabelRoot(LabelOnGround? label, out object? root)
            => DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out root) && root != null;

        internal static ulong GetLabelElementAddress(LabelOnGround? label)
        {
            if (!TryGetLabelRoot(label, out object? root) || root == null)
                return 0;

            // Fail-closed: a non-numeric Address payload from the obfuscated API must not throw in the selection hot path — treat it as unresolvable (0) instead.
            try
            {
                return DynamicAccess.TryGetDynamicValue(root, DynamicAccessProfiles.Address, out object? rawAddress)
                    && rawAddress != null
                    ? unchecked((ulong)Convert.ToInt64(rawAddress))
                    : 0;
            }
            catch
            {
                return 0;
            }
        }

        internal static bool IsUltimatumLabel(LabelOnGround? label)
        {
            if (!TryGetLabelItemPath(label, out string path) || !IsUltimatumPath(path))
                return false;

            if (!TryGetLabelRoot(label, out object? root) || root == null)
                return false;

            return DynamicAccess.TryGetChildAtIndex(root, 0, out object? rawChild0)
                && rawChild0 != null
                && DynamicAccess.TryReadBool(rawChild0, DynamicAccessProfiles.IsVisible, out bool isVisible)
                && isVisible;
        }

        internal static bool ShouldSuppressInactiveUltimatumLabel(bool isUltimatumPath, bool isUltimatumLabel)
            => isUltimatumPath && !isUltimatumLabel;

        internal static bool ShouldSuppressInactiveUltimatumLabel(LabelOnGround? label)
            => ShouldSuppressInactiveUltimatumLabel(
                TryGetLabelItemPath(label, out string path) && IsUltimatumPath(path),
                IsUltimatumLabel(label));
    }
}
