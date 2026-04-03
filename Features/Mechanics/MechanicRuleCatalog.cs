namespace ClickIt.Features.Mechanics
{
    internal static class MechanicRuleCatalog
    {
        private static readonly (string MechanicId, string Marker, bool ExcludeVerisiumBossTransition)[] SettlersOreRules =
        [
            (MechanicIds.SettlersCrimsonIron, MechanicIds.SettlersCrimsonIronMarker, false),
            (MechanicIds.SettlersCopper, MechanicIds.SettlersCopperMarker, false),
            (MechanicIds.SettlersPetrifiedWood, MechanicIds.SettlersPetrifiedWoodMarker, false),
            (MechanicIds.SettlersBismuth, MechanicIds.SettlersBismuthMarker, false),
            (MechanicIds.SettlersHourglass, MechanicIds.SettlersHourglassMarker, false),
            (MechanicIds.SettlersVerisium, MechanicIds.SettlersVerisiumMarker, true)
        ];

        internal static bool TryResolveSettlersOreMechanicId(string? path, out string? mechanicId)
        {
            mechanicId = null;
            if (string.IsNullOrWhiteSpace(path))
                return false;

            for (int i = 0; i < SettlersOreRules.Length; i++)
            {
                var rule = SettlersOreRules[i];
                if (!MatchesSettlersOrePathMarker(path, rule.Marker))
                    continue;

                if (rule.ExcludeVerisiumBossTransition
                    && path.Contains(MechanicIds.VerisiumBossSubAreaTransitionPathMarker, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                mechanicId = rule.MechanicId;
                return true;
            }

            return false;
        }

        internal static bool IsSettlersOrePath(string? path)
            => TryResolveSettlersOreMechanicId(path, out _);

        internal static bool IsSettlersPetrifiedWoodPath(string? path)
            => MatchesSettlersOrePathMarker(path, MechanicIds.SettlersPetrifiedWoodMarker);

        internal static bool IsSettlersVerisiumPath(string? path)
            => MatchesSettlersOrePathMarker(path, MechanicIds.SettlersVerisiumMarker)
               && (path?.Contains(MechanicIds.VerisiumBossSubAreaTransitionPathMarker, StringComparison.OrdinalIgnoreCase) != true);

        private static bool MatchesSettlersOrePathMarker(string? path, string fullMarker)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(fullMarker))
                return false;

            if (string.Equals(path, fullMarker, StringComparison.OrdinalIgnoreCase))
                return true;

            string markerWithSlash = fullMarker + "/";
            return path.StartsWith(markerWithSlash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
