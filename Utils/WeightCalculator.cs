using ClickIt.Components;
using System;
using System.Collections.Generic;
namespace ClickIt.Utils
{
    public class WeightCalculator
    {
        private readonly ClickItSettings _settings;
        public WeightCalculator(ClickItSettings settings)
        {
            _settings = settings;
        }
        public AltarWeights CalculateAltarWeights(PrimaryAltarComponent altar)
        {
            decimal TopUpsideWeight = CalculateUpsideWeight(altar.TopMods.Upsides);
            decimal TopDownsideWeight = CalculateDownsideWeight(altar.TopMods.Downsides);
            decimal BottomUpsideWeight = CalculateUpsideWeight(altar.BottomMods.Upsides);
            decimal BottomDownsideWeight = CalculateDownsideWeight(altar.BottomMods.Downsides);
            return new AltarWeights
            {
                TopUpsideWeight = TopUpsideWeight,
                TopDownsideWeight = TopDownsideWeight,
                BottomUpsideWeight = BottomUpsideWeight,
                BottomDownsideWeight = BottomDownsideWeight,
                TopDownside1Weight = CalculateDownsideWeight([altar.TopMods.FirstDownside]),
                TopDownside2Weight = CalculateDownsideWeight([altar.TopMods.SecondDownside]),
                TopDownside3Weight = CalculateDownsideWeight([altar.TopMods.ThirdDownside]),
                TopDownside4Weight = CalculateDownsideWeight([altar.TopMods.FourthDownside]),
                BottomDownside1Weight = CalculateDownsideWeight([altar.BottomMods.FirstDownside]),
                BottomDownside2Weight = CalculateDownsideWeight([altar.BottomMods.SecondDownside]),
                BottomDownside3Weight = CalculateDownsideWeight([altar.BottomMods.ThirdDownside]),
                BottomDownside4Weight = CalculateDownsideWeight([altar.BottomMods.FourthDownside]),
                TopUpside1Weight = CalculateUpsideWeight([altar.TopMods.FirstUpside]),
                TopUpside2Weight = CalculateUpsideWeight([altar.TopMods.SecondUpside]),
                TopUpside3Weight = CalculateUpsideWeight([altar.TopMods.ThirdUpside]),
                TopUpside4Weight = CalculateUpsideWeight([altar.TopMods.FourthUpside]),
                BottomUpside1Weight = CalculateUpsideWeight([altar.BottomMods.FirstUpside]),
                BottomUpside2Weight = CalculateUpsideWeight([altar.BottomMods.SecondUpside]),
                BottomUpside3Weight = CalculateUpsideWeight([altar.BottomMods.ThirdUpside]),
                BottomUpside4Weight = CalculateUpsideWeight([altar.BottomMods.FourthUpside]),
                TopWeight = Math.Round(TopUpsideWeight / TopDownsideWeight, 2),
                BottomWeight = Math.Round(BottomUpsideWeight / BottomDownsideWeight, 2)
            };
        }
        public decimal CalculateUpsideWeight(List<string> upsides)
        {
            decimal totalWeight = 0;
            if (upsides == null) return totalWeight;
            foreach (string upside in upsides)
            {
                if (string.IsNullOrEmpty(upside)) continue;
                int weight = _settings.GetModTier(upside);
                totalWeight += weight;
            }
            return totalWeight;
        }
        public decimal CalculateDownsideWeight(List<string> downsides)
        {
            decimal totalWeight = 1;
            if (downsides == null) return totalWeight;
            foreach (string downside in downsides)
            {
                if (string.IsNullOrEmpty(downside)) continue;
                int weight = _settings.GetModTier(downside);
                totalWeight += weight;
            }
            return totalWeight;
        }
    }
    public struct AltarWeights
    {
        public decimal TopUpsideWeight { get; set; }
        public decimal TopDownsideWeight { get; set; }
        public decimal BottomUpsideWeight { get; set; }
        public decimal BottomDownsideWeight { get; set; }
        public decimal TopDownside1Weight { get; set; }
        public decimal TopDownside2Weight { get; set; }
        public decimal TopDownside3Weight { get; set; }
        public decimal TopDownside4Weight { get; set; }
        public decimal BottomDownside1Weight { get; set; }
        public decimal BottomDownside2Weight { get; set; }
        public decimal BottomDownside3Weight { get; set; }
        public decimal BottomDownside4Weight { get; set; }
        public decimal TopUpside1Weight { get; set; }
        public decimal TopUpside2Weight { get; set; }
        public decimal TopUpside3Weight { get; set; }
        public decimal TopUpside4Weight { get; set; }
        public decimal BottomUpside1Weight { get; set; }
        public decimal BottomUpside2Weight { get; set; }
        public decimal BottomUpside3Weight { get; set; }
        public decimal BottomUpside4Weight { get; set; }
        public decimal TopWeight { get; set; }
        public decimal BottomWeight { get; set; }
    }
}
