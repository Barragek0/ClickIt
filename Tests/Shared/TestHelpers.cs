using System.Collections.Generic;
using ClickIt.Components;

namespace ClickIt.Tests.Shared
{
    public static class TestHelpers
    {
        public static ClickItSettings CreateSettingsWithTiers(Dictionary<string, int> tiers = null)
        {
            var s = new ClickItSettings();
            s.ModTiers.Clear();
            if (tiers != null)
            {
                foreach (var kv in tiers)
                    s.ModTiers[kv.Key] = kv.Value;
            }
            return s;
        }

        public static ClickIt.Utils.WeightCalculator CreateWeightCalculator(Dictionary<string, int> tiers = null)
        {
            var s = CreateSettingsWithTiers(tiers);
            return new ClickIt.Utils.WeightCalculator(s);
        }

        public static PrimaryAltarComponent CreatePrimaryAltar(
            List<string> topUps = null, List<string> topDowns = null,
            List<string> bottomUps = null, List<string> bottomDowns = null)
        {
            var top = new SecondaryAltarComponent(topUps ?? new List<string>(), topDowns ?? new List<string>());
            var bottom = new SecondaryAltarComponent(bottomUps ?? new List<string>(), bottomDowns ?? new List<string>());
            return new PrimaryAltarComponent(top, bottom);
        }
    }
}
