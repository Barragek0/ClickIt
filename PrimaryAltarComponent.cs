using static ClickIt.ClickIt;

namespace ClickIt
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

        public AltarType AltarType;
        public SecondaryAltarComponent TopMods;
        public AltarButton TopButton;
        public SecondaryAltarComponent BottomMods;
        public AltarButton BottomButton;
    }
}
