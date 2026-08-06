
namespace ClickIt.Features.Labels.Classification
{
    internal static class MechanicClassifier
    {
        private const string BlightCystPathMarker = "Chests/Blight";
        private const string BreachGraspingCoffersPathMarker = "Breach/BreachBoxChest";
        private const string SynthesisSynthesisedStashPathMarker = "SynthesisChests/SynthesisChest";
        private const string AllflameCursedTreasurePathMarker = "LeagueDeepwater/CursedTreasure";
        private const string AllflameBrinerotPlunderPathMarker = "LeagueDeepwater/BrinerotStores";
        private const string AllflameCoralNestPathMarker = "LeagueDeepwater/GiantCoralChest";
        private const string HeistHazardsPathMarker = "Heist/Objects/Level/Hazards";

        private readonly record struct LeagueChestRule(
            string SpecificId,
            Func<string?, string?, bool> Matches);

        private static readonly LeagueChestRule[] LeagueChestRules =
        [
            new(MechanicIds.MirageGoldenDjinnCache, static (name, _) => IsMirageGoldenDjinnCacheName(name)),
            new(MechanicIds.MirageSilverDjinnCache, static (name, _) => IsMirageSilverDjinnCacheName(name)),
            new(MechanicIds.MirageBronzeDjinnCache, static (name, _) => IsMirageBronzeDjinnCacheName(name)),
            new(MechanicIds.HeistSecureRepository, static (name, _) => IsHeistSecureRepositoryName(name)),
            new(MechanicIds.HeistSecureLocker, static (name, _) => IsHeistSecureLockerName(name)),
            new(MechanicIds.HeistHazards, static (_, path) => IsHeistHazardsPath(path)),
            new(MechanicIds.BlightCyst, static (_, path) => IsBlightCystPath(path)),
            new(MechanicIds.BreachGraspingCoffers, static (_, path) => IsBreachGraspingCoffersPath(path)),
            new(MechanicIds.SynthesisSynthesisedStash, static (_, path) => IsSynthesisSynthesisedStashPath(path)),
            new(MechanicIds.AllflameCursedTreasure, static (_, path) => IsAllflameCursedTreasurePath(path)),
            new(MechanicIds.AllflameBrinerotPlunder, static (_, path) => IsAllflameBrinerotPlunderPath(path)),
            new(MechanicIds.AllflameCoralNest, static (_, path) => IsAllflameCoralNestPath(path))
        ];

        internal static string? GetClickableMechanicId(
            LabelOnGround label,
            Entity item,
            ClickSettings settings,
            GameController? gameController,
            IWorldItemMetadataPolicy worldItemMetadataPolicy,
            InventoryInteractionPolicy inventoryInteractionPolicy)
        {
            EntityType type = DynamicAccess.TryGetDynamicValue(item, DynamicAccessProfiles.Type, out object? rawType)
                && rawType is EntityType resolvedType
                ? resolvedType
                : default;
            string path = type == EntityType.WorldItem
                ? worldItemMetadataPolicy.GetWorldItemMetadataPath(item)
                : (DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string resolvedPath)
                    ? resolvedPath
                    : string.Empty);

            string? mechanicId = ResolvePrimaryMechanicId(settings, path, label, gameController, inventoryInteractionPolicy);
            if (!string.IsNullOrWhiteSpace(mechanicId))
                return mechanicId;

            if (type == EntityType.WorldItem)
            {
                if (!worldItemMetadataPolicy.ShouldAllowWorldItemByMetadata(
                        settings,
                        item,
                        gameController,
                        label,
                        inventoryInteractionPolicy.ShouldAllowWorldItemWhenInventoryFull))
                    return null;

                if (ShouldClickWorldItemCore(settings.ClickItems, type, item))
                    return MechanicIds.Items;
            }

            return ResolveFallbackMechanicId(settings, type, path, label);
        }

        internal static string? GetAreaTransitionMechanicId(bool clickAreaTransitions, bool clickLabyrinthTrials, EntityType type, string path)
        {
            bool isAreaTransition = type == EntityType.AreaTransition
                || path.Contains("AreaTransition", StringComparison.OrdinalIgnoreCase);
            if (!isAreaTransition)
                return null;

            if (IsLabyrinthTrialTransitionPath(path))
                return clickLabyrinthTrials ? MechanicIds.LabyrinthTrials : null;

            return clickAreaTransitions ? MechanicIds.AreaTransitions : null;
        }

        internal static bool IsLabyrinthTrialTransitionPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return path.Contains("LabyrinthTrial", StringComparison.OrdinalIgnoreCase)
                || path.Contains("Labyrinth/Trial", StringComparison.OrdinalIgnoreCase)
                || path.Contains("TrialPortal", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldClickWorldItemCore(bool clickItems, EntityType type, Entity item)
            => ShouldClickWorldItemCore(
                clickItems,
                type,
                DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string resolvedPath)
                    ? resolvedPath
                    : string.Empty);

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
            if (IsHeistHazardsPath(path))
            {
                if (!clickLeagueChests)
                    return null;

                return IsLeagueChestSpecificRuleEnabled(enabledSpecificLeagueChestIds, MechanicIds.HeistHazards)
                    ? MechanicIds.HeistHazards
                    : null;
            }

            if (type != EntityType.Chest)
                return null;

            if (!string.IsNullOrEmpty(path) && path.Contains("strongbox", StringComparison.OrdinalIgnoreCase))
                return null;

            bool isBasic = IsBasicChestName(renderName);
            if (clickBasicChests && isBasic)
                return MechanicIds.BasicChests;

            if (!clickLeagueChests || isBasic)
                return null;

            if (TryResolveConfiguredLeagueChestMechanicId(renderName, path, enabledSpecificLeagueChestIds, out string? configuredMechanicId))
                return configuredMechanicId;

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
            InventoryInteractionPolicy inventoryInteractionPolicy)
        {
            string? special = GetSpecialPathMechanicId(settings, path, label, gameController, inventoryInteractionPolicy);
            if (!string.IsNullOrWhiteSpace(special))
                return special;

            string? altar = GetAltarMechanicId(settings, path);
            if (!string.IsNullOrWhiteSpace(altar))
                return altar;

            if (ShouldClickEssence(settings.ClickEssences, label))
                return MechanicIds.Essences;

            return GetRitualMechanicId(settings.ClickRitualInitiate, settings.ClickRitualCompleted, path, label);
        }

        private static string? ResolveFallbackMechanicId(ClickSettings settings, EntityType type, string path, LabelOnGround label)
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

            LabelItemMetadata metadata = ResolveLabelItemMetadata(label);
            string? named = GetNamedInteractableMechanicId(
                settings.ClickDoors,
                settings.ClickHeistDoors,
                settings.ClickLevers,
                metadata.RenderName,
                path);
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
            LabelItemMetadata metadata = ResolveLabelItemMetadata(label);
            return GetChestMechanicIdFromConfiguredRules(
                clickBasicChests,
                clickLeagueChests,
                clickLeagueChestsOther,
                enabledSpecificLeagueChestIds,
                type,
                metadata.Path,
                metadata.RenderName);
        }

        private readonly record struct LabelItemMetadata(string Path, string RenderName);

        private static LabelItemMetadata ResolveLabelItemMetadata(LabelOnGround? label)
        {
            object? rawItem = DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? itemValue)
                ? itemValue
                : null;

            string path = DynamicAccess.TryReadString(rawItem, DynamicAccessProfiles.Path, out string resolvedPath)
                ? resolvedPath
                : string.Empty;
            string renderName = DynamicAccess.TryReadString(rawItem, DynamicAccessProfiles.RenderName, out string resolvedRenderName)
                ? resolvedRenderName
                : string.Empty;
            return new LabelItemMetadata(path, renderName);
        }

        private static bool TryResolveConfiguredLeagueChestMechanicId(
            string? renderName,
            string? path,
            IReadOnlySet<string>? enabledSpecificLeagueChestIds,
            out string? mechanicId)
        {
            if (TryResolveHeistSecureChestMechanicId(renderName, path, enabledSpecificLeagueChestIds, out mechanicId))
                return true;

            for (int i = 0; i < LeagueChestRules.Length; i++)
            {
                LeagueChestRule rule = LeagueChestRules[i];
                if (!rule.Matches(renderName, path))
                    continue;

                mechanicId = IsLeagueChestSpecificRuleEnabled(enabledSpecificLeagueChestIds, rule.SpecificId)
                    ? rule.SpecificId
                    : null;
                return true;
            }

            return false;
        }

        private static bool TryResolveHeistSecureChestMechanicId(
            string? renderName,
            string? path,
            IReadOnlySet<string>? enabledSpecificLeagueChestIds,
            out string? mechanicId)
        {
            mechanicId = null;
            bool isLockerByName = IsHeistSecureLockerName(renderName);
            bool isRepositoryByName = IsHeistSecureRepositoryName(renderName);
            bool isHeistSecureContainerPath = IsHeistSecureContainerPath(path);
            if (!isLockerByName && !isRepositoryByName && !isHeistSecureContainerPath)
                return false;

            bool lockerEnabled = IsLeagueChestSpecificRuleEnabled(enabledSpecificLeagueChestIds, MechanicIds.HeistSecureLocker);
            bool repositoryEnabled = IsLeagueChestSpecificRuleEnabled(enabledSpecificLeagueChestIds, MechanicIds.HeistSecureRepository);

            if (isRepositoryByName)
            {
                mechanicId = repositoryEnabled ? MechanicIds.HeistSecureRepository : null;
                return true;
            }

            if (isLockerByName)
            {
                mechanicId = lockerEnabled ? MechanicIds.HeistSecureLocker : null;
                return true;
            }

            // Path-only heist container remains grouped because locker/repository cannot be inferred safely.
            mechanicId = (lockerEnabled || repositoryEnabled)
                ? MechanicIds.LeagueChests
                : null;
            return true;
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

        private static bool IsHeistSecureRepositoryName(string? name)
            => !string.IsNullOrWhiteSpace(name)
            && name.Contains("Secure Repository", StringComparison.OrdinalIgnoreCase);

        private static bool IsHeistSecureContainerPath(string? path)
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

        private static bool IsAllflameCursedTreasurePath(string? path)
            => !string.IsNullOrWhiteSpace(path)
               && path.Contains(AllflameCursedTreasurePathMarker, StringComparison.OrdinalIgnoreCase);

        private static bool IsAllflameBrinerotPlunderPath(string? path)
            => !string.IsNullOrWhiteSpace(path)
               && path.Contains(AllflameBrinerotPlunderPathMarker, StringComparison.OrdinalIgnoreCase);

        private static bool IsAllflameCoralNestPath(string? path)
            => !string.IsNullOrWhiteSpace(path)
               && path.Contains(AllflameCoralNestPathMarker, StringComparison.OrdinalIgnoreCase);

        private static bool IsHeistHazardsPath(string? path)
            => !string.IsNullOrWhiteSpace(path)
               && path.Contains(HeistHazardsPathMarker, StringComparison.OrdinalIgnoreCase);

        private static bool IsDjinnCacheName(string? name, string tier)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(tier))
                return false;

            return name.Equals($"{tier} djinn's cache", StringComparison.OrdinalIgnoreCase)
                || name.Equals($"{tier} djinns cache", StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetNamedInteractableMechanicId(
            bool clickDoors,
            bool clickHeistDoors,
            bool clickLevers,
            string? renderName,
            string? metadataPath)
        {
            _ = renderName;
            string path = metadataPath?.Trim() ?? string.Empty;
            bool isHeistDoor = IsHeistDoorPath(path);

            bool isDoor = path.Contains("MiscellaneousObjects/Lights", StringComparison.OrdinalIgnoreCase)
                || path.Contains("MiscellaneousObjects/Door", StringComparison.OrdinalIgnoreCase)
                || path.Contains("Heist/Objects/Level/Door_Basic", StringComparison.OrdinalIgnoreCase);
            bool isLever = path.Contains("Switch_Once", StringComparison.OrdinalIgnoreCase);

            if (clickHeistDoors && isHeistDoor)
                return MechanicIds.HeistDoors;
            if (clickDoors && isDoor)
                return MechanicIds.Doors;
            if (clickLevers && isLever)
                return MechanicIds.Levers;

            return null;
        }

        private static bool IsHeistDoorPath(string? path)
            => !string.IsNullOrWhiteSpace(path)
               && path.Contains("Heist/Objects/Level/Door", StringComparison.OrdinalIgnoreCase)
               && !path.Contains("Heist/Objects/Level/Door_Basic", StringComparison.OrdinalIgnoreCase);

        internal static bool IsSettlersPetrifiedWoodPath(string path)
            => MechanicRuleCatalog.IsSettlersPetrifiedWoodPath(path);

        private static string? GetSpecialPathMechanicId(
            ClickSettings settings,
            string path,
            LabelOnGround label,
            GameController? gameController,
            InventoryInteractionPolicy inventoryInteractionPolicy)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (TryGetSettlersOreMechanicId(path, out string? settlersMechanicId) && !string.IsNullOrWhiteSpace(settlersMechanicId))
                return SettlersMechanicPolicy.IsEnabled(SettlersClickFlags.From(settings), settlersMechanicId)
        ? settlersMechanicId
        : null;


            return ResolveSpecialNonSettlersMechanic(settings, path, label, gameController, inventoryInteractionPolicy);
        }

        private static string? ResolveSpecialNonSettlersMechanic(
            ClickSettings settings,
            string path,
            LabelOnGround label,
            GameController? gameController,
            InventoryInteractionPolicy inventoryInteractionPolicy)
            => InteractionMechanicRuleCatalog.TryResolve(settings, path, label, gameController, inventoryInteractionPolicy);

        private static string? GetAltarMechanicId(ClickSettings settings, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if ((settings.HighlightExarch || settings.ClickExarch)
                && path.Contains(Constants.CleansingFireAltar, StringComparison.OrdinalIgnoreCase))
                return MechanicIds.AltarsSearingExarch;


            if ((settings.HighlightEater || settings.ClickEater)
                && path.Contains(Constants.TangleAltar, StringComparison.OrdinalIgnoreCase))
                return MechanicIds.AltarsEaterOfWorlds;


            return null;
        }

        internal static bool ShouldClickEssence(bool clickEssences, LabelOnGround label)
        {
            if (!clickEssences)
                return false;

            return LabelContainsText(label, "The monster is imprisoned by powerful Essences.");
        }

        internal static string? GetRitualMechanicId(bool clickRitualInitiate, bool clickRitualCompleted, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path) || !path.Contains("Leagues/Ritual", StringComparison.OrdinalIgnoreCase))
                return null;

            bool hasFavoursText = LabelContainsText(label, "Interact to view Favours");
            if (clickRitualInitiate && !hasFavoursText)
                return MechanicIds.RitualInitiate;
            if (clickRitualCompleted && hasFavoursText)
                return MechanicIds.RitualCompleted;

            return null;
        }

        internal static bool ShouldClickStrongbox(ClickSettings settings, string path, LabelOnGround label)
        {
            if (string.IsNullOrEmpty(path) || !TryGetLabelItem(label, out object? item) || item == null)
                return false;

            if (!TryGetChestLocked(item, out bool isLocked) || isLocked)
                return false;

            IReadOnlyList<string> clickMetadata = settings.StrongboxClickMetadata ?? [];
            IReadOnlyList<string> dontClickMetadata = settings.StrongboxDontClickMetadata ?? [];
            if (clickMetadata.Count == 0)
                return false;

            if (IsUniqueStrongbox(item))
            {
                if (ContainsStrongboxUniqueIdentifier(dontClickMetadata))
                    return false;

                return ContainsStrongboxUniqueIdentifier(clickMetadata);
            }

            string renderName = DynamicAccess.TryReadString(item, DynamicAccessProfiles.RenderName, out string resolvedRenderName)
                ? resolvedRenderName
                : string.Empty;
            bool dontClickMatch = MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(path, renderName, dontClickMetadata);
            if (dontClickMatch)
                return false;

            return MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(path, renderName, clickMetadata);
        }

        private static bool LabelContainsText(LabelOnGround? label, string text)
        {
            return TryGetLabelAdapter(label, out IElementAdapter? adapter)
                && LabelElementSearch.GetElementByStringCore(adapter, text) != null;
        }

        private static bool TryGetLabelAdapter(LabelOnGround? label, out IElementAdapter? adapter)
        {
            adapter = null;
            if (!DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabel)
                || rawLabel == null)
                return false;

            adapter = rawLabel switch
            {
                IElementAdapter existingAdapter => existingAdapter,
                Element element => new ElementAdapter(element),
                _ => null,
            };

            return adapter != null;
        }

        private static bool TryGetLabelItem(LabelOnGround? label, out object? item)
        {
            return DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out item)
                && item != null;
        }

        private static bool TryGetChestLocked(object item, out bool isLocked)
        {
            isLocked = false;
            if (!DynamicAccess.TryGetComponent<Chest>(item, out object? rawChest)
                || rawChest == null)
                return false;

            return DynamicAccess.TryReadBool(rawChest, DynamicAccessProfiles.IsLocked, out isLocked);
        }

        private static bool ContainsStrongboxUniqueIdentifier(IReadOnlyList<string> metadataIdentifiers)
        {
            if (metadataIdentifiers == null || metadataIdentifiers.Count == 0)
                return false;

            for (int i = 0; i < metadataIdentifiers.Count; i++)
                if (string.Equals(metadataIdentifiers[i], "special:strongbox-unique", StringComparison.OrdinalIgnoreCase))
                    return true;


            return false;
        }

        private static bool IsUniqueStrongbox(object item)
        {
            if (!DynamicAccess.TryGetDynamicValue(item, DynamicAccessProfiles.Rarity, out object? rawRarity)
                || rawRarity == null)
                return false;

            return rawRarity switch
            {
                MonsterRarity rarity => rarity == MonsterRarity.Unique,
                int rarityValue => rarityValue == (int)MonsterRarity.Unique,
                _ => false,
            };
        }
    }

    internal static class MetadataIdentifierRuleSet
    {
        internal static bool ContainsAnyMetadataIdentifier(string metadataPath, string itemName, IReadOnlyList<string> identifiers)
            => ContainsAnyMetadataIdentifier(metadataPath, itemName, item: null, labelText: string.Empty, identifiers);

        internal static bool ContainsAnyMetadataIdentifier(string metadataPath, string itemName, Entity? item, string labelText, IReadOnlyList<string> identifiers)
        {
            if (identifiers == null || identifiers.Count == 0)
                return false;

            metadataPath ??= string.Empty;
            itemName ??= string.Empty;
            labelText ??= string.Empty;

            for (int i = 0; i < identifiers.Count; i++)
            {
                string identifier = identifiers[i] ?? string.Empty;
                if (identifier.Length == 0)
                    continue;

                if (TryGetSpecialRule(identifier, out string specialRule))
                {
                    if (MatchesSpecialRule(specialRule, metadataPath, itemName, item, labelText))
                        return true;
                    continue;
                }

                if (MetadataIdentifierMatcher.ContainsSingle(metadataPath, itemName, identifier))
                    return true;
            }

            return false;
        }

        private static bool TryGetSpecialRule(string identifier, out string specialRule)
        {
            specialRule = string.Empty;
            if (!identifier.StartsWith("special:", StringComparison.OrdinalIgnoreCase))
                return false;

            specialRule = identifier["special:".Length..].Trim();
            return specialRule.Length > 0;
        }

        private static bool MatchesSpecialRule(string specialRule, string metadataPath, string itemName, Entity? item, string labelText)
        {
            if (specialRule.Equals("unique-items", StringComparison.OrdinalIgnoreCase))
                return item != null && IsUniqueItem(item);
            if (specialRule.Equals("heist-quest-contract", StringComparison.OrdinalIgnoreCase))
                return IsHeistQuestContract(itemName);
            if (specialRule.Equals("heist-non-quest-contract", StringComparison.OrdinalIgnoreCase))
                return IsHeistNonQuestContract(itemName);
            if (specialRule.Equals("inscribed-ultimatum", StringComparison.OrdinalIgnoreCase))
                return (item != null && IsInscribedUltimatum(item)) || metadataPath.Contains("ItemisedTrial", StringComparison.OrdinalIgnoreCase);
            if (specialRule.Equals("jewels-regular", StringComparison.OrdinalIgnoreCase))
                return IsRegularJewelsMetadataPath(metadataPath);
            if (specialRule.Equals("mysterious-wombgift-label", StringComparison.OrdinalIgnoreCase))
                return string.Equals(labelText.Trim(), "Mysterious Wombgift", StringComparison.OrdinalIgnoreCase);

            return false;
        }

        private static bool IsRegularJewelsMetadataPath(string metadataPath)
        {
            return metadataPath.Contains("Items/Jewels/", StringComparison.OrdinalIgnoreCase)
                && !metadataPath.Contains("Items/Jewels/JewelAbyss", StringComparison.OrdinalIgnoreCase)
                && !metadataPath.Contains("Items/Jewels/JewelPassiveTreeExpansion", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUniqueItem(Entity item)
        {
            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                Mods? mods = itemEntity?.GetComponent<Mods>();
                return mods?.ItemRarity == ItemRarity.Unique
                    && !(itemEntity?.Path?.StartsWith("Metadata/Items/Metamorphosis/", StringComparison.OrdinalIgnoreCase) ?? false);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsHeistQuestContract(string itemName)
            => !string.IsNullOrWhiteSpace(itemName) && Constants.HeistQuestContractNames.Contains(itemName);

        private static bool IsHeistNonQuestContract(string itemName)
            => !string.IsNullOrWhiteSpace(itemName)
               && itemName.StartsWith("Contract:", StringComparison.OrdinalIgnoreCase)
               && !Constants.HeistQuestContractNames.Contains(itemName);

        private static bool IsInscribedUltimatum(Entity item)
        {
            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                return itemEntity?.Path?.Contains("ItemisedTrial", StringComparison.OrdinalIgnoreCase) == true;
            }
            catch
            {
                return false;
            }
        }
    }

    internal interface IWorldItemMetadataPolicy
    {
        string GetWorldItemMetadataPath(Entity item);
        string GetWorldItemBaseName(Entity item);
        bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController, LabelOnGround? label, Func<Entity, GameController?, bool> shouldAllowWhenInventoryFull);
    }

    internal sealed class WorldItemMetadataPolicy : IWorldItemMetadataPolicy
    {
        public string GetWorldItemMetadataPath(Entity item)
        {
            try
            {
                string resolvedMetadata = EntityHelpers.ResolveWorldItemMetadataPath(item);
                if (TryGetWorldItemComponentMetadata(item, out string componentMetadata))
                    return SelectBestWorldItemMetadataPath(resolvedMetadata, componentMetadata);

                return resolvedMetadata;
            }
            catch
            {
                return string.Empty;
            }
        }

        public string GetWorldItemBaseName(Entity item)
        {
            return ResolveWorldItemBaseName(item);
        }

        public bool ShouldAllowWorldItemByMetadata(ClickSettings settings, Entity item, GameController? gameController, LabelOnGround? label, Func<Entity, GameController?, bool> shouldAllowWhenInventoryFull)
        {
            string metadata = GetWorldItemMetadataPath(item);
            string itemName = ResolveWorldItemBaseName(item);
            string labelText = GetWorldItemLabelText(label);

            IReadOnlyList<string> whitelist = settings.ItemTypeWhitelistMetadata ?? [];
            IReadOnlyList<string> blacklist = settings.ItemTypeBlacklistMetadata ?? [];

            bool whitelistPass = whitelist.Count == 0 || MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(metadata, itemName, item, labelText, whitelist);
            if (!whitelistPass)
                return false;

            bool blacklistMatch = blacklist.Count > 0 && MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(metadata, itemName, item, labelText, blacklist);
            if (blacklistMatch)
                return false;

            return shouldAllowWhenInventoryFull(item, gameController);
        }

        internal static string SelectBestWorldItemMetadataPath(string resolvedMetadata, string componentMetadata)
        {
            if (string.IsNullOrWhiteSpace(componentMetadata))
                return resolvedMetadata ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resolvedMetadata))
                return componentMetadata;

            if (resolvedMetadata.Contains("Metadata/MiscellaneousObjects/", StringComparison.OrdinalIgnoreCase))
                return componentMetadata;

            return resolvedMetadata;
        }

        private static bool TryGetWorldItemComponentMetadata(Entity? item, out string metadata)
        {
            metadata = string.Empty;
            if (item == null)
                return false;

            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                string candidate = itemEntity?.Metadata ?? string.Empty;
                if (string.IsNullOrWhiteSpace(candidate))
                    return false;

                metadata = candidate;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveWorldItemBaseName(Entity item)
        {
            try
            {
                WorldItem? worldItemComp = item.GetComponent<WorldItem>();
                Entity? itemEntity = worldItemComp?.ItemEntity;
                return itemEntity?.GetComponent<Base>()?.Name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetWorldItemLabelText(LabelOnGround? label)
        {
            try
            {
                return label?.Label?.GetText(512) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

}