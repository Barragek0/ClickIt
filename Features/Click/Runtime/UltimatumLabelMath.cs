namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumLabelMath
    {
        internal static bool IsUltimatumPath(string? path)
            => Constants.IsUltimatumInteractablePath(path);

        internal static bool IsUltimatumLabel(LabelOnGround? label)
        {
            if (!IsUltimatumPath(label?.ItemOnGround?.Path))
                return false;

            Element? child0 = label?.Label?.GetChildAtIndex(0);
            return child0?.IsVisible == true;
        }

        internal static bool ShouldSuppressInactiveUltimatumLabel(bool isUltimatumPath, bool isUltimatumLabel)
            => isUltimatumPath && !isUltimatumLabel;

        internal static bool ShouldSuppressInactiveUltimatumLabel(LabelOnGround? label)
            => ShouldSuppressInactiveUltimatumLabel(IsUltimatumPath(label?.ItemOnGround?.Path), IsUltimatumLabel(label));
    }
}