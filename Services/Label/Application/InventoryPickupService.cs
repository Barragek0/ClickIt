using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;

namespace ClickIt.Services.Label.Application
{
    internal sealed class InventoryPickupService(
        Func<Entity, GameController?, bool> shouldAllowWorldItemWhenInventoryFullCore,
        Func<GameController?, bool> shouldAllowClosedDoorPastMechanicCore)
    {
        private readonly Func<Entity, GameController?, bool> _shouldAllowWorldItemWhenInventoryFullCore = shouldAllowWorldItemWhenInventoryFullCore;
        private readonly Func<GameController?, bool> _shouldAllowClosedDoorPastMechanicCore = shouldAllowClosedDoorPastMechanicCore;

        public bool ShouldAllowWorldItemWhenInventoryFull(Entity groundItem, GameController? gameController)
            => _shouldAllowWorldItemWhenInventoryFullCore(groundItem, gameController);

        public bool ShouldAllowClosedDoorPastMechanic(GameController? gameController)
            => _shouldAllowClosedDoorPastMechanicCore(gameController);
    }
}