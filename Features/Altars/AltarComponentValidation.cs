namespace ClickIt.Features.Altars
{
    internal static class AltarComponentValidation
    {
        internal static bool IsComponentComplete(PrimaryAltarComponent altarComponent)
            => altarComponent.TopMods != null
                && altarComponent.TopButton != null
                && altarComponent.BottomMods != null
                && altarComponent.BottomButton != null;

        internal static bool ShouldRemoveInvalidCachedComponent(PrimaryAltarComponent altar)
            => altar.TopMods?.Element == null
                || altar.BottomMods?.Element == null
                || !altar.TopMods.Element.IsValid
                || !altar.BottomMods.Element.IsValid;
    }
}