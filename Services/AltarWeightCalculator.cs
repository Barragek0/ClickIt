
using System.Collections.Generic;
using System.Linq;

namespace ClickIt.Services
{
    /// <summary>
    /// Handles altar weight calculations and decision-making logic
    /// </summary>
    public class AltarWeightCalculator
    {
        private readonly ClickItSettings _settings;

        public AltarWeightCalculator(ClickItSettings settings)
        {
            _settings = settings;
        }

        public AltarWeights CalculateAltarWeights(PrimaryAltarComponent altar)
        {
            decimal topUpsideWeight = CalculateUpsideWeight(altar.TopMods.Upsides);
            decimal topDownsideWeight = CalculateDownsideWeight(altar.TopMods.Downsides);
            decimal bottomUpsideWeight = CalculateUpsideWeight(altar.BottomMods.Upsides);
            decimal bottomDownsideWeight = CalculateDownsideWeight(altar.BottomMods.Downsides);

            return new AltarWeights
            {
                TopUpsideWeight = topUpsideWeight,
                TopDownsideWeight = topDownsideWeight,
                BottomUpsideWeight = bottomUpsideWeight,
                BottomDownsideWeight = bottomDownsideWeight,
                TopDownside1Weight = CalculateDownsideWeight(new List<string> { altar.TopMods.FirstDownside }),
                TopDownside2Weight = CalculateDownsideWeight(new List<string> { altar.TopMods.SecondDownside }),
                BottomDownside1Weight = CalculateDownsideWeight(new List<string> { altar.BottomMods.FirstDownside }),
                BottomDownside2Weight = CalculateDownsideWeight(new List<string> { altar.BottomMods.SecondDownside }),
                TopUpside1Weight = CalculateUpsideWeight(new List<string> { altar.TopMods.FirstUpside }),
                TopUpside2Weight = CalculateUpsideWeight(new List<string> { altar.TopMods.SecondUpside }),
                BottomUpside1Weight = CalculateUpsideWeight(new List<string> { altar.BottomMods.FirstUpside }),
                BottomUpside2Weight = CalculateUpsideWeight(new List<string> { altar.BottomMods.SecondUpside }),
                TopWeight = System.Math.Round(topUpsideWeight / topDownsideWeight, 2),
                BottomWeight = System.Math.Round(bottomUpsideWeight / bottomDownsideWeight, 2)
            };
        }

        private decimal CalculateUpsideWeight(List<string> upsides)
        {
            decimal totalWeight = 0;
            if (upsides == null) return totalWeight;

            foreach (string upside in upsides.Where(u => !string.IsNullOrEmpty(u)))
            {
                int weight = _settings.GetModTier(upside);
                totalWeight += weight;
            }
            return totalWeight;
        }

        private decimal CalculateDownsideWeight(List<string> downsides)
        {
            decimal totalWeight = 1; // Start with 1 to avoid division by zero
            if (downsides == null) return totalWeight;

            foreach (string downside in downsides.Where(d => !string.IsNullOrEmpty(d)))
            {
                int weight = _settings.GetModTier(downside);
                totalWeight += weight;
            }
            return totalWeight;
        }
    }

    public struct AltarWeights
    {
        public decimal TopUpsideWeight;
        public decimal TopDownsideWeight;
        public decimal BottomUpsideWeight;
        public decimal BottomDownsideWeight;
        public decimal TopDownside1Weight;
        public decimal TopDownside2Weight;
        public decimal BottomDownside1Weight;
        public decimal BottomDownside2Weight;
        public decimal TopUpside1Weight;
        public decimal TopUpside2Weight;
        public decimal BottomUpside1Weight;
        public decimal BottomUpside2Weight;
        public decimal TopWeight;
        public decimal BottomWeight;
    }
}