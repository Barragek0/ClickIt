using ClickIt.Definitions;
using ImGuiNET;
using System.Numerics;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private void DrawItemTypeFiltersPanel()
        {
            EnsureItemTypeFiltersInitialized();

            ImGui.SetNextItemOpen(false, ImGuiCond.Once);
            bool sectionOpen = ImGui.TreeNode("Item Filters");
            DrawInlineTooltip("Configure item whitelist/blacklist behavior. Use arrows to move entries between lists and click a row to open subtype options.");
            if (!sectionOpen)
                return;

            try
            {
                ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Click a table row to open subtype filter options.");
                ImGui.Spacing();

                DrawSearchBar("##ItemTypeSearch", "Clear##ItemTypeClear", ref itemTypeSearchFilter);
                if (DrawResetDefaultsButton("Reset Defaults##ItemTypeDefaults"))
                {
                    ItemTypeWhitelistIds = new HashSet<string>(ItemCategoryCatalog.DefaultWhitelistIds, StringComparer.OrdinalIgnoreCase);
                    ItemTypeBlacklistIds = new HashSet<string>(ItemCategoryCatalog.DefaultBlacklistIds, StringComparer.OrdinalIgnoreCase);
                    ItemTypeWhitelistSubtypeIds.Clear();
                    ItemTypeBlacklistSubtypeIds.Clear();
                    _expandedItemTypeRowKey = string.Empty;
                }

                ImGui.Spacing();

                bool tableOpen = ImGui.BeginTable("ItemTypeFilterLists", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable);
                if (!tableOpen)
                    return;

                try
                {
                    SetupTwoColumnFilterTableHeader(
                        leftHeader: "Whitelist",
                        rightHeader: "Blacklist",
                        leftBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                        rightBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f));

                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    DrawItemTypeList("Whitelist##ItemType", ItemTypeWhitelistIds, moveToWhitelist: false, textColor: WhitelistTextColor);

                    ImGui.TableSetColumnIndex(1);
                    DrawItemTypeList("Blacklist##ItemType", ItemTypeBlacklistIds, moveToWhitelist: true, textColor: BlacklistTextColor);
                }
                finally
                {
                    ImGui.EndTable();
                }
            }
            finally
            {
                ImGui.TreePop();
            }
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
                    _expandedItemTypeRowKey = string.Empty;
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
            return sourceSet.Contains(category.Id) && MatchesItemTypeSearch(category, itemTypeSearchFilter);
        }

        private readonly struct ItemTypeRowRenderState(bool rowClicked, bool arrowClicked, bool rowHovered)
        {
            public bool RowClicked { get; } = rowClicked;
            public bool ArrowClicked { get; } = arrowClicked;
            public bool RowHovered { get; } = rowHovered;
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

        private static float CalculateItemTypeRowWidth()
        {
            float availableWidth = Math.Max(80f, ImGui.GetContentRegionAvail().X);
            const float arrowWidth = 28f;
            return Math.Max(40f, availableWidth - arrowWidth - 6f);
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
                return;

            string examples = string.Join(", ", category.ExampleItems);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
            ImGui.Indent();
            ImGui.TextWrapped($"Examples: {examples}");
            ImGui.Unindent();
            ImGui.PopStyleColor();
        }

        private void DrawItemTypeSubtypePanel(string listId, ItemCategoryDefinition category, bool isSourceWhitelist)
        {
            if (!TryGetSubtypeDefinitions(category.Id, out ItemSubtypeDefinition[] subtypeDefinitions))
                return;

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
            return string.Equals(_expandedItemTypeRowKey, BuildExpandedRowKey(listId, categoryId), StringComparison.Ordinal);
        }

        private void ToggleExpandedRow(string listId, string categoryId)
        {
            string rowKey = BuildExpandedRowKey(listId, categoryId);
            if (string.Equals(_expandedItemTypeRowKey, rowKey, StringComparison.Ordinal))
            {
                _expandedItemTypeRowKey = string.Empty;
            }
            else
            {
                _expandedItemTypeRowKey = rowKey;
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
            EnsureItemTypeFiltersInitialized();
            return BuildItemTypeMetadataIdentifiers(
                primaryIds: ItemTypeWhitelistIds,
                primaryIsWhitelist: true,
                oppositeIds: ItemTypeBlacklistIds,
                oppositeIsWhitelist: false);
        }

        public IReadOnlyList<string> GetItemTypeBlacklistMetadataIdentifiers()
        {
            EnsureItemTypeFiltersInitialized();
            return BuildItemTypeMetadataIdentifiers(
                primaryIds: ItemTypeBlacklistIds,
                primaryIsWhitelist: false,
                oppositeIds: ItemTypeWhitelistIds,
                oppositeIsWhitelist: true);
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
                return Array.Empty<string>();
            }

            if (!TryGetSubtypeDefinitions(categoryId, out ItemSubtypeDefinition[] subtypeDefinitions))
            {
                if (includeOppositeSubtypeSelections)
                    return Array.Empty<string>();

                return category.MetadataIdentifiers;
            }

            Dictionary<string, HashSet<string>> subtypeConfig = isWhitelist ? ItemTypeWhitelistSubtypeIds : ItemTypeBlacklistSubtypeIds;
            if (!subtypeConfig.TryGetValue(categoryId, out HashSet<string>? selectedSubtypeIds) || selectedSubtypeIds.Count == 0)
            {
                if (includeOppositeSubtypeSelections)
                    return Array.Empty<string>();

                return category.MetadataIdentifiers;
            }

            return subtypeDefinitions
                .Where(x => includeOppositeSubtypeSelections
                    ? !selectedSubtypeIds.Contains(x.Id)
                    : selectedSubtypeIds.Contains(x.Id))
                .SelectMany(x => x.MetadataIdentifiers)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}