using ClickIt.Definitions;
using ClickIt.Core.Settings.Runtime;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private readonly struct ItemTypeRowRenderState(bool rowClicked, bool arrowClicked, bool rowHovered)
        {
            public bool RowClicked { get; } = rowClicked;
            public bool ArrowClicked { get; } = arrowClicked;
            public bool RowHovered { get; } = rowHovered;
        }

        private void DrawItemTypeFiltersPanel()
        {
            SettingsDefaultsService.EnsureItemTypeFiltersInitialized(this);

            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Table rows with [v] next to them can be clicked to open subtype filter options.");
            ImGui.Spacing();

            DrawSearchBar("##ItemTypeSearch", "Clear##ItemTypeClear", ref UiState.ItemTypeSearchFilter);
            if (DrawResetDefaultsButton("Reset Defaults##ItemTypeDefaults"))
            {
                SettingsDefaultsService.ResetItemTypeFilterDefaults(this);
            }

            ImGui.Spacing();

            DrawDualTransferTable(
                tableId: "ItemTypeFilterLists",
                leftHeader: "Click",
                rightHeader: "Don't Click",
                leftBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                drawLeft: () => DrawItemTypeList("Click##ItemType", ItemTypeWhitelistIds, moveToWhitelist: false, textColor: WhitelistTextColor),
                drawRight: () => DrawItemTypeList("Don't Click##ItemType", ItemTypeBlacklistIds, moveToWhitelist: true, textColor: BlacklistTextColor));
        }

        private void DrawItemTypeList(string id, HashSet<string> sourceSet, bool moveToWhitelist, Vector4 textColor)
        {
            ImGui.PushID(id);

            bool hasEntries = false;
            foreach (ItemCategoryDefinition category in ItemCategoryCatalog.All)
            {
                if (!ShouldRenderItemTypeCategory(sourceSet, category))
                    continue;

                hasEntries = true;
                ItemTypeRowRenderState rowState = DrawItemTypeRow(id, category, moveToWhitelist, textColor);

                if (rowState.ArrowClicked)
                {
                    MoveItemTypeCategory(category.Id, moveToWhitelist);
                    UiState.ExpandedItemTypeRowKey = string.Empty;
                    break;
                }

                if (rowState.RowClicked)
                {
                    ToggleExpandedRow(id, category.Id);
                }

                DrawItemTypeExamplesIfHovered(category, rowState.RowHovered);

                if (IsExpandedRow(id, category.Id))
                {
                    DrawItemTypeSubtypePanel(id, category, isSourceWhitelist: !moveToWhitelist);
                }
            }

            DrawNoEntriesPlaceholder(hasEntries);

            ImGui.PopID();
        }

        private bool ShouldRenderItemTypeCategory(HashSet<string> sourceSet, ItemCategoryDefinition category)
        {
            return sourceSet.Contains(category.Id) && MatchesItemTypeSearch(category, UiState.ItemTypeSearchFilter);
        }

        private ItemTypeRowRenderState DrawItemTypeRow(string id, ItemCategoryDefinition category, bool moveToWhitelist, Vector4 textColor)
        {
            string label = BuildItemTypeRowLabel(id, category);
            float rowWidth = CalculateItemTypeRowWidth();
            const float arrowWidth = 28f;

            if (moveToWhitelist)
            {
                bool leftArrowClicked = ImGui.Button($"<-##Move_{id}_{category.Id}", new Vector2(arrowWidth, 0));
                ImGui.SameLine();
                bool rowClicked = DrawItemTypeSelectable(id, category, textColor, label, rowWidth);
                bool rowHovered = ImGui.IsItemHovered();
                return new ItemTypeRowRenderState(rowClicked, leftArrowClicked, rowHovered);
            }

            bool clicked = DrawItemTypeSelectable(id, category, textColor, label, rowWidth);
            bool hovered = ImGui.IsItemHovered();
            ImGui.SameLine();
            bool rightArrowClicked = ImGui.Button($"->##Move_{id}_{category.Id}", new Vector2(arrowWidth, 0));
            return new ItemTypeRowRenderState(clicked, rightArrowClicked, hovered);
        }

        private static string BuildItemTypeRowLabel(string id, ItemCategoryDefinition category)
        {
            bool hasSubtypeMenu = TryGetSubtypeDefinitions(category.Id, out _);
            string submenuIndicator = hasSubtypeMenu ? " [v]" : string.Empty;
            return $"{category.DisplayName}{submenuIndicator}##{id}_{category.Id}";
        }

        private bool DrawItemTypeSelectable(string id, ItemCategoryDefinition category, Vector4 textColor, string label, float rowWidth)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            bool clicked = ImGui.Selectable(label, IsExpandedRow(id, category.Id), ImGuiSelectableFlags.AllowDoubleClick, new Vector2(rowWidth, 0));
            ImGui.PopStyleColor();
            return clicked;
        }

        private static void DrawItemTypeExamplesIfHovered(ItemCategoryDefinition category, bool rowHovered)
        {
            if (!rowHovered || category.ExampleItems.Count == 0)
            {
                return;
            }

            string examples = string.Join(", ", category.ExampleItems);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
            ImGui.Indent();
            ImGui.TextWrapped($"Examples: {examples}");
            ImGui.Unindent();
            ImGui.PopStyleColor();
        }

        private void DrawEssenceCorruptionTablePanel()
        {
            SettingsDefaultsService.EnsureEssenceCorruptionFiltersInitialized(this);

            DrawSearchBar("##EssenceSearch", "Clear##EssenceSearchClear", ref UiState.EssenceSearchFilter);
            if (DrawResetDefaultsButton("Reset Defaults##EssenceResetDefaults"))
            {
                SettingsDefaultsService.ResetEssenceCorruptionDefaults(this);
            }

            ImGui.Spacing();

            DrawDualTransferTable(
                tableId: "EssenceCorruptionLists",
                leftHeader: "Corrupt",
                rightHeader: "Don't Corrupt",
                leftBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                drawLeft: () => DrawEssenceCorruptionList("Corrupt##Essence", EssenceCorruptNames, moveToCorrupt: false, textColor: new Vector4(0.8f, 0.4f, 0.4f, 1.0f)),
                drawRight: () => DrawEssenceCorruptionList("DontCorrupt##Essence", EssenceDontCorruptNames, moveToCorrupt: true, textColor: new Vector4(0.4f, 0.8f, 0.4f, 1.0f)));
        }

        private void DrawEssenceCorruptionList(string id, HashSet<string> sourceSet, bool moveToCorrupt, Vector4 textColor)
        {
            ImGui.PushID(id);

            bool hasEntries = false;
            foreach (string essenceName in EssenceAllTableNames)
            {
                if (!sourceSet.Contains(essenceName))
                    continue;
                if (!MatchesEssenceSearch(essenceName, UiState.EssenceSearchFilter))
                    continue;

                hasEntries = true;
                bool arrowClicked = DrawTransferListRow(id, essenceName, essenceName, moveToCorrupt, textColor);

                if (arrowClicked)
                {
                    MoveEssenceName(essenceName, moveToCorrupt);
                    break;
                }
            }

            DrawNoEntriesPlaceholder(hasEntries);

            ImGui.PopID();
        }

        private void DrawStrongboxFilterTablePanel()
        {
            SettingsDefaultsService.EnsureStrongboxFiltersInitialized(this);

            DrawSearchBar("##StrongboxSearch", "Clear##StrongboxSearchClear", ref UiState.StrongboxSearchFilter);
            if (DrawResetDefaultsButton("Reset Defaults##StrongboxResetDefaults"))
            {
                SettingsDefaultsService.ResetStrongboxFilterDefaults(this);
            }

            ImGui.Spacing();

            DrawDualTransferTable(
                tableId: "StrongboxFilterLists",
                leftHeader: "Click",
                rightHeader: "Don't Click",
                leftBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                drawLeft: () => DrawStrongboxFilterList("Click##Strongbox", StrongboxClickIds, moveToClick: false, textColor: new Vector4(0.4f, 0.8f, 0.4f, 1.0f)),
                drawRight: () => DrawStrongboxFilterList("DontClick##Strongbox", StrongboxDontClickIds, moveToClick: true, textColor: new Vector4(0.8f, 0.4f, 0.4f, 1.0f)));
        }

        private void DrawStrongboxFilterList(string id, HashSet<string> sourceSet, bool moveToClick, Vector4 textColor)
        {
            ImGui.PushID(id);

            bool hasEntries = false;
            foreach (StrongboxFilterEntry entry in StrongboxTableEntries)
            {
                if (!sourceSet.Contains(entry.Id))
                    continue;
                if (!MatchesStrongboxSearch(entry, UiState.StrongboxSearchFilter))
                    continue;

                hasEntries = true;
                bool arrowClicked = DrawTransferListRow(id, entry.Id, entry.DisplayName, moveToClick, textColor);

                if (arrowClicked)
                {
                    MoveStrongboxFilter(entry.Id, moveToClick);
                    break;
                }
            }

            DrawNoEntriesPlaceholder(hasEntries);

            ImGui.PopID();
        }

        private void MoveStrongboxFilter(string strongboxId, bool moveToClick)
        {
            HashSet<string> source = moveToClick ? StrongboxDontClickIds : StrongboxClickIds;
            HashSet<string> target = moveToClick ? StrongboxClickIds : StrongboxDontClickIds;

            source.Remove(strongboxId);
            target.Add(strongboxId);
        }

        private static bool MatchesStrongboxSearch(StrongboxFilterEntry entry, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            string term = filter.Trim();
            if (entry.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (string metadataIdentifier in entry.MetadataIdentifiers)
            {
                if (metadataIdentifier.Contains(term, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void MoveEssenceName(string essenceName, bool moveToCorrupt)
        {
            HashSet<string> source = moveToCorrupt ? EssenceDontCorruptNames : EssenceCorruptNames;
            HashSet<string> target = moveToCorrupt ? EssenceCorruptNames : EssenceDontCorruptNames;

            source.Remove(essenceName);
            target.Add(essenceName);
        }

        private static bool MatchesEssenceSearch(string essenceName, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            return essenceName.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
        }

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
            return _strongboxClickMetadataSnapshot;
        }

        public IReadOnlyList<string> GetStrongboxDontClickMetadataIdentifiers()
        {
            SettingsDefaultsService.EnsureStrongboxFiltersInitialized(this);
            RefreshStrongboxMetadataSnapshotsIfNeeded();
            return _strongboxDontClickMetadataSnapshot;
        }

        private void RefreshStrongboxMetadataSnapshotsIfNeeded()
        {
            int signature = ComputeStrongboxMetadataSignature();
            MetadataSnapshotCache.RefreshPair(
                ref _strongboxMetadataSnapshotSignature,
                signature,
                () => BuildStrongboxMetadataIdentifiers(StrongboxClickIds),
                () => BuildStrongboxMetadataIdentifiers(StrongboxDontClickIds),
                out string[] clickSnapshot,
                out string[] dontClickSnapshot);

            if (clickSnapshot.Length > 0 || dontClickSnapshot.Length > 0)
            {
                _strongboxClickMetadataSnapshot = clickSnapshot;
                _strongboxDontClickMetadataSnapshot = dontClickSnapshot;
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

            return metadataIdentifiers.ToArray();
        }

        private void DrawItemTypeSubtypePanel(string listId, ItemCategoryDefinition category, bool isSourceWhitelist)
        {
            if (!TryGetSubtypeDefinitions(category.Id, out ItemSubtypeDefinition[] subtypeDefinitions))
            {
                return;
            }

            HashSet<string> selectedSubtypeIds = GetOrCreateSubtypeSelection(isSourceWhitelist, category.Id);

            ImGui.Indent();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.75f, 0.75f, 0.75f, 1f));
            ImGui.TextWrapped("Subtype filter: select subtypes to narrow this category. Example: choosing only Helmets means Body Armours/Gloves/Boots/Shields will be treated as being in the opposite list.");
            ImGui.PopStyleColor();

            bool hasActiveSelection = selectedSubtypeIds.Count > 0;
            foreach (ItemSubtypeDefinition subtype in subtypeDefinitions)
            {
                DrawSubtypeCheckboxRow(listId, category.Id, isSourceWhitelist, hasActiveSelection, selectedSubtypeIds, subtype);
            }

            ImGui.Unindent();
        }

        private void DrawSubtypeCheckboxRow(
            string listId,
            string categoryId,
            bool isSourceWhitelist,
            bool hasActiveSelection,
            HashSet<string> selectedSubtypeIds,
            ItemSubtypeDefinition subtype)
        {
            bool isSelected = selectedSubtypeIds.Contains(subtype.Id);
            bool subtypeIsWhitelistSide = hasActiveSelection
                ? (isSourceWhitelist ? isSelected : !isSelected)
                : isSourceWhitelist;
            Vector4 subtypeTextColor = subtypeIsWhitelistSide ? WhitelistTextColor : BlacklistTextColor;

            ImGui.PushStyleColor(ImGuiCol.Text, subtypeTextColor);
            if (ImGui.Checkbox($"{subtype.DisplayName}##Subtype_{listId}_{categoryId}_{subtype.Id}", ref isSelected))
            {
                if (isSelected)
                {
                    selectedSubtypeIds.Add(subtype.Id);
                }
                else
                {
                    selectedSubtypeIds.Remove(subtype.Id);
                }
            }
            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
            {
                string metadataPreview = string.Join("\n", subtype.MetadataIdentifiers);
                ImGui.SetTooltip(metadataPreview);
            }
        }

        private static bool TryGetSubtypeDefinitions(string categoryId, out ItemSubtypeDefinition[] definitions)
        {
            return ItemSubtypeCatalog.TryGetValue(categoryId, out definitions!);
        }

        private HashSet<string> GetOrCreateSubtypeSelection(bool isWhitelist, string categoryId)
        {
            Dictionary<string, HashSet<string>> source = isWhitelist ? ItemTypeWhitelistSubtypeIds : ItemTypeBlacklistSubtypeIds;
            if (!source.TryGetValue(categoryId, out HashSet<string>? subtypeSelection))
            {
                subtypeSelection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                source[categoryId] = subtypeSelection;
            }

            return subtypeSelection;
        }

        private static string BuildExpandedRowKey(string listId, string categoryId)
        {
            return $"{listId}:{categoryId}";
        }

        private bool IsExpandedRow(string listId, string categoryId)
        {
            return string.Equals(UiState.ExpandedItemTypeRowKey, BuildExpandedRowKey(listId, categoryId), StringComparison.Ordinal);
        }

        private void ToggleExpandedRow(string listId, string categoryId)
        {
            string rowKey = BuildExpandedRowKey(listId, categoryId);
            if (string.Equals(UiState.ExpandedItemTypeRowKey, rowKey, StringComparison.Ordinal))
            {
                UiState.ExpandedItemTypeRowKey = string.Empty;
            }
            else
            {
                UiState.ExpandedItemTypeRowKey = rowKey;
            }
        }

        private void MoveItemTypeCategory(string categoryId, bool moveToWhitelist)
        {
            HashSet<string> sourceSet = moveToWhitelist ? ItemTypeBlacklistIds : ItemTypeWhitelistIds;
            HashSet<string> targetSet = moveToWhitelist ? ItemTypeWhitelistIds : ItemTypeBlacklistIds;
            Dictionary<string, HashSet<string>> sourceSubtypeDict = moveToWhitelist ? ItemTypeBlacklistSubtypeIds : ItemTypeWhitelistSubtypeIds;
            Dictionary<string, HashSet<string>> targetSubtypeDict = moveToWhitelist ? ItemTypeWhitelistSubtypeIds : ItemTypeBlacklistSubtypeIds;

            sourceSet.Remove(categoryId);
            targetSet.Add(categoryId);

            if (sourceSubtypeDict.TryGetValue(categoryId, out HashSet<string>? subtypeSelection))
            {
                sourceSubtypeDict.Remove(categoryId);
                targetSubtypeDict[categoryId] = new HashSet<string>(subtypeSelection, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                targetSubtypeDict.Remove(categoryId);
            }
        }

        private static bool MatchesItemTypeSearch(ItemCategoryDefinition category, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            string term = filter.Trim();
            return category.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || category.MetadataIdentifiers.Any(x => x.Contains(term, StringComparison.OrdinalIgnoreCase))
                || category.Id.Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        public IReadOnlyList<string> GetItemTypeWhitelistMetadataIdentifiers()
        {
            SettingsDefaultsService.EnsureItemTypeFiltersInitialized(this);
            RefreshItemTypeMetadataSnapshotsIfNeeded();
            return _itemTypeWhitelistMetadataSnapshot;
        }

        public IReadOnlyList<string> GetItemTypeBlacklistMetadataIdentifiers()
        {
            SettingsDefaultsService.EnsureItemTypeFiltersInitialized(this);
            RefreshItemTypeMetadataSnapshotsIfNeeded();
            return _itemTypeBlacklistMetadataSnapshot;
        }

        private void RefreshItemTypeMetadataSnapshotsIfNeeded()
        {
            int signature = ComputeItemTypeMetadataSignature();
            MetadataSnapshotCache.RefreshPair(
                ref _itemTypeMetadataSnapshotSignature,
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

            if (whitelistSnapshot.Length > 0 || blacklistSnapshot.Length > 0)
            {
                _itemTypeWhitelistMetadataSnapshot = whitelistSnapshot;
                _itemTypeBlacklistMetadataSnapshot = blacklistSnapshot;
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

            return metadataIdentifiers.ToArray();
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
