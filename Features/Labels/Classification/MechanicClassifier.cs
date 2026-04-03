#nullable enable

namespace ClickIt.Features.Labels.Classification
{
    internal readonly record struct MechanicClassifierDependencies(
        Func<Entity, string> GetWorldItemMetadataPath,
        Func<ClickSettings, Entity, GameController?, LabelOnGround, bool> ShouldAllowWorldItemByMetadata,
        Func<ClickSettings, string, LabelOnGround, bool> ShouldClickStrongbox,
        Func<bool, LabelOnGround, bool> ShouldClickEssence,
        Func<bool, bool, string, LabelOnGround, string?> GetRitualMechanicId,
        Func<GameController?, bool> ShouldAllowClosedDoorPastMechanic);

    internal static class MechanicClassifier
    {
        private const string StrongboxUniqueIdentifier = "special:strongbox-unique";
        private const string BlightCystPathMarker = "Chests/Blight";
        private const string BreachGraspingCoffersPathMarker = "Breach/BreachBoxChest";
        private const string SynthesisSynthesisedStashPathMarker = "SynthesisChests/SynthesisChest";

        private readonly record struct LeagueChestRule(
            string SpecificId,
            Func<string?, string?, bool> Matches);

        private enum LeagueChestRuleMatchState
        {
            None,
            Enabled,
            Disabled
        }

        private static readonly LeagueChestRule[] LeagueChestRules =
        [
            new(MechanicIds.MirageGoldenDjinnCache, static (name, _) => IsMirageGoldenDjinnCacheName(name)),
            new(MechanicIds.MirageSilverDjinnCache, static (name, _) => IsMirageSilverDjinnCacheName(name)),
            new(MechanicIds.MirageBronzeDjinnCache, static (name, _) => IsMirageBronzeDjinnCacheName(name)),
            new(MechanicIds.HeistSecureLocker, static (name, path) => IsHeistSecureLockerName(name) || IsHeistSecureLockerPath(path)),
            new(MechanicIds.BlightCyst, static (_, path) => IsBlightCystPath(path)),
            new(MechanicIds.BreachGraspingCoffers, static (_, path) => IsBreachGraspingCoffersPath(path)),
            new(MechanicIds.SynthesisSynthesisedStash, static (_, path) => IsSynthesisSynthesisedStashPath(path))
        ];

        internal static string? GetClickableMechanicId(
            LabelOnGround label,
            Entity item,
            ClickSettings settings,
            GameController? gameController,
            in MechanicClassifierDependencies dependencies)
        {
            EntityType type = item.Type;
            string path = type == EntityType.WorldItem
                ? dependencies.GetWorldItemMetadataPath(item)
                : (item.Path ?? string.Empty);

            string? mechanicId = ResolvePrimaryMechanicId(settings, path, label, gameController, dependencies);
            if (!string.IsNullOrWhiteSpace(mechanicId))
                return mechanicId;

            if (type == EntityType.WorldItem)
            {
                if (!dependencies.ShouldAllowWorldItemByMetadata(settings, item, gameController, label))
                    return null;

                if (ShouldClickWorldItemCore(settings.ClickItems, type, item))
                    return MechanicIds.Items;
            }

            return ResolveFallbackMechanicId(settings, type, path, label, item);
        }

        internal static string? GetAreaTransitionMechanicId(bool clickAreaTransitions, bool clickLabyrinthTrials, EntityType type, string path)
            => TransitionMechanicClassifier.GetAreaTransitionMechanicId(clickAreaTransitions, clickLabyrinthTrials, type, path);

        internal static bool ShouldClickWorldItemCore(bool clickItems, EntityType type, Entity item)
            => ShouldClickWorldItemCore(clickItems, type, item?.Path);

        internal static bool ShouldClickWorldItemCore(bool clickItems, EntityType type, string? itemPath)
        {
            if (!clickItems || type != EntityType.WorldItem)
                return false;

            return string.IsNullOrEmpty(itemPath)
                || !itemPath.Contains("strongbox", StringComparison.OrdinalIgnoreCase);
        }

        internal static string? GetChestMechanicIdFromConfiguredRules(
            bool clickBasicChests,
            bool clickLeagueChests,
            bool clickLeagueChestsOther,
            IReadOnlySet<string>? enabledSpecificLeagueChestIds,
            EntityType type,
            string? path,
            string renderName)
        {
            if (type != EntityType.Chest)
                return null;

            if (!string.IsNullOrEmpty(path) && path.Contains("strongbox", StringComparison.OrdinalIgnoreCase))
                return null;

            bool isBasic = IsBasicChestName(renderName);
            if (clickBasicChests && isBasic)
                return MechanicIds.BasicChests;

            if (!clickLeagueChests || isBasic)
                return null;

            LeagueChestRuleMatchState configuredLeagueChestMatchState = TryResolveConfiguredLeagueChestMechanicId(renderName, path, enabledSpecificLeagueChestIds);
            if (configuredLeagueChestMatchState == LeagueChestRuleMatchState.Enabled)
                return MechanicIds.LeagueChests;
            if (configuredLeagueChestMatchState == LeagueChestRuleMatchState.Disabled)
                return null;

            if (clickLeagueChestsOther)
                return MechanicIds.LeagueChests;

            return null;
        }

        internal static bool TryGetSettlersOreMechanicId(string? path, out string? mechanicId)
        {
            return MechanicRuleCatalog.TryResolveSettlersOreMechanicId(path, out mechanicId);
        }

        internal static bool IsHarvestPath(string path)
            => path.Contains("Harvest/Irrigator", StringComparison.OrdinalIgnoreCase)
               || path.Contains("Harvest/Extractor", StringComparison.OrdinalIgnoreCase);

        internal static bool IsSettlersOrePath(string path)
        {
            return MechanicRuleCatalog.IsSettlersOrePath(path);
        }

        internal static bool IsSettlersVerisiumPath(string path)
            => MechanicRuleCatalog.IsSettlersVerisiumPath(path);

        internal static bool ShouldClickAltar(bool highlightEater, bool highlightExarch, bool clickEater, bool clickExarch, string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (!(highlightEater || highlightExarch || clickEater || clickExarch))
                return false;

            return path.Contains(Constants.CleansingFireAltar, StringComparison.OrdinalIgnoreCase)
                || path.Contains(Constants.TangleAltar, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsBasicChestName(string? name)
        {
            name ??= string.Empty;
            return name.Equals("chest", StringComparison.OrdinalIgnoreCase)
                || name.Equals("tribal chest", StringComparison.OrdinalIgnoreCase)
                || name.Equals("golden chest", StringComparison.OrdinalIgnoreCase)
                || name.Equals("bone chest", StringComparison.OrdinalIgnoreCase)
                || name.Contains("cocoon", StringComparison.OrdinalIgnoreCase)
                || name.Equals("weapon rack", StringComparison.OrdinalIgnoreCase)
                || name.Equals("armour rack", StringComparison.OrdinalIgnoreCase)
                || name.Equals("trunk", StringComparison.OrdinalIgnoreCase)
                || name.Equals("sealed remains", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ResolvePrimaryMechanicId(
            ClickSettings settings,
            string path,
            LabelOnGround label,
            GameController? gameController,
            in MechanicClassifierDependencies dependencies)
        {
            string? special = GetSpecialPathMechanicId(settings, path, label, gameController, dependencies);
            if (!string.IsNullOrWhiteSpace(special))
                return special;

            string? altar = GetAltarMechanicId(settings, path);
            if (!string.IsNullOrWhiteSpace(altar))
                return altar;

            if (dependencies.ShouldClickEssence(settings.ClickEssences, label))
                return MechanicIds.Essences;

            return dependencies.GetRitualMechanicId(settings.ClickRitualInitiate, settings.ClickRitualCompleted, path, label);
        }

        private static string? ResolveFallbackMechanicId(ClickSettings settings, EntityType type, string path, LabelOnGround label, Entity item)
        {
            string? chest = GetChestMechanicId(
                settings.ClickBasicChests,
                settings.ClickLeagueChests,
                settings.ClickLeagueChestsOther,
                settings.EnabledLeagueChestSpecificIds,
                type,
                label);
            if (!string.IsNullOrWhiteSpace(chest))
                return chest;

            string? named = GetNamedInteractableMechanicId(settings.ClickDoors, settings.ClickLevers, item.RenderName, path);
            if (!string.IsNullOrWhiteSpace(named))
                return named;

            return GetAreaTransitionMechanicId(settings.ClickAreaTransitions, settings.ClickLabyrinthTrials, type, path);
        }

        private static string? GetChestMechanicId(
            bool clickBasicChests,
            bool clickLeagueChests,
            bool clickLeagueChestsOther,
            IReadOnlySet<string>? enabledSpecificLeagueChestIds,
            EntityType type,
            LabelOnGround label)
        {
            string? path = label.ItemOnGround?.Path;
            string renderName = label.ItemOnGround?.RenderName ?? string.Empty;
            return GetChestMechanicIdFromConfiguredRules(
                clickBasicChests,
                clickLeagueChests,
                clickLeagueChestsOther,
                enabledSpecificLeagueChestIds,
                type,
                path,
                renderName);
        }

        private static LeagueChestRuleMatchState TryResolveConfiguredLeagueChestMechanicId(string? renderName, string? path, IReadOnlySet<string>? enabledSpecificLeagueChestIds)
        {
            for (int i = 0; i < LeagueChestRules.Length; i++)
            {
                LeagueChestRule rule = LeagueChestRules[i];
                if (!rule.Matches(renderName, path))
                    continue;

                return IsLeagueChestSpecificRuleEnabled(enabledSpecificLeagueChestIds, rule.SpecificId)
                    ? LeagueChestRuleMatchState.Enabled
                    : LeagueChestRuleMatchState.Disabled;
            }

            return LeagueChestRuleMatchState.None;
        }

        private static bool IsLeagueChestSpecificRuleEnabled(IReadOnlySet<string>? enabledSpecificLeagueChestIds, string? specificId)
            => enabledSpecificLeagueChestIds != null
               && !string.IsNullOrWhiteSpace(specificId)
               && enabledSpecificLeagueChestIds.Contains(specificId);

        private static bool IsMirageGoldenDjinnCacheName(string? name)
            => IsDjinnCacheName(name, "golden");

        private static bool IsMirageSilverDjinnCacheName(string? name)
            => IsDjinnCacheName(name, "silver");

        private static bool IsMirageBronzeDjinnCacheName(string? name)
            => IsDjinnCacheName(name, "bronze");

        private static bool IsHeistSecureLockerName(string? name)
            => !string.IsNullOrWhiteSpace(name)
               && name.Contains("Secure Locker", StringComparison.OrdinalIgnoreCase);

        private static bool IsHeistSecureLockerPath(string? path)
            => !string.IsNullOrWhiteSpace(path)
               && path.Contains("/LeagueHeist/", StringComparison.OrdinalIgnoreCase);

        private static bool IsBreachGraspingCoffersPath(string? path)
            => !string.IsNullOrWhiteSpace(path)
               && path.Contains(BreachGraspingCoffersPathMarker, StringComparison.OrdinalIgnoreCase);

        private static bool IsBlightCystPath(string? path)
            => !string.IsNullOrWhiteSpace(path)
               && path.Contains(BlightCystPathMarker, StringComparison.OrdinalIgnoreCase);

        private static bool IsSynthesisSynthesisedStashPath(string? path)
            => !string.IsNullOrWhiteSpace(path)
               && path.Contains(SynthesisSynthesisedStashPathMarker, StringComparison.OrdinalIgnoreCase);

        private static bool IsDjinnCacheName(string? name, string tier)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(tier))
                return false;

            return name.Equals($"{tier} djinn's cache", StringComparison.OrdinalIgnoreCase)
                || name.Equals($"{tier} djinns cache", StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetNamedInteractableMechanicId(bool clickDoors, bool clickLevers, string? renderName, string? metadataPath)
        {
            string path = metadataPath?.Trim() ?? string.Empty;

            bool isDoor = path.Contains("MiscellaneousObjects/Lights", StringComparison.OrdinalIgnoreCase)
                || path.Contains("MiscellaneousObjects/Door", StringComparison.OrdinalIgnoreCase)
                || path.Contains("Heist/Objects/Level/Door_Basic", StringComparison.OrdinalIgnoreCase);
            bool isLever = path.Contains("Switch_Once", StringComparison.OrdinalIgnoreCase);

            if (clickDoors && isDoor)
                return MechanicIds.Doors;
            if (clickLevers && isLever)
                return MechanicIds.Levers;

            return null;
        }

        internal static bool IsSettlersPetrifiedWoodPath(string path)
            => MechanicRuleCatalog.IsSettlersPetrifiedWoodPath(path);

        private static string? GetSpecialPathMechanicId(
            ClickSettings settings,
            string path,
            LabelOnGround label,
            GameController? gameController,
            in MechanicClassifierDependencies dependencies)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (TryGetSettlersOreMechanicId(path, out string? settlersMechanicId) && !string.IsNullOrWhiteSpace(settlersMechanicId))
            {
                return SettlersMechanicPolicy.IsEnabled(settings, settlersMechanicId)
                    ? settlersMechanicId
                    : null;
            }

            return ResolveSpecialNonSettlersMechanic(settings, path, label, gameController, dependencies);
        }

        private static string? ResolveSpecialNonSettlersMechanic(
            ClickSettings settings,
            string path,
            LabelOnGround label,
            GameController? gameController,
            in MechanicClassifierDependencies dependencies)
            => InteractionMechanicRuleCatalog.TryResolve(settings, path, label, gameController, dependencies);

        private static string? GetAltarMechanicId(ClickSettings settings, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if ((settings.HighlightExarch || settings.ClickExarch)
                && path.Contains(Constants.CleansingFireAltar, StringComparison.OrdinalIgnoreCase))
            {
                return MechanicIds.AltarsSearingExarch;
            }

            if ((settings.HighlightEater || settings.ClickEater)
                && path.Contains(Constants.TangleAltar, StringComparison.OrdinalIgnoreCase))
            {
                return MechanicIds.AltarsEaterOfWorlds;
            }

            return null;
        }

    }
}