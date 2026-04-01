using ExileCore.PoEMemory.Elements;
using ClickIt.Services.Label.Selection;

namespace ClickIt.Services
{
    public partial class LabelFilterService
    {
        internal ClickSettings CreateClickSettings(IReadOnlyList<LabelOnGround>? allLabels)
        {
            var factory = new ClickSettingsFactory(
                _settings,
                _mechanicPrioritySnapshotService,
                labels => LazyModeRestrictedChecker(this, labels),
                KeyStateProvider);

            return factory.Create(allLabels);
        }

    }
}