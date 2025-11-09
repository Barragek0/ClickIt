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

            // Pre-cache upside/downside strings to avoid repeated list access
            _firstUpside = Upsides.Count > 0 ? Upsides[0] : "";
            _secondUpside = Upsides.Count > 1 ? Upsides[1] : "";
            _thirdUpside = Upsides.Count > 2 ? Upsides[2] : "";
            _fourthUpside = Upsides.Count > 3 ? Upsides[3] : "";
            _firstDownside = Downsides.Count > 0 ? Downsides[0] : "";
            _secondDownside = Downsides.Count > 1 ? Downsides[1] : "";
            _thirdDownside = Downsides.Count > 2 ? Downsides[2] : "";
            _fourthDownside = Downsides.Count > 3 ? Downsides[3] : "";
        }

        public Element Element { get; set; }
        public List<string> Upsides { get; set; }
        public List<string> Downsides { get; set; }
        public bool HasUnmatchedMods { get; set; }

        // Cached values for performance
        private readonly string _firstUpside;
        private readonly string _secondUpside;
        private readonly string _thirdUpside;
        private readonly string _fourthUpside;
        private readonly string _firstDownside;
        private readonly string _secondDownside;
        private readonly string _thirdDownside;
        private readonly string _fourthDownside;

        public string FirstUpside => _firstUpside;
        public string SecondUpside => _secondUpside;
        public string ThirdUpside => _thirdUpside;
        public string FourthUpside => _fourthUpside;
        public string FirstDownside => _firstDownside;
        public string SecondDownside => _secondDownside;
        public string ThirdDownside => _thirdDownside;
        public string FourthDownside => _fourthDownside;
    }
}
