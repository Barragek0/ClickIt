using System.Collections.Generic;

namespace ClickIt.Tests
{
    public class MockAltarComponent
    {
        public MockSecondaryAltarComponent TopMods { get; set; }
        public MockSecondaryAltarComponent BottomMods { get; set; }
    }

    public class MockSecondaryAltarComponent
    {
        public List<string> Upsides { get; set; } = new List<string>();
        public List<string> Downsides { get; set; } = new List<string>();

        public string GetUpsideByIndex(int index)
        {
            if (index < 0 || index >= 4) return "";
            return Upsides.Count > index ? Upsides[index] : "";
        }

        public string GetDownsideByIndex(int index)
        {
            if (index < 0 || index >= 4) return "";
            return Downsides.Count > index ? Downsides[index] : "";
        }

        public string FirstUpside => GetUpsideByIndex(0);
        public string SecondUpside => GetUpsideByIndex(1);
        public string ThirdUpside => GetUpsideByIndex(2);
        public string FourthUpside => GetUpsideByIndex(3);
        public string FifthUpside => GetUpsideByIndex(4);
        public string SixthUpside => GetUpsideByIndex(5);
        public string SeventhUpside => GetUpsideByIndex(6);
        public string EighthUpside => GetUpsideByIndex(7);
        public string FirstDownside => GetDownsideByIndex(0);
        public string SecondDownside => GetDownsideByIndex(1);
        public string ThirdDownside => GetDownsideByIndex(2);
        public string FourthDownside => GetDownsideByIndex(3);
        public string FifthDownside => GetDownsideByIndex(4);
        public string SixthDownside => GetDownsideByIndex(5);
        public string SeventhDownside => GetDownsideByIndex(6);
        public string EighthDownside => GetDownsideByIndex(7);
    }
}
