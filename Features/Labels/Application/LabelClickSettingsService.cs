using ClickIt.Features.Mechanics;
using ClickIt.Features.Labels.Selection;
using ExileCore.PoEMemory.Elements;

namespace ClickIt.Features.Labels.Application
{
    internal sealed class LabelClickSettingsService(
        ClickItSettings settings,
        IMechanicPrioritySnapshotProvider mechanicPrioritySnapshotProvider,
        Func<IReadOnlyList<LabelOnGround>?, bool> hasLazyModeRestrictedItems,
        Func<Keys, bool> isClickHotkeyHeld)
    {
        private readonly ClickSettingsFactory _factory = new(
            settings,
            mechanicPrioritySnapshotProvider,
            hasLazyModeRestrictedItems,
            isClickHotkeyHeld);

        public ClickSettings Create(IReadOnlyList<LabelOnGround>? allLabels)
            => _factory.Create(allLabels);
    }
}