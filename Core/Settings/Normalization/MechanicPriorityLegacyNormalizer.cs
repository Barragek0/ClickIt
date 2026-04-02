using ClickIt.Definitions;

namespace ClickIt
{
    internal static class MechanicPriorityLegacyNormalizer
    {
        internal static void Normalize(ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            HashSet<string> normalizedIgnoreDistance = new(StringComparer.OrdinalIgnoreCase);
            foreach (string id in settings.MechanicPriorityIgnoreDistanceIds)
            {
                foreach (string expandedId in ExpandLegacyMechanicId(id))
                {
                    if (!string.IsNullOrWhiteSpace(expandedId))
                        normalizedIgnoreDistance.Add(expandedId);
                }
            }

            if (normalizedIgnoreDistance.Count > 0)
                settings.MechanicPriorityIgnoreDistanceIds = normalizedIgnoreDistance;

            if (settings.MechanicPriorityIgnoreDistanceWithinById.Count == 0)
                return;

            Dictionary<string, int> normalizedWithinById = new(StringComparer.OrdinalIgnoreCase);
            foreach ((string id, int value) in settings.MechanicPriorityIgnoreDistanceWithinById)
            {
                foreach (string expandedId in ExpandLegacyMechanicId(id))
                {
                    if (string.IsNullOrWhiteSpace(expandedId))
                        continue;

                    normalizedWithinById.TryAdd(expandedId, value);
                }
            }

            if (normalizedWithinById.Count > 0)
                settings.MechanicPriorityIgnoreDistanceWithinById = normalizedWithinById;
        }

        internal static IEnumerable<string> ExpandLegacyMechanicId(string? mechanicId)
        {
            if (string.IsNullOrWhiteSpace(mechanicId))
                yield break;

            if (mechanicId.Equals("altars", StringComparison.OrdinalIgnoreCase))
            {
                yield return MechanicIds.AltarsSearingExarch;
                yield return MechanicIds.AltarsEaterOfWorlds;
                yield break;
            }

            if (mechanicId.Equals("ultimatum", StringComparison.OrdinalIgnoreCase))
            {
                yield return MechanicIds.UltimatumInitialOverlay;
                yield return MechanicIds.UltimatumWindow;
                yield break;
            }

            if (mechanicId.Equals("sulphite-veins", StringComparison.OrdinalIgnoreCase))
            {
                yield return MechanicIds.DelveSulphiteVeins;
                yield break;
            }

            if (mechanicId.Equals("azurite-veins", StringComparison.OrdinalIgnoreCase))
            {
                yield return MechanicIds.DelveAzuriteVeins;
                yield break;
            }

            if (mechanicId.Equals("delve-spawners", StringComparison.OrdinalIgnoreCase))
            {
                yield return MechanicIds.DelveEncounterInitiators;
                yield break;
            }

            if (mechanicId.Equals("settlers-ore", StringComparison.OrdinalIgnoreCase))
            {
                yield return MechanicIds.SettlersCrimsonIron;
                yield return MechanicIds.SettlersCopper;
                yield return MechanicIds.SettlersPetrifiedWood;
                yield return MechanicIds.SettlersBismuth;
                yield return MechanicIds.SettlersHourglass;
                yield return MechanicIds.SettlersVerisium;
                yield break;
            }

            yield return mechanicId;
        }
    }
}