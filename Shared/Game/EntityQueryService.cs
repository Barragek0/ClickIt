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

        internal static Entity? FindEntityByAddress(GameController? gameController, long address)
        {
            if (address == 0)
                return null;

            Entity? found = null;
            VisitValidEntities(gameController, entity =>
            {
                if (entity.Address != address)
                    return false;

                found = entity;
                return true;
            });

            return found;
        }
    }
}