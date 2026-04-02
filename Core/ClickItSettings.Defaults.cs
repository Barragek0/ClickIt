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

        internal void InitializeDefaultsForMigration()
        {
            EnsureItemTypeFiltersInitialized();
            EnsureMechanicPrioritiesInitialized();
            EnsureEssenceCorruptionFiltersInitialized();
            EnsureStrongboxFiltersInitialized();
            EnsureUltimatumModifiersInitialized();
            EnsureUltimatumTakeRewardModifiersInitialized();
        }

        internal void NormalizeForMigration()
        {
            EnsureLazyModeNearbyMonsterFiltersInitialized();
        }

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

            EnsureMissingItemTypeCategoriesAreAssignedToDefaultList();

            SanitizeSubtypeDictionary(ItemTypeWhitelistSubtypeIds, ItemTypeWhitelistIds);
            SanitizeSubtypeDictionary(ItemTypeBlacklistSubtypeIds, ItemTypeBlacklistIds);
        }

        private void EnsureMissingItemTypeCategoriesAreAssignedToDefaultList()
        {
            for (int i = 0; i < ItemCategoryCatalog.All.Length; i++)
            {
                ItemCategoryDefinition category = ItemCategoryCatalog.All[i];
                if (ItemTypeWhitelistIds.Contains(category.Id) || ItemTypeBlacklistIds.Contains(category.Id))
                    continue;

                if (category.DefaultList == ItemListKind.Whitelist)
                {
                    ItemTypeWhitelistIds.Add(category.Id);
                }
                else
                {
                    ItemTypeBlacklistIds.Add(category.Id);
                }
            }
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

        private void ResetItemTypeFilterDefaults()
        {
            ItemTypeWhitelistIds = new HashSet<string>(ItemCategoryCatalog.DefaultWhitelistIds, StringComparer.OrdinalIgnoreCase);
            ItemTypeBlacklistIds = new HashSet<string>(ItemCategoryCatalog.DefaultBlacklistIds, StringComparer.OrdinalIgnoreCase);
            ItemTypeWhitelistSubtypeIds.Clear();
            ItemTypeBlacklistSubtypeIds.Clear();
            UiState.ExpandedItemTypeRowKey = string.Empty;
        }

        private void ResetEssenceCorruptionDefaults()
        {
            EssenceCorruptNames = BuildDefaultCorruptEssenceNames();
            EssenceDontCorruptNames = BuildDefaultDontCorruptEssenceNames();
        }

        private void ResetStrongboxFilterDefaults()
        {
            StrongboxClickIds = BuildDefaultClickStrongboxIds();
            StrongboxDontClickIds = BuildDefaultDontClickStrongboxIds();
        }

        private void ResetMechanicPriorityDefaults()
        {
            MechanicPriorityOrder = MechanicPriorityCatalog.DefaultOrderIds.ToList();
            MechanicPriorityIgnoreDistanceIds = new HashSet<string>(PriorityComparer)
            {
                MechanicIds.Shrines
            };
            MechanicPriorityIgnoreDistanceWithinById = MechanicPriorityCatalog.Ids
                .ToDictionary(static x => x, static _ => MechanicIgnoreDistanceWithinDefault, PriorityComparer);
        }

        private static void SanitizeMutuallyExclusiveSets(
            HashSet<string> primarySet,
            HashSet<string> secondarySet,
            HashSet<string> allowedValues,
            IEnumerable<string> canonicalOrder)
        {
            primarySet.RemoveWhere(x => !allowedValues.Contains(x));
            secondarySet.RemoveWhere(x => !allowedValues.Contains(x));

            foreach (string value in primarySet.ToArray())
            {
                secondarySet.Remove(value);
            }

            foreach (string value in canonicalOrder)
            {
                if (!primarySet.Contains(value) && !secondarySet.Contains(value))
                {
                    secondarySet.Add(value);
                }
            }
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
            SanitizeMutuallyExclusiveSets(EssenceCorruptNames, EssenceDontCorruptNames, allowed, EssenceAllTableNames);
        }

        private void EnsureMechanicPrioritiesInitialized()
        {
            MechanicPriorityOrder ??= new List<string>();
            MechanicPriorityIgnoreDistanceIds ??= new HashSet<string>(PriorityComparer);
            MechanicPriorityIgnoreDistanceWithinById ??= new Dictionary<string, int>(PriorityComparer);

            MechanicPriorityLegacyNormalizer.Normalize(this);

            HashSet<string> valid = new(MechanicPriorityCatalog.Ids, PriorityComparer);

            bool applyDefaultIgnoreDistance = MechanicPriorityIgnoreDistanceIds.Count == 0;
            MechanicPriorityOrder = BuildSanitizedMechanicPriorityOrder(valid);
            SanitizeMechanicIgnoreDistance(valid, applyDefaultIgnoreDistance);
            SanitizeMechanicIgnoreDistanceWithin(valid);
        }

        private List<string> BuildSanitizedMechanicPriorityOrder(HashSet<string> validMechanicIds)
        {
            var sanitizedOrder = new List<string>(MechanicPriorityCatalog.Entries.Length);
            HashSet<string> seen = new(PriorityComparer);

            AddValidUniqueMechanicIds(MechanicPriorityOrder, validMechanicIds, seen, sanitizedOrder);
            AddValidUniqueMechanicIds(MechanicPriorityCatalog.DefaultOrderIds, validMechanicIds, seen, sanitizedOrder);

            foreach (MechanicPriorityEntry entry in MechanicPriorityCatalog.Entries)
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
                foreach (string normalizedMechanicId in MechanicPriorityLegacyNormalizer.ExpandLegacyMechanicId(mechanicId))
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
            SanitizeMutuallyExclusiveSets(
                StrongboxClickIds,
                StrongboxDontClickIds,
                allowed,
                StrongboxTableEntries.Select(static x => x.Id));
        }

        private static StrongboxFilterEntry? TryGetStrongboxFilterById(string id)
        {
            return StrongboxTableEntriesById.TryGetValue(id, out StrongboxFilterEntry? entry)
                ? entry
                : null;
        }

    }
}

