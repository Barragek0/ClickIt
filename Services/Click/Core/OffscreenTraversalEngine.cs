using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace ClickIt.Services
{
    internal sealed class OffscreenTraversalEngine(OffscreenPathingCoordinatorDependencies dependencies)
    {
        private readonly OffscreenPathingCoordinator _coordinator = new(dependencies);

        internal bool TryWalkTowardOffscreenTarget(Entity? preferredTarget = null)
            => _coordinator.TryWalkTowardOffscreenTarget(preferredTarget);

        internal bool TryHandleStickyOffscreenTarget(Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels)
            => _coordinator.TryHandleStickyOffscreenTarget(windowTopLeft, allLabels);

        internal bool IsStickyTarget(Entity? entity)
            => _coordinator.IsStickyTarget(entity);

        internal void ClearStickyOffscreenTarget()
            => _coordinator.ClearStickyOffscreenTarget();
    }
}