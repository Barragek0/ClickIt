using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using System.Diagnostics;

namespace ClickIt.Services
{
    public partial class ShrineService(GameController gameController, Camera camera)
    {
        private readonly GameController _gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
        private readonly Camera _camera = camera ?? throw new ArgumentNullException(nameof(camera));

        private const int SHRINE_CACHE_DURATION_MS = 200; // 200ms cache for shrines
        private readonly Stopwatch _shrineCacheTimer = new();
        private List<Entity>? _cachedShrines;
        private long _lastShrineCacheTime;

        // Thread safety for multi-threading
        [ThreadStatic]
        private static List<Entity>? _threadLocalShrineList;

        private static List<Entity> GetThreadLocalShrineList()
            => _threadLocalShrineList ??= [];

        public static bool IsShrine(Entity item)
        {
            if (item == null)
                return false;

            if (item.HasComponent<Shrine>())
                return true;

            string path = item.Path ?? string.Empty;
            return path.Contains("DarkShrine", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsClickableShrineCandidate(Entity? item)
        {
            if (item == null)
                return false;

            return IsShrine(item)
                && !item.IsOpened
                && item.IsTargetable
                && !item.IsHidden
                && item.IsValid;
        }

        /// <summary>
        /// Get cached list of shrine entities, updating cache if expired
        /// </summary>
        private List<Entity> GetCachedShrineEntities()
        {
            if (!_shrineCacheTimer.IsRunning)
                _shrineCacheTimer.Start();

            long currentTime = _shrineCacheTimer.ElapsedMilliseconds;

            if (!HasUsableShrineCache(currentTime))
                RefreshShrineCache(currentTime);

            return _cachedShrines!;
        }

        private bool HasUsableShrineCache(long currentTime)
            => _cachedShrines != null && (currentTime - _lastShrineCacheTime) <= SHRINE_CACHE_DURATION_MS;

        private void RefreshShrineCache(long currentTime)
        {
            _cachedShrines = GetShrineEntitiesUncached();
            _lastShrineCacheTime = currentTime;
        }

        /// <summary>
        /// Get all shrine entities without caching (expensive operation)
        /// </summary>
        private List<Entity> GetShrineEntitiesUncached()
        {
            var shrines = GetThreadLocalShrineList();
            shrines.Clear();

            var validEntities = _gameController.EntityListWrapper?.ValidEntitiesByType;
            if (validEntities == null)
                return shrines;

            foreach (var entityType in validEntities)
            {
                var entities = entityType.Value;
                if (entities == null)
                    continue;

                foreach (var entity in entities)
                {
                    if (IsClickableShrineCandidate(entity))
                        shrines.Add(entity);
                }
            }

            return shrines;
        }

        public bool AreShrinesPresent()
        {
            return GetCachedShrineEntities().Count > 0;
        }

        public bool AreShrinesPresentInClickableArea(Func<Vector2, bool> isInClickableArea)
        {
            var shrines = GetCachedShrineEntities();

            foreach (var shrine in shrines)
            {
                if (shrine == null)
                    continue;

                var screenPosRaw = _camera.WorldToScreen(shrine.PosNum);
                Vector2 clickPos = new(screenPosRaw.X, screenPosRaw.Y);
                if (isInClickableArea(clickPos))
                    return true;
            }

            return false;
        }

        public Entity? GetNearestShrineInRange(int clickDistance, Func<Vector2, bool>? isInClickableArea = null)
        {
            Entity? nearestShrine = null;
            float minDistance = float.MaxValue;

            var shrines = GetCachedShrineEntities();

            foreach (var shrine in shrines)
            {
                if (shrine == null)
                    continue;

                float distance = shrine.DistancePlayer;

                // Early distance check to avoid expensive calculations
                if (distance > clickDistance || distance >= minDistance)
                    continue;

                if (isInClickableArea != null)
                {
                    var screenPosRaw = _camera.WorldToScreen(shrine.PosNum);
                    Vector2 screenPos = new(screenPosRaw.X, screenPosRaw.Y);
                    if (!isInClickableArea(screenPos))
                        continue;
                }

                minDistance = distance;
                nearestShrine = shrine;
            }

            return nearestShrine;
        }

        /// <summary>
        /// Invalidate the shrine cache (call when area changes or shrines are clicked)
        /// </summary>
        public void InvalidateCache()
        {
            _cachedShrines = null;
            _lastShrineCacheTime = 0;
        }
    }
}
