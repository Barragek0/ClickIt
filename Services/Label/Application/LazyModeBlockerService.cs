using ExileCore.PoEMemory.Elements;

namespace ClickIt.Services.Label.Application
{
    internal sealed class LazyModeBlockerService(Func<IReadOnlyList<LabelOnGround>?, bool> hasRestrictedItemsOnScreenCore)
    {
        private readonly Func<IReadOnlyList<LabelOnGround>?, bool> _hasRestrictedItemsOnScreenCore = hasRestrictedItemsOnScreenCore;

        public bool HasRestrictedItemsOnScreen(IReadOnlyList<LabelOnGround>? allLabels)
            => _hasRestrictedItemsOnScreenCore(allLabels);
    }
}