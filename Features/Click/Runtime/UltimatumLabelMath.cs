namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumLabelMath
    {
        internal static bool IsUltimatumPath(string? path)
            => Constants.IsUltimatumInteractablePath(path);

        internal static bool TryGetLabelItemPath(LabelOnGround? label, out string path)
        {
            path = string.Empty;

            if (!DynamicAccess.TryGetDynamicValue(label, static l => l.ItemOnGround, out object? rawItem)
                || rawItem == null)
                return false;


            return DynamicAccess.TryReadString(rawItem, static i => i.Path, out path);
        }

        internal static Element? TryGetLabelElement(LabelOnGround? label)
        {
            return DynamicAccess.TryGetDynamicValue(label, static l => l.Label, out object? rawLabel)
                && rawLabel is Element element
                ? element
                : null;
        }

        internal static ulong GetLabelElementAddress(LabelOnGround? label)
        {
            Element? element = TryGetLabelElement(label);
            if (element == null)
                return 0;

            return DynamicAccess.TryGetDynamicValue(element, static e => e.Address, out object? rawAddress)
                && rawAddress != null
                ? unchecked((ulong)Convert.ToInt64(rawAddress))
                : 0;
        }

        internal static bool IsLabelElementValid(LabelOnGround? label)
            => TryGetLabelElement(label)?.IsValid == true;

        internal static bool IsUltimatumLabel(LabelOnGround? label)
        {
            if (!TryGetLabelItemPath(label, out string path) || !IsUltimatumPath(path))
                return false;

            Element? child0 = TryGetLabelElement(label)?.GetChildAtIndex(0);
            return child0?.IsVisible == true;
        }

        internal static bool ShouldSuppressInactiveUltimatumLabel(bool isUltimatumPath, bool isUltimatumLabel)
            => isUltimatumPath && !isUltimatumLabel;

        internal static bool ShouldSuppressInactiveUltimatumLabel(LabelOnGround? label)
            => ShouldSuppressInactiveUltimatumLabel(
                TryGetLabelItemPath(label, out string path) && IsUltimatumPath(path),
                IsUltimatumLabel(label));
    }
}