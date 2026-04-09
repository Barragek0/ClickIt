namespace ClickIt
{
    public partial class ClickItSettings
    {
        public IReadOnlyList<string> GetCorruptEssenceNames()
        {
            SettingsDefaultsService.EnsureEssenceCorruptionFiltersInitialized(this);

            HashSet<string> uniqueNames = new(StringComparer.OrdinalIgnoreCase);
            foreach (string essenceName in EssenceCorruptNames)
            {
                if (!string.IsNullOrWhiteSpace(essenceName))
                {
                    uniqueNames.Add(essenceName);
                }
            }

            return [.. uniqueNames];
        }

        public IReadOnlyList<string> GetStrongboxClickMetadataIdentifiers()
        {
            SettingsDefaultsService.EnsureStrongboxFiltersInitialized(this);
            RefreshStrongboxMetadataSnapshotsIfNeeded();
            return TransientState.RuntimeCache.StrongboxClickMetadataSnapshot;
        }

        public IReadOnlyList<string> GetStrongboxDontClickMetadataIdentifiers()
        {
            SettingsDefaultsService.EnsureStrongboxFiltersInitialized(this);
            RefreshStrongboxMetadataSnapshotsIfNeeded();
            return TransientState.RuntimeCache.StrongboxDontClickMetadataSnapshot;
        }

        public IReadOnlyList<string> GetItemTypeWhitelistMetadataIdentifiers()
        {
            SettingsDefaultsService.EnsureItemTypeFiltersInitialized(this);
            RefreshItemTypeMetadataSnapshotsIfNeeded();
            return TransientState.RuntimeCache.ItemTypeWhitelistMetadataSnapshot;
        }

        public IReadOnlyList<string> GetItemTypeBlacklistMetadataIdentifiers()
        {
            SettingsDefaultsService.EnsureItemTypeFiltersInitialized(this);
            RefreshItemTypeMetadataSnapshotsIfNeeded();
            return TransientState.RuntimeCache.ItemTypeBlacklistMetadataSnapshot;
        }

        internal static bool TryGetSubtypeDefinitions(string categoryId, out ItemSubtypeDefinition[] definitions)
        {
            return ItemSubtypeCatalog.TryGetValue(categoryId, out definitions!);
        }

        private void RefreshStrongboxMetadataSnapshotsIfNeeded()
        {
            ClickItSettingsRuntimeCacheState runtimeCache = TransientState.RuntimeCache;
            int signature = ComputeStrongboxMetadataSignature();
            int currentSignature = runtimeCache.StrongboxMetadataSnapshotSignature;
            bool snapshotChanged = MetadataSnapshotCache.RefreshPair(
                ref currentSignature,
                signature,
                () => BuildStrongboxMetadataIdentifiers(StrongboxClickIds),
                () => BuildStrongboxMetadataIdentifiers(StrongboxDontClickIds),
                out string[] clickSnapshot,
                out string[] dontClickSnapshot);
            runtimeCache.StrongboxMetadataSnapshotSignature = currentSignature;

            if (snapshotChanged)
            {
                runtimeCache.StrongboxClickMetadataSnapshot = clickSnapshot;
                runtimeCache.StrongboxDontClickMetadataSnapshot = dontClickSnapshot;
            }
        }

        private int ComputeStrongboxMetadataSignature()
        {
            int clickHash = ComputeCaseInsensitiveSetHash(StrongboxClickIds);
            int dontClickHash = ComputeCaseInsensitiveSetHash(StrongboxDontClickIds);
            return HashCode.Combine(StrongboxClickIds.Count, clickHash, StrongboxDontClickIds.Count, dontClickHash);
        }

        private static int ComputeCaseInsensitiveSetHash(IEnumerable<string> values)
        {
            int hash = 0;
            foreach (string value in values)
            {
                hash ^= StringComparer.OrdinalIgnoreCase.GetHashCode(value ?? string.Empty);
            }

            return hash;
        }

        private static string[] BuildStrongboxMetadataIdentifiers(HashSet<string> strongboxIds)
        {
            HashSet<string> metadataIdentifiers = new(StringComparer.OrdinalIgnoreCase);

            foreach (string id in strongboxIds)
            {
                StrongboxFilterEntry? entry = SettingsDefaultsService.TryGetStrongboxFilterById(id);
                if (entry?.MetadataIdentifiers == null)
                    continue;

                foreach (string metadataIdentifier in entry.MetadataIdentifiers)
                {
                    if (!string.IsNullOrWhiteSpace(metadataIdentifier))
                    {
                        metadataIdentifiers.Add(metadataIdentifier);
                    }
                }
            }

            return [.. metadataIdentifiers];
        }

        private void RefreshItemTypeMetadataSnapshotsIfNeeded()
        {
            ClickItSettingsRuntimeCacheState runtimeCache = TransientState.RuntimeCache;
            int signature = ComputeItemTypeMetadataSignature();
            int currentSignature = runtimeCache.ItemTypeMetadataSnapshotSignature;
            bool snapshotChanged = MetadataSnapshotCache.RefreshPair(
                ref currentSignature,
                signature,
                () => BuildItemTypeMetadataIdentifiers(
                    primaryIds: ItemTypeWhitelistIds,
                    primaryIsWhitelist: true,
                    oppositeIds: ItemTypeBlacklistIds,
                    oppositeIsWhitelist: false),
                () => BuildItemTypeMetadataIdentifiers(
                    primaryIds: ItemTypeBlacklistIds,
                    primaryIsWhitelist: false,
                    oppositeIds: ItemTypeWhitelistIds,
                    oppositeIsWhitelist: true),
                out string[] whitelistSnapshot,
                out string[] blacklistSnapshot);
            runtimeCache.ItemTypeMetadataSnapshotSignature = currentSignature;

            if (snapshotChanged)
            {
                runtimeCache.ItemTypeWhitelistMetadataSnapshot = whitelistSnapshot;
                runtimeCache.ItemTypeBlacklistMetadataSnapshot = blacklistSnapshot;
            }
        }

        private int ComputeItemTypeMetadataSignature()
        {
            int whitelistHash = ComputeCaseInsensitiveSetHash(ItemTypeWhitelistIds);
            int blacklistHash = ComputeCaseInsensitiveSetHash(ItemTypeBlacklistIds);
            int whitelistSubtypeHash = ComputeSubtypeDictionaryHash(ItemTypeWhitelistSubtypeIds);
            int blacklistSubtypeHash = ComputeSubtypeDictionaryHash(ItemTypeBlacklistSubtypeIds);

            return HashCode.Combine(
                ItemTypeWhitelistIds.Count,
                ItemTypeBlacklistIds.Count,
                ItemTypeWhitelistSubtypeIds.Count,
                ItemTypeBlacklistSubtypeIds.Count,
                whitelistHash,
                blacklistHash,
                whitelistSubtypeHash,
                blacklistSubtypeHash);
        }

        private static int ComputeSubtypeDictionaryHash(Dictionary<string, HashSet<string>> source)
        {
            int hash = 0;
            foreach ((string key, HashSet<string> values) in source)
            {
                int entryHash = StringComparer.OrdinalIgnoreCase.GetHashCode(key ?? string.Empty);
                IEnumerable<string> entries = values;
                entryHash = HashCode.Combine(entryHash, values.Count, ComputeCaseInsensitiveSetHash(entries));
                hash ^= entryHash;
            }

            return hash;
        }

        private string[] BuildItemTypeMetadataIdentifiers(
            HashSet<string> primaryIds,
            bool primaryIsWhitelist,
            HashSet<string> oppositeIds,
            bool oppositeIsWhitelist)
        {
            HashSet<string> metadataIdentifiers = new(StringComparer.OrdinalIgnoreCase);

            AddEffectiveMetadataIdentifiers(metadataIdentifiers, primaryIds, primaryIsWhitelist, includeOppositeSubtypeSelections: false);
            AddEffectiveMetadataIdentifiers(metadataIdentifiers, oppositeIds, oppositeIsWhitelist, includeOppositeSubtypeSelections: true);

            return [.. metadataIdentifiers];
        }

        private void AddEffectiveMetadataIdentifiers(
            HashSet<string> target,
            HashSet<string> categoryIds,
            bool isWhitelist,
            bool includeOppositeSubtypeSelections)
        {
            foreach (string categoryId in categoryIds)
            {
                foreach (string metadataIdentifier in GetEffectiveMetadataIdentifiers(categoryId, isWhitelist, includeOppositeSubtypeSelections))
                {
                    if (!string.IsNullOrWhiteSpace(metadataIdentifier))
                    {
                        target.Add(metadataIdentifier);
                    }
                }
            }
        }

        private IEnumerable<string> GetEffectiveMetadataIdentifiers(string categoryId, bool isWhitelist, bool includeOppositeSubtypeSelections)
        {
            if (!ItemCategoryCatalog.TryGet(categoryId, out ItemCategoryDefinition? category))
            {
                return [];
            }

            if (!TryGetSubtypeDefinitions(categoryId, out ItemSubtypeDefinition[] subtypeDefinitions))
            {
                if (includeOppositeSubtypeSelections)
                {
                    return [];
                }

                return category.MetadataIdentifiers;
            }

            Dictionary<string, HashSet<string>> subtypeConfig = isWhitelist ? ItemTypeWhitelistSubtypeIds : ItemTypeBlacklistSubtypeIds;
            if (!subtypeConfig.TryGetValue(categoryId, out HashSet<string>? selectedSubtypeIds) || selectedSubtypeIds.Count == 0)
            {
                if (includeOppositeSubtypeSelections)
                {
                    return [];
                }

                return category.MetadataIdentifiers;
            }

            HashSet<string> metadataIdentifiers = new(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < subtypeDefinitions.Length; i++)
            {
                ItemSubtypeDefinition subtypeDefinition = subtypeDefinitions[i];
                bool includeSubtype = includeOppositeSubtypeSelections
                    ? !selectedSubtypeIds.Contains(subtypeDefinition.Id)
                    : selectedSubtypeIds.Contains(subtypeDefinition.Id);
                if (!includeSubtype)
                    continue;

                IReadOnlyList<string> subtypeMetadataIdentifiers = subtypeDefinition.MetadataIdentifiers;
                for (int j = 0; j < subtypeMetadataIdentifiers.Count; j++)
                {
                    string metadataIdentifier = subtypeMetadataIdentifiers[j];
                    if (!string.IsNullOrWhiteSpace(metadataIdentifier))
                    {
                        metadataIdentifiers.Add(metadataIdentifier);
                    }
                }
            }

            return [.. metadataIdentifiers];
        }
    }
}