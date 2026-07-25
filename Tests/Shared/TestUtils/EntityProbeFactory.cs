namespace ClickIt.Tests.Shared.TestUtils
{
    internal static class EntityProbeFactory
    {
        internal static Entity Create(
            string path = "",
            string renderName = "",
            bool isValid = true,
            bool isHidden = false,
            long address = 0,
            bool isTargetable = true,
            EntityType type = EntityType.Monster,
            float distancePlayer = 0,
            float gridX = 0,
            float gridY = 0,
            float posX = 0,
            float posY = 0,
            float posZ = 0,
            bool isOpened = false)
        {
            var entity = (EntityProbe)RuntimeHelpers.GetUninitializedObject(typeof(EntityProbe));
            entity.Address = address;
            entity.Path = path;
            entity.RenderName = renderName;
            entity.IsValid = isValid;
            entity.IsHidden = isHidden;
            entity.IsTargetable = isTargetable;
            entity.Type = type;
            entity.DistancePlayer = distancePlayer;
            entity.GridPosNum = new System.Numerics.Vector2(gridX, gridY);
            entity.PosNum = new System.Numerics.Vector3(posX, posY, posZ);
            entity.IsOpened = isOpened;
            entity.Components = new Dictionary<Type, Component>();
            return entity;
        }

        internal static Entity WithComponent<T>(Entity entity, T component) where T : Component
        {
            if (entity is not EntityProbe probe)
                throw new InvalidOperationException($"Expected {typeof(EntityProbe).FullName}, got {entity.GetType().FullName}.");

            probe.Components[typeof(T)] = component;
            return probe;
        }
    }

    public sealed class EntityProbe : Entity
    {
        public new long Address { get; set; }

        public new bool IsValid { get; set; }

        public new bool IsHidden { get; set; }

        public new string Path { get; set; } = string.Empty;

        public new bool IsTargetable { get; set; }

        public new string RenderName { get; set; } = string.Empty;

        public new EntityType Type { get; set; }

        public new float DistancePlayer { get; set; }

        public new System.Numerics.Vector2 GridPosNum { get; set; }

        public new System.Numerics.Vector3 PosNum { get; set; }

        public new bool IsOpened { get; set; }

        public Dictionary<Type, Component> Components { get; set; } = new();

        public new T GetComponent<T>() where T : Component
            => Components.TryGetValue(typeof(T), out Component? component)
                ? (T)component
                : null!;
    }
}