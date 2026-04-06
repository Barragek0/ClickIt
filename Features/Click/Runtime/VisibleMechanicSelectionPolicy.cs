namespace ClickIt.Features.Click.Runtime
{
    internal static class VisibleMechanicSelectionPolicy
    {
        private const string LostShipmentPathMarker = "Metadata/Chests/LostShipmentCrate";
        private const string LostShipmentLoosePathMarker = "LostShipment";
        private const string LostGoodsRenderNameMarker = "Lost Goods";
        private const string LostShipmentRenderNameMarker = "Lost Shipment";
        private const string HeistHazardsPathMarker = "Heist/Objects/Level/Hazards";

        internal static bool IsLostShipmentPath(string? path)
            => ContainsAny(path, LostShipmentPathMarker, LostShipmentLoosePathMarker);

        internal static bool IsLostShipmentEntity(string? path, string? renderName)
            => IsLostShipmentPath(path)
               || ContainsAny(renderName, LostGoodsRenderNameMarker, LostShipmentRenderNameMarker);

        internal static bool IsHeistHazardsPath(string? path)
            => ContainsAny(path, HeistHazardsPathMarker);

        internal static bool ShouldSkipLostShipmentEntity(bool isValid, float distance, int clickDistance, bool isOpened)
            => !isValid || isOpened || distance > clickDistance;

        internal static bool ShouldSkipSettlersOreEntity(bool isValid, float distance, int clickDistance)
            => !isValid || distance > clickDistance;

        internal static bool ArePlayerDistancesEquivalent(float left, float right)
            => Math.Abs(left - right) <= 0.001f;

        internal static bool IsFirstCandidateCloserToCursor(Vector2 firstClickPoint, Vector2 secondClickPoint, Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            float first = ManualCursorSelectionMath.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, firstClickPoint, windowTopLeft);
            float second = ManualCursorSelectionMath.GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, secondClickPoint, windowTopLeft);
            return first < second;
        }

        private static bool ContainsAny(string? value, params string[] markers)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            for (int i = 0; i < markers.Length; i++)
            {
                if (value.Contains(markers[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}