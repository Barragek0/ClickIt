namespace ClickIt.Tests.Shared.TestUtils
{
    internal static class OffscreenStickyTargetGraphShaper
    {
        internal static Entity CreateActiveStickyEntity(
            long address,
            string path = "Metadata/Monsters/Test",
            bool isValid = true,
            bool isHidden = false,
            bool isTargetable = true)
        {
            var entity = (StickyTargetProbeEntity)RuntimeHelpers.GetUninitializedObject(typeof(StickyTargetProbeEntity));
            entity.Address = address;
            entity.Path = path;
            entity.IsValid = isValid;
            entity.IsHidden = isHidden;
            entity.IsTargetable = isTargetable;
            entity.RenderName = "Test Sticky Target";
            entity.Type = EntityType.Monster;
            SeedBaseAddress(entity, address);
            return entity;
        }

        internal static LabelOnGround CreateVisibleLabel(Entity itemOnGround)
        {
            var label = (StickyTargetProbeLabel)RuntimeHelpers.GetUninitializedObject(typeof(StickyTargetProbeLabel));
            label.ItemOnGround = itemOnGround;
            label.IsVisible = true;
            return label;
        }

        private static void SeedBaseAddress(Entity entity, long address)
        {
            Type? currentType = entity.GetType().BaseType;
            while (currentType != null)
            {
                FieldInfo? field = currentType.GetField("<Address>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?? currentType.GetField("_address", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?? currentType.GetField("address", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(entity, address);
                    return;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Unable to seed base Address for {entity.GetType().FullName}.");
        }
    }

    public sealed class StickyTargetProbeEntity : Entity
    {
        public new long Address { get; set; }

        public new bool IsValid { get; set; }

        public new bool IsHidden { get; set; }

        public new string Path { get; set; } = string.Empty;

        public new bool IsTargetable { get; set; }

        public new string RenderName { get; set; } = string.Empty;

        public new EntityType Type { get; set; }

        public new T GetComponent<T>() where T : Component
            => null!;
    }

    public sealed class StickyTargetProbeLabel : LabelOnGround
    {
        public new bool IsVisible { get; set; }

        public new Entity ItemOnGround { get; set; } = null!;
    }
}