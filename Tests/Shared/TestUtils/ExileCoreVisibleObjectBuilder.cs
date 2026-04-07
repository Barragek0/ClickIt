namespace ClickIt.Tests.Shared.TestUtils
{
    internal static class ExileCoreVisibleObjectBuilder
    {
        internal static LabelOnGround CreateSelectableLabel()
            => new LabelOnGround();

        internal static GameController CreateGameControllerWithWindow(RectangleF windowRect)
        {
            GameController gameController = ExileCoreOpaqueFactory.CreateOpaqueGameController();
            object window = RuntimeHelpers.GetUninitializedObject(RuntimeMemberAccessor.ResolveRequiredMemberType(gameController, nameof(GameController.Window)));

            RuntimeMemberAccessor.SetRequiredMember(
                window,
                "_lastValid",
                new System.Drawing.Rectangle(
                    (int)windowRect.X,
                    (int)windowRect.Y,
                    (int)windowRect.Width,
                    (int)windowRect.Height));
            RuntimeMemberAccessor.SetRequiredMember(gameController, nameof(GameController.Window), window);
            return gameController;
        }

        internal static GameController CreateGameControllerWithWindowAndGame(RectangleF windowRect)
        {
            GameController gameController = CreateGameControllerWithWindow(windowRect);
            object game = RuntimeHelpers.GetUninitializedObject(RuntimeMemberAccessor.ResolveRequiredMemberType(gameController, nameof(GameController.Game)));

            RuntimeMemberAccessor.SetRequiredMember(gameController, nameof(GameController.Game), game);
            return gameController;
        }

        internal static GameController CreateGameControllerWithRitualBlocker(params string[] ritualPaths)
        {
            GameController gameController = CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f));
            object entityListWrapper = RuntimeHelpers.GetUninitializedObject(RuntimeMemberAccessor.ResolveRequiredMemberType(gameController, nameof(GameController.EntityListWrapper)));
            var entities = ritualPaths.Select(CreateEntityWithPath).ToList();

            RuntimeMemberAccessor.SetRequiredMember(entityListWrapper, "OnlyValidEntities", entities);
            RuntimeMemberAccessor.SetRequiredMember(gameController, nameof(GameController.EntityListWrapper), entityListWrapper);
            return gameController;
        }

        internal static GameController CreateGameControllerWithEntities(params Entity[] entities)
        {
            GameController gameController = CreateGameControllerWithWindow(new RectangleF(100f, 200f, 1280f, 720f));
            object entityListWrapper = RuntimeHelpers.GetUninitializedObject(RuntimeMemberAccessor.ResolveRequiredMemberType(gameController, nameof(GameController.EntityListWrapper)));
            List<Entity> seededEntities = entities?.Where(static entity => entity != null).ToList() ?? [];

            RuntimeMemberAccessor.SetRequiredMember(
                entityListWrapper,
                "ValidEntitiesByType",
                new Dictionary<EntityType, List<Entity>>
                {
                    [EntityType.Monster] = seededEntities
                });
            RuntimeMemberAccessor.SetRequiredMember(entityListWrapper, "OnlyValidEntities", seededEntities);
            RuntimeMemberAccessor.SetRequiredMember(gameController, nameof(GameController.EntityListWrapper), entityListWrapper);
            return gameController;
        }

        internal static Entity CreateEntityWithPath(string path)
            => CreateEntity(path: path);

        internal static Entity CreateEntity(
            string path = "",
            bool isValid = true,
            bool isHidden = false,
            long address = 0,
            bool isTargetable = true,
            EntityType type = EntityType.Monster,
            float gridX = 0,
            float gridY = 0)
        {
            Entity entity = ExileCoreOpaqueFactory.CreateOpaqueEntity();
            SetEntityMember(entity, nameof(Entity.Path), path, "_path", "path", "<Path>k__BackingField");
            SetEntityMember(entity, nameof(Entity.IsValid), isValid, "_isValid", "isValid", "<IsValid>k__BackingField");
            SetEntityMember(entity, nameof(Entity.IsHidden), isHidden, "_isHidden", "isHidden", "<IsHidden>k__BackingField");

            if (!RuntimeMemberAccessor.TrySetMember(entity, "_address", address)
                && !RuntimeMemberAccessor.TrySetMember(entity, "address", address)
                && !RuntimeMemberAccessor.TrySetMember(entity, "<Address>k__BackingField", address))
            {
                throw new InvalidOperationException($"Unable to seed an address backing field for {typeof(Entity).FullName}.");
            }

            SetEntityMember(entity, nameof(Entity.IsTargetable), isTargetable, "_isTargetable", "isTargetable", "<IsTargetable>k__BackingField");
            SetEntityMember(entity, nameof(Entity.Type), type, "_type", "type", "<Type>k__BackingField");
            SetEntityMember(entity, nameof(Entity.GridPosNum), CreateGridPosition(entity, gridX, gridY), "_gridPosNum", "gridPosNum", "<GridPosNum>k__BackingField");
            return entity;
        }

        private static object CreateGridPosition(object owner, float gridX, float gridY)
        {
            Type gridType = RuntimeMemberAccessor.ResolveRequiredMemberType(owner, nameof(Entity.GridPosNum));
            object gridPosition = Activator.CreateInstance(gridType)
                ?? RuntimeHelpers.GetUninitializedObject(gridType);

            RuntimeMemberAccessor.SetRequiredMember(gridPosition, "X", gridX);
            RuntimeMemberAccessor.SetRequiredMember(gridPosition, "Y", gridY);
            return gridPosition;
        }

        private static void SetEntityMember<T>(Entity entity, string memberName, T value, params string[] backingFieldCandidates)
        {
            for (int i = 0; i < backingFieldCandidates.Length; i++)
            {
                if (RuntimeMemberAccessor.TrySetMember(entity, backingFieldCandidates[i], value))
                    return;
            }

            RuntimeMemberAccessor.SetRequiredMember(entity, memberName, value);
        }
    }
}