namespace ClickIt.UI.Settings.Panels
{
    internal sealed class ItemTypeFiltersPanelRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        public void DrawPanel(bool embedded = false)
        {
            SettingsDefaultsService.EnsureItemTypeFiltersInitialized(_settings);

            if (!embedded)
            {
                SettingsUiRenderHelpers.DrawInstructionText("Table rows with [v] next to them can be clicked to open subtype filter options.");
                ImGui.Spacing();
            }

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