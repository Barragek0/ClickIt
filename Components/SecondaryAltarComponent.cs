using ExileCore.PoEMemory;
using System.Collections.Generic;
using ClickIt.Utils;
namespace ClickIt.Components
{
#nullable enable
    public class SecondaryAltarComponent
    {
        public SecondaryAltarComponent(Element? element, List<string> upsides, List<string> downsides, bool hasUnmatchedMods = false)
        {
            this.Element = element;
            this.Upsides = upsides ?? [];
            this.Downsides = downsides ?? [];
            this.HasUnmatchedMods = hasUnmatchedMods;

            _upsides = new string[8];
            _downsides = new string[8];

            for (int i = 0; i < 8; i++)
            {
                _upsides[i] = Upsides.Count > i ? Upsides[i] : "";
                _downsides[i] = Downsides.Count > i ? Downsides[i] : "";
            }
        }

        public Element? Element { get; set; }
        public List<string> Upsides { get; set; }
        public List<string> Downsides { get; set; }
        public bool HasUnmatchedMods { get; set; }

        private readonly string[] _upsides;
        private readonly string[] _downsides;

        public string this[int index] => GetModByIndex(index);

        public string GetModByIndex(int index)
        {
            if (index < 0 || index >= 8) return "";
            return index < 4 ? _upsides[index] : _downsides[index - 4];
        }

        public string GetUpsideByIndex(int index)
        {
            if (index < 0 || index >= 8) return "";
            return _upsides[index];
        }

        public string GetDownsideByIndex(int index)
        {
            if (index < 0 || index >= 8) return "";
            return _downsides[index];
        }

        public string[] GetAllUpsides() => _upsides;
        public string[] GetAllDownsides() => _downsides;
    }
}
