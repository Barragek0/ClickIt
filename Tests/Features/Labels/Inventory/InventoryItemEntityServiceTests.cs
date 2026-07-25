namespace ClickIt.Tests.Features.Labels.Inventory
{
    [TestClass]
    public class InventoryItemEntityServiceTests
    {
        [TestMethod]
        public void TryEnumeratePrimaryInventoryItemEntitiesFast_FiltersDuplicatesAndNonInventoryEntries()
        {
            Entity inventoryEntity = CreateEntity(address: 1001);
            Entity nonInventoryEntity = CreateEntity(address: 2002);
            object inventoryEntry = new();
            object duplicateInventoryEntry = new();
            object nonInventoryEntry = new();

            var service = new InventoryItemEntityService(new InventoryItemEntityServiceDependencies(
                CacheWindowMs: 50,
                TryGetPrimaryServerInventory: _ => (false, (object?)null),
                TryGetPrimaryServerInventorySlotItems: _ => (true, new object[] { inventoryEntry, duplicateInventoryEntry, nonInventoryEntry }),
                EnumerateObjects: collection => collection as IEnumerable<object?> ?? Array.Empty<object?>(),
                TryGetInventoryItemEntityFromEntry: entry => entry switch
                {
                    var candidate when ReferenceEquals(candidate, inventoryEntry) => inventoryEntity,
                    var candidate when ReferenceEquals(candidate, duplicateInventoryEntry) => inventoryEntity,
                    var candidate when ReferenceEquals(candidate, nonInventoryEntry) => nonInventoryEntity,
                    _ => (Entity?)null,
                },
                ClassifyInventoryItemEntity: entity => ReferenceEquals(entity, inventoryEntity)
                    ? (true, string.Empty)
                    : (false, string.Empty)));

            bool success = service.TryEnumeratePrimaryInventoryItemEntitiesFast(new object(), out IReadOnlyList<Entity> items);

            success.Should().BeTrue();
            items.Should().ContainSingle();
            items[0].Address.Should().Be(1001);
        }

        [TestMethod]
        public void TryEnumerateInventoryItemEntities_UsesFreshCacheForSameController()
        {
            var controller = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
            object primaryInventory = new();
            object entry = new();
            Entity inventoryEntity = CreateEntity(address: 3003);
            int primaryInventoryCalls = 0;

            var service = new InventoryItemEntityService(new InventoryItemEntityServiceDependencies(
                CacheWindowMs: 1000,
                TryGetPrimaryServerInventory: _ =>
                {
                    primaryInventoryCalls++;
                    return (true, primaryInventory);
                },
                TryGetPrimaryServerInventorySlotItems: _ => (true, new[] { entry }),
                EnumerateObjects: collection => collection as IEnumerable<object?> ?? Array.Empty<object?>(),
                TryGetInventoryItemEntityFromEntry: _ => inventoryEntity,
                ClassifyInventoryItemEntity: _ => (true, string.Empty)));

            bool firstSuccess = service.TryEnumerateInventoryItemEntities(controller, out IReadOnlyList<Entity> firstItems);
            bool secondSuccess = service.TryEnumerateInventoryItemEntities(controller, out IReadOnlyList<Entity> secondItems);

            firstSuccess.Should().BeTrue();
            secondSuccess.Should().BeTrue();
            firstItems.Should().ContainSingle();
            secondItems.Should().ContainSingle();
            primaryInventoryCalls.Should().Be(1);
        }

        private static Entity CreateEntity(long address)
        {
            var entity = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            SetMember(entity, "Address", address);
            return entity;
        }

        private static void SetMember(object instance, string memberName, object value)
        {
            Type? currentType = instance.GetType();
            while (currentType != null)
            {
                FieldInfo? backingField = currentType.GetField($"<{memberName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?? currentType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    ?? currentType.GetField($"_{char.ToLowerInvariant(memberName[0])}{memberName[1..]}", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (backingField != null)
                {
                    backingField.SetValue(instance, value);
                    return;
                }

                PropertyInfo? property = currentType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                MethodInfo? setMethod = property?.GetSetMethod(nonPublic: true);
                if (setMethod != null)
                {
                    setMethod.Invoke(instance, [value]);
                    return;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Unable to set member '{memberName}' on {instance.GetType().FullName}.");
        }
    }
}