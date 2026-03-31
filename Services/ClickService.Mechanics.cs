using ClickIt.Definitions;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using ClickIt.Services.Mechanics;

namespace ClickIt.Services
{
    public partial class ClickService
    {
        internal const string ShrineMechanicId = MechanicIds.Shrines;
        internal const string LostShipmentMechanicId = MechanicIds.LostShipment;
        private const string LostShipmentPathMarker = "Metadata/Chests/LostShipmentCrate";
        private const string LostShipmentLoosePathMarker = "LostShipment";
        private const string LostGoodsRenderNameMarker = "Lost Goods";
        private const string LostShipmentRenderNameMarker = "Lost Shipment";
        private const string VerisiumMechanicId = MechanicIds.SettlersVerisium;
        private const string VerisiumBossSubAreaTransitionPathMarker = MechanicIds.VerisiumBossSubAreaTransitionPathMarker;
        internal const int HiddenFallbackCandidateCacheWindowMs = 150;
        internal const int VisibleMechanicCandidateCacheWindowMs = 80;
        internal const int GroundLabelEntityAddressCacheWindowMs = 150;

        private readonly IMechanicPrioritySnapshotProvider _mechanicPrioritySnapshotService = new MechanicPrioritySnapshotService();

        internal static readonly Vector2[] NearbyClickProbeOffsets =
        [
            new Vector2(0f, 0f),
            new Vector2(12f, 0f),
            new Vector2(-12f, 0f),
            new Vector2(0f, 12f),
            new Vector2(0f, -12f),
            new Vector2(24f, 0f),
            new Vector2(-24f, 0f),
            new Vector2(0f, 24f),
            new Vector2(0f, -24f)
        ];

        internal static bool IsLostShipmentPath(string? path)
            => ContainsAny(path, LostShipmentPathMarker, LostShipmentLoosePathMarker);

        internal static bool ShouldClickShrineWhenGroundItemsHidden(Entity? shrine) => shrine != null;

        internal static bool ShouldUseHoldClickForSettlersMechanic(string? mechanicId)
            => string.Equals(mechanicId, VerisiumMechanicId, StringComparison.OrdinalIgnoreCase);

        internal static bool IsLostShipmentEntity(string? path, string? renderName)
            => IsLostShipmentPath(path)
               || ContainsAny(renderName, LostGoodsRenderNameMarker, LostShipmentRenderNameMarker);

        internal static bool ShouldSkipLostShipmentEntity(bool isValid, float distance, int clickDistance, bool isOpened)
            => !isValid || isOpened || distance > clickDistance;

        internal static bool ShouldSkipSettlersOreEntity(bool isValid, float distance, int clickDistance)
            => !isValid || distance > clickDistance;

        internal static bool ShouldReuseTimedLabelCountCache(long now, long cachedAtMs, int cachedLabelCount, int currentLabelCount, int cacheWindowMs)
        {
            if (cachedAtMs <= 0 || cacheWindowMs <= 0)
                return false;

            if (cachedLabelCount != currentLabelCount)
                return false;

            long age = now - cachedAtMs;
            return age >= 0 && age <= cacheWindowMs;
        }

        private MechanicPriorityContext CreateMechanicPriorityContext()
        {
            MechanicPrioritySnapshot snapshot = _mechanicPrioritySnapshotService.Snapshot;
            return new(
                snapshot.PriorityIndexMap,
                snapshot.IgnoreDistanceSet,
                snapshot.IgnoreDistanceWithinByMechanicId,
                settings.MechanicPriorityDistancePenalty.Value);
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

        internal static bool ArePlayerDistancesEquivalent(float left, float right)
            => Math.Abs(left - right) <= 0.001f;

        internal static bool IsFirstCandidateCloserToCursor(Vector2 firstClickPoint, Vector2 secondClickPoint, Vector2 cursorAbsolute, Vector2 windowTopLeft)
        {
            float first = GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, firstClickPoint, windowTopLeft);
            float second = GetManualCursorDistanceSquaredInEitherSpace(cursorAbsolute, secondClickPoint, windowTopLeft);
            return first < second;
        }

        private void RefreshMechanicPriorityCaches()
        {
            _ = _mechanicPrioritySnapshotService.Refresh(
                settings.GetMechanicPriorityOrder(),
                settings.GetMechanicPriorityIgnoreDistanceIds(),
                settings.GetMechanicPriorityIgnoreDistanceWithinById());
        }

        private static bool IsSettlersMechanicId(string? mechanicId)
            => !string.IsNullOrWhiteSpace(mechanicId)
               && mechanicId.StartsWith("settlers-", StringComparison.OrdinalIgnoreCase);
    }
}