namespace ClickIt.Core.Runtime
{
    internal static class PluginDelveFlarePolicy
    {
        private const string DarknessBuffName = "delve_degen_buff";

        internal static int FindDarknessDebuffCharges(IEnumerable<Buff>? buffs)
        {
            if (buffs == null)
                return -1;

            foreach (Buff? buff in buffs)
            {
                if (buff != null && string.Equals(buff.Name, DarknessBuffName, StringComparison.Ordinal))
                    return buff.BuffCharges;
            }

            return -1;
        }

        internal static bool ShouldUseFlare(
            int darknessDebuffCharges,
            int darknessDebuffThreshold,
            float healthPercent,
            int healthThreshold,
            float energyShieldPercent,
            int energyShieldThreshold)
        {
            return darknessDebuffCharges >= darknessDebuffThreshold
                && healthPercent <= healthThreshold
                && energyShieldPercent <= energyShieldThreshold;
        }
    }
}