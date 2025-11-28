using ClickIt.Components;

namespace ClickIt.Services
{
    public partial class AltarService
    {
        // Adapter-based update helper (internal for tests)
        internal static void UpdateAltarComponentFromAdapter(bool top, PrimaryAltarComponent altarComponent, IElementAdapter element,
            List<string> upsides, List<string> downsides, bool hasUnmatchedMods)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            var underlying = element.Underlying;
            // Allow underlying to be null for unit tests that mock adapters without creating real Elements
            UpdateAltarComponent(top, altarComponent, underlying, upsides, downsides, hasUnmatchedMods);
        }
    }
}
