using ClickIt.Definitions;
using ClickIt.Services.Label.Classification;
using ExileCore.PoEMemory.Elements;

namespace ClickIt.Services.Mechanics.Rules
{
    internal readonly record struct InteractionRuleContext(
        ClickSettings Settings,
        string Path,
        LabelOnGround Label,
        ExileCore.GameController? GameController,
        MechanicClassifierDependencies Dependencies);

    internal static class InteractionMechanicRuleCatalog
    {
        private interface IInteractionRule
        {
            string? TryResolve(in InteractionRuleContext context);
        }

        private static readonly IInteractionRule[] OrderedInteractionRules =
        [
            new HarvestInteractionRule(),
            new DelveSulphiteInteractionRule(),
            new StrongboxInteractionRule(),
            new SanctumInteractionRule(),
            new BetrayalInteractionRule(),
            new BlightInteractionRule(),
            new AlvaTempleDoorInteractionRule(),
            new LegionPillarInteractionRule(),
            new DelveAzuriteInteractionRule(),
            new UltimatumInitialOverlayInteractionRule(),
            new DelveEncounterInitiatorInteractionRule(),
            new CraftingRecipeInteractionRule(),
            new BreachInteractionRule()
        ];

        internal static string? TryResolve(
            ClickSettings settings,
            string path,
            LabelOnGround label,
            ExileCore.GameController? gameController,
            in MechanicClassifierDependencies dependencies)
        {
            InteractionRuleContext context = new(settings, path, label, gameController, dependencies);
            for (int i = 0; i < OrderedInteractionRules.Length; i++)
            {
                string? mechanicId = OrderedInteractionRules[i].TryResolve(context);
                if (!string.IsNullOrWhiteSpace(mechanicId))
                    return mechanicId;
            }

            return null;
        }

        private static bool IsUltimatumPath(string path)
            => Constants.IsUltimatumInteractablePath(path);

        private sealed class HarvestInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => context.Settings.NearestHarvest && MechanicClassifier.IsHarvestPath(context.Path)
                    ? MechanicIds.Harvest
                    : null;
        }

        private sealed class DelveSulphiteInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => context.Settings.ClickSulphite && context.Path.Contains("DelveMineral", StringComparison.OrdinalIgnoreCase)
                    ? MechanicIds.DelveSulphiteVeins
                    : null;
        }

        private sealed class StrongboxInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => (context.Settings.StrongboxClickMetadata?.Count ?? 0) > 0
                    && context.Dependencies.ShouldClickStrongbox(context.Settings, context.Path, context.Label)
                        ? MechanicIds.Strongboxes
                        : null;
        }

        private sealed class SanctumInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => context.Settings.ClickSanctum && context.Path.Contains("Sanctum", StringComparison.OrdinalIgnoreCase)
                    ? MechanicIds.Sanctum
                    : null;
        }

        private sealed class BetrayalInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => context.Settings.ClickBetrayal && context.Path.Contains("BetrayalMakeChoice", StringComparison.OrdinalIgnoreCase)
                    ? MechanicIds.Betrayal
                    : null;
        }

        private sealed class BlightInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => context.Settings.ClickBlight && context.Path.Contains("BlightPump", StringComparison.OrdinalIgnoreCase)
                    ? MechanicIds.Blight
                    : null;
        }

        private sealed class AlvaTempleDoorInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => context.Settings.ClickAlvaTempleDoors
                    && context.Path.Contains(Constants.ClosedDoorPast, StringComparison.OrdinalIgnoreCase)
                    && context.Dependencies.ShouldAllowClosedDoorPastMechanic(context.GameController)
                        ? MechanicIds.AlvaTempleDoors
                        : null;
        }

        private sealed class LegionPillarInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => context.Settings.ClickLegionPillars
                    && context.Path.Contains(Constants.LegionInitiator, StringComparison.OrdinalIgnoreCase)
                        ? MechanicIds.LegionPillars
                        : null;
        }

        private sealed class DelveAzuriteInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => context.Settings.ClickAzurite
                    && context.Path.Contains("AzuriteEncounterController", StringComparison.OrdinalIgnoreCase)
                        ? MechanicIds.DelveAzuriteVeins
                        : null;
        }

        private sealed class UltimatumInitialOverlayInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => context.Settings.ClickInitialUltimatum && IsUltimatumPath(context.Path)
                    ? MechanicIds.UltimatumInitialOverlay
                    : null;
        }

        private sealed class DelveEncounterInitiatorInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => context.Settings.ClickDelveSpawners
                    && context.Path.Contains("Delve/Objects/Encounter", StringComparison.OrdinalIgnoreCase)
                        ? MechanicIds.DelveEncounterInitiators
                        : null;
        }

        private sealed class CraftingRecipeInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => context.Settings.ClickCrafting
                    && context.Path.Contains("CraftingUnlocks", StringComparison.OrdinalIgnoreCase)
                        ? MechanicIds.CraftingRecipes
                        : null;
        }

        private sealed class BreachInteractionRule : IInteractionRule
        {
            public string? TryResolve(in InteractionRuleContext context)
                => context.Settings.ClickBreach
                    && context.Path.Contains(Constants.Brequel, StringComparison.OrdinalIgnoreCase)
                        ? MechanicIds.BreachNodes
                        : null;
        }
    }
}