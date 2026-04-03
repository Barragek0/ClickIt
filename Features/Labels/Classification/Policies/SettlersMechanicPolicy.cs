
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
        {
            if (!settings.ClickSettlersOre || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            return mechanicId switch
            {
                var id when string.Equals(id, MechanicIds.SettlersCrimsonIron, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersCrimsonIron,
                var id when string.Equals(id, MechanicIds.SettlersCopper, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersCopper,
                var id when string.Equals(id, MechanicIds.SettlersPetrifiedWood, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersPetrifiedWood,
                var id when string.Equals(id, MechanicIds.SettlersBismuth, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersBismuth,
                var id when string.Equals(id, MechanicIds.SettlersHourglass, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersOre,
                var id when string.Equals(id, MechanicIds.SettlersVerisium, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersVerisium,
                _ => false
            };
        }

        internal static bool IsEnabled(ClickItSettings settings, string? mechanicId)
        {
            if (settings.ClickSettlersOre?.Value != true || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            return mechanicId switch
            {
                var id when string.Equals(id, MechanicIds.SettlersCrimsonIron, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersCrimsonIron?.Value == true,
                var id when string.Equals(id, MechanicIds.SettlersCopper, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersCopper?.Value == true,
                var id when string.Equals(id, MechanicIds.SettlersPetrifiedWood, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersPetrifiedWood?.Value == true,
                var id when string.Equals(id, MechanicIds.SettlersBismuth, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersBismuth?.Value == true,
                var id when string.Equals(id, MechanicIds.SettlersHourglass, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersOre?.Value == true,
                var id when string.Equals(id, MechanicIds.SettlersVerisium, StringComparison.OrdinalIgnoreCase) => settings.ClickSettlersVerisium?.Value == true,
                _ => false
            };
        }
    }
}