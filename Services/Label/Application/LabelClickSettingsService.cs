using ClickIt.Services.Mechanics;
using ClickIt.Services.Label.Selection;
using ExileCore.PoEMemory.Elements;
using System.Windows.Forms;

namespace ClickIt.Services.Label.Application
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