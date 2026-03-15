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
        private void DrawPanelSafe(string panelName, Action drawAction)
        {
            try
            {
                drawAction();
            }
            catch (Exception ex)
            {
                _lastSettingsUiError = $"{panelName}: {ex.GetType().Name}: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ClickItSettings UI Error] {_lastSettingsUiError}{Environment.NewLine}{ex}");

                ImGui.Separator();
                ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "Settings UI error caught");
                ImGui.TextWrapped(_lastSettingsUiError);

                if (ImGui.Button($"Throw Last UI Error##{panelName}"))
                {
                    throw new InvalidOperationException(_lastSettingsUiError, ex);
                }
            }
        }

        public bool IsLazyModeDisableHotkeyToggleModeEnabled()
        {
            return LazyModeDisableKeyToggleMode?.Value == true;
        }

        public bool IsInitialUltimatumClickEnabled()
        {
            return ClickInitialUltimatum?.Value == true;
        }

        public bool IsOtherUltimatumClickEnabled()
        {
            return ClickUltimatumChoices?.Value == true;
        }

        public bool IsAnyUltimatumClickEnabled()
        {
            return IsInitialUltimatumClickEnabled() || IsOtherUltimatumClickEnabled();
        }

        public bool IsAnyDetailedDebugSectionEnabled()
        {
            return DebugShowStatus
                || DebugShowGameState
                || DebugShowPerformance
                || DebugShowClickFrequencyTarget
                || DebugShowAltarDetection
                || DebugShowAltarService
                || DebugShowLabels
                || DebugShowHoveredItemMetadata
                || DebugShowPathfinding
                || DebugShowClicking
                || DebugShowRecentErrors;
        }

        private void DrawAltarsPanel()
        {
            DrawExarchSection();
            DrawEaterSection();
            DrawAltarWeightingSection();
            DrawAlertSoundSection();
        }
        private void DrawDebugTestingPanel()
        {
            DrawToggleNodeControl(
                "Debug Mode",
                DebugMode,
                "Enables debug mode to help with troubleshooting issues.");

            DrawToggleNodeControl(
                "Additional Debug Information",
                RenderDebug,
                "Provides more debug text related to rendering the overlay.");

            DrawToggleNodeControl(
                "Auto-Copy Additional Debug Information",
                AutoCopyAdditionalDebugInfoToClipboard,
                "Automatically copies the current Additional Debug Information text to clipboard.");

            if (AutoCopyAdditionalDebugInfoToClipboard.Value)
            {
                DrawRangeNodeControl(
                    "Auto-Copy Interval (ms)",
                    AutoCopyAdditionalDebugInfoIntervalMs,
                    250,
                    10000,
                    "Minimum delay between clipboard updates when auto-copy is enabled.");
            }

            if (RenderDebug.Value)
            {
                ImGui.Indent();
                DrawToggleNodeControl("Status", DebugShowStatus, "Show/hide the status debug section");
                DrawToggleNodeControl("Game State", DebugShowGameState, "Show/hide the Game State debug section");
                DrawToggleNodeControl("Performance", DebugShowPerformance, "Show/hide the performance debug section");
                DrawToggleNodeControl("Click Frequency Target", DebugShowClickFrequencyTarget, "Show/hide the Click Frequency Target debug section");
                DrawToggleNodeControl("Altar Detection", DebugShowAltarDetection, "Show/hide the Altar Detection debug section");
                DrawToggleNodeControl("Altar Service", DebugShowAltarService, "Show/hide the Altar Service debug section");
                DrawToggleNodeControl("Labels", DebugShowLabels, "Show/hide the labels debug section");
                DrawToggleNodeControl("Hovered Item Metadata", DebugShowHoveredItemMetadata, "Show/hide the hovered item metadata debug section");
                DrawToggleNodeControl("Pathfinding", DebugShowPathfinding, "Show/hide offscreen pathfinding debug section");
                DrawToggleNodeControl("Clicking", DebugShowClicking, "Show/hide clicking debug section");
                DrawToggleNodeControl("Recent Errors", DebugShowRecentErrors, "Show/hide the Recent Errors debug section");
                DrawToggleNodeControl("Debug Frames", DebugShowFrames, "Show/hide the debug screen area frames");
                ImGui.Unindent();
            }

            DrawToggleNodeControl(
                "Log messages",
                LogMessages,
                "This will flood your log and screen with debug text.");

            if (ImGui.Button("Report Bug"))
            {
                TriggerButtonNode(ReportBugButton);
            }
            DrawInlineTooltip("If you run into a bug that hasn't already been reported, please report it here.");
        }

        private void DrawItemTypeFiltersPanel()
        {
            EnsureItemTypeFiltersInitialized();


            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Table rows with [v] next to them can be clicked to open subtype filter options.");
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
                    leftHeader: "Click",
                    rightHeader: "Don't Click",
                    leftBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                    rightBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f));

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                DrawItemTypeList("Click##ItemType", ItemTypeWhitelistIds, moveToWhitelist: false, textColor: WhitelistTextColor);

                ImGui.TableSetColumnIndex(1);
                DrawItemTypeList("Don't Click##ItemType", ItemTypeBlacklistIds, moveToWhitelist: true, textColor: BlacklistTextColor);
            }
            finally
            {
                ImGui.EndTable();
            }
        }

        private void DrawItemTypeList(string id, HashSet<string> sourceSet, bool moveToWhitelist, Vector4 textColor)
        {
            // Avoid BeginChild here for compatibility with older ImGuiNET builds bundled by ExileAPI.
            ImGui.PushID(id);

            bool hasEntries = false;
            foreach (ItemCategoryDefinition category in ItemCategoryCatalog.All)
            {
                if (!ShouldRenderItemTypeCategory(sourceSet, category))
                    continue;

                hasEntries = true;
                var rowState = DrawItemTypeRow(id, category, moveToWhitelist, textColor);

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
            EnsureEssenceCorruptionFiltersInitialized();


            DrawSearchBar("##EssenceSearch", "Clear##EssenceSearchClear", ref essenceSearchFilter);
            if (DrawResetDefaultsButton("Reset Defaults##EssenceResetDefaults"))
            {
                EssenceCorruptNames = BuildDefaultCorruptEssenceNames();
                EssenceDontCorruptNames = BuildDefaultDontCorruptEssenceNames();
            }

            ImGui.Spacing();

            if (!ImGui.BeginTable("EssenceCorruptionLists", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
                return;

            try
            {
                SetupTwoColumnFilterTableHeader(
                    leftHeader: "Corrupt",
                    rightHeader: "Don't Corrupt",
                    leftBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                    rightBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f));

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                DrawEssenceCorruptionList("Corrupt##Essence", EssenceCorruptNames, moveToCorrupt: false, textColor: new Vector4(0.8f, 0.4f, 0.4f, 1.0f));

                ImGui.TableSetColumnIndex(1);
                DrawEssenceCorruptionList("DontCorrupt##Essence", EssenceDontCorruptNames, moveToCorrupt: true, textColor: new Vector4(0.4f, 0.8f, 0.4f, 1.0f));
            }
            finally
            {
                ImGui.EndTable();
            }
        }

        private void DrawEssenceCorruptionList(string id, HashSet<string> sourceSet, bool moveToCorrupt, Vector4 textColor)
        {
            ImGui.PushID(id);

            bool hasEntries = false;
            foreach (string essenceName in EssenceAllTableNames)
            {
                if (!sourceSet.Contains(essenceName))
                    continue;
                if (!MatchesEssenceSearch(essenceName, essenceSearchFilter))
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
            EnsureStrongboxFiltersInitialized();


            DrawSearchBar("##StrongboxSearch", "Clear##StrongboxSearchClear", ref strongboxSearchFilter);
            if (DrawResetDefaultsButton("Reset Defaults##StrongboxResetDefaults"))
            {
                StrongboxClickIds = BuildDefaultClickStrongboxIds();
                StrongboxDontClickIds = BuildDefaultDontClickStrongboxIds();
            }

            ImGui.Spacing();

            if (!ImGui.BeginTable("StrongboxFilterLists", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
                return;

            try
            {
                SetupTwoColumnFilterTableHeader(
                    leftHeader: "Click",
                    rightHeader: "Don't Click",
                    leftBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                    rightBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f));

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                DrawStrongboxFilterList("Click##Strongbox", StrongboxClickIds, moveToClick: false, textColor: new Vector4(0.4f, 0.8f, 0.4f, 1.0f));

                ImGui.TableSetColumnIndex(1);
                DrawStrongboxFilterList("DontClick##Strongbox", StrongboxDontClickIds, moveToClick: true, textColor: new Vector4(0.8f, 0.4f, 0.4f, 1.0f));
            }
            finally
            {
                ImGui.EndTable();
            }
        }

        private void DrawStrongboxFilterList(string id, HashSet<string> sourceSet, bool moveToClick, Vector4 textColor)
        {
            ImGui.PushID(id);

            bool hasEntries = false;
            foreach (StrongboxFilterEntry entry in StrongboxTableEntries)
            {
                if (!sourceSet.Contains(entry.Id))
                    continue;
                if (!MatchesStrongboxSearch(entry, strongboxSearchFilter))
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

        private sealed record MechanicToggleGroupEntry(string Id, string DisplayName);
        private sealed record MechanicToggleTableEntry(string Id, string DisplayName, ToggleNode Node, string? GroupId = null);

        private static readonly MechanicToggleGroupEntry[] MechanicToggleGroups =
        [
            new("settlers", "Settlers"),
            new("delve", "Delve"),
            new("ultimatum", "Ultimatum"),
            new("altars", "Altars")
        ];

        private void DrawMechanicsTablePanel()
        {
            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Table rows with [v] next to them can be clicked to open subtype filter options.");

            ImGui.Spacing();

            DrawSearchBar("##MechanicsSearch", "Clear##MechanicsSearchClear", ref mechanicsSearchFilter);
            if (DrawResetDefaultsButton("Reset Defaults##MechanicsResetDefaults"))
            {
                ResetMechanicsTableDefaults();
                _expandedMechanicsTableRowId = string.Empty;
            }

            ImGui.Spacing();

            if (!ImGui.BeginTable("MechanicsFilterLists", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
                return;

            try
            {
                SetupTwoColumnFilterTableHeader(
                    leftHeader: "Click",
                    rightHeader: "Don't Click",
                    leftBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                    rightBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f));

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                DrawMechanicsTableList("Click##Mechanics", moveToClick: false, WhitelistTextColor);

                ImGui.TableSetColumnIndex(1);
                DrawMechanicsTableList("DontClick##Mechanics", moveToClick: true, BlacklistTextColor);
            }
            finally
            {
                ImGui.EndTable();
            }
        }

        private void DrawMechanicsTableList(string listId, bool moveToClick, Vector4 textColor)
        {
            ImGui.PushID(listId);

            MechanicToggleTableEntry[] entries = BuildMechanicTableEntries();
            bool hasEntries = false;

            foreach (MechanicToggleTableEntry entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.GroupId))
                    continue;
                if (!ShouldRenderMechanicEntry(entry, moveToClick, mechanicsSearchFilter))
                    continue;

                hasEntries = true;
                bool arrowClicked = DrawTransferListRow(listId, entry.Id, entry.DisplayName, moveToClick, textColor);
                if (arrowClicked)
                {
                    entry.Node.Value = moveToClick;
                    _expandedMechanicsTableRowId = string.Empty;
                    break;
                }
            }

            foreach (MechanicToggleGroupEntry group in MechanicToggleGroups)
            {
                if (!ShouldRenderMechanicGroup(group, entries, moveToClick, mechanicsSearchFilter))
                    continue;

                hasEntries = true;
                MechanicGroupRowRenderState rowState = DrawMechanicGroupRow(listId, group, moveToClick, textColor);
                if (rowState.ArrowClicked)
                {
                    SetMechanicGroupState(group.Id, entries, moveToClick);
                    _expandedMechanicsTableRowId = string.Empty;
                    break;
                }

                if (rowState.RowClicked)
                {
                    ToggleExpandedMechanicTableRow(listId, group.Id);
                }

                if (IsExpandedMechanicTableRow(listId, group.Id))
                {
                    DrawMechanicGroupSubmenu(listId, group, entries);
                }
            }

            DrawNoEntriesPlaceholder(hasEntries);

            ImGui.PopID();
        }

        private readonly struct MechanicGroupRowRenderState(bool rowClicked, bool arrowClicked)
        {
            public bool RowClicked { get; } = rowClicked;
            public bool ArrowClicked { get; } = arrowClicked;
        }

        private MechanicGroupRowRenderState DrawMechanicGroupRow(string listId, MechanicToggleGroupEntry group, bool moveToClick, Vector4 textColor)
        {
            string label = $"{group.DisplayName} [v]##{listId}_{group.Id}";
            float rowWidth = CalculateItemTypeRowWidth();
            const float arrowWidth = 28f;

            if (moveToClick)
            {
                bool leftArrowClicked = ImGui.Button($"<-##MoveGroup_{listId}_{group.Id}", new Vector2(arrowWidth, 0));
                ImGui.SameLine();
                bool rowClicked = DrawMechanicGroupSelectable(listId, group.Id, label, rowWidth, textColor);
                return new MechanicGroupRowRenderState(rowClicked, leftArrowClicked);
            }

            bool clicked = DrawMechanicGroupSelectable(listId, group.Id, label, rowWidth, textColor);
            ImGui.SameLine();
            bool rightArrowClicked = ImGui.Button($"->##MoveGroup_{listId}_{group.Id}", new Vector2(arrowWidth, 0));
            return new MechanicGroupRowRenderState(clicked, rightArrowClicked);
        }

        private bool DrawMechanicGroupSelectable(string listId, string groupId, string label, float rowWidth, Vector4 textColor)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            bool clicked = ImGui.Selectable(label, IsExpandedMechanicTableRow(listId, groupId), ImGuiSelectableFlags.AllowDoubleClick, new Vector2(rowWidth, 0));
            ImGui.PopStyleColor();
            return clicked;
        }

        private void DrawMechanicGroupSubmenu(string listId, MechanicToggleGroupEntry group, IReadOnlyList<MechanicToggleTableEntry> entries)
        {
            ImGui.Indent();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.75f, 0.75f, 0.75f, 1f));
            ImGui.TextWrapped($"{group.DisplayName} submenu: toggle individual mechanics in this group.");
            ImGui.PopStyleColor();

            foreach (MechanicToggleTableEntry entry in entries)
            {
                if (!string.Equals(entry.GroupId, group.Id, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!MatchesMechanicsSearch(entry.DisplayName, mechanicsSearchFilter) && !MatchesMechanicsSearch(group.DisplayName, mechanicsSearchFilter))
                    continue;

                bool enabled = entry.Node.Value;
                Vector4 rowColor = enabled ? WhitelistTextColor : BlacklistTextColor;
                ImGui.PushStyleColor(ImGuiCol.Text, rowColor);
                if (ImGui.Checkbox($"{entry.DisplayName}##MechanicSubmenu_{listId}_{group.Id}_{entry.Id}", ref enabled))
                {
                    entry.Node.Value = enabled;
                }
                ImGui.PopStyleColor();
            }

            ImGui.Unindent();
        }

        private static bool ShouldRenderMechanicEntry(MechanicToggleTableEntry entry, bool moveToClick, string filter)
        {
            bool inSourceSet = moveToClick ? !entry.Node.Value : entry.Node.Value;
            return inSourceSet && MatchesMechanicsSearch(entry.DisplayName, filter);
        }

        private static bool ShouldRenderMechanicGroup(MechanicToggleGroupEntry group, IReadOnlyList<MechanicToggleTableEntry> entries, bool moveToClick, string filter)
        {
            bool hasMatchingChild = false;
            bool matchesFilter = MatchesMechanicsSearch(group.DisplayName, filter);

            foreach (MechanicToggleTableEntry entry in entries)
            {
                if (!string.Equals(entry.GroupId, group.Id, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool inSourceSet = moveToClick ? !entry.Node.Value : entry.Node.Value;
                bool childMatchesFilter = MatchesMechanicsSearch(entry.DisplayName, filter);
                if (inSourceSet && (matchesFilter || childMatchesFilter))
                {
                    hasMatchingChild = true;
                    break;
                }
            }

            return hasMatchingChild;
        }

        private static bool MatchesMechanicsSearch(string name, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            return name.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildExpandedMechanicTableRowKey(string listId, string rowId)
        {
            return $"{listId}:{rowId}";
        }

        private bool IsExpandedMechanicTableRow(string listId, string rowId)
        {
            return string.Equals(_expandedMechanicsTableRowId, BuildExpandedMechanicTableRowKey(listId, rowId), StringComparison.Ordinal);
        }

        private void ToggleExpandedMechanicTableRow(string listId, string rowId)
        {
            string rowKey = BuildExpandedMechanicTableRowKey(listId, rowId);
            if (string.Equals(_expandedMechanicsTableRowId, rowKey, StringComparison.Ordinal))
            {
                _expandedMechanicsTableRowId = string.Empty;
            }
            else
            {
                _expandedMechanicsTableRowId = rowKey;
            }
        }

        private static void SetMechanicGroupState(string groupId, IReadOnlyList<MechanicToggleTableEntry> entries, bool enabled)
        {
            foreach (MechanicToggleTableEntry entry in entries)
            {
                if (!string.Equals(entry.GroupId, groupId, StringComparison.OrdinalIgnoreCase))
                    continue;

                entry.Node.Value = enabled;
            }
        }

        private void ResetMechanicsTableDefaults()
        {
            foreach (MechanicToggleTableEntry entry in BuildMechanicTableEntries())
            {
                entry.Node.Value = GetDefaultMechanicToggleState(entry.Id);
            }
        }

        private static bool GetDefaultMechanicToggleState(string id)
        {
            return id switch
            {
                "shrines" => true,
                "lost-shipment" => true,
                "basic-chests" => false,
                "league-chests" => true,
                "area-transitions" => false,
                "labyrinth-trials" => false,
                "crafting-recipes" => true,
                "doors" => false,
                "levers" => false,
                "alva-temple-doors" => true,
                "betrayal" => false,
                "blight" => true,
                "breach-nodes" => false,
                "legion-pillars" => true,
                "harvest" => true,
                "sanctum" => true,
                "settlers-crimson-iron" => true,
                "settlers-copper" => true,
                "settlers-petrified-wood" => true,
                "settlers-bismuth" => true,
                "settlers-verisium" => true,
                "items" => true,
                "essences" => true,
                "ritual-initiate" => true,
                "ritual-completed" => true,
                "delve-azurite-veins" => true,
                "delve-sulphite-veins" => true,
                "delve-encounter-initiators" => true,
                "ultimatum-initial-overlay" => false,
                "ultimatum-window" => false,
                "altars-searing-exarch" => false,
                "altars-eater-of-worlds" => false,
                _ => false
            };
        }

        private MechanicToggleTableEntry[] BuildMechanicTableEntries()
        {
            return
            [
                new("basic-chests", "Basic Chests", ClickBasicChests),
                new("league-chests", "League Mechanic Chests", ClickLeagueChests),
                new("shrines", "Shrines", ClickShrines),
                new("area-transitions", "Area Transitions", ClickAreaTransitions),
                new("labyrinth-trials", "Labyrinth Trials", ClickLabyrinthTrials),
                new("crafting-recipes", "Crafting Recipes", ClickCraftingRecipes),
                new("doors", "Doors", ClickDoors),
                new("levers", "Levers", ClickLevers),
                new("alva-temple-doors", "Alva Temple Doors", ClickAlvaTempleDoors),
                new("betrayal", "Betrayal", ClickBetrayal),
                new("blight", "Blight", ClickBlight),
                new("breach-nodes", "Breach Nodes", ClickBreachNodes),
                new("legion-pillars", "Legion Pillars", ClickLegionPillars),
                new("harvest", "Nearest Harvest Plot", NearestHarvest),
                new("sanctum", "Sanctum", ClickSanctum),
                new("items", "Items", ClickItems),
                new("essences", "Essences", ClickEssences),
                new("ritual-initiate", "Ritual Altars", ClickRitualInitiate),
                new("ritual-completed", "Completed Ritual Altars", ClickRitualCompleted),
                new("lost-shipment", "Lost Shipment", ClickLostShipmentCrates, "settlers"),
                new("settlers-crimson-iron", "Crimson Iron", ClickSettlersOre, "settlers"),
                new("settlers-copper", "Copper", ClickSettlersOre, "settlers"),
                new("settlers-petrified-wood", "Petrified Wood", ClickSettlersOre, "settlers"),
                new("settlers-bismuth", "Bismuth", ClickSettlersOre, "settlers"),
                new("settlers-verisium", "Verisium", ClickSettlersOre, "settlers"),
                new("delve-azurite-veins", "Azurite Veins", ClickAzuriteVeins, "delve"),
                new("delve-sulphite-veins", "Sulphite Veins", ClickSulphiteVeins, "delve"),
                new("delve-encounter-initiators", "Encounter Initiators", ClickDelveSpawners, "delve"),
                new("ultimatum-initial-overlay", "Initial Ultimatum Overlay", ClickInitialUltimatum, "ultimatum"),
                new("ultimatum-window", "Ultimatum Window", ClickUltimatumChoices, "ultimatum"),
                new("altars-searing-exarch", "Searing Exarch", ClickExarchAltars, "altars"),
                new("altars-eater-of-worlds", "Eater of Worlds", ClickEaterAltars, "altars")
            ];
        }

        private static bool DrawTransferListRow(string listId, string key, string displayText, bool moveToPrimaryList, Vector4 textColor)
        {
            float rowWidth = CalculateItemTypeRowWidth();
            const float arrowWidth = 28f;

            if (moveToPrimaryList)
            {
                bool leftArrowClicked = ImGui.Button($"<-##Move_{listId}_{key}", new Vector2(arrowWidth, 0));
                ImGui.SameLine();
                DrawTransferListSelectable(listId, key, displayText, rowWidth, textColor);
                return leftArrowClicked;
            }

            DrawTransferListSelectable(listId, key, displayText, rowWidth, textColor);
            ImGui.SameLine();
            bool rightArrowClicked = ImGui.Button($"->##Move_{listId}_{key}", new Vector2(arrowWidth, 0));
            return rightArrowClicked;
        }

        private static void DrawTransferListSelectable(string listId, string key, string displayText, float rowWidth, Vector4 textColor)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.Selectable($"{displayText}##{listId}_{key}", false, ImGuiSelectableFlags.None, new Vector2(rowWidth, 0));
            ImGui.PopStyleColor();
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
            return entry.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || entry.MetadataIdentifiers.Any(x => x.Contains(term, StringComparison.OrdinalIgnoreCase));
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
            EnsureEssenceCorruptionFiltersInitialized();
            return EssenceCorruptNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<string> GetStrongboxClickMetadataIdentifiers()
        {
            EnsureStrongboxFiltersInitialized();
            return BuildStrongboxMetadataIdentifiers(StrongboxClickIds);
        }

        public IReadOnlyList<string> GetStrongboxDontClickMetadataIdentifiers()
        {
            EnsureStrongboxFiltersInitialized();
            return BuildStrongboxMetadataIdentifiers(StrongboxDontClickIds);
        }

        private static string[] BuildStrongboxMetadataIdentifiers(HashSet<string> strongboxIds)
        {
            HashSet<string> metadataIdentifiers = new(StringComparer.OrdinalIgnoreCase);

            foreach (string id in strongboxIds)
            {
                StrongboxFilterEntry? entry = TryGetStrongboxFilterById(id);
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

        public IReadOnlyList<string> GetUltimatumModifierPriority()
        {
            EnsureUltimatumModifiersInitialized();

            if (HasMatchingUltimatumSnapshot())
            {
                return _ultimatumPrioritySnapshot;
            }

            _ultimatumPrioritySnapshot = UltimatumModifierPriority.ToArray();
            return _ultimatumPrioritySnapshot;
        }

        private bool HasMatchingUltimatumSnapshot()
        {
            if (_ultimatumPrioritySnapshot == null)
                return false;

            if (_ultimatumPrioritySnapshot.Length != UltimatumModifierPriority.Count)
                return false;

            for (int i = 0; i < UltimatumModifierPriority.Count; i++)
            {
                if (!string.Equals(_ultimatumPrioritySnapshot[i], UltimatumModifierPriority[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private void DrawUltimatumModifierTablePanel()
        {
            EnsureUltimatumModifiersInitialized();


            DrawSearchBar("##UltimatumSearch", "Clear##UltimatumSearchClear", ref ultimatumSearchFilter);
            if (DrawResetDefaultsButton("Reset Defaults##UltimatumResetDefaults"))
            {
                UltimatumModifierPriority = new List<string>(UltimatumModifiersConstants.AllModifierNames);
            }

            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Priority: top row is highest, bottom row is lowest.");
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));
            ImGui.TextWrapped("Example: if this table has Resistant Monsters above Reduced Recovery above Ruin, and those three are offered, Resistant Monsters is selected.");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            float tableWidth = Math.Min(600f, Math.Max(100f, ImGui.GetContentRegionAvail().X));
            if (!ImGui.BeginTable("UltimatumModifierPriorityTable", 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                return;

            try
            {
                ImGui.TableSetupColumn("Modifiers", ImGuiTableColumnFlags.WidthFixed, tableWidth);

                ImGui.TableNextRow(ImGuiTableRowFlags.None);
                ImGui.TableSetColumnIndex(0);
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.6f, 0.3f)));
                ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), "Modifiers");

                for (int i = 0; i < UltimatumModifierPriority.Count; i++)
                {
                    string modifier = UltimatumModifierPriority[i];
                    if (!MatchesUltimatumSearch(modifier, ultimatumSearchFilter))
                        continue;

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    Vector4 priorityColor = GetUltimatumPriorityRowColor(i, UltimatumModifierPriority.Count);
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(priorityColor));

                    if (DrawUltimatumArrowButton(ImGuiDir.Up, $"UltimatumUp_{i}", enabled: i > 0))
                    {
                        (UltimatumModifierPriority[i], UltimatumModifierPriority[i - 1]) = (UltimatumModifierPriority[i - 1], UltimatumModifierPriority[i]);
                        continue;
                    }

                    ImGui.SameLine();

                    if (DrawUltimatumArrowButton(ImGuiDir.Down, $"UltimatumDown_{i}", enabled: i < UltimatumModifierPriority.Count - 1))
                    {
                        (UltimatumModifierPriority[i], UltimatumModifierPriority[i + 1]) = (UltimatumModifierPriority[i + 1], UltimatumModifierPriority[i]);
                        continue;
                    }

                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.95f, 0.95f, 1f));
                    ImGui.Selectable($"{modifier}##UltimatumModifier_{i}", false, ImGuiSelectableFlags.None, new Vector2(0, 0));
                    ImGui.PopStyleColor();

                    if (ImGui.IsItemHovered())
                    {
                        string description = UltimatumModifiersConstants.GetDescription(modifier);
                        if (!string.IsNullOrWhiteSpace(description))
                        {
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);
                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
                            ImGui.TextWrapped(description);
                            ImGui.PopStyleColor();
                        }
                    }
                }
            }
            finally
            {
                ImGui.EndTable();
            }
        }

        private static bool MatchesUltimatumSearch(string modifier, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            return modifier.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static Vector4 GetUltimatumPriorityRowColor(int index, int totalCount)
        {
            return UltimatumModifiersConstants.GetPriorityGradientColor(index, totalCount, 0.30f);
        }

        private static bool DrawUltimatumArrowButton(ImGuiDir direction, string id, bool enabled)
        {
            if (!enabled)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * 0.5f);
            }

            bool clicked = ImGui.ArrowButton(id, direction);

            if (!enabled)
            {
                ImGui.PopStyleVar();
                return false;
            }

            return clicked;
        }

        public IReadOnlyList<string> GetMechanicPriorityOrder()
        {
            EnsureMechanicPrioritiesInitialized();

            if (HasMatchingMechanicPrioritySnapshot())
            {
                return _mechanicPrioritySnapshot;
            }

            _mechanicPrioritySnapshot = MechanicPriorityOrder.ToArray();
            return _mechanicPrioritySnapshot;
        }

        public IReadOnlyCollection<string> GetMechanicPriorityIgnoreDistanceIds()
        {
            EnsureMechanicPrioritiesInitialized();

            if (HasMatchingMechanicIgnoreDistanceSnapshot())
            {
                return _mechanicIgnoreDistanceSnapshot;
            }

            _mechanicIgnoreDistanceSnapshot = MechanicPriorityIgnoreDistanceIds.OrderBy(static x => x, PriorityComparer).ToArray();
            return _mechanicIgnoreDistanceSnapshot;
        }

        public IReadOnlyDictionary<string, int> GetMechanicPriorityIgnoreDistanceWithinById()
        {
            EnsureMechanicPrioritiesInitialized();

            if (HasMatchingMechanicIgnoreDistanceWithinSnapshot())
            {
                return _mechanicIgnoreDistanceWithinMapSnapshot;
            }

            _mechanicIgnoreDistanceWithinSnapshot = MechanicPriorityIgnoreDistanceWithinById
                .OrderBy(static x => x.Key, PriorityComparer)
                .ToArray();
            _mechanicIgnoreDistanceWithinMapSnapshot = new Dictionary<string, int>(
                _mechanicIgnoreDistanceWithinSnapshot.ToDictionary(static x => x.Key, static x => x.Value, PriorityComparer),
                PriorityComparer);
            return _mechanicIgnoreDistanceWithinMapSnapshot;
        }

        private bool HasMatchingMechanicPrioritySnapshot()
        {
            if (_mechanicPrioritySnapshot == null)
                return false;
            if (_mechanicPrioritySnapshot.Length != MechanicPriorityOrder.Count)
                return false;

            for (int i = 0; i < MechanicPriorityOrder.Count; i++)
            {
                if (!string.Equals(_mechanicPrioritySnapshot[i], MechanicPriorityOrder[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private bool HasMatchingMechanicIgnoreDistanceSnapshot()
        {
            if (_mechanicIgnoreDistanceSnapshot == null)
                return false;
            if (_mechanicIgnoreDistanceSnapshot.Length != MechanicPriorityIgnoreDistanceIds.Count)
                return false;

            var current = MechanicPriorityIgnoreDistanceIds.OrderBy(static x => x, PriorityComparer).ToArray();
            for (int i = 0; i < current.Length; i++)
            {
                if (!string.Equals(current[i], _mechanicIgnoreDistanceSnapshot[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private bool HasMatchingMechanicIgnoreDistanceWithinSnapshot()
        {
            if (_mechanicIgnoreDistanceWithinSnapshot == null)
                return false;
            if (_mechanicIgnoreDistanceWithinSnapshot.Length != MechanicPriorityIgnoreDistanceWithinById.Count)
                return false;

            var current = MechanicPriorityIgnoreDistanceWithinById
                .OrderBy(static x => x.Key, PriorityComparer)
                .ToArray();
            for (int i = 0; i < current.Length; i++)
            {
                if (!string.Equals(current[i].Key, _mechanicIgnoreDistanceWithinSnapshot[i].Key, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (current[i].Value != _mechanicIgnoreDistanceWithinSnapshot[i].Value)
                    return false;
            }

            return true;
        }

        private void DrawMechanicPriorityTablePanel()
        {
            EnsureMechanicPrioritiesInitialized();


            if (DrawResetDefaultsButton("Reset Defaults##MechanicPriorityResetDefaults"))
            {
                MechanicPriorityOrder = MechanicPriorityDefaultOrderIds.ToList();
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(PriorityComparer)
                {
                    "shrines"
                };
                MechanicPriorityIgnoreDistanceWithinById = MechanicPriorityIds
                    .ToDictionary(static x => x, static _ => MechanicIgnoreDistanceWithinDefault, PriorityComparer);
            }

            DrawMechanicPrioritySectionDescription();

            float tableWidth = Math.Min(700f, Math.Max(160f, ImGui.GetContentRegionAvail().X));
            if (!ImGui.BeginTable("MechanicPriorityTable", 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                return;

            try
            {
                ImGui.TableSetupColumn("Mechanics", ImGuiTableColumnFlags.WidthFixed, tableWidth);
                DrawMechanicPriorityTableHeader();
                DrawMechanicPriorityRows();
            }
            finally
            {
                ImGui.EndTable();
            }
        }

        private static void DrawMechanicPriorityTableHeader()
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            ImGui.TableSetColumnIndex(0);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.6f, 0.3f)));
            ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), "Mechanics");
        }

        private static void DrawMechanicPrioritySectionDescription()
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Priority: top row is highest, bottom row is lowest.");
            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Click a table row to open Ignore Distance options.");
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));
            ImGui.TextWrapped("Non-ignored mechanics use distance + (priority index * Priority Distance Penalty). Ignore Distance mechanics still use priority-first comparison.");
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }

        private void DrawMechanicPriorityRows()
        {
            List<VisibleMechanicPriorityRow> visibleRows = GetVisibleMechanicPriorityRows();

            for (int i = 0; i < visibleRows.Count; i++)
            {
                VisibleMechanicPriorityRow row = visibleRows[i];

                if (TryDrawMechanicPriorityMoveRow(visibleRows, i, row.OrderIndex, row.MechanicId, row.Entry))
                    continue;

                DrawMechanicPriorityExpandedOptions(row.MechanicId);
            }
        }

        private readonly struct VisibleMechanicPriorityRow(int orderIndex, string mechanicId, MechanicPriorityEntry entry)
        {
            public int OrderIndex { get; } = orderIndex;
            public string MechanicId { get; } = mechanicId;
            public MechanicPriorityEntry Entry { get; } = entry;
        }

        private List<VisibleMechanicPriorityRow> GetVisibleMechanicPriorityRows()
        {
            List<VisibleMechanicPriorityRow> rows = [];
            for (int orderIndex = 0; orderIndex < MechanicPriorityOrder.Count; orderIndex++)
            {
                string mechanicId = MechanicPriorityOrder[orderIndex];
                if (!TryGetMechanicPriorityEntry(mechanicId, out MechanicPriorityEntry? entry) || entry == null)
                    continue;
                if (!IsMechanicPriorityMechanicEnabled(mechanicId))
                    continue;

                rows.Add(new VisibleMechanicPriorityRow(orderIndex, mechanicId, entry));
            }

            return rows;
        }

        private bool IsMechanicPriorityMechanicEnabled(string mechanicId)
        {
            return mechanicId switch
            {
                "basic-chests" => ClickBasicChests.Value,
                "league-chests" => ClickLeagueChests.Value,
                "shrines" => ClickShrines.Value,
                "lost-shipment" => ClickLostShipmentCrates.Value,
                "items" => ClickItems.Value,
                "essences" => ClickEssences.Value,
                "area-transitions" => ClickAreaTransitions.Value,
                "labyrinth-trials" => ClickLabyrinthTrials.Value,
                "crafting-recipes" => ClickCraftingRecipes.Value,
                "doors" => ClickDoors.Value,
                "levers" => ClickLevers.Value,
                "alva-temple-doors" => ClickAlvaTempleDoors.Value,
                "betrayal" => ClickBetrayal.Value,
                "blight" => ClickBlight.Value,
                "breach-nodes" => ClickBreachNodes.Value,
                "legion-pillars" => ClickLegionPillars.Value,
                "harvest" => NearestHarvest.Value,
                "sanctum" => ClickSanctum.Value,
                "settlers-crimson-iron" => ClickSettlersOre.Value,
                "settlers-copper" => ClickSettlersOre.Value,
                "settlers-petrified-wood" => ClickSettlersOre.Value,
                "settlers-bismuth" => ClickSettlersOre.Value,
                "settlers-verisium" => ClickSettlersOre.Value,
                "ritual-initiate" => ClickRitualInitiate.Value,
                "ritual-completed" => ClickRitualCompleted.Value,
                "delve-sulphite-veins" => ClickSulphiteVeins.Value,
                "delve-azurite-veins" => ClickAzuriteVeins.Value,
                "delve-encounter-initiators" => ClickDelveSpawners.Value,
                "ultimatum-initial-overlay" => ClickInitialUltimatum.Value,
                "ultimatum-window" => ClickUltimatumChoices.Value,
                "altars-searing-exarch" => ClickExarchAltars.Value,
                "altars-eater-of-worlds" => ClickEaterAltars.Value,
                _ => true
            };
        }

        private bool TryDrawMechanicPriorityMoveRow(
            IReadOnlyList<VisibleMechanicPriorityRow> visibleRows,
            int visibleIndex,
            int orderIndex,
            string mechanicId,
            MechanicPriorityEntry entry)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);

            Vector4 priorityColor = GetUltimatumPriorityRowColor(visibleIndex, visibleRows.Count);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(priorityColor));

            if (DrawUltimatumArrowButton(ImGuiDir.Up, $"MechanicPriorityUp_{mechanicId}_{orderIndex}", enabled: visibleIndex > 0))
            {
                int targetOrderIndex = visibleRows[visibleIndex - 1].OrderIndex;
                (MechanicPriorityOrder[orderIndex], MechanicPriorityOrder[targetOrderIndex]) = (MechanicPriorityOrder[targetOrderIndex], MechanicPriorityOrder[orderIndex]);
                return true;
            }

            ImGui.SameLine();
            if (DrawUltimatumArrowButton(ImGuiDir.Down, $"MechanicPriorityDown_{mechanicId}_{orderIndex}", enabled: visibleIndex < visibleRows.Count - 1))
            {
                int targetOrderIndex = visibleRows[visibleIndex + 1].OrderIndex;
                (MechanicPriorityOrder[orderIndex], MechanicPriorityOrder[targetOrderIndex]) = (MechanicPriorityOrder[targetOrderIndex], MechanicPriorityOrder[orderIndex]);
                return true;
            }

            ImGui.SameLine();
            bool isExpanded = string.Equals(_expandedMechanicPriorityRowId, mechanicId, StringComparison.OrdinalIgnoreCase);
            bool rowClicked = ImGui.Selectable($"{entry.DisplayName}##MechanicPriority_{mechanicId}", isExpanded, ImGuiSelectableFlags.AllowDoubleClick, new Vector2(0, 0));
            if (rowClicked)
                _expandedMechanicPriorityRowId = isExpanded ? string.Empty : mechanicId;

            return false;
        }

        private void DrawMechanicPriorityExpandedOptions(string mechanicId)
        {
            if (!string.Equals(_expandedMechanicPriorityRowId, mechanicId, StringComparison.OrdinalIgnoreCase))
                return;

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.12f, 0.85f)));
            ImGui.Indent(34f);

            bool ignoreDistance = MechanicPriorityIgnoreDistanceIds.Contains(mechanicId);
            if (ImGui.Checkbox($"Ignore Distance##IgnoreDistance_{mechanicId}", ref ignoreDistance))
            {
                if (ignoreDistance)
                    MechanicPriorityIgnoreDistanceIds.Add(mechanicId);
                else
                    MechanicPriorityIgnoreDistanceIds.Remove(mechanicId);
            }

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));
            ImGui.TextWrapped("When enabled, this mechanic bypasses distance sorting and is resolved from configured priority order.");
            ImGui.PopStyleColor();

            if (ignoreDistance)
            {
                int currentWithin = MechanicPriorityIgnoreDistanceWithinById.TryGetValue(mechanicId, out int configuredWithin)
                    ? configuredWithin
                    : MechanicIgnoreDistanceWithinDefault;
                ImGui.SetNextItemWidth(400f);
                if (ImGui.SliderInt($"Ignore Distance Within##IgnoreDistanceWithin_{mechanicId}", ref currentWithin, MechanicIgnoreDistanceWithinMin, MechanicIgnoreDistanceWithinMax))
                {
                    MechanicPriorityIgnoreDistanceWithinById[mechanicId] = currentWithin;
                }

                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));
                ImGui.TextWrapped("Ignore Distance applies only while this mechanic is within the configured distance from the player.");
                ImGui.PopStyleColor();
            }

            ImGui.Unindent(34f);
        }

        private static bool TryGetMechanicPriorityEntry(string id, out MechanicPriorityEntry? entry)
        {
            entry = MechanicPriorityEntries.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            return entry != null;
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

            return subtypeDefinitions
                .Where(x => includeOppositeSubtypeSelections
                    ? !selectedSubtypeIds.Contains(x.Id)
                    : selectedSubtypeIds.Contains(x.Id))
                .SelectMany(x => x.MetadataIdentifiers)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void DrawExarchSection()
        {
            DrawToggleNodeControl(
                "Show Searing Exarch Overlay##Exarch",
                HighlightExarchAltars,
                "Highlights the recommended option for you to choose for searing exarch altars, based on a decision tree created from your settings below.");
        }
        private void DrawEaterSection()
        {
            DrawToggleNodeControl(
                "Show Eater of Worlds Overlay##Eater",
                HighlightEaterAltars,
                "Highlights the recommended option for you to choose for eater of worlds altars, based on a decision tree created from your settings below.");
        }
        private void DrawAltarWeightingSection()
        {
            if (!ImGui.TreeNode("Altar Weights"))
                return;

            DrawAltarModWeights();
            DrawAltarWeightOverridesSection();

            ImGui.TreePop();
        }

        private void DrawAltarWeightOverridesSection()
        {
            if (!ImGui.TreeNode("Altar Weight Overrides"))
                return;

            DrawToggleNodeControl(
                "Valuable Upside",
                ValuableUpside,
                "When enabled, automatically chooses the altar option with modifiers that have weights above the threshold, even if the overall weight calculation would suggest otherwise.");

            DrawRangeNodeControl(
                "Valuable Upside Threshold",
                ValuableUpsideThreshold,
                1,
                100,
                "Minimum weight threshold for upside modifiers to trigger the high value override. Modifiers with weights at or above this value will cause the plugin to choose that altar option.");

            DrawToggleNodeControl(
                "Unvaluable Upside",
                UnvaluableUpside,
                "When enabled, automatically chooses the opposite altar option when modifiers have weights at or below the threshold, avoiding potentially undesirable choices.");

            DrawRangeNodeControl(
                "Unvaluable Upside Threshold",
                UnvaluableUpsideThreshold,
                1,
                100,
                "Weight threshold that triggers the low value override. When any modifier has a weight at or below this value, the plugin will choose the opposite altar option.");

            DrawToggleNodeControl(
                "Dangerous Downside",
                DangerousDownside,
                "When enabled, automatically avoids altar options with dangerous downside modifiers that have weights above the threshold.");

            DrawRangeNodeControl(
                "Dangerous Downside Threshold",
                DangerousDownsideThreshold,
                1,
                100,
                "Maximum weight threshold for downside modifiers to trigger the dangerous override. Modifiers with weights at or above this value will cause the plugin to choose the opposite altar option.");

            DrawToggleNodeControl(
                "Minimum Weight",
                MinWeightThresholdEnabled,
                "When enabled, the plugin will enforce a minimum final weight for altar options. If an option's final weight is below this value the plugin will avoid picking it (and will choose the opposite option if available).");

            DrawRangeNodeControl(
                "Minimum Weight Threshold",
                MinWeightThreshold,
                1,
                100,
                "Minimum final weight (1 - 100) an option must have to be considered valid. If both options are below this value, neither will be auto-chosen.");

            ImGui.TreePop();
        }
        private void DrawAlertSoundSection()
        {
            if (!ImGui.TreeNode("Alert Sound"))
                return;

            DrawToggleNodeControl(
                "Auto-download Default Alert Sound",
                AutoDownloadAlertSound,
                "When enabled the plugin will attempt to download a default 'alert.wav' from the project's GitHub repository into your plugin config folder if the file is missing.");

            if (ImGui.Button("Open Config Directory"))
            {
                TriggerButtonNode(OpenConfigDirectory);
            }
            DrawInlineTooltip("Open the plugin config directory where you should put 'alert.wav'");

            if (ImGui.Button("Reload Alert Sound"))
            {
                TriggerButtonNode(ReloadAlertSound);
            }
            DrawInlineTooltip("Reloads the 'alert.wav' sound file from the config directory");

            DrawRangeNodeControl(
                "Alert Volume",
                AlertSoundVolume,
                0,
                100,
                "Volume to play alert sound at (0-100)");

            ImGui.TreePop();
        }
        private static void DrawToggleNodeControl(string label, ToggleNode node, string tooltip)
        {
            bool value = node.Value;
            if (ImGui.Checkbox(label, ref value))
            {
                node.Value = value;
            }
            DrawInlineTooltip(tooltip);
        }

        private static void PushStandardSliderWidth()
        {
            ImGui.PushItemWidth(400f);
        }

        private static void PopStandardSliderWidth()
        {
            ImGui.PopItemWidth();
        }

        private static void DrawRangeNodeControl(string label, RangeNode<int> node, int min, int max, string tooltip, bool useStandardWidth = true)
        {
            int value = node.Value;
            if (useStandardWidth)
            {
                ImGui.SetNextItemWidth(400f);
            }

            if (ImGui.SliderInt(label, ref value, min, max))
            {
                node.Value = value;
            }
            DrawInlineTooltip(tooltip);
        }
        private static void DrawInlineTooltip(string tooltip)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tooltip);
            }
        }
        private static void TriggerButtonNode(ButtonNode buttonNode)
        {
            if (buttonNode == null)
                return;

            try
            {
                buttonNode.OnPressed?.Invoke();
            }
            catch
            {
                // Best effort invocation.
            }
        }
        private void DrawAltarModWeights()
        {
            DrawUpsideModsSection();
            DrawDownsideModsSection();
        }
        private void DrawUpsideModsSection()
        {
            bool isOpen = ImGui.TreeNode("Altar Weight Upsides");
            DrawInlineTooltip("Set weights for upside modifiers. Higher values are more desirable and can influence recommended altar choices.");
            if (!isOpen) return;
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextWrapped("Weight Scale (Higher = More Valuable):");
            DrawWeightScale(bestAtHigh: true);
            ImGui.Spacing();
            ImGui.Spacing();
            DrawSearchBar("##UpsideSearch", "Clear##UpsideClear", ref upsideSearchFilter);
            ImGui.Spacing();
            DrawUpsideModsTable();
            ImGui.TreePop();
        }
        private void DrawDownsideModsSection()
        {
            bool isOpen = ImGui.TreeNode("Altar Weight Downsides");
            DrawInlineTooltip("Set weights for downside modifiers. Higher values are more dangerous and can influence recommended altar choices.");
            if (!isOpen) return;
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextWrapped("Weight Scale (Higher = More Dangerous):");
            DrawWeightScale(bestAtHigh: false);
            ImGui.Spacing();
            ImGui.Spacing();
            DrawSearchBar("##DownsideSearch", "Clear##DownsideClear", ref downsideSearchFilter);
            ImGui.Spacing();
            DrawDownsideModsTable();
            ImGui.TreePop();
        }
        private static void DrawSearchBar(string searchId, string clearId, ref string searchFilter)
        {
            ImGui.SetNextItemWidth(300);
            ImGui.InputTextWithHint(searchId, "Search", ref searchFilter, 256);
            ImGui.SameLine();
            if (ImGui.Button(clearId))
            {
                searchFilter = "";
            }
        }

        private static bool DrawResetDefaultsButton(string buttonId)
        {
            ImGui.SameLine();
            return ImGui.Button(buttonId);
        }

        private static void DrawNoEntriesPlaceholder(bool hasEntries)
        {
            if (!hasEntries)
            {
                ImGui.TextDisabled("No entries");
            }
        }

        private static void SetupTwoColumnFilterTableHeader(string leftHeader, string rightHeader, Vector4 leftBackground, Vector4 rightBackground)
        {
            ImGui.TableSetupColumn(leftHeader, ImGuiTableColumnFlags.WidthStretch, 0.5f);
            ImGui.TableSetupColumn(rightHeader, ImGuiTableColumnFlags.WidthStretch, 0.5f);

            ImGui.TableNextRow(ImGuiTableRowFlags.None);

            ImGui.TableSetColumnIndex(0);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(leftBackground));
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), leftHeader);

            ImGui.TableSetColumnIndex(1);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(rightBackground));
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), rightHeader);
        }
        private void DrawUpsideModsTable()
        {
            if (!ImGui.BeginTable("UpsideModsConfig", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                return;
            SetupModTableColumns(isUpside: true);
            string currentSection = "";
            foreach ((string id, string name, string type, int _) in AltarModsConstants.UpsideMods)
            {
                if (!MatchesSearchFilter(name, type, upsideSearchFilter))
                    continue;
                string sectionHeader = GetUpsideSectionHeader(type);
                DrawSectionHeaderIfNeeded(ref currentSection, sectionHeader, type);
                DrawUpsideModRow(id, name, type);
            }
            ImGui.EndTable();
        }
        private void DrawDownsideModsTable()
        {
            if (!ImGui.BeginTable("DownsideModsConfig", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                return;
            SetupModTableColumns(isUpside: false);
            string lastProcessedSection = "";
            foreach ((string id, string name, string type, int defaultWeight) in AltarModsConstants.DownsideMods)
            {
                if (!MatchesSearchFilter(name, type, downsideSearchFilter))
                    continue;
                string sectionHeader = GetDownsideSectionHeader(defaultWeight);
                DrawDownsideSectionHeaderIfNeeded(ref lastProcessedSection, sectionHeader);
                DrawDownsideModRow(id, name, type, sectionHeader);
            }
            ImGui.EndTable();
        }
        private static void SetupModTableColumns(bool isUpside = false)
        {
            ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthFixed, 125);
            var modWidth = isUpside ? 760 : 830;
            ImGui.TableSetupColumn("Mod", ImGuiTableColumnFlags.WidthFixed, modWidth);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 50);
            if (isUpside)
            {
                ImGui.TableSetupColumn("Alert", ImGuiTableColumnFlags.WidthFixed, 55);
            }
            ImGui.TableHeadersRow();
        }
        private static bool MatchesSearchFilter(string name, string type, string filter)
        {
            return string.IsNullOrEmpty(filter) ||
                   name.ToLower().Contains(filter.ToLower()) ||
                   type.ToLower().Contains(filter.ToLower());
        }
        private static string GetUpsideSectionHeader(string type)
        {
            return type switch
            {
                AltarTypeMinion => "Minion Drops",
                AltarTypeBoss => "Boss Drops",
                AltarTypePlayer => "Player Bonuses",
                _ => ""
            };
        }
        private static string GetDownsideSectionHeader(int defaultWeight)
        {
            return defaultWeight switch
            {
                100 => "Build Bricking Modifiers",
                >= 70 => "Very Dangerous Modifiers",
                >= 40 => "Dangerous Modifiers",
                >= 2 => "Ok Modifiers",
                _ => "Free Modifiers"
            };
        }
        private static void DrawSectionHeaderIfNeeded(ref string currentSection, string sectionHeader, string type)
        {
            if (string.IsNullOrEmpty(sectionHeader) || sectionHeader == currentSection)
                return;
            currentSection = sectionHeader;
            DrawUpsideSectionHeader(sectionHeader, type);
        }
        private static void DrawUpsideSectionHeader(string sectionHeader, string type)
        {
            DrawSectionHeaderRow(sectionHeader, GetUpsideSectionHeaderColor(type));
        }
        private static void DrawDownsideSectionHeaderIfNeeded(ref string lastProcessedSection, string sectionHeader)
        {
            if (string.IsNullOrEmpty(sectionHeader) || sectionHeader == lastProcessedSection)
                return;
            lastProcessedSection = sectionHeader;
            DrawDownsideSectionHeader(sectionHeader);
        }
        private static void DrawDownsideSectionHeader(string sectionHeader)
        {
            DrawSectionHeaderRow(sectionHeader, GetDownsideSectionHeaderColor(sectionHeader), new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
        }

        private static void DrawSectionHeaderRow(string sectionHeader, Vector4 headerColor, Vector4? textColor = null)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            ImGui.TableNextColumn();
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(headerColor));
            ImGui.Text("");
            ImGui.TableNextColumn();

            if (textColor.HasValue)
            {
                ImGui.TextColored(textColor.Value, sectionHeader);
                return;
            }

            ImGui.Text($"{sectionHeader}");
        }

        private static Vector4 GetUpsideSectionHeaderColor(string type)
        {
            return type switch
            {
                AltarTypeMinion => new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                AltarTypeBoss => new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                AltarTypePlayer => new Vector4(0.2f, 0.2f, 0.6f, 0.3f),
                _ => new Vector4(0.4f, 0.4f, 0.4f, 0.3f)
            };
        }

        private static Vector4 GetDownsideSectionHeaderColor(string sectionHeader)
        {
            return sectionHeader switch
            {
                "Build Bricking Modifiers" => new Vector4(1.0f, 0.0f, 0.0f, 0.6f),
                "Very Dangerous Modifiers" => new Vector4(0.9f, 0.1f, 0.1f, 0.5f),
                "Dangerous Modifiers" => new Vector4(1.0f, 0.5f, 0.0f, 0.4f),
                "Ok Modifiers" => new Vector4(1.0f, 1.0f, 0.0f, 0.3f),
                "Free Modifiers" => new Vector4(0.0f, 0.7f, 0.0f, 0.3f),
                _ => new Vector4(0.4f, 0.4f, 0.4f, 0.3f)
            };
        }
        private void DrawUpsideModRow(string id, string name, string type)
        {
            ImGui.PushID($"upside_{type}_{id}");
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            DrawModWeightSliderCell(id, type);
            DrawModNameAndTypeCells(name, type, 760, GetUpsideModTextColor(type));

            if (ModAlerts != null)
            {
                _ = ImGui.TableNextColumn();
                var avail = ImGui.GetContentRegionAvail();
                float checkboxSize = 18f; // small visual estimate for a checkbox
                float currentX = ImGui.GetCursorPosX();
                float offset = (avail.X - checkboxSize) * 0.5f;
                if (offset > 0)
                {
                    ImGui.SetCursorPosX(currentX + offset);
                }

                bool currentAlert = GetModAlert(id, type);
                if (ImGui.Checkbox("##alert", ref currentAlert))
                {
                    ModAlerts[BuildCompositeKey(type, id)] = currentAlert;
                }
            }
            ImGui.PopID();
        }
        private void DrawDownsideModRow(string id, string name, string type, string sectionHeader)
        {
            ImGui.PushID($"downside_{type}_{id}");
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            DrawModWeightSliderCell(id, type);
            DrawModNameAndTypeCells(name, type, 830, GetDownsideModTextColor(sectionHeader));
            ImGui.PopID();
        }

        private void DrawModWeightSliderCell(string id, string type)
        {
            _ = ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(125);
            int currentValue = GetModTier(id, type);
            // Unique internal id for the slider prevents conflicts with other widgets in the same row
            if (ImGui.SliderInt("##weight", ref currentValue, 1, 100))
            {
                ModTiers[BuildCompositeKey(type, id)] = currentValue;
            }
        }

        private static void DrawModNameAndTypeCells(string name, string type, float modColumnWidth, Vector4 textColor)
        {
            ImGui.SetNextItemWidth(modColumnWidth);
            _ = ImGui.TableNextColumn();
            ImGui.TextColored(textColor, name);
            _ = ImGui.TableNextColumn();
            ImGui.Text(type);
        }

        private static Vector4 GetUpsideModTextColor(string type)
        {
            return type switch
            {
                AltarTypeMinion => new Vector4(0.4f, 0.8f, 0.4f, 1.0f),
                AltarTypeBoss => new Vector4(0.8f, 0.4f, 0.4f, 1.0f),
                AltarTypePlayer => new Vector4(0.4f, 0.7f, 0.9f, 1.0f),
                _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
            };
        }

        private static Vector4 GetDownsideModTextColor(string sectionHeader)
        {
            return sectionHeader switch
            {
                "Build Bricking Modifiers" => new Vector4(1.0f, 0.2f, 0.2f, 1.0f),
                "Very Dangerous Modifiers" => new Vector4(1.0f, 0.4f, 0.4f, 1.0f),
                "Dangerous Modifiers" => new Vector4(1.0f, 0.7f, 0.3f, 1.0f),
                "Ok Modifiers" => new Vector4(1.0f, 1.0f, 0.5f, 1.0f),
                "Free Modifiers" => new Vector4(0.5f, 1.0f, 0.5f, 1.0f),
                _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)
            };
        }
        internal void InitializeDefaultWeights()
        {
            foreach ((string id, _, string type, int defaultValue) in AltarModsConstants.UpsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                if (ModTiers.ContainsKey(compositeKey))
                    continue;

                ModTiers[compositeKey] = defaultValue;
            }

            foreach ((string id, _, string type, int defaultValue) in AltarModsConstants.DownsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                if (ModTiers.ContainsKey(compositeKey))
                    continue;

                ModTiers[compositeKey] = defaultValue;
            }
            foreach ((string id, _, string type, int _) in AltarModsConstants.UpsideMods)
            {
                var compositeKey = BuildCompositeKey(type, id);
                if (!ModAlerts.ContainsKey(compositeKey))
                {
                    if ((type == AltarTypeMinion && id == "#% chance to drop an additional Divine Orb") ||
                        (type == AltarTypeBoss && id == "Final Boss drops # additional Divine Orbs"))
                    {
                        ModAlerts[compositeKey] = true;
                    }
                    else
                    {
                        ModAlerts[compositeKey] = false;
                    }
                }
            }
        }

        private static string BuildCompositeKey(string type, string id)
        {
            return $"{type}|{id}";
        }
        public void EnsureAllModsHaveWeights()
        {
            InitializeDefaultWeights();
        }
        public int GetModTier(string modId)
        {
            if (string.IsNullOrEmpty(modId)) return 1;
            return ModTiers.TryGetValue(modId, out int value) ? value : 1;
        }

        public int GetModTier(string modId, string type)
        {
            if (string.IsNullOrEmpty(modId)) return 1;
            string compositeKey = BuildCompositeKey(type, modId);
            if (ModTiers.TryGetValue(compositeKey, out int value)) return value;
            return 1;
        }

        public bool GetModAlert(string modId, string type)
        {
            if (string.IsNullOrEmpty(modId)) return false;
            string compositeKey = BuildCompositeKey(type, modId);
            if (ModAlerts.TryGetValue(compositeKey, out bool enabled)) return enabled;
            // fallback to id-only key if present
            if (ModAlerts.TryGetValue(modId, out enabled)) return enabled;
            return false;
        }
        public Dictionary<string, int> ModTiers { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, bool> ModAlerts { get; set; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static void DrawWeightScale(bool bestAtHigh = true, float width = 400f, float height = 20f)
        {
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 p = ImGui.GetCursorScreenPos();
            Vector4 colGood = new(0.2f, 1.0f, 0.2f, 1.0f);
            Vector4 colBad = new(1.0f, 0.2f, 0.2f, 1.0f);
            uint colLeft = ImGui.GetColorU32(bestAtHigh ? colBad : colGood);
            uint colRight = ImGui.GetColorU32(bestAtHigh ? colGood : colBad);
            Vector2 rectMin = p;
            Vector2 rectMax = new(p.X + width, p.Y + height);
            drawList.AddRectFilledMultiColor(rectMin, rectMax, colLeft, colRight, colRight, colLeft);
            uint borderCol = ImGui.GetColorU32(ImGuiCol.Border);
            drawList.AddRect(rectMin, rectMax, borderCol);
            int steps = 4;
            float stepPx = width / steps;
            float tickTop = rectMax.Y;
            float tickBottom = rectMax.Y + 6f;
            float labelY = rectMax.Y + 8f;
            for (int i = 0; i <= steps; i++)
            {
                float x = rectMin.X + (i * stepPx);
                drawList.AddLine(new Vector2(x, tickTop), new Vector2(x, tickBottom), ImGui.GetColorU32(ImGuiCol.Text), 1.0f);
                string label = (i == 0 ? 1 : i * 25).ToString();
                Vector2 textSize = ImGui.CalcTextSize(label);
                Vector2 textPos = new(x - (textSize.X * 0.5f), labelY);
                drawList.AddText(textPos, ImGui.GetColorU32(ImGuiCol.Text), label);
            }
            string leftLegend = bestAtHigh ? "Worst" : "Best";
            string rightLegend = bestAtHigh ? "Best" : "Worst";
            Vector2 leftLegendSize = ImGui.CalcTextSize(leftLegend);
            Vector2 rightLegendSize = ImGui.CalcTextSize(rightLegend);
            float margin = 2f;
            Vector2 leftPos = new(rectMin.X + margin, labelY + leftLegendSize.Y + 4f);
            Vector2 rightPos = new(rectMax.X - rightLegendSize.X - margin, labelY + rightLegendSize.Y + 4f);
            drawList.AddText(leftPos, ImGui.GetColorU32(ImGuiCol.Text), leftLegend);
            drawList.AddText(rightPos, ImGui.GetColorU32(ImGuiCol.Text), rightLegend);
            ImGui.Dummy(new Vector2(width, height + 28f + leftLegendSize.Y));
        }
    }
}
