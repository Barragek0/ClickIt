namespace ClickIt.Features.Labels.Classification.Policies
{
    // The six Settlers ore toggles read from either settings type; the enabled rule is a single method over this value.
    internal readonly record struct SettlersClickFlags(
        bool ClickOre,
        bool ClickCrimsonIron,
        bool ClickCopper,
        bool ClickPetrifiedWood,
        bool ClickBismuth,
        bool ClickVerisium)
    {
        internal static SettlersClickFlags From(ClickSettings settings)
            => new(
                settings.ClickSettlersOre,
                settings.ClickSettlersCrimsonIron,
                settings.ClickSettlersCopper,
                settings.ClickSettlersPetrifiedWood,
                settings.ClickSettlersBismuth,
                settings.ClickSettlersVerisium);

        internal static SettlersClickFlags From(ClickItSettings settings)
            => new(
                settings.ClickSettlersOre?.Value == true,
                settings.ClickSettlersCrimsonIron?.Value == true,
                settings.ClickSettlersCopper?.Value == true,
                settings.ClickSettlersPetrifiedWood?.Value == true,
                settings.ClickSettlersBismuth?.Value == true,
                settings.ClickSettlersVerisium?.Value == true);
    }

    internal static class SettlersMechanicPolicy
    {
        internal static bool IsSettlersMechanicId(string? mechanicId)
            => !string.IsNullOrWhiteSpace(mechanicId)
               && mechanicId.StartsWith("settlers-", StringComparison.OrdinalIgnoreCase);

        internal static bool RequiresHoldClick(string? mechanicId)
            => string.Equals(mechanicId, MechanicIds.SettlersVerisium, StringComparison.OrdinalIgnoreCase);

        internal static bool IsEnabled(SettlersClickFlags flags, string? mechanicId)
        {
            if (!flags.ClickOre || string.IsNullOrWhiteSpace(mechanicId))
                return false;

            return mechanicId switch
            {
                var id when string.Equals(id, MechanicIds.SettlersCrimsonIron, StringComparison.OrdinalIgnoreCase) => flags.ClickCrimsonIron,
                var id when string.Equals(id, MechanicIds.SettlersCopper, StringComparison.OrdinalIgnoreCase) => flags.ClickCopper,
                var id when string.Equals(id, MechanicIds.SettlersPetrifiedWood, StringComparison.OrdinalIgnoreCase) => flags.ClickPetrifiedWood,
                var id when string.Equals(id, MechanicIds.SettlersBismuth, StringComparison.OrdinalIgnoreCase) => flags.ClickBismuth,
                var id when string.Equals(id, MechanicIds.SettlersHourglass, StringComparison.OrdinalIgnoreCase) => flags.ClickOre,
                var id when string.Equals(id, MechanicIds.SettlersVerisium, StringComparison.OrdinalIgnoreCase) => flags.ClickVerisium,
                _ => false
            };
        }
    }
}