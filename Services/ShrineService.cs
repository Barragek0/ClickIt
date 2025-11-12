using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using System;
using System.Collections.Generic;

namespace ClickIt.Services
{
    public class ShrineService
    {
        private readonly GameController _gameController;
        private readonly Camera _camera;

        public ShrineService(GameController gameController, Camera camera)
        {
            _gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        }

        public static bool IsShrine(Entity item)
        {
            if (item == null) return false;

            if (item.HasComponent<Shrine>())
                return true;

            return !string.IsNullOrEmpty(item.Path) && item.Path.Contains("DarkShrine");
        }

        public bool AreShrinesPresent()
        {
            if (_gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return false;

            var validEntities = _gameController.EntityListWrapper.ValidEntitiesByType;

            // Iterate through all entity types to find shrines
            foreach (var entityType in validEntities)
            {
                if (entityType.Value == null) continue;

                var entities = entityType.Value;
                foreach (var entity in entities)
                {
                    if (entity != null && IsShrine(entity) && !entity.IsOpened && entity.IsTargetable && !entity.IsHidden && entity.IsValid)
                        return true;
                }
            }

            return false;
        }

        public bool AreShrinesPresentInClickableArea(Func<Vector2, bool> isInClickableArea)
        {
            if (_gameController?.EntityListWrapper?.ValidEntitiesByType == null)
                return false;

            var validEntities = _gameController.EntityListWrapper.ValidEntitiesByType;

            // Iterate through all entity types to find shrines in clickable area
            foreach (var entityType in validEntities)
            {
                if (entityType.Value == null) continue;

                var entities = entityType.Value;
                foreach (var entity in entities)
                {
                    if (entity != null && IsShrine(entity) && !entity.IsOpened && entity.IsTargetable && !entity.IsHidden && entity.IsValid)
                    {
                        // Check if the shrine is in a clickable area (on screen)
                        Vector2 clickPos = new Vector2(_camera.WorldToScreen(entity.PosNum).X, _camera.WorldToScreen(entity.PosNum).Y);
                        if (isInClickableArea(clickPos))
                            return true;
                    }
                }
            }

            return false;
        }
        public List<Entity> GetShrineEntities()
        {
            var shrines = new List<Entity>();

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

        public Entity GetNearestShrineInRange(int clickDistance, Func<Vector2, bool>? isInClickableArea = null)
        {
            Entity nearestShrine = null;
            float minDistance = float.MaxValue;

            var shrines = GetShrineEntities();

            foreach (var shrine in shrines)
            {
                if (shrine == null) continue;

                float distance = shrine.DistancePlayer;
                if (distance <= clickDistance && distance < minDistance)
                {
                    // If a clickable area checker is provided, ensure the shrine is on screen
                    if (isInClickableArea != null && _camera != null)
                    {
                        Vector2 screenPos = new Vector2(_camera.WorldToScreen(shrine.PosNum).X, _camera.WorldToScreen(shrine.PosNum).Y);
                        if (!isInClickableArea(screenPos))
                        {
                            continue; // Skip shrines that aren't in the clickable area
                        }
                    }

                    minDistance = distance;
                    nearestShrine = shrine;
                }
            }

            return nearestShrine;
        }
    }
}