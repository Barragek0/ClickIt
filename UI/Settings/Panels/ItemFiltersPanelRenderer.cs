namespace ClickIt.UI.Settings.Panels
{
    internal sealed class ItemFiltersPanelRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        public void DrawItemTypeFiltersPanel()
        {
            SettingsDefaultsService.EnsureItemTypeFiltersInitialized(_settings);

            SettingsUiRenderHelpers.DrawInstructionText("Table rows with [v] next to them can be clicked to open subtype filter options.");
            ImGui.Spacing();

            SettingsUiRenderHelpers.DrawSearchBar("##ItemTypeSearch", "Clear##ItemTypeClear", ref _settings.UiState.ItemTypeSearchFilter);
            if (SettingsUiRenderHelpers.DrawResetDefaultsButton("Reset Defaults##ItemTypeDefaults"))
            {
                SettingsDefaultsService.ResetItemTypeFilterDefaults(_settings);
            }

            ImGui.Spacing();

            SettingsUiRenderHelpers.DrawDualTransferTable(
                tableId: "ItemTypeFilterLists",
                leftHeader: "Click",
                rightHeader: "Don't Click",
                leftBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                drawLeft: () => DrawItemTypeList("Click##ItemType", _settings.ItemTypeWhitelistIds, moveToWhitelist: false, textColor: SettingsUiPalette.WhitelistTextColor),
                drawRight: () => DrawItemTypeList("Don't Click##ItemType", _settings.ItemTypeBlacklistIds, moveToWhitelist: true, textColor: SettingsUiPalette.BlacklistTextColor));
        }

        public void DrawEssenceCorruptionTablePanel()
        {
            SettingsDefaultsService.EnsureEssenceCorruptionFiltersInitialized(_settings);

            SettingsUiRenderHelpers.DrawSearchBar("##EssenceSearch", "Clear##EssenceSearchClear", ref _settings.UiState.EssenceSearchFilter);
            if (SettingsUiRenderHelpers.DrawResetDefaultsButton("Reset Defaults##EssenceResetDefaults"))
            {
                SettingsDefaultsService.ResetEssenceCorruptionDefaults(_settings);
            }

            ImGui.Spacing();

            SettingsUiRenderHelpers.DrawDualTransferTable(
                tableId: "EssenceCorruptionLists",
                leftHeader: "Corrupt",
                rightHeader: "Don't Corrupt",
                leftBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                drawLeft: () => DrawEssenceCorruptionList("Corrupt##Essence", _settings.EssenceCorruptNames, moveToCorrupt: false, textColor: new Vector4(0.8f, 0.4f, 0.4f, 1.0f)),
                drawRight: () => DrawEssenceCorruptionList("DontCorrupt##Essence", _settings.EssenceDontCorruptNames, moveToCorrupt: true, textColor: new Vector4(0.4f, 0.8f, 0.4f, 1.0f)));
        }

        public void DrawStrongboxFilterTablePanel()
        {
            SettingsDefaultsService.EnsureStrongboxFiltersInitialized(_settings);

            SettingsUiRenderHelpers.DrawSearchBar("##StrongboxSearch", "Clear##StrongboxSearchClear", ref _settings.UiState.StrongboxSearchFilter);
            if (SettingsUiRenderHelpers.DrawResetDefaultsButton("Reset Defaults##StrongboxResetDefaults"))
            {
                SettingsDefaultsService.ResetStrongboxFilterDefaults(_settings);
            }

            ImGui.Spacing();

            SettingsUiRenderHelpers.DrawDualTransferTable(
                tableId: "StrongboxFilterLists",
                leftHeader: "Click",
                rightHeader: "Don't Click",
                leftBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                drawLeft: () => DrawStrongboxFilterList("Click##Strongbox", _settings.StrongboxClickIds, moveToClick: false, textColor: SettingsUiPalette.WhitelistTextColor),
                drawRight: () => DrawStrongboxFilterList("DontClick##Strongbox", _settings.StrongboxDontClickIds, moveToClick: true, textColor: SettingsUiPalette.BlacklistTextColor));
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
                SettingsUiRenderHelpers.ExpandableTransferRowState rowState = DrawItemTypeRow(id, category, moveToWhitelist, textColor);

                if (rowState.ArrowClicked)
                {
                    MoveItemTypeCategory(category.Id, moveToWhitelist);
                    _settings.UiState.ExpandedItemTypeRowKey = string.Empty;
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

            SettingsUiRenderHelpers.DrawNoEntriesPlaceholder(hasEntries);

            ImGui.PopID();
        }

        private bool ShouldRenderItemTypeCategory(HashSet<string> sourceSet, ItemCategoryDefinition category)
        {
            return sourceSet.Contains(category.Id) && MatchesItemTypeSearch(category, _settings.UiState.ItemTypeSearchFilter);
        }

        private SettingsUiRenderHelpers.ExpandableTransferRowState DrawItemTypeRow(string id, ItemCategoryDefinition category, bool moveToWhitelist, Vector4 textColor)
        {
            string label = BuildItemTypeRowLabel(id, category);
            return SettingsUiRenderHelpers.DrawExpandableTransferListRow(
                $"Move_{id}_{category.Id}",
                label,
                IsExpandedRow(id, category.Id),
                moveToWhitelist,
                textColor);
        }

        private static string BuildItemTypeRowLabel(string id, ItemCategoryDefinition category)
        {
            bool hasSubtypeMenu = ClickItSettings.TryGetSubtypeDefinitions(category.Id, out _);
            string submenuIndicator = hasSubtypeMenu ? " [v]" : string.Empty;
            return $"{category.DisplayName}{submenuIndicator}##{id}_{category.Id}";
        }

        private static void DrawItemTypeExamplesIfHovered(ItemCategoryDefinition category, bool rowHovered)
        {
            if (!rowHovered || category.ExampleItems.Count == 0)
            {
                return;
            }

            string examples = string.Join(", ", category.ExampleItems);
            SettingsUiRenderHelpers.DrawWrappedText($"Examples: {examples}", new Vector4(0.65f, 0.65f, 0.65f, 1f), 1f);
        }

        private void DrawEssenceCorruptionList(string id, HashSet<string> sourceSet, bool moveToCorrupt, Vector4 textColor)
        {
            ImGui.PushID(id);

            bool hasEntries = false;
            foreach (string essenceName in ClickItSettings.EssenceAllTableNames)
            {
                if (!sourceSet.Contains(essenceName))
                    continue;
                if (!SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.EssenceSearchFilter, essenceName))
                    continue;

                hasEntries = true;
                bool arrowClicked = SettingsUiRenderHelpers.DrawTransferListRow(id, essenceName, essenceName, moveToCorrupt, textColor);

                if (arrowClicked)
                {
                    MoveEssenceName(essenceName, moveToCorrupt);
                    break;
                }
            }

            SettingsUiRenderHelpers.DrawNoEntriesPlaceholder(hasEntries);

            ImGui.PopID();
        }

        private void DrawStrongboxFilterList(string id, HashSet<string> sourceSet, bool moveToClick, Vector4 textColor)
        {
            ImGui.PushID(id);

            bool hasEntries = false;
            foreach (ClickItSettings.StrongboxFilterEntry entry in ClickItSettings.StrongboxTableEntries)
            {
                if (!sourceSet.Contains(entry.Id))
                    continue;
                if (!MatchesStrongboxSearch(entry, _settings.UiState.StrongboxSearchFilter))
                    continue;

                hasEntries = true;
                bool arrowClicked = SettingsUiRenderHelpers.DrawTransferListRow(id, entry.Id, entry.DisplayName, moveToClick, textColor);

                if (arrowClicked)
                {
                    MoveStrongboxFilter(entry.Id, moveToClick);
                    break;
                }
            }

            SettingsUiRenderHelpers.DrawNoEntriesPlaceholder(hasEntries);

            ImGui.PopID();
        }

        private void MoveStrongboxFilter(string strongboxId, bool moveToClick)
        {
            HashSet<string> source = moveToClick ? _settings.StrongboxDontClickIds : _settings.StrongboxClickIds;
            HashSet<string> target = moveToClick ? _settings.StrongboxClickIds : _settings.StrongboxDontClickIds;

            source.Remove(strongboxId);
            target.Add(strongboxId);
        }

        private static bool MatchesStrongboxSearch(ClickItSettings.StrongboxFilterEntry entry, string filter)
        {
            return SettingsUiRenderHelpers.MatchesSearch(
                filter,
                entry.MetadataIdentifiers.Prepend(entry.DisplayName));
        }

        private void MoveEssenceName(string essenceName, bool moveToCorrupt)
        {
            HashSet<string> source = moveToCorrupt ? _settings.EssenceDontCorruptNames : _settings.EssenceCorruptNames;
            HashSet<string> target = moveToCorrupt ? _settings.EssenceCorruptNames : _settings.EssenceDontCorruptNames;

            source.Remove(essenceName);
            target.Add(essenceName);
        }

        private void DrawItemTypeSubtypePanel(string listId, ItemCategoryDefinition category, bool isSourceWhitelist)
        {
            if (!ClickItSettings.TryGetSubtypeDefinitions(category.Id, out ClickItSettings.ItemSubtypeDefinition[] subtypeDefinitions))
            {
                return;
            }

            HashSet<string> selectedSubtypeIds = GetOrCreateSubtypeSelection(isSourceWhitelist, category.Id);

            ImGui.Indent();
            SettingsUiRenderHelpers.DrawWrappedText(
                "Subtype filter: select subtypes to narrow this category. Example: choosing only Helmets means Body Armours/Gloves/Boots/Shields will be treated as being in the opposite list.",
                new Vector4(0.75f, 0.75f, 0.75f, 1f));

            bool hasActiveSelection = selectedSubtypeIds.Count > 0;
            foreach (ClickItSettings.ItemSubtypeDefinition subtype in subtypeDefinitions)
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
            ClickItSettings.ItemSubtypeDefinition subtype)
        {
            bool isSelected = selectedSubtypeIds.Contains(subtype.Id);
            bool subtypeIsWhitelistSide = hasActiveSelection
                ? (isSourceWhitelist ? isSelected : !isSelected)
                : isSourceWhitelist;
            Vector4 subtypeTextColor = subtypeIsWhitelistSide ? SettingsUiPalette.WhitelistTextColor : SettingsUiPalette.BlacklistTextColor;

            if (SettingsUiRenderHelpers.DrawCheckbox($"{subtype.DisplayName}##Subtype_{listId}_{categoryId}_{subtype.Id}", ref isSelected, subtypeTextColor))
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

            if (ImGui.IsItemHovered())
            {
                string metadataPreview = string.Join("\n", subtype.MetadataIdentifiers);
                ImGui.SetTooltip(metadataPreview);
            }
        }

        private HashSet<string> GetOrCreateSubtypeSelection(bool isWhitelist, string categoryId)
        {
            Dictionary<string, HashSet<string>> source = isWhitelist ? _settings.ItemTypeWhitelistSubtypeIds : _settings.ItemTypeBlacklistSubtypeIds;
            if (!source.TryGetValue(categoryId, out HashSet<string>? subtypeSelection))
            {
                subtypeSelection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                source[categoryId] = subtypeSelection;
            }

            return subtypeSelection;
        }

        private bool IsExpandedRow(string listId, string categoryId)
        {
            return string.Equals(_settings.UiState.ExpandedItemTypeRowKey, SettingsUiRenderHelpers.BuildExpandedRowKey(listId, categoryId), StringComparison.Ordinal);
        }

        private void ToggleExpandedRow(string listId, string categoryId)
        {
            _settings.UiState.ExpandedItemTypeRowKey = SettingsUiRenderHelpers.ToggleExpandedRowKey(_settings.UiState.ExpandedItemTypeRowKey, listId, categoryId);
        }

        private void MoveItemTypeCategory(string categoryId, bool moveToWhitelist)
        {
            HashSet<string> sourceSet = moveToWhitelist ? _settings.ItemTypeBlacklistIds : _settings.ItemTypeWhitelistIds;
            HashSet<string> targetSet = moveToWhitelist ? _settings.ItemTypeWhitelistIds : _settings.ItemTypeBlacklistIds;
            Dictionary<string, HashSet<string>> sourceSubtypeDict = moveToWhitelist ? _settings.ItemTypeBlacklistSubtypeIds : _settings.ItemTypeWhitelistSubtypeIds;
            Dictionary<string, HashSet<string>> targetSubtypeDict = moveToWhitelist ? _settings.ItemTypeWhitelistSubtypeIds : _settings.ItemTypeBlacklistSubtypeIds;

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
            return SettingsUiRenderHelpers.MatchesSearch(
                filter,
                category.MetadataIdentifiers.Prepend(category.DisplayName).Append(category.Id));
        }

    }
}