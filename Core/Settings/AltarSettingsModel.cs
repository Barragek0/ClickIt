using System;
using System.Collections.Generic;
using ClickIt.Definitions;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        internal void InitializeDefaultWeights()
        {
            foreach ((string id, _, string type, int defaultValue) in AltarModsConstants.UpsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                ModTiers.TryAdd(compositeKey, defaultValue);
            }

            foreach ((string id, _, string type, int defaultValue) in AltarModsConstants.DownsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                ModTiers.TryAdd(compositeKey, defaultValue);
            }

            foreach ((string id, _, string type, int _) in AltarModsConstants.UpsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                bool isDivineOrbAlert = (type == AltarTypeMinion && id == "#% chance to drop an additional Divine Orb")
                    || (type == AltarTypeBoss && id == "Final Boss drops # additional Divine Orbs");

                if (ModAlerts.TryAdd(compositeKey, false) && isDivineOrbAlert)
                {
                    ModAlerts[compositeKey] = true;
                }
            }
        }

        private static string BuildCompositeKey(string type, string id)
        {
            return $"{type}|{id}";
        }

        internal void EnsureAllModsHaveWeights()
        {
            InitializeDefaultWeights();
        }

        internal int GetModTier(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return 1;

            return ModTiers.TryGetValue(modId, out int value) ? value : 1;
        }

        internal int GetModTier(string modId, string type)
        {
            if (string.IsNullOrEmpty(modId))
                return 1;

            string compositeKey = BuildCompositeKey(type, modId);
            return ModTiers.TryGetValue(compositeKey, out int value) ? value : 1;
        }

        internal bool GetModAlert(string modId, string type)
        {
            if (string.IsNullOrEmpty(modId))
                return false;

            string compositeKey = BuildCompositeKey(type, modId);
            if (ModAlerts.TryGetValue(compositeKey, out bool enabled))
                return enabled;

            if (ModAlerts.TryGetValue(modId, out enabled))
                return enabled;

            return false;
        }

        public Dictionary<string, int> ModTiers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, bool> ModAlerts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}