using System.Collections.Generic;
using System;
using ClickIt.Utils;

namespace ClickIt.Components
{
    // Minimal test-only SecondaryAltarComponent to satisfy WeightCalculator dependencies
    public class SecondaryAltarComponent
    {
        // Test-only flag to indicate unmatched mods
        public bool HasUnmatchedMods { get; set; } = false;

        public object Element { get; set; }
        private readonly string[] _upsides = new string[8];
        private readonly string[] _downsides = new string[8];

        public SecondaryAltarComponent(List<string> upsides = null, List<string> downsides = null)
        {
            if (upsides != null)
            {
                for (int i = 0; i < upsides.Count && i < 8; i++) _upsides[i] = upsides[i] ?? "";
            }
            if (downsides != null)
            {
                for (int i = 0; i < downsides.Count && i < 8; i++) _downsides[i] = downsides[i] ?? "";
            }
            Element = new object();
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

    public class AltarButton { public int Dummy { get; set; } }

    public enum AltarType { Exarch, Eater }

    // Minimal PrimaryAltarComponent used in tests
    public class PrimaryAltarComponent
    {
        public AltarType AltarType { get; set; }
        public SecondaryAltarComponent TopMods { get; set; }
        public AltarButton TopButton { get; set; }
        public SecondaryAltarComponent BottomMods { get; set; }
        public AltarButton BottomButton { get; set; }

        private AltarWeights? _cachedWeights;

        public PrimaryAltarComponent(SecondaryAltarComponent top, SecondaryAltarComponent bottom)
        {
            TopMods = top;
            BottomMods = bottom;
            TopButton = new AltarButton();
            BottomButton = new AltarButton();
        }

        // Simplified test-friendly implementation matching production API used by ClickService
        public bool IsValidCached()
        {
            // In tests we treat the presence of Element as validity and no unmatched mods
            return TopMods?.Element != null && BottomMods?.Element != null && !TopMods.HasUnmatchedMods && !BottomMods.HasUnmatchedMods;
        }

        public AltarWeights? GetCachedWeights(Func<PrimaryAltarComponent, AltarWeights> weightCalculator)
        {
            if (!_cachedWeights.HasValue)
            {
                _cachedWeights = weightCalculator(this);
            }
            return _cachedWeights;
        }
    }
}
