using ClickIt.Components;

namespace ClickIt.Services
{
    public partial class AltarService
    {
        internal static void UpdateAltarComponentFromAdapter(bool top, PrimaryAltarComponent altarComponent, IElementAdapter element,
            List<string> upsides, List<string> downsides, bool hasUnmatchedMods)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            var underlying = element.Underlying;
            UpdateAltarComponent(top, altarComponent, underlying, upsides, downsides, hasUnmatchedMods);
        }
    }
}
