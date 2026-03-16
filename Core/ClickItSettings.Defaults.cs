using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using Newtonsoft.Json;
using System.Numerics;
using ClickIt.Definitions;

namespace ClickIt
{
    public partial class ClickItSettings : ISettings
    {
        private const int LazyModeNearbyMonsterCountMin = 0;
        private const int LazyModeNearbyMonsterCountMax = 200;
        private const int LazyModeNearbyMonsterDistanceMin = 1;
        private const int LazyModeNearbyMonsterDistanceMax = 300;

        private void EnsureLazyModeNearbyMonsterFiltersInitialized()
        {
            LazyModeNormalMonsterBlockCount = SanitizeLazyModeNearbyMonsterCount(LazyModeNormalMonsterBlockCount);
            LazyModeNormalMonsterBlockDistance = SanitizeLazyModeNearbyMonsterDistance(LazyModeNormalMonsterBlockDistance);

            LazyModeMagicMonsterBlockCount = SanitizeLazyModeNearbyMonsterCount(LazyModeMagicMonsterBlockCount);
            LazyModeMagicMonsterBlockDistance = SanitizeLazyModeNearbyMonsterDistance(LazyModeMagicMonsterBlockDistance);

            LazyModeRareMonsterBlockCount = SanitizeLazyModeNearbyMonsterCount(LazyModeRareMonsterBlockCount);
            LazyModeRareMonsterBlockDistance = SanitizeLazyModeNearbyMonsterDistance(LazyModeRareMonsterBlockDistance);

            LazyModeUniqueMonsterBlockCount = SanitizeLazyModeNearbyMonsterCount(LazyModeUniqueMonsterBlockCount);
            LazyModeUniqueMonsterBlockDistance = SanitizeLazyModeNearbyMonsterDistance(LazyModeUniqueMonsterBlockDistance);
        }

        private static int SanitizeLazyModeNearbyMonsterCount(int value)
            => Math.Clamp(value, LazyModeNearbyMonsterCountMin, LazyModeNearbyMonsterCountMax);

        private static int SanitizeLazyModeNearbyMonsterDistance(int value)
            => Math.Clamp(value, LazyModeNearbyMonsterDistanceMin, LazyModeNearbyMonsterDistanceMax);

        private void EnsureItemTypeFiltersInitialized()
        {
            ItemTypeWhitelistIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ItemTypeBlacklistIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ItemTypeWhitelistSubtypeIds ??= new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            ItemTypeBlacklistSubtypeIds ??= new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            if (ItemTypeWhitelistIds.Count == 0 && ItemTypeBlacklistIds.Count == 0)
            {
                ItemTypeWhitelistIds = new HashSet<string>(ItemCategoryCatalog.DefaultWhitelistIds, StringComparer.OrdinalIgnoreCase);
                ItemTypeBlacklistIds = new HashSet<string>(ItemCategoryCatalog.DefaultBlacklistIds, StringComparer.OrdinalIgnoreCase);
                ItemTypeBlacklistSubtypeIds["jewels"] = new HashSet<string>(new[] { "regular-jewels", "abyss-jewels" }, StringComparer.OrdinalIgnoreCase);
                return;
            }

            ItemTypeWhitelistIds.RemoveWhere(x => !ItemCategoryCatalog.AllIds.Contains(x));
            ItemTypeBlacklistIds.RemoveWhere(x => !ItemCategoryCatalog.AllIds.Contains(x));

            foreach (string id in ItemTypeWhitelistIds.ToArray())
            {
                ItemTypeBlacklistIds.Remove(id);
            }

            SanitizeSubtypeDictionary(ItemTypeWhitelistSubtypeIds, ItemTypeWhitelistIds);
            SanitizeSubtypeDictionary(ItemTypeBlacklistSubtypeIds, ItemTypeBlacklistIds);
        }

        private static void SanitizeSubtypeDictionary(Dictionary<string, HashSet<string>> subtypeSelections, HashSet<string> parentCategoryIds)
        {
            string[] invalidParentIds = subtypeSelections.Keys
                .Where(id => !parentCategoryIds.Contains(id) || !ItemSubtypeCatalog.ContainsKey(id))
                .ToArray();

            foreach (string invalidParentId in invalidParentIds)
            {
                subtypeSelections.Remove(invalidParentId);
            }

            foreach ((string parentId, HashSet<string> selectedSubtypes) in subtypeSelections.ToArray())
            {
                if (!ItemSubtypeCatalog.TryGetValue(parentId, out ItemSubtypeDefinition[]? subtypeDefinitions))
                {
                    subtypeSelections.Remove(parentId);
                    continue;
                }

                HashSet<string> validSubtypeIds = new HashSet<string>(subtypeDefinitions.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
                selectedSubtypes.RemoveWhere(id => !validSubtypeIds.Contains(id));
            }
        }

        private static HashSet<string> BuildDefaultCorruptEssenceNames()
        {
            return new HashSet<string>(
                EssenceAllTableNames.Where(name => EssenceMedsSuffixes.Any(meds => name.EndsWith($"of {meds}", StringComparison.OrdinalIgnoreCase))),
                StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<string> BuildDefaultDontCorruptEssenceNames()
        {
            HashSet<string> defaults = new HashSet<string>(EssenceAllTableNames, StringComparer.OrdinalIgnoreCase);
            defaults.RemoveWhere(name => EssenceMedsSuffixes.Any(meds => name.EndsWith($"of {meds}", StringComparison.OrdinalIgnoreCase)));
            return defaults;
        }

        private static HashSet<string> BuildDefaultClickStrongboxIds()
        {
            return new HashSet<string>(StrongboxDefaultClickIds, StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<string> BuildDefaultDontClickStrongboxIds()
        {
            HashSet<string> defaults = new HashSet<string>(StrongboxTableEntries.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
            defaults.ExceptWith(StrongboxDefaultClickIds);
            return defaults;
        }

        private void EnsureEssenceCorruptionFiltersInitialized()
        {
            EssenceCorruptNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            EssenceDontCorruptNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (EssenceCorruptNames.Count == 0 && EssenceDontCorruptNames.Count == 0)
            {
                EssenceCorruptNames = BuildDefaultCorruptEssenceNames();
                EssenceDontCorruptNames = BuildDefaultDontCorruptEssenceNames();
                return;
            }

            HashSet<string> allowed = new HashSet<string>(EssenceAllTableNames, StringComparer.OrdinalIgnoreCase);

            EssenceCorruptNames.RemoveWhere(x => !allowed.Contains(x));
            EssenceDontCorruptNames.RemoveWhere(x => !allowed.Contains(x));

            foreach (string name in EssenceCorruptNames.ToArray())
            {
                EssenceDontCorruptNames.Remove(name);
            }

            foreach (string essenceName in EssenceAllTableNames)
            {
                if (!EssenceCorruptNames.Contains(essenceName) && !EssenceDontCorruptNames.Contains(essenceName))
                {
                    EssenceDontCorruptNames.Add(essenceName);
                }
            }
        }

        private void EnsureMechanicPrioritiesInitialized()
        {
            MechanicPriorityOrder ??= new List<string>();
            MechanicPriorityIgnoreDistanceIds ??= new HashSet<string>(PriorityComparer);
            MechanicPriorityIgnoreDistanceWithinById ??= new Dictionary<string, int>(PriorityComparer);

            NormalizeLegacyMechanicPriorityFields();

            HashSet<string> valid = new(MechanicPriorityIds, PriorityComparer);

            bool applyDefaultIgnoreDistance = MechanicPriorityIgnoreDistanceIds.Count == 0;
            MechanicPriorityOrder = BuildSanitizedMechanicPriorityOrder(valid);
            SanitizeMechanicIgnoreDistance(valid, applyDefaultIgnoreDistance);
            SanitizeMechanicIgnoreDistanceWithin(valid);
        }

        private void NormalizeLegacyMechanicPriorityFields()
        {
            HashSet<string> normalizedIgnoreDistance = new(PriorityComparer);
            foreach (string id in MechanicPriorityIgnoreDistanceIds)
            {
                foreach (string expandedId in ExpandLegacyMechanicId(id))
                {
                    if (!string.IsNullOrWhiteSpace(expandedId))
                        normalizedIgnoreDistance.Add(expandedId);
                }
            }

            if (normalizedIgnoreDistance.Count > 0)
                MechanicPriorityIgnoreDistanceIds = normalizedIgnoreDistance;

            if (MechanicPriorityIgnoreDistanceWithinById.Count == 0)
                return;

            Dictionary<string, int> normalizedWithinById = new(PriorityComparer);
            foreach ((string id, int value) in MechanicPriorityIgnoreDistanceWithinById)
            {
                foreach (string expandedId in ExpandLegacyMechanicId(id))
                {
                    if (string.IsNullOrWhiteSpace(expandedId))
                        continue;
                    if (!normalizedWithinById.ContainsKey(expandedId))
                        normalizedWithinById[expandedId] = value;
                }
            }

            if (normalizedWithinById.Count > 0)
                MechanicPriorityIgnoreDistanceWithinById = normalizedWithinById;
        }

        private List<string> BuildSanitizedMechanicPriorityOrder(HashSet<string> validMechanicIds)
        {
            var sanitizedOrder = new List<string>(MechanicPriorityEntries.Length);
            HashSet<string> seen = new(PriorityComparer);

            AddValidUniqueMechanicIds(MechanicPriorityOrder, validMechanicIds, seen, sanitizedOrder);
            AddValidUniqueMechanicIds(MechanicPriorityDefaultOrderIds, validMechanicIds, seen, sanitizedOrder);

            foreach (MechanicPriorityEntry entry in MechanicPriorityEntries)
            {
                if (seen.Add(entry.Id))
                    sanitizedOrder.Add(entry.Id);
            }

            return sanitizedOrder;
        }

        private static void AddValidUniqueMechanicIds(IEnumerable<string> sourceIds, HashSet<string> validMechanicIds, HashSet<string> seen, List<string> destination)
        {
            foreach (string mechanicId in sourceIds)
            {
                foreach (string normalizedMechanicId in ExpandLegacyMechanicId(mechanicId))
                {
                    if (string.IsNullOrWhiteSpace(normalizedMechanicId))
                        continue;
                    if (!validMechanicIds.Contains(normalizedMechanicId))
                        continue;
                    if (!seen.Add(normalizedMechanicId))
                        continue;

                    destination.Add(normalizedMechanicId);
                }
            }
        }

        private static IEnumerable<string> ExpandLegacyMechanicId(string? mechanicId)
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
                yield return MechanicIds.SettlersVerisium;
                yield break;
            }

            yield return mechanicId;
        }

        private void SanitizeMechanicIgnoreDistance(HashSet<string> validMechanicIds, bool applyDefaultIgnoreDistance)
        {
            MechanicPriorityIgnoreDistanceIds.RemoveWhere(id => string.IsNullOrWhiteSpace(id) || !validMechanicIds.Contains(id));
            if (applyDefaultIgnoreDistance)
                MechanicPriorityIgnoreDistanceIds.Add(MechanicIds.Shrines);
        }

        private void SanitizeMechanicIgnoreDistanceWithin(HashSet<string> validMechanicIds)
        {
            string[] invalidKeys = MechanicPriorityIgnoreDistanceWithinById.Keys
                .Where(id => string.IsNullOrWhiteSpace(id) || !validMechanicIds.Contains(id))
                .ToArray();

            foreach (string invalidKey in invalidKeys)
            {
                MechanicPriorityIgnoreDistanceWithinById.Remove(invalidKey);
            }

            foreach (string mechanicId in validMechanicIds)
            {
                if (!MechanicPriorityIgnoreDistanceWithinById.TryGetValue(mechanicId, out int value))
                {
                    MechanicPriorityIgnoreDistanceWithinById[mechanicId] = MechanicIgnoreDistanceWithinDefault;
                    continue;
                }

                MechanicPriorityIgnoreDistanceWithinById[mechanicId] = Math.Clamp(
                    value,
                    MechanicIgnoreDistanceWithinMin,
                    MechanicIgnoreDistanceWithinMax);
            }
        }

        private void EnsureStrongboxFiltersInitialized()
        {
            StrongboxClickIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            StrongboxDontClickIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (StrongboxClickIds.Count == 0 && StrongboxDontClickIds.Count == 0)
            {
                StrongboxClickIds = BuildDefaultClickStrongboxIds();
                StrongboxDontClickIds = BuildDefaultDontClickStrongboxIds();
                return;
            }

            HashSet<string> allowed = new HashSet<string>(StrongboxTableEntries.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);

            StrongboxClickIds.RemoveWhere(x => !allowed.Contains(x));
            StrongboxDontClickIds.RemoveWhere(x => !allowed.Contains(x));

            foreach (string id in StrongboxClickIds.ToArray())
            {
                StrongboxDontClickIds.Remove(id);
            }

            foreach (StrongboxFilterEntry entry in StrongboxTableEntries)
            {
                if (!StrongboxClickIds.Contains(entry.Id) && !StrongboxDontClickIds.Contains(entry.Id))
                {
                    StrongboxDontClickIds.Add(entry.Id);
                }
            }
        }

        private static StrongboxFilterEntry? TryGetStrongboxFilterById(string id)
        {
            return StrongboxTableEntries.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        private void EnsureUltimatumModifiersInitialized()
        {
            UltimatumModifierPriority ??= new List<string>();

            if (UltimatumModifierPriority.Count == 0)
            {
                UltimatumModifierPriority = new List<string>(UltimatumModifiersConstants.AllModifierNames);
                return;
            }

            HashSet<string> valid = new(UltimatumModifiersConstants.AllModifierNames, StringComparer.OrdinalIgnoreCase);
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            var sanitized = new List<string>(UltimatumModifierPriority.Count);
            foreach (string modifier in UltimatumModifierPriority)
            {
                if (string.IsNullOrWhiteSpace(modifier))
                    continue;
                if (!valid.Contains(modifier))
                    continue;
                if (!seen.Add(modifier))
                    continue;

                sanitized.Add(modifier);
            }

            foreach (string modifier in UltimatumModifiersConstants.AllModifierNames)
            {
                if (seen.Add(modifier))
                    sanitized.Add(modifier);
            }

            UltimatumModifierPriority = sanitized;
        }

        private void EnsureUltimatumTakeRewardModifiersInitialized()
        {
            UltimatumTakeRewardModifierNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            UltimatumContinueModifierNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (UltimatumTakeRewardModifierNames.Count == 0 && UltimatumContinueModifierNames.Count == 0)
            {
                UltimatumContinueModifierNames = new HashSet<string>(UltimatumModifiersConstants.AllModifierNamesWithStages, StringComparer.OrdinalIgnoreCase);
                return;
            }

            HashSet<string> allowed = new(UltimatumModifiersConstants.AllModifierNamesWithStages, StringComparer.OrdinalIgnoreCase);
            UltimatumTakeRewardModifierNames.RemoveWhere(x => !allowed.Contains(x));
            UltimatumContinueModifierNames.RemoveWhere(x => !allowed.Contains(x));

            foreach (string name in UltimatumTakeRewardModifierNames.ToArray())
            {
                UltimatumContinueModifierNames.Remove(name);
            }

            foreach (string name in UltimatumModifiersConstants.AllModifierNamesWithStages)
            {
                if (!UltimatumTakeRewardModifierNames.Contains(name) && !UltimatumContinueModifierNames.Contains(name))
                {
                    UltimatumContinueModifierNames.Add(name);
                }
            }
        }

    }
}
