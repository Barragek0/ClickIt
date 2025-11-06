using static ClickIt.ClickIt;
namespace ClickIt.Components
{
    public class PrimaryAltarComponent
    {
        public PrimaryAltarComponent(AltarType AltarType, SecondaryAltarComponent TopMods, AltarButton TopButton, SecondaryAltarComponent BottomMods, AltarButton BottomButton)
        {
            this.AltarType = AltarType;
            this.TopMods = TopMods;
            this.TopButton = TopButton;
            this.BottomMods = BottomMods;
            this.BottomButton = BottomButton;
        }
        public AltarType AltarType { get; set; }
        public SecondaryAltarComponent TopMods { get; set; }
        public AltarButton TopButton { get; set; }
        public SecondaryAltarComponent BottomMods { get; set; }
        public AltarButton BottomButton { get; set; }
    }
}
