namespace ClickIt.Core.Settings.Defaults
{
    internal sealed class SettingsDefaultsService : ISettingsDefaultsService
    {
        public void Apply(ClickItSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            EnsureItemTypeFiltersInitialized(settings);
            EnsureMechanicPrioritiesInitialized(settings);
            EnsureEssenceCorruptionFiltersInitialized(settings);
            EnsureStrongboxFiltersInitialized(settings);
            settings.EnsureUltimatumModifiersInitialized();
            settings.EnsureUltimatumTakeRewardModifiersInitialized();
        }

        internal static void EnsureItemTypeFiltersInitialized(ClickItSettings settings)
        {
            settings.ItemTypeWhitelistIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            settings.ItemTypeBlacklistIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            settings.ItemTypeWhitelistSubtypeIds ??= new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            settings.ItemTypeBlacklistSubtypeIds ??= new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            if (settings.ItemTypeWhitelistIds.Count == 0 && settings.ItemTypeBlacklistIds.Count == 0)
            {
                ResetItemTypeFilterDefaults(settings);
                settings.ItemTypeBlacklistSubtypeIds["jewels"] = new HashSet<string>(new[] { "regular-jewels", "abyss-jewels" }, StringComparer.OrdinalIgnoreCase);
                return;
            }

            settings.ItemTypeWhitelistIds.RemoveWhere(x => !ItemCategoryCatalog.AllIds.Contains(x));
            settings.ItemTypeBlacklistIds.RemoveWhere(x => !ItemCategoryCatalog.AllIds.Contains(x));

            foreach (string id in settings.ItemTypeWhitelistIds.ToArray())
            {
                settings.ItemTypeBlacklistIds.Remove(id);
            }

            EnsureMissingItemTypeCategoriesAreAssignedToDefaultList(settings);

            SanitizeSubtypeDictionary(settings.ItemTypeWhitelistSubtypeIds, settings.ItemTypeWhitelistIds);
            SanitizeSubtypeDictionary(settings.ItemTypeBlacklistSubtypeIds, settings.ItemTypeBlacklistIds);
        }

        internal static void ResetItemTypeFilterDefaults(ClickItSettings settings)
        {
            settings.ItemTypeWhitelistIds = new HashSet<string>(ItemCategoryCatalog.DefaultWhitelistIds, StringComparer.OrdinalIgnoreCase);
            settings.ItemTypeBlacklistIds = new HashSet<string>(ItemCategoryCatalog.DefaultBlacklistIds, StringComparer.OrdinalIgnoreCase);
            settings.ItemTypeWhitelistSubtypeIds.Clear();
            settings.ItemTypeBlacklistSubtypeIds.Clear();
            settings.UiState.ExpandedItemTypeRowKey = string.Empty;
        }

        internal static void EnsureEssenceCorruptionFiltersInitialized(ClickItSettings settings)
        {
            settings.EssenceCorruptNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            settings.EssenceDontCorruptNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (settings.EssenceCorruptNames.Count == 0 && settings.EssenceDontCorruptNames.Count == 0)
            {
                ResetEssenceCorruptionDefaults(settings);
                return;
            }

            HashSet<string> allowed = new HashSet<string>(ClickItSettings.EssenceAllTableNames, StringComparer.OrdinalIgnoreCase);
            SanitizeMutuallyExclusiveSets(settings.EssenceCorruptNames, settings.EssenceDontCorruptNames, allowed, ClickItSettings.EssenceAllTableNames);
        }

        internal static void ResetEssenceCorruptionDefaults(ClickItSettings settings)
        {
            settings.EssenceCorruptNames = BuildDefaultCorruptEssenceNames();
            settings.EssenceDontCorruptNames = BuildDefaultDontCorruptEssenceNames();
        }

        internal static void EnsureMechanicPrioritiesInitialized(ClickItSettings settings)
        {
            settings.MechanicPriorityOrder ??= new List<string>();
            settings.MechanicPriorityIgnoreDistanceIds ??= new HashSet<string>(ClickItSettings.PriorityComparer);
            settings.MechanicPriorityIgnoreDistanceWithinById ??= new Dictionary<string, int>(ClickItSettings.PriorityComparer);

            MechanicPriorityLegacyNormalizer.Normalize(settings);

            HashSet<string> valid = new(MechanicPriorityCatalog.Ids, ClickItSettings.PriorityComparer);

            bool applyDefaultIgnoreDistance = settings.MechanicPriorityIgnoreDistanceIds.Count == 0;
            settings.MechanicPriorityOrder = BuildSanitizedMechanicPriorityOrder(settings, valid);
            SanitizeMechanicIgnoreDistance(settings, valid, applyDefaultIgnoreDistance);
            SanitizeMechanicIgnoreDistanceWithin(settings, valid);
        }

        internal static void ResetMechanicPriorityDefaults(ClickItSettings settings)
        {
            settings.MechanicPriorityOrder = MechanicPriorityCatalog.DefaultOrderIds.ToList();
            settings.MechanicPriorityIgnoreDistanceIds = new HashSet<string>(ClickItSettings.PriorityComparer)
            {
                MechanicIds.Shrines
            };
            settings.MechanicPriorityIgnoreDistanceWithinById = MechanicPriorityCatalog.Ids
                .ToDictionary(static x => x, static _ => ClickItSettings.MechanicIgnoreDistanceWithinDefault, ClickItSettings.PriorityComparer);
        }

        internal static void EnsureStrongboxFiltersInitialized(ClickItSettings settings)
        {
            settings.StrongboxClickIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            settings.StrongboxDontClickIds ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (settings.StrongboxClickIds.Count == 0 && settings.StrongboxDontClickIds.Count == 0)
            {
                ResetStrongboxFilterDefaults(settings);
                return;
            }

            HashSet<string> allowed = new HashSet<string>(ClickItSettings.StrongboxTableEntries.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
            SanitizeMutuallyExclusiveSets(
                settings.StrongboxClickIds,
                settings.StrongboxDontClickIds,
                allowed,
                ClickItSettings.StrongboxTableEntries.Select(static x => x.Id));
        }

        internal static void ResetStrongboxFilterDefaults(ClickItSettings settings)
        {
            settings.StrongboxClickIds = BuildDefaultClickStrongboxIds();
            settings.StrongboxDontClickIds = BuildDefaultDontClickStrongboxIds();
        }

        internal static ClickItSettings.StrongboxFilterEntry? TryGetStrongboxFilterById(string id)
            => ClickItSettings.StrongboxTableEntriesById.TryGetValue(id, out ClickItSettings.StrongboxFilterEntry? entry)
                ? entry
                : null;

        private static void EnsureMissingItemTypeCategoriesAreAssignedToDefaultList(ClickItSettings settings)
        {
            for (int i = 0; i < ItemCategoryCatalog.All.Length; i++)
            {
                ItemCategoryDefinition category = ItemCategoryCatalog.All[i];
                if (settings.ItemTypeWhitelistIds.Contains(category.Id) || settings.ItemTypeBlacklistIds.Contains(category.Id))
                    continue;

                if (category.DefaultList == ItemListKind.Whitelist)
                {
                    settings.ItemTypeWhitelistIds.Add(category.Id);
                }
                else
                {
                    settings.ItemTypeBlacklistIds.Add(category.Id);
                }
            }
        }

        private static void SanitizeSubtypeDictionary(Dictionary<string, HashSet<string>> subtypeSelections, HashSet<string> parentCategoryIds)
        {
            string[] invalidParentIds = subtypeSelections.Keys
                .Where(id => !parentCategoryIds.Contains(id) || !ClickItSettings.ItemSubtypeCatalog.ContainsKey(id))
                .ToArray();

            foreach (string invalidParentId in invalidParentIds)
            {
                subtypeSelections.Remove(invalidParentId);
            }

            foreach ((string parentId, HashSet<string> selectedSubtypes) in subtypeSelections.ToArray())
            {
                if (!ClickItSettings.ItemSubtypeCatalog.TryGetValue(parentId, out ClickItSettings.ItemSubtypeDefinition[]? subtypeDefinitions))
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
                ClickItSettings.EssenceAllTableNames.Where(name => ClickItSettings.EssenceMedsSuffixes.Any(meds => name.EndsWith($"of {meds}", StringComparison.OrdinalIgnoreCase))),
                StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<string> BuildDefaultDontCorruptEssenceNames()
        {
            HashSet<string> defaults = new HashSet<string>(ClickItSettings.EssenceAllTableNames, StringComparer.OrdinalIgnoreCase);
            defaults.RemoveWhere(name => ClickItSettings.EssenceMedsSuffixes.Any(meds => name.EndsWith($"of {meds}", StringComparison.OrdinalIgnoreCase)));
            return defaults;
        }

        private static HashSet<string> BuildDefaultClickStrongboxIds()
            => new HashSet<string>(ClickItSettings.StrongboxDefaultClickIds, StringComparer.OrdinalIgnoreCase);

        private static HashSet<string> BuildDefaultDontClickStrongboxIds()
        {
            HashSet<string> defaults = new HashSet<string>(ClickItSettings.StrongboxTableEntries.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
            defaults.ExceptWith(ClickItSettings.StrongboxDefaultClickIds);
            return defaults;
        }

        internal static void SanitizeMutuallyExclusiveSets(
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

        private static List<string> BuildSanitizedMechanicPriorityOrder(ClickItSettings settings, HashSet<string> validMechanicIds)
        {
            var sanitizedOrder = new List<string>(MechanicPriorityCatalog.Entries.Length);
            HashSet<string> seen = new(ClickItSettings.PriorityComparer);

            AddValidUniqueMechanicIds(settings.MechanicPriorityOrder, validMechanicIds, seen, sanitizedOrder);
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

        private static void SanitizeMechanicIgnoreDistance(ClickItSettings settings, HashSet<string> validMechanicIds, bool applyDefaultIgnoreDistance)
        {
            settings.MechanicPriorityIgnoreDistanceIds.RemoveWhere(id => string.IsNullOrWhiteSpace(id) || !validMechanicIds.Contains(id));
            if (applyDefaultIgnoreDistance)
                settings.MechanicPriorityIgnoreDistanceIds.Add(MechanicIds.Shrines);
        }

        private static void SanitizeMechanicIgnoreDistanceWithin(ClickItSettings settings, HashSet<string> validMechanicIds)
        {
            string[] invalidKeys = settings.MechanicPriorityIgnoreDistanceWithinById.Keys
                .Where(id => string.IsNullOrWhiteSpace(id) || !validMechanicIds.Contains(id))
                .ToArray();

            foreach (string invalidKey in invalidKeys)
            {
                settings.MechanicPriorityIgnoreDistanceWithinById.Remove(invalidKey);
            }

            foreach (string mechanicId in validMechanicIds)
            {
                if (!settings.MechanicPriorityIgnoreDistanceWithinById.TryGetValue(mechanicId, out int value))
                {
                    settings.MechanicPriorityIgnoreDistanceWithinById[mechanicId] = ClickItSettings.MechanicIgnoreDistanceWithinDefault;
                    continue;
                }

                settings.MechanicPriorityIgnoreDistanceWithinById[mechanicId] = Math.Clamp(
                    value,
                    ClickItSettings.MechanicIgnoreDistanceWithinMin,
                    ClickItSettings.MechanicIgnoreDistanceWithinMax);
            }
        }
    }
}
