namespace ClickIt.Shared.Game
{
    internal static class EntityQueryService
    {
        internal static bool VisitValidEntities(GameController? gameController, Func<Entity, bool> visitor)
        {
            ArgumentNullException.ThrowIfNull(visitor);

            Dictionary<EntityType, List<Entity>>? entitiesByType = gameController?.EntityListWrapper?.ValidEntitiesByType;
            if (entitiesByType == null)
                return false;

            foreach (KeyValuePair<EntityType, List<Entity>> kv in entitiesByType)
            {
                List<Entity> entities = kv.Value;
                if (entities == null)
                    continue;

                for (int i = 0; i < entities.Count; i++)
                {
                    Entity entity = entities[i];
                    if (entity == null)
                        continue;

                    if (visitor(entity))
                        return true;
                }
            }

            return false;
        }

        // The retained cache includes streamed-out structures. Report an unavailable or empty
        // cache separately so callers can fall back to the active entity lists.
        internal static bool TryVisitRetainedEntities(GameController? gameController, Action<Entity> visitor)
        {
            ArgumentNullException.ThrowIfNull(visitor);

            ICollection<Entity>? all;
            try { all = gameController?.EntityListWrapper?.Entities; }
            catch { return false; }
            if (all == null)
                return false;

            try
            {
                bool visited = false;
                foreach (Entity entity in all)
                {
                    if (entity == null)
                        continue;

                    visitor(entity);
                    visited = true;
                }
                return visited;
            }
            catch
            {
                return false;
            }
        }

        internal static Entity? FindEntityByAddress(GameController? gameController, long address)
        {
            if (address == 0)
                return null;

            Entity? found = null;
            VisitValidEntities(gameController, entity =>
            {
                if (!TryGetEntityAddress(entity, out long entityAddress) || entityAddress != address)
                    return false;

                found = entity;
                return true;
            });

            return found;
        }

        private static bool TryGetEntityAddress(Entity entity, out long address)
        {
            address = 0;
            if (!DynamicAccess.TryGetDynamicValue(entity, DynamicAccessProfiles.Address, out object? rawAddress) || rawAddress == null)
                return false;

            try
            {
                address = Convert.ToInt64(rawAddress, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
