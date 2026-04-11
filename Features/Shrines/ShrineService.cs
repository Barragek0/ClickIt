namespace ClickIt.Features.Shrines
{
    public class ShrineService(GameController gameController, Camera camera)
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

            if (DynamicAccess.TryHasComponent<Shrine>(item, out bool hasShrineComponent) && hasShrineComponent)
                return true;

            string path = DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            return path.Contains("DarkShrine", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsClickableShrineCandidate(Entity? item)
        {
            if (item == null)
                return false;

            if (!DynamicAccess.TryReadBool(item, DynamicAccessProfiles.IsOpened, out bool isOpened)
                || !DynamicAccess.TryReadBool(item, DynamicAccessProfiles.IsTargetable, out bool isTargetable)
                || !DynamicAccess.TryReadBool(item, DynamicAccessProfiles.IsHidden, out bool isHidden)
                || !DynamicAccess.TryReadBool(item, DynamicAccessProfiles.IsValid, out bool isValid))
            {
                return false;
            }

            return IsShrine(item)
                && !isOpened
                && isTargetable
                && !isHidden
                && isValid;
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
            List<Entity> shrines = GetThreadLocalShrineList();
            shrines.Clear();

            Dictionary<EntityType, List<Entity>>? validEntities = _gameController.EntityListWrapper?.ValidEntitiesByType;
            if (validEntities == null)
                return shrines;

            foreach (KeyValuePair<EntityType, List<Entity>> entityType in validEntities)
            {
                List<Entity> entities = entityType.Value;
                if (entities == null)
                    continue;

                foreach (Entity? entity in entities)
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
            List<Entity> shrines = GetCachedShrineEntities();

            foreach (Entity shrine in shrines)
            {
                if (shrine == null)
                    continue;

                if (!TryProjectShrineScreenPosition(shrine, out Vector2 clickPos))
                    continue;

                if (isInClickableArea(clickPos))
                    return true;
            }

            return false;
        }

        public Entity? GetNearestShrineInRange(
            int clickDistance,
            Func<Vector2, bool>? isInClickableArea = null,
            Func<Entity, float>? cursorDistanceResolver = null)
        {
            Entity? nearestShrine = null;
            float minDistance = float.MaxValue;
            float minCursorDistance = float.MaxValue;

            List<Entity> shrines = GetCachedShrineEntities();

            foreach (Entity shrine in shrines)
            {
                if (shrine == null)
                    continue;

                if (!DynamicAccess.TryReadFloat(shrine, DynamicAccessProfiles.DistancePlayer, out float distance))
                    continue;

                // Early distance check to avoid expensive calculations
                if (distance > clickDistance)
                    continue;

                if (isInClickableArea != null)
                {
                    if (!TryProjectShrineScreenPosition(shrine, out Vector2 screenPos))
                        continue;

                    if (!isInClickableArea(screenPos))
                        continue;
                }

                float cursorDistance = cursorDistanceResolver?.Invoke(shrine) ?? float.MaxValue;
                bool isCloserByDistance = distance < minDistance;
                bool isDistanceTieButCursorCloser = SystemMath.Abs(distance - minDistance) <= 0.001f
                    && cursorDistance < minCursorDistance;
                if (!isCloserByDistance && !isDistanceTieButCursorCloser)
                    continue;

                minDistance = distance;
                minCursorDistance = cursorDistance;
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

        internal static void ClearThreadLocalStorageForCurrentThread()
        {
            _threadLocalShrineList = null;
        }

        internal static void ResetThreadLocalStorage()
        {
            _threadLocalShrineList = null;
        }

        internal static int GetThreadLocalShrineListInstanceId()
        {
            return RuntimeHelpers.GetHashCode(GetThreadLocalShrineList());
        }

        internal void SeedCacheWithSingleNullEntry(long lastShrineCacheTime)
        {
            _cachedShrines = [null!];
            _lastShrineCacheTime = lastShrineCacheTime;
        }

        internal void EnsureCacheTimerStarted()
        {
            if (!_shrineCacheTimer.IsRunning)
            {
                _shrineCacheTimer.Start();
            }
        }

        internal long GetCacheElapsedMilliseconds()
        {
            return _shrineCacheTimer.ElapsedMilliseconds;
        }

        internal bool HasCachedShrines()
        {
            return _cachedShrines != null;
        }

        internal long GetLastShrineCacheTime()
        {
            return _lastShrineCacheTime;
        }

        private bool TryProjectShrineScreenPosition(Entity shrine, out Vector2 screenPos)
        {
            screenPos = default;

            if (!DynamicAccess.TryGetDynamicValue(shrine, DynamicAccessProfiles.PosNum, out object? rawPosition)
                || rawPosition is not System.Numerics.Vector3 position)
            {
                return false;
            }

            NumVector2 screenPosRaw = _camera.WorldToScreen(position);
            screenPos = new(screenPosRaw.X, screenPosRaw.Y);
            return true;
        }
    }
}
