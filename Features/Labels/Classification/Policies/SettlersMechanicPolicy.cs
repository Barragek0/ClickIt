namespace ClickIt.Features.Labels.Classification.Policies
{
    internal static class SettlersMechanicPolicy
    {
        internal static bool IsSettlersMechanicId(string? mechanicId)
            => !string.IsNullOrWhiteSpace(mechanicId)
               && mechanicId.StartsWith("settlers-", StringComparison.OrdinalIgnoreCase);

        internal static bool RequiresHoldClick(string? mechanicId)
            => string.Equals(mechanicId, MechanicIds.SettlersVerisium, StringComparison.OrdinalIgnoreCase);

        internal static bool IsEnabled(ClickSettings settings, string? mechanicId)
            => IsEnabledCore(
                settings.ClickSettlersOre,
                mechanicId,
                settings.ClickSettlersCrimsonIron,
                settings.ClickSettlersCopper,
                settings.ClickSettlersPetrifiedWood,
                settings.ClickSettlersBismuth,
                settings.ClickSettlersVerisium);

        internal static bool IsEnabled(ClickItSettings settings, string? mechanicId)
            => IsEnabledCore(
                settings.ClickSettlersOre?.Value == true,
                mechanicId,
                settings.ClickSettlersCrimsonIron?.Value == true,
                settings.ClickSettlersCopper?.Value == true,
                settings.ClickSettlersPetrifiedWood?.Value == true,
                settings.ClickSettlersBismuth?.Value == true,
                settings.ClickSettlersVerisium?.Value == true);

        private static bool IsEnabledCore(
            bool clickSettlersOre,
            string? mechanicId,
            bool clickCrimsonIron,
            bool clickCopper,
            bool clickPetrifiedWood,
            bool clickBismuth,
            bool clickVerisium)
        {
            if (!clickSettlersOre || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            return mechanicId switch
            {
                var id when string.Equals(id, MechanicIds.SettlersCrimsonIron, StringComparison.OrdinalIgnoreCase) => clickCrimsonIron,
                var id when string.Equals(id, MechanicIds.SettlersCopper, StringComparison.OrdinalIgnoreCase) => clickCopper,
                var id when string.Equals(id, MechanicIds.SettlersPetrifiedWood, StringComparison.OrdinalIgnoreCase) => clickPetrifiedWood,
                var id when string.Equals(id, MechanicIds.SettlersBismuth, StringComparison.OrdinalIgnoreCase) => clickBismuth,
                var id when string.Equals(id, MechanicIds.SettlersHourglass, StringComparison.OrdinalIgnoreCase) => clickSettlersOre,
                var id when string.Equals(id, MechanicIds.SettlersVerisium, StringComparison.OrdinalIgnoreCase) => clickVerisium,
                _ => false
            };
        }
    }
}