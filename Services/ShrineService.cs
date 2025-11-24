using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ClickIt.Services
{
    public class ShrineService(GameController gameController, Camera camera)
    {
        private readonly GameController _gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
        private readonly Camera _camera = camera ?? throw new ArgumentNullException(nameof(camera));

        // Performance caching - shrines update less frequently than labels
        private const int SHRINE_CACHE_DURATION_MS = 200; // 200ms cache for shrines
        private readonly Stopwatch _shrineCacheTimer = new();
        private List<Entity>? _cachedShrines;
        private long _lastShrineCacheTime;

        // Thread safety for multi-threading
        [ThreadStatic]
        private static List<Entity>? _threadLocalShrineList;

        private static List<Entity> GetThreadLocalShrineList()
        {
            if (_threadLocalShrineList == null)
            {
                _threadLocalShrineList = [];
            }
            return _threadLocalShrineList;
        }

        public static bool IsShrine(Entity item)
        {
            if (item == null) return false;

            if (item.HasComponent<Shrine>())
                return true;

            return !string.IsNullOrEmpty(item.Path) && item.Path.Contains("DarkShrine");
        }

        /// <summary>
        /// Get cached list of shrine entities, updating cache if expired
        /// </summary>
        private List<Entity> GetCachedShrineEntities()
        {
            // Ensure timer is running
            if (!_shrineCacheTimer.IsRunning)
                _shrineCacheTimer.Start();

            long currentTime = _shrineCacheTimer.ElapsedMilliseconds;
            if (_cachedShrines == null || (currentTime - _lastShrineCacheTime) > SHRINE_CACHE_DURATION_MS)
            {
                _cachedShrines = GetShrineEntitiesUncached();
                _lastShrineCacheTime = currentTime;
            }
            return _cachedShrines;
        }

        /// <summary>
        /// Get all shrine entities without caching (expensive operation)
        /// </summary>
        private List<Entity> GetShrineEntitiesUncached()
        {
            var shrines = GetThreadLocalShrineList();
            shrines.Clear();

            if (_gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return shrines;

            var validEntities = _gameController.EntityListWrapper.ValidEntitiesByType;

            // Find all shrine entities for potential clicking
            foreach (var entityType in validEntities)
            {
                if (entityType.Value == null) continue;

                var entities = entityType.Value;
                foreach (var entity in entities)
                {
                    if (entity != null && IsShrine(entity) && !entity.IsOpened && entity.IsTargetable && !entity.IsHidden && entity.IsValid)
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
                if (shrine == null) continue;

                // Check if the shrine is in a clickable area (on screen)
                Vector2 clickPos = new(_camera.WorldToScreen(shrine.PosNum).X, _camera.WorldToScreen(shrine.PosNum).Y);
                if (isInClickableArea(clickPos))
                    return true;
            }

            return false;
        }

        public List<Entity> GetShrineEntities()
        {
            // Return a copy of the cached list to prevent external modification
            return new List<Entity>(GetCachedShrineEntities());
        }

        public Entity? GetNearestShrineInRange(int clickDistance, Func<Vector2, bool>? isInClickableArea = null)
        {
            Entity? nearestShrine = null;
            float minDistance = float.MaxValue;

            var shrines = GetCachedShrineEntities();

            foreach (var shrine in shrines)
            {
                if (shrine == null) continue;

                float distance = shrine.DistancePlayer;

                // Early distance check to avoid expensive calculations
                if (distance > clickDistance || distance >= minDistance)
                    continue;

                // If a clickable area checker is provided, ensure the shrine is on screen
                if (isInClickableArea != null && _camera != null)
                {
                    Vector2 screenPos = new(_camera.WorldToScreen(shrine.PosNum).X, _camera.WorldToScreen(shrine.PosNum).Y);
                    if (!isInClickableArea(screenPos))
                    {
                        continue; // Skip shrines that aren't in the clickable area
                    }
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