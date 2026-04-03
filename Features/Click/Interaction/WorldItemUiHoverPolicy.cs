namespace ClickIt.Features.Click.Interaction
{
    internal static class WorldItemUiHoverPolicy
    {
        private const string HeistContractPathMarker = "Metadata/Items/Heist/Contracts/";
        private const string HeistContractNamePrefix = "Contract:";
        private const string HeistBlueprintPathMarker = "Items/Heist/HeistBlueprint";
        private const string HeistBlueprintCurrencyPathMarker = "Items/Currency/Heist/Blueprint";
        private const string HeistBlueprintNamePrefix = "Blueprint:";
        private const string RoguesMarkerPathMarker = "Items/Heist/HeistCoin";
        private const string RoguesMarkerName = "Rogue's Marker";

        internal static bool IsHeistContractWorldItem(string? itemPath, string? renderName)
        {
            bool byPath = !string.IsNullOrWhiteSpace(itemPath)
                && itemPath.IndexOf(HeistContractPathMarker, StringComparison.OrdinalIgnoreCase) >= 0;
            if (byPath)
                return true;

            return !string.IsNullOrWhiteSpace(renderName)
                && renderName.StartsWith(HeistContractNamePrefix, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsHeistBlueprintWorldItem(string? itemPath, string? renderName)
        {
            bool byPath = !string.IsNullOrWhiteSpace(itemPath)
                && (itemPath.IndexOf(HeistBlueprintPathMarker, StringComparison.OrdinalIgnoreCase) >= 0
                    || itemPath.IndexOf(HeistBlueprintCurrencyPathMarker, StringComparison.OrdinalIgnoreCase) >= 0);
            if (byPath)
                return true;

            return !string.IsNullOrWhiteSpace(renderName)
                && renderName.StartsWith(HeistBlueprintNamePrefix, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsRoguesMarkerWorldItem(string? itemPath, string? renderName)
        {
            bool byPath = !string.IsNullOrWhiteSpace(itemPath)
                && itemPath.IndexOf(RoguesMarkerPathMarker, StringComparison.OrdinalIgnoreCase) >= 0;
            if (byPath)
                return true;

            return !string.IsNullOrWhiteSpace(renderName)
                && string.Equals(renderName, RoguesMarkerName, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldForceUiHoverVerificationForWorldItem(string? itemPath, string? renderName)
            => IsHeistContractWorldItem(itemPath, renderName)
                || IsHeistBlueprintWorldItem(itemPath, renderName)
                || IsRoguesMarkerWorldItem(itemPath, renderName);

        internal static Vector2 ResolvePreferredLabelPoint(RectangleF rect, EntityType itemType, int chestHeightOffset, string? itemPath, string? renderName)
        {
            Vector2 preferredPoint = rect.Center;

            if (itemType == EntityType.Chest)
                preferredPoint.Y -= chestHeightOffset;

            if (itemType == EntityType.WorldItem && IsHeistContractWorldItem(itemPath, renderName))
            {
                float safeLowerY = rect.Top + (rect.Height * 0.84f);
                preferredPoint.Y = Math.Clamp(safeLowerY, rect.Top + 1f, rect.Bottom - 1f);
            }

            return preferredPoint;
        }
    }
}