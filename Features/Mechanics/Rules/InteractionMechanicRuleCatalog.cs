namespace ClickIt.Features.Mechanics.Rules
{
    internal readonly record struct InteractionRuleContext(
        ClickSettings Settings,
        string Path,
        LabelOnGround Label,
        GameController? GameController,
        MechanicClassifierDependencies Dependencies);

    internal static class InteractionMechanicRuleCatalog
    {
        private static readonly Func<InteractionRuleContext, string?>[] OrderedRules =
        [
            static ctx => ctx.Settings.ClickHarvest && MechanicClassifier.IsHarvestPath(ctx.Path)
                ? MechanicIds.Harvest : null,
            static ctx => ctx.Settings.ClickSulphite && ctx.Path.Contains("DelveMineral", StringComparison.OrdinalIgnoreCase)
                ? MechanicIds.DelveSulphiteVeins : null,
            static ctx => ctx.Settings.ClickStrongboxes
                && (ctx.Settings.StrongboxClickMetadata?.Count ?? 0) > 0
                && ctx.Dependencies.ShouldClickStrongbox(ctx.Settings, ctx.Path, ctx.Label)
                    ? MechanicIds.Strongboxes : null,
            static ctx => ctx.Settings.ClickSanctum && ctx.Path.Contains("Sanctum", StringComparison.OrdinalIgnoreCase)
                ? MechanicIds.Sanctum : null,
            static ctx => ctx.Settings.ClickBetrayal && ctx.Path.Contains("BetrayalMakeChoice", StringComparison.OrdinalIgnoreCase)
                ? MechanicIds.Betrayal : null,
            static ctx => ctx.Settings.ClickBlight && ctx.Path.Contains("BlightPump", StringComparison.OrdinalIgnoreCase)
                ? MechanicIds.Blight : null,
            static ctx => ctx.Settings.ClickAlvaTempleDoors
                && ctx.Path.Contains(Constants.ClosedDoorPast, StringComparison.OrdinalIgnoreCase)
                && ctx.Dependencies.ShouldAllowClosedDoorPastMechanic(ctx.GameController)
                    ? MechanicIds.AlvaTempleDoors : null,
            static ctx => ctx.Settings.ClickLegionPillars
                && ctx.Path.Contains(Constants.LegionInitiator, StringComparison.OrdinalIgnoreCase)
                    ? MechanicIds.LegionPillars : null,
            static ctx => ctx.Settings.ClickAzurite
                && ctx.Path.Contains("AzuriteEncounterController", StringComparison.OrdinalIgnoreCase)
                    ? MechanicIds.DelveAzuriteVeins : null,
            static ctx => ctx.Settings.ClickInitialUltimatum
                && Constants.IsUltimatumInteractablePath(ctx.Path)
                    ? MechanicIds.UltimatumInitialOverlay : null,
            static ctx => ctx.Settings.ClickDelveSpawners
                && ctx.Path.Contains("Delve/Objects/Encounter", StringComparison.OrdinalIgnoreCase)
                    ? MechanicIds.DelveEncounterInitiators : null,
            static ctx => ctx.Settings.ClickCrafting
                && ctx.Path.Contains("CraftingUnlocks", StringComparison.OrdinalIgnoreCase)
                    ? MechanicIds.CraftingRecipes : null,
            static ctx => ctx.Settings.ClickBreach
                && ctx.Path.Contains(Constants.Brequel, StringComparison.OrdinalIgnoreCase)
                    ? MechanicIds.BreachNodes : null,
        ];

        internal static string? TryResolve(
            ClickSettings settings,
            string path,
            LabelOnGround label,
            GameController? gameController,
            in MechanicClassifierDependencies dependencies)
        {
            InteractionRuleContext context = new(settings, path, label, gameController, dependencies);
            for (int i = 0; i < OrderedRules.Length; i++)
            {
                string? mechanicId = OrderedRules[i](context);
                if (!string.IsNullOrWhiteSpace(mechanicId))
                    return mechanicId;
            }

            return null;
        }
    }
}