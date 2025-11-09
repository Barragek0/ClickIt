using System.Collections.Generic;
using System.Linq;
using ClickIt.Components;
using ClickIt.Utils;

namespace ClickIt.Services
{
    public class AltarWeightCalculator
    {
        private readonly ClickItSettings _settings;
        public AltarWeightCalculator(ClickItSettings settings)
        {
            _settings = settings;
        }
        public Utils.AltarWeights CalculateAltarWeights(PrimaryAltarComponent altar)
        {
            decimal topUpsideWeight = CalculateUpsideWeight(altar.TopMods.Upsides);
            decimal topDownsideWeight = CalculateDownsideWeight(altar.TopMods.Downsides);
            decimal bottomUpsideWeight = CalculateUpsideWeight(altar.BottomMods.Upsides);
            decimal bottomDownsideWeight = CalculateDownsideWeight(altar.BottomMods.Downsides);
            return new Utils.AltarWeights
            {
                TopUpsideWeight = topUpsideWeight,
                TopDownsideWeight = topDownsideWeight,
                BottomUpsideWeight = bottomUpsideWeight,
                BottomDownsideWeight = bottomDownsideWeight,
                TopDownside1Weight = CalculateDownsideWeight(new List<string> { altar.TopMods.FirstDownside }),
                TopDownside2Weight = CalculateDownsideWeight(new List<string> { altar.TopMods.SecondDownside }),
                TopDownside3Weight = CalculateDownsideWeight(new List<string> { altar.TopMods.ThirdDownside }),
                TopDownside4Weight = CalculateDownsideWeight(new List<string> { altar.TopMods.FourthDownside }),
                BottomDownside1Weight = CalculateDownsideWeight(new List<string> { altar.BottomMods.FirstDownside }),
                BottomDownside2Weight = CalculateDownsideWeight(new List<string> { altar.BottomMods.SecondDownside }),
                BottomDownside3Weight = CalculateDownsideWeight(new List<string> { altar.BottomMods.ThirdDownside }),
                BottomDownside4Weight = CalculateDownsideWeight(new List<string> { altar.BottomMods.FourthDownside }),
                TopUpside1Weight = CalculateUpsideWeight(new List<string> { altar.TopMods.FirstUpside }),
                TopUpside2Weight = CalculateUpsideWeight(new List<string> { altar.TopMods.SecondUpside }),
                TopUpside3Weight = CalculateUpsideWeight(new List<string> { altar.TopMods.ThirdUpside }),
                TopUpside4Weight = CalculateUpsideWeight(new List<string> { altar.TopMods.FourthUpside }),
                BottomUpside1Weight = CalculateUpsideWeight(new List<string> { altar.BottomMods.FirstUpside }),
                BottomUpside2Weight = CalculateUpsideWeight(new List<string> { altar.BottomMods.SecondUpside }),
                BottomUpside3Weight = CalculateUpsideWeight(new List<string> { altar.BottomMods.ThirdUpside }),
                BottomUpside4Weight = CalculateUpsideWeight(new List<string> { altar.BottomMods.FourthUpside }),
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
            decimal totalWeight = 1;
            if (downsides == null) return totalWeight;
            foreach (string downside in downsides.Where(d => !string.IsNullOrEmpty(d)))
            {
                int weight = _settings.GetModTier(downside);
                totalWeight += weight;
            }
            return totalWeight;
        }
    }
}
