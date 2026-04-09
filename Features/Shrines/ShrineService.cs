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

            if (DynamicAccess.TryReadBool(item, static i => i.HasComponent<Shrine>(), out bool hasShrineComponent) && hasShrineComponent)
                return true;

            string path = DynamicAccess.TryReadString(item, static i => i.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            return path.Contains("DarkShrine", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsClickableShrineCandidate(Entity? item)
        {
            if (item == null)
                return false;

            if (!DynamicAccess.TryReadBool(item, static i => i.IsOpened, out bool isOpened)
                || !DynamicAccess.TryReadBool(item, static i => i.IsTargetable, out bool isTargetable)
                || !DynamicAccess.TryReadBool(item, static i => i.IsHidden, out bool isHidden)
                || !DynamicAccess.TryReadBool(item, static i => i.IsValid, out bool isValid))
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

        public Entity? GetNearestShrineInRange(
            int clickDistance,
            Func<Vector2, bool>? isInClickableArea = null,
            Func<Entity, float>? cursorDistanceResolver = null)
        {
            Entity? nearestShrine = null;
            float minDistance = float.MaxValue;
            float minCursorDistance = float.MaxValue;

            var shrines = GetCachedShrineEntities();

            foreach (var shrine in shrines)
            {
                if (shrine == null)
                    continue;

                float distance = shrine.DistancePlayer;

                // Early distance check to avoid expensive calculations
                if (distance > clickDistance)
                    continue;

                if (isInClickableArea != null)
                {
                    var screenPosRaw = _camera.WorldToScreen(shrine.PosNum);
                    Vector2 screenPos = new(screenPosRaw.X, screenPosRaw.Y);
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
    }
}
