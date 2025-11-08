using ExileCore.PoEMemory;
using System.Collections.Generic;
namespace ClickIt.Components
{
    public class SecondaryAltarComponent
    {
        public SecondaryAltarComponent(Element element, List<string> upsides, List<string> downsides, bool hasUnmatchedMods = false)
        {
            this.Element = element;
            this.Upsides = upsides;
            this.Downsides = downsides;
            this.HasUnmatchedMods = hasUnmatchedMods;
        }
        public Element Element { get; set; }
        public List<string> Upsides { get; set; }
        public List<string> Downsides { get; set; }
        public bool HasUnmatchedMods { get; set; }
        public string FirstUpside => Upsides.Count > 0 ? Upsides[0] : "";
        public string SecondUpside => Upsides.Count > 1 ? Upsides[1] : "";
        public string FirstDownside => Downsides.Count > 0 ? Downsides[0] : "";
        public string SecondDownside => Downsides.Count > 1 ? Downsides[1] : "";
    }
}
