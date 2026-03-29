using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using Newtonsoft.Json;
using System.Linq;
using System.Numerics;
using ClickIt.Definitions;

namespace ClickIt
{
    /**
        * This file contains methods related to the ClickItSettings class, which are used to render the settings UI and manage related logic.
        * It is separate from ClickItSettings.cs to keep the core settings data structure and the UI/logic code separate for better maintainability.
        * This file cannot be split into partial files because 
          doing so would affect ordering of the elements inside
          of the settings panel.
        */
    public partial class ClickItSettings : ISettings
    {
        private MechanicToggleTableEntry[]? _mechanicTableEntriesCache;
        private Dictionary<string, ToggleNode>? _mechanicToggleNodeByIdCache;
        private int _itemTypeMetadataSnapshotSignature = int.MinValue;
        private string[] _itemTypeWhitelistMetadataSnapshot = [];
        private string[] _itemTypeBlacklistMetadataSnapshot = [];
        private int _strongboxMetadataSnapshotSignature = int.MinValue;
        private string[] _strongboxClickMetadataSnapshot = [];
        private string[] _strongboxDontClickMetadataSnapshot = [];

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

        public bool IsClickHotkeyToggleModeEnabled()
        {
            return ClickHotkeyToggleMode?.Value == true;
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

        public bool IsUltimatumTakeRewardButtonClickEnabled()
        {
            return ClickUltimatumTakeRewardButton?.Value != false;
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
                || DebugShowInventoryPickup
                || DebugShowHoveredItemMetadata
                || DebugShowPathfinding
                || DebugShowUltimatum
                || DebugShowClicking
                || DebugShowRuntimeDebugLogOverlay
                || DebugShowRecentErrors;
        }

        public bool IsOnlyPathfindingDetailedDebugSectionEnabled()
        {
            return DebugShowPathfinding
            && !DebugShowStatus
            && !DebugShowGameState
            && !DebugShowPerformance
            && !DebugShowClickFrequencyTarget
            && !DebugShowAltarDetection
            && !DebugShowAltarService
            && !DebugShowLabels
            && !DebugShowInventoryPickup
            && !DebugShowHoveredItemMetadata
            && !DebugShowUltimatum
            && !DebugShowClicking
            && !DebugShowRuntimeDebugLogOverlay
            && !DebugShowRecentErrors;
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

            if (ImGui.Button("Copy Additional Debug Information"))
            {
                TriggerButtonNode(CopyAdditionalDebugInfoButton);
            }
            DrawInlineTooltip("Copies the current Additional Debug Information text to clipboard.");

            if (MemoryDumpInProgress)
            {
                float progress = Math.Clamp(MemoryDumpProgressPercent, 0, 100) / 100f;
                ImGui.ProgressBar(progress, new Vector2(-1f, 0f), $"Memory Dump: {MemoryDumpProgressPercent}%");
                if (!string.IsNullOrWhiteSpace(MemoryDumpStatusText))
                    ImGui.TextWrapped(MemoryDumpStatusText);
            }
            else if (MemoryDumpLastRunSucceeded && !string.IsNullOrWhiteSpace(MemoryDumpOutputPath))
            {
                ImGui.TextColored(new Vector4(0.25f, 0.85f, 0.35f, 1.0f), "memoryData.dat written to:");
                ImGui.TextWrapped(MemoryDumpOutputPath);
            }
            else if (!string.IsNullOrWhiteSpace(MemoryDumpStatusText))
            {
                ImGui.TextColored(new Vector4(0.95f, 0.45f, 0.35f, 1.0f), MemoryDumpStatusText);
            }

            if (RenderDebug.Value)
            {
                ImGui.Indent();
                DrawDebugSectionToggles();
                ImGui.Unindent();
            }

            DrawToggleNodeControl(
                "Auto Copy Inventory Warning Debug",
                AutoCopyInventoryWarningDebug,
                "Automatically copies inventory warning debug details when the 'Your inventory is full' overlay is triggered. Copy attempts are throttled to once per second.");

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

        private void DrawDebugSectionToggles()
        {
            var toggles = new[]
            {
                new DebugSectionToggleDescriptor("Status", DebugShowStatus, "Show/hide the status debug section"),
                new DebugSectionToggleDescriptor("Game State", DebugShowGameState, "Show/hide the Game State debug section"),
                new DebugSectionToggleDescriptor("Performance", DebugShowPerformance, "Show/hide the performance debug section"),
                new DebugSectionToggleDescriptor("Click Frequency Target", DebugShowClickFrequencyTarget, "Show/hide the Click Frequency Target debug section"),
                new DebugSectionToggleDescriptor("Altar Detection", DebugShowAltarDetection, "Show/hide the Altar Detection debug section"),
                new DebugSectionToggleDescriptor("Altar Service", DebugShowAltarService, "Show/hide the Altar Service debug section"),
                new DebugSectionToggleDescriptor("Labels", DebugShowLabels, "Show/hide the labels debug section"),
                new DebugSectionToggleDescriptor("Inventory Pickup", DebugShowInventoryPickup, "Show/hide inventory pickup/fullness debug section"),
                new DebugSectionToggleDescriptor("Hovered Item Metadata", DebugShowHoveredItemMetadata, "Show/hide the hovered item metadata debug section"),
                new DebugSectionToggleDescriptor("Pathfinding", DebugShowPathfinding, "Show/hide offscreen pathfinding debug section"),
                new DebugSectionToggleDescriptor("Ultimatum", DebugShowUltimatum, "Show/hide ultimatum automation debug section"),
                new DebugSectionToggleDescriptor("Clicking", DebugShowClicking, "Show/hide clicking debug section"),
                new DebugSectionToggleDescriptor("Debug Log Overlay", DebugShowRuntimeDebugLogOverlay, "Show/hide overlay section that displays DebugLog messages as a recent-stage style trail"),
                new DebugSectionToggleDescriptor("Recent Errors", DebugShowRecentErrors, "Show/hide the Recent Errors debug section"),
                new DebugSectionToggleDescriptor("Debug Frames", DebugShowFrames, "Show/hide the debug screen area frames")
            };

            foreach (DebugSectionToggleDescriptor toggle in toggles)
            {
                DrawToggleNodeControl(toggle.Label, toggle.Node, toggle.Tooltip);
            }
        }

        private void DrawItemTypeFiltersPanel()
        {
            EnsureItemTypeFiltersInitialized();


            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Table rows with [v] next to them can be clicked to open subtype filter options.");
            ImGui.Spacing();

            DrawSearchBar("##ItemTypeSearch", "Clear##ItemTypeClear", ref itemTypeSearchFilter);
            if (DrawResetDefaultsButton("Reset Defaults##ItemTypeDefaults"))
            {
                ResetItemTypeFilterDefaults();
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

        private void DrawLazyModeNearbyMonsterRulesPanel()
        {
            EnsureLazyModeNearbyMonsterFiltersInitialized();

            DrawLazyModeNearbyMonsterRuleRows([
                new LazyModeNearbyMonsterRuleDescriptor(
                    "Normal",
                    "Normal",
                    () => LazyModeNormalMonsterBlockCount,
                    () => LazyModeNormalMonsterBlockDistance,
                    (count, distance) =>
                    {
                        LazyModeNormalMonsterBlockCount = count;
                        LazyModeNormalMonsterBlockDistance = distance;
                    }),
                new LazyModeNearbyMonsterRuleDescriptor(
                    "Magic",
                    "Magic",
                    () => LazyModeMagicMonsterBlockCount,
                    () => LazyModeMagicMonsterBlockDistance,
                    (count, distance) =>
                    {
                        LazyModeMagicMonsterBlockCount = count;
                        LazyModeMagicMonsterBlockDistance = distance;
                    }),
                new LazyModeNearbyMonsterRuleDescriptor(
                    "Rare",
                    "Rare",
                    () => LazyModeRareMonsterBlockCount,
                    () => LazyModeRareMonsterBlockDistance,
                    (count, distance) =>
                    {
                        LazyModeRareMonsterBlockCount = count;
                        LazyModeRareMonsterBlockDistance = distance;
                    }),
                new LazyModeNearbyMonsterRuleDescriptor(
                    "Unique",
                    "Unique",
                    () => LazyModeUniqueMonsterBlockCount,
                    () => LazyModeUniqueMonsterBlockDistance,
                    (count, distance) =>
                    {
                        LazyModeUniqueMonsterBlockCount = count;
                        LazyModeUniqueMonsterBlockDistance = distance;
                    })
            ]);

            ImGui.Spacing();
            ImGui.TextDisabled("Set count to 0 to disable a specific rarity rule.");
        }

        private void DrawLazyModeNearbyMonsterRuleRows(IReadOnlyList<LazyModeNearbyMonsterRuleDescriptor> rows)
        {
            foreach (LazyModeNearbyMonsterRuleDescriptor row in rows)
            {
                DrawLazyModeNearbyMonsterRuleRow(
                    row.RowId,
                    row.RarityLabel,
                    row.GetCount(),
                    row.GetDistance(),
                    row.Apply);
            }
        }

        private void DrawLazyModeNearbyMonsterRuleRow(string rowId, string rarityLabel, int currentCount, int currentDistance, Action<int, int> apply)
        {
            int count = currentCount;
            int distance = currentDistance;

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Disable Lazy Mode when");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(70f);
            bool changed = ImGui.InputInt($"##LazyModeNearbyMonsterCount{rowId}", ref count, 1, 10);
            count = SanitizeLazyModeNearbyMonsterCount(count);

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"{rarityLabel} Monsters are within");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(80f);
            changed |= ImGui.InputInt($"##LazyModeNearbyMonsterDistance{rowId}", ref distance, 1, 10);
            distance = SanitizeLazyModeNearbyMonsterDistance(distance);

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Distance");

            if (changed || count != currentCount || distance != currentDistance)
            {
                apply(count, distance);
            }
        }

        private static void DrawDualTransferTable(
            string tableId,
            string leftHeader,
            string rightHeader,
            Vector4 leftBackground,
            Vector4 rightBackground,
            Action drawLeft,
            Action drawRight)
        {
            if (!ImGui.BeginTable(tableId, 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
                return;

            try
            {
                SetupTwoColumnFilterTableHeader(
                    leftHeader,
                    rightHeader,
                    leftBackground,
                    rightBackground);

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                drawLeft();

                ImGui.TableSetColumnIndex(1);
                drawRight();
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
                ResetEssenceCorruptionDefaults();
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
                ResetStrongboxFilterDefaults();
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
        private sealed record MechanicToggleTableEntry(string Id, string DisplayName, ToggleNode Node, string? GroupId = null, bool DefaultEnabled = false, string? Subgroup = null);

        private const string LeagueChestSubgroupMirage = "Mirage";
        private const string LeagueChestSubgroupHeist = "Heist";
        private const string LeagueChestSubgroupBlight = "Blight";
        private const string LeagueChestSubgroupBreach = "Breach";
        private const string LeagueChestSubgroupSynthesis = "Synthesis";

        private readonly struct DebugSectionToggleDescriptor(string label, ToggleNode node, string tooltip)
        {
            public string Label { get; } = label;
            public ToggleNode Node { get; } = node;
            public string Tooltip { get; } = tooltip;
        }

        private readonly struct LazyModeNearbyMonsterRuleDescriptor(
            string rowId,
            string rarityLabel,
            Func<int> getCount,
            Func<int> getDistance,
            Action<int, int> apply)
        {
            public string RowId { get; } = rowId;
            public string RarityLabel { get; } = rarityLabel;
            public Func<int> GetCount { get; } = getCount;
            public Func<int> GetDistance { get; } = getDistance;
            public Action<int, int> Apply { get; } = apply;
        }

        private readonly struct ChestDropSettleSettingsDescriptor(
            string labelPrefix,
            string idPrefix,
            ToggleNode pauseNode,
            RangeNode<int> initialDelayNode,
            RangeNode<int> pollIntervalNode,
            RangeNode<int> quietWindowNode)
        {
            public string LabelPrefix { get; } = labelPrefix;
            public string IdPrefix { get; } = idPrefix;
            public ToggleNode PauseNode { get; } = pauseNode;
            public RangeNode<int> InitialDelayNode { get; } = initialDelayNode;
            public RangeNode<int> PollIntervalNode { get; } = pollIntervalNode;
            public RangeNode<int> QuietWindowNode { get; } = quietWindowNode;
        }

        private static readonly MechanicToggleGroupEntry[] MechanicToggleGroups =
        [
            new("league-chests", "League Mechanic Chests"),
            new("basic-chests", "Basic Chests"),
            new("ritual-altars", "Ritual"),
            new("settlers", "Settlers"),
            new("delve", "Delve"),
            new("ultimatum", "Ultimatum"),
            new("altars", "Altars")
        ];

        private void DrawMechanicsTablePanel()
        {
            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Table rows with [v] next to them can be clicked to open submenus.");

            ImGui.Spacing();

            DrawSearchBar("##MechanicsSearch", "Clear##MechanicsSearchClear", ref mechanicsSearchFilter);
            if (DrawResetDefaultsButton("Reset Defaults##MechanicsResetDefaults"))
            {
                ResetMechanicsTableDefaults();
                _expandedMechanicsTableRowId = string.Empty;
            }

            ImGui.Spacing();

            DrawDualTransferTable(
                tableId: "MechanicsFilterLists",
                leftHeader: "Click",
                rightHeader: "Don't Click",
                leftBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                drawLeft: () => DrawMechanicsTableList("Click##Mechanics", moveToClick: false, WhitelistTextColor),
                drawRight: () => DrawMechanicsTableList("DontClick##Mechanics", moveToClick: true, BlacklistTextColor));
        }

        private void DrawMechanicsTableList(string listId, bool moveToClick, Vector4 textColor)
        {
            ImGui.PushID(listId);

            IReadOnlyList<MechanicToggleTableEntry> entries = GetMechanicTableEntries();
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

            if (string.Equals(group.Id, "league-chests", StringComparison.OrdinalIgnoreCase))
            {
                DrawLeagueChestGroupSubmenu(listId, group, entries);
                DrawMechanicGroupExtraSettings(group.Id);
                ImGui.Unindent();
                return;
            }

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

            DrawMechanicGroupExtraSettings(group.Id);

            ImGui.Unindent();
        }

        private void DrawLeagueChestGroupSubmenu(string listId, MechanicToggleGroupEntry group, IReadOnlyList<MechanicToggleTableEntry> entries)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
            ImGui.TextWrapped("I'll add more specific league chests to this list in future releases of the plugin.");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            List<MechanicToggleTableEntry> leagueEntries = entries
                .Where(e => string.Equals(e.GroupId, group.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var subgroupNames = leagueEntries
                .Where(static e => !string.IsNullOrWhiteSpace(e.Subgroup))
                .Select(static e => e.Subgroup!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            bool searchIsEmpty = string.IsNullOrWhiteSpace(mechanicsSearchFilter);

            foreach (string subgroupName in subgroupNames)
            {
                MechanicToggleTableEntry[] subgroupEntries = leagueEntries
                    .Where(e => string.Equals(e.Subgroup, subgroupName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                bool subgroupMatchesSearch = MatchesMechanicsSearch(subgroupName, mechanicsSearchFilter);
                bool hasEntryMatch = subgroupEntries.Any(e => MatchesMechanicsSearch(e.DisplayName, mechanicsSearchFilter));
                if (!searchIsEmpty && !subgroupMatchesSearch && !hasEntryMatch)
                    continue;

                bool isOpen = ImGui.TreeNodeEx($"{subgroupName}##MechanicSubmenu_{listId}_{group.Id}_{subgroupName}", ImGuiTreeNodeFlags.DefaultOpen);
                if (!isOpen)
                    continue;

                for (int i = 0; i < subgroupEntries.Length; i++)
                {
                    MechanicToggleTableEntry entry = subgroupEntries[i];
                    if (!searchIsEmpty && !subgroupMatchesSearch && !MatchesMechanicsSearch(entry.DisplayName, mechanicsSearchFilter))
                        continue;

                    DrawMechanicSubmenuCheckbox(listId, group.Id, entry);
                }

                ImGui.TreePop();
            }

            MechanicToggleTableEntry[] ungroupedEntries = leagueEntries
                .Where(static e => string.IsNullOrWhiteSpace(e.Subgroup))
                .ToArray();

            for (int i = 0; i < ungroupedEntries.Length; i++)
            {
                MechanicToggleTableEntry entry = ungroupedEntries[i];
                if (!searchIsEmpty
                    && !MatchesMechanicsSearch(entry.DisplayName, mechanicsSearchFilter)
                    && !MatchesMechanicsSearch(group.DisplayName, mechanicsSearchFilter))
                {
                    continue;
                }

                DrawMechanicSubmenuCheckbox(listId, group.Id, entry);
            }
        }

        private void DrawMechanicSubmenuCheckbox(string listId, string groupId, MechanicToggleTableEntry entry)
        {
            bool enabled = entry.Node.Value;
            Vector4 rowColor = enabled ? WhitelistTextColor : BlacklistTextColor;
            ImGui.PushStyleColor(ImGuiCol.Text, rowColor);
            if (ImGui.Checkbox($"{entry.DisplayName}##MechanicSubmenu_{listId}_{groupId}_{entry.Id}", ref enabled))
            {
                entry.Node.Value = enabled;
            }
            ImGui.PopStyleColor();
        }

        private void DrawMechanicGroupExtraSettings(string groupId)
        {
            if (string.Equals(groupId, "basic-chests", StringComparison.OrdinalIgnoreCase))
            {
                DrawChestDropSettleSettings(
                    new ChestDropSettleSettingsDescriptor(
                        "Basic Chest",
                        "BasicChests",
                        PauseAfterOpeningBasicChests,
                        PauseAfterOpeningBasicChestsInitialDelayMs,
                        PauseAfterOpeningBasicChestsPollIntervalMs,
                        PauseAfterOpeningBasicChestsQuietWindowMs));
                return;
            }

            if (string.Equals(groupId, "league-chests", StringComparison.OrdinalIgnoreCase))
            {
                DrawChestDropSettleSettings(
                    new ChestDropSettleSettingsDescriptor(
                        "League Mechanic Chest",
                        "LeagueChests",
                        PauseAfterOpeningLeagueChests,
                        PauseAfterOpeningLeagueChestsInitialDelayMs,
                        PauseAfterOpeningLeagueChestsPollIntervalMs,
                        PauseAfterOpeningLeagueChestsQuietWindowMs));
            }
        }

        private void DrawChestDropSettleSettings(ChestDropSettleSettingsDescriptor descriptor)
        {
            ImGui.Spacing();
            DrawToggleNodeControl(
                $"Wait for Drops to Settle##{descriptor.IdPrefix}PauseEnabled",
                descriptor.PauseNode,
                $"When enabled, ClickIt waits for new loot labels after opening a {descriptor.LabelPrefix} before resuming clicks.");
            DrawToggleNodeControl(
                $"Allow Nearby Mechanics while Waiting##{descriptor.IdPrefix}AllowNearbyMechanics",
                AllowNearbyMechanicsWhileWaitingForChestDropsToSettle,
                "When enabled, nearby mechanics around the opened chest can still be clicked while drops are settling.");
            DrawRangeNodeControl(
                $"Nearby mechanic distance##{descriptor.IdPrefix}AllowNearbyMechanicsDistance",
                AllowNearbyMechanicsWhileWaitingForChestDropsToSettleDistance,
                0,
                100,
                "Maximum distance from the opened chest where mechanics are still allowed during settle wait.");
            DrawRangeNodeControl(
                $"Initial delay (ms)##{descriptor.IdPrefix}InitialDelayMs",
                descriptor.InitialDelayNode,
                100,
                1500,
                "How long to wait after click confirmation before checking for new labels.");
            DrawRangeNodeControl(
                $"Poll interval (ms)##{descriptor.IdPrefix}PollIntervalMs",
                descriptor.PollIntervalNode,
                50,
                500,
                "How frequently ClickIt checks ItemsOnGroundLabels for newly added drops.");
            DrawRangeNodeControl(
                $"Quiet window (ms)##{descriptor.IdPrefix}QuietWindowMs",
                descriptor.QuietWindowNode,
                100,
                2000,
                "Loot is considered settled after this many milliseconds pass without new labels.");
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
            foreach (MechanicToggleTableEntry entry in GetMechanicTableEntries())
            {
                entry.Node.Value = entry.DefaultEnabled;
            }
        }

        private IReadOnlyList<MechanicToggleTableEntry> GetMechanicTableEntries()
        {
            if (_mechanicTableEntriesCache == null)
            {
                _mechanicTableEntriesCache = BuildMechanicTableEntries();
                _mechanicToggleNodeByIdCache = BuildMechanicToggleNodeById(_mechanicTableEntriesCache);
            }

            return _mechanicTableEntriesCache;
        }

        private static Dictionary<string, ToggleNode> BuildMechanicToggleNodeById(IEnumerable<MechanicToggleTableEntry> entries)
        {
            Dictionary<string, ToggleNode> nodesById = new(StringComparer.OrdinalIgnoreCase);
            foreach (MechanicToggleTableEntry entry in entries)
            {
                nodesById[entry.Id] = entry.Node;
            }

            return nodesById;
        }

        private MechanicToggleTableEntry[] BuildMechanicTableEntries()
        {
            return
            [
                new(MechanicIds.BasicChests, "Basic Chests", ClickBasicChests, "basic-chests", false),
                new(MechanicIds.MirageGoldenDjinnCache, "Golden Djinn's Cache", ClickMirageGoldenDjinnCache, "league-chests", true, LeagueChestSubgroupMirage),
                new(MechanicIds.MirageSilverDjinnCache, "Silver Djinn's Cache", ClickMirageSilverDjinnCache, "league-chests", true, LeagueChestSubgroupMirage),
                new(MechanicIds.MirageBronzeDjinnCache, "Bronze Djinn's Cache", ClickMirageBronzeDjinnCache, "league-chests", true, LeagueChestSubgroupMirage),
                new(MechanicIds.HeistSecureLocker, "Secure Locker", ClickHeistSecureLocker, "league-chests", true, LeagueChestSubgroupHeist),
                new(MechanicIds.BlightCyst, "Blight Cyst", ClickBlightCyst, "league-chests", true, LeagueChestSubgroupBlight),
                new(MechanicIds.BreachGraspingCoffers, "Grasping Coffers", ClickBreachGraspingCoffers, "league-chests", true, LeagueChestSubgroupBreach),
                new(MechanicIds.SynthesisSynthesisedStash, "Synthesised Stash", ClickSynthesisSynthesisedStash, "league-chests", true, LeagueChestSubgroupSynthesis),
                new(MechanicIds.LeagueChests, "Other League Mechanic Chests", ClickLeagueChestsOther, "league-chests", true),
                new(MechanicIds.Shrines, "Shrines", ClickShrines, null, true),
                new(MechanicIds.AreaTransitions, "Area Transitions", ClickAreaTransitions, null, false),
                new(MechanicIds.LabyrinthTrials, "Labyrinth Trials", ClickLabyrinthTrials, null, false),
                new(MechanicIds.CraftingRecipes, "Crafting Recipes", ClickCraftingRecipes, null, true),
                new(MechanicIds.Doors, "Doors", ClickDoors, null, false),
                new(MechanicIds.Levers, "Levers", ClickLevers, null, false),
                new(MechanicIds.AlvaTempleDoors, "Alva Temple Doors", ClickAlvaTempleDoors, null, true),
                new(MechanicIds.Betrayal, "Betrayal", ClickBetrayal, null, false),
                new(MechanicIds.Blight, "Blight", ClickBlight, null, true),
                new(MechanicIds.BreachNodes, "Breach Nodes", ClickBreachNodes, null, false),
                new(MechanicIds.LegionPillars, "Legion Pillars", ClickLegionPillars, null, true),
                new(MechanicIds.Harvest, "Nearest Harvest Plot", NearestHarvest, null, true),
                new(MechanicIds.Sanctum, "Sanctum", ClickSanctum, null, true),
                new(MechanicIds.Items, "Items", ClickItems, null, true),
                new(MechanicIds.Essences, "Essences", ClickEssences, null, true),
                new(MechanicIds.RitualInitiate, "Uncompleted Altars", ClickRitualInitiate, "ritual-altars", true),
                new(MechanicIds.RitualCompleted, "Completed Altars", ClickRitualCompleted, "ritual-altars", true),
                new(MechanicIds.LostShipment, "Lost Shipment", ClickLostShipmentCrates, "settlers", true),
                new(MechanicIds.SettlersCrimsonIron, "Crimson Iron", ClickSettlersCrimsonIron, "settlers", true),
                new(MechanicIds.SettlersCopper, "Copper", ClickSettlersCopper, "settlers", true),
                new(MechanicIds.SettlersPetrifiedWood, "Petrified Wood", ClickSettlersPetrifiedWood, "settlers", true),
                new(MechanicIds.SettlersBismuth, "Bismuth", ClickSettlersBismuth, "settlers", true),
                new(MechanicIds.SettlersHourglass, "Hourglass", ClickSettlersOre, "settlers", true),
                new(MechanicIds.SettlersVerisium, "Verisium", ClickSettlersVerisium, "settlers", true),
                new(MechanicIds.DelveAzuriteVeins, "Azurite Veins", ClickAzuriteVeins, "delve", true),
                new(MechanicIds.DelveSulphiteVeins, "Sulphite Veins", ClickSulphiteVeins, "delve", true),
                new(MechanicIds.DelveEncounterInitiators, "Encounter Initiators", ClickDelveSpawners, "delve", true),
                new(MechanicIds.UltimatumInitialOverlay, "Initial Ultimatum Overlay", ClickInitialUltimatum, "ultimatum", false),
                new(MechanicIds.UltimatumWindow, "Ultimatum Window", ClickUltimatumChoices, "ultimatum", false),
                new(MechanicIds.AltarsSearingExarch, "Searing Exarch", ClickExarchAltars, "altars", false),
                new(MechanicIds.AltarsEaterOfWorlds, "Eater of Worlds", ClickEaterAltars, "altars", false)
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

        private static bool BeginSingleColumnPriorityTable(string tableId, string headerText, float tableWidth)
        {
            if (!ImGui.BeginTable(tableId, 1, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX | ImGuiTableFlags.NoPadOuterX))
                return false;

            ImGui.TableSetupColumn(headerText, ImGuiTableColumnFlags.WidthFixed, tableWidth);
            DrawPriorityTableHeaderCell(headerText);
            return true;
        }

        private static void DrawPriorityTableHeaderCell(string headerText)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None);
            ImGui.TableSetColumnIndex(0);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.6f, 0.3f)));
            ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), headerText);
        }

        private void MoveStrongboxFilter(string strongboxId, bool moveToClick)
        {
            HashSet<string> source = moveToClick ? StrongboxDontClickIds : StrongboxClickIds;
            HashSet<string> target = moveToClick ? StrongboxClickIds : StrongboxDontClickIds;

            source.Remove(strongboxId);
            target.Add(strongboxId);
        }

        private void MoveUltimatumTakeRewardModifier(string modifierName, bool moveToTakeReward)
        {
            HashSet<string> source = moveToTakeReward ? UltimatumContinueModifierNames : UltimatumTakeRewardModifierNames;
            HashSet<string> target = moveToTakeReward ? UltimatumTakeRewardModifierNames : UltimatumContinueModifierNames;

            source.Remove(modifierName);
            target.Add(modifierName);
        }

        private static bool TryGetUltimatumModifierBaseName(string modifierName, out string baseModifierName)
        {
            baseModifierName = string.Empty;
            if (string.IsNullOrWhiteSpace(modifierName))
                return false;

            string[] suffixes = [" I", " II", " III", " IV"];
            string trimmed = modifierName.Trim();
            for (int i = 0; i < suffixes.Length; i++)
            {
                string suffix = suffixes[i];
                if (!trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                baseModifierName = trimmed[..^suffix.Length].Trim();
                return baseModifierName.Length > 0;
            }

            return false;
        }

        private static string NormalizeUltimatumModifierForMatching(string modifierName)
        {
            if (string.IsNullOrWhiteSpace(modifierName))
                return string.Empty;

            string normalized = modifierName.Trim();

            // Some panel strings are shaped like: InternalId (Display Name).
            int closeParen = normalized.LastIndexOf(')');
            if (closeParen == normalized.Length - 1)
            {
                int openParen = normalized.LastIndexOf('(');
                if (openParen >= 0 && openParen < closeParen)
                {
                    string inner = normalized[(openParen + 1)..closeParen].Trim();
                    if (!string.IsNullOrWhiteSpace(inner))
                        normalized = inner;
                }
            }

            return normalized;
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
            EnsureEssenceCorruptionFiltersInitialized();

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
            EnsureStrongboxFiltersInitialized();
            RefreshStrongboxMetadataSnapshotsIfNeeded();
            return _strongboxClickMetadataSnapshot;
        }

        public IReadOnlyList<string> GetStrongboxDontClickMetadataIdentifiers()
        {
            EnsureStrongboxFiltersInitialized();
            RefreshStrongboxMetadataSnapshotsIfNeeded();
            return _strongboxDontClickMetadataSnapshot;
        }

        private void RefreshStrongboxMetadataSnapshotsIfNeeded()
        {
            int signature = ComputeStrongboxMetadataSignature();
            if (signature == _strongboxMetadataSnapshotSignature)
                return;

            _strongboxClickMetadataSnapshot = BuildStrongboxMetadataIdentifiers(StrongboxClickIds);
            _strongboxDontClickMetadataSnapshot = BuildStrongboxMetadataIdentifiers(StrongboxDontClickIds);
            _strongboxMetadataSnapshotSignature = signature;
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

        public IReadOnlyCollection<string> GetUltimatumTakeRewardModifierNames()
        {
            EnsureUltimatumTakeRewardModifiersInitialized();
            return UltimatumTakeRewardModifierNames;
        }

        public bool ShouldTakeRewardForGruelingGauntletModifier(string? modifierName)
        {
            // Hot-path safe guard: fully sanitize only when collections are missing.
            if (UltimatumTakeRewardModifierNames == null || UltimatumContinueModifierNames == null)
                EnsureUltimatumTakeRewardModifiersInitialized();

            HashSet<string> takeRewardSet = UltimatumTakeRewardModifierNames ?? [];

            if (string.IsNullOrWhiteSpace(modifierName))
                return false;

            string normalized = NormalizeUltimatumModifierForMatching(modifierName);
            if (takeRewardSet.Contains(normalized))
                return true;

            if (takeRewardSet.Contains($"{normalized} I"))
                return true;

            if (TryGetUltimatumModifierBaseName(normalized, out string baseName)
                && takeRewardSet.Contains(baseName))
            {
                return true;
            }

            return false;
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
                ResetUltimatumModifierPriorityDefaults();
            }

            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Priority: top row is highest, bottom row is lowest.");
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));
            ImGui.TextWrapped("Example: if this table has Resistant Monsters above Reduced Recovery above Ruin, and those three are offered, Resistant Monsters is selected.");
            ImGui.PopStyleColor();
            ImGui.Spacing();

            float tableWidth = Math.Min(600f, Math.Max(100f, ImGui.GetContentRegionAvail().X));
            if (!BeginSingleColumnPriorityTable("UltimatumModifierPriorityTable", "Modifiers", tableWidth))
                return;

            try
            {
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

        private void DrawUltimatumTakeRewardModifierTablePanel()
        {
            EnsureUltimatumTakeRewardModifiersInitialized();

            bool hasDetection = Services.ClickService.TryGetGruelingGauntletDetectionForSettings(out bool isGruelingGauntletActive);
            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "This table only does anything when Gruelling Gauntlet is allocated.");

            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Rows with [v] can be clicked to open stage-specific submenu options.");
            ImGui.Spacing();

            DrawSearchBar("##UltimatumTakeRewardSearch", "Clear##UltimatumTakeRewardSearchClear", ref ultimatumTakeRewardSearchFilter);
            if (DrawResetDefaultsButton("Reset Defaults##UltimatumTakeRewardResetDefaults"))
            {
                ResetUltimatumTakeRewardModifierDefaults();
            }

            ImGui.Spacing();

            DrawDualTransferTable(
                tableId: "UltimatumTakeRewardLists",
                leftHeader: "Take Reward",
                rightHeader: "Keep Going",
                leftBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                drawLeft: () => DrawUltimatumTakeRewardList("TakeReward##Ultimatum", UltimatumTakeRewardModifierNames, moveToTakeReward: false, textColor: new Vector4(0.8f, 0.4f, 0.4f, 1.0f)),
                drawRight: () => DrawUltimatumTakeRewardList("Continue##Ultimatum", UltimatumContinueModifierNames, moveToTakeReward: true, textColor: new Vector4(0.4f, 0.8f, 0.4f, 1.0f)));
        }

        private void DrawUltimatumTakeRewardList(string id, HashSet<string> sourceSet, bool moveToTakeReward, Vector4 textColor)
        {
            ImGui.PushID(id);

            bool hasEntries = false;
            foreach (string modifier in UltimatumModifiersConstants.AllModifierNamesWithStages)
            {
                if (UltimatumTieredModifierNames.Contains(modifier))
                    continue;
                if (!sourceSet.Contains(modifier))
                    continue;
                if (!MatchesUltimatumSearch(modifier, ultimatumTakeRewardSearchFilter))
                    continue;

                hasEntries = true;
                bool arrowClicked = DrawTransferListRow(id, modifier, modifier, moveToTakeReward, textColor);

                if (ImGui.IsItemHovered())
                {
                    string description = UltimatumModifiersConstants.GetDescription(modifier);
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
                        ImGui.Indent();
                        ImGui.TextWrapped(description);
                        ImGui.Unindent();
                        ImGui.PopStyleColor();
                    }
                }

                if (arrowClicked)
                {
                    MoveUltimatumTakeRewardModifier(modifier, moveToTakeReward);
                    _expandedUltimatumTakeRewardRowKey = string.Empty;
                    ImGui.PopID();
                    return;
                }
            }

            foreach (UltimatumModifierGroupEntry group in UltimatumModifierGroups)
            {
                if (!ShouldRenderUltimatumModifierGroup(group, sourceSet, ultimatumTakeRewardSearchFilter))
                    continue;

                hasEntries = true;
                UltimatumGroupRowRenderState rowState = DrawUltimatumModifierGroupRow(id, group, moveToTakeReward, textColor);
                if (rowState.ArrowClicked)
                {
                    SetUltimatumModifierGroupState(group, moveToTakeReward);
                    _expandedUltimatumTakeRewardRowKey = string.Empty;
                    ImGui.PopID();
                    return;
                }

                if (rowState.RowClicked)
                    ToggleExpandedUltimatumTakeRewardRow(id, group.Id);

                if (rowState.IsHovered)
                {
                    string description = UltimatumModifiersConstants.GetDescription(group.DisplayName);
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
                        ImGui.Indent();
                        ImGui.TextWrapped(description);
                        ImGui.Unindent();
                        ImGui.PopStyleColor();
                    }
                }

                if (IsExpandedUltimatumTakeRewardRow(id, group.Id))
                    DrawUltimatumModifierGroupSubmenu(id, group, moveToTakeReward);
            }

            DrawNoEntriesPlaceholder(hasEntries);
            ImGui.PopID();
        }

        private readonly struct UltimatumGroupRowRenderState(bool rowClicked, bool arrowClicked, bool isHovered)
        {
            public bool RowClicked { get; } = rowClicked;
            public bool ArrowClicked { get; } = arrowClicked;
            public bool IsHovered { get; } = isHovered;
        }

        private sealed record UltimatumModifierGroupEntry(string Id, string DisplayName, string[] Members);

        private static readonly UltimatumModifierGroupEntry[] UltimatumModifierGroups = BuildUltimatumModifierGroups();
        private static readonly HashSet<string> UltimatumTieredModifierNames = BuildUltimatumTieredModifierNames();

        private UltimatumGroupRowRenderState DrawUltimatumModifierGroupRow(string listId, UltimatumModifierGroupEntry group, bool moveToTakeReward, Vector4 textColor)
        {
            string label = $"{group.DisplayName} [v]##{listId}_{group.Id}";
            float rowWidth = CalculateItemTypeRowWidth();
            const float arrowWidth = 28f;

            if (moveToTakeReward)
            {
                bool leftArrowClicked = ImGui.Button($"<-##MoveUltimatumGroup_{listId}_{group.Id}", new Vector2(arrowWidth, 0));
                bool leftArrowHovered = ImGui.IsItemHovered();
                ImGui.SameLine();
                bool rowClicked = DrawUltimatumModifierGroupSelectable(listId, group.Id, label, rowWidth, textColor);
                bool rowHovered = ImGui.IsItemHovered();
                return new UltimatumGroupRowRenderState(rowClicked, leftArrowClicked, rowHovered || leftArrowHovered);
            }

            bool clicked = DrawUltimatumModifierGroupSelectable(listId, group.Id, label, rowWidth, textColor);
            bool rowIsHovered = ImGui.IsItemHovered();
            ImGui.SameLine();
            bool rightArrowClicked = ImGui.Button($"->##MoveUltimatumGroup_{listId}_{group.Id}", new Vector2(arrowWidth, 0));
            bool rightArrowHovered = ImGui.IsItemHovered();
            return new UltimatumGroupRowRenderState(clicked, rightArrowClicked, rowIsHovered || rightArrowHovered);
        }

        private bool DrawUltimatumModifierGroupSelectable(string listId, string groupId, string label, float rowWidth, Vector4 textColor)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            bool clicked = ImGui.Selectable(label, IsExpandedUltimatumTakeRewardRow(listId, groupId), ImGuiSelectableFlags.AllowDoubleClick, new Vector2(rowWidth, 0));
            ImGui.PopStyleColor();
            return clicked;
        }

        private void DrawUltimatumModifierGroupSubmenu(string listId, UltimatumModifierGroupEntry group, bool moveToTakeReward)
        {
            ImGui.Indent();

            foreach (string modifier in UltimatumModifiersConstants.AllModifierNamesWithStages)
            {
                if (!group.Members.Contains(modifier, StringComparer.OrdinalIgnoreCase))
                    continue;
                if (!MatchesUltimatumSearch(modifier, ultimatumTakeRewardSearchFilter))
                    continue;

                bool isTakeReward = UltimatumTakeRewardModifierNames.Contains(modifier);
                bool enabledForList = moveToTakeReward ? !isTakeReward : isTakeReward;
                Vector4 rowColor = enabledForList ? (moveToTakeReward ? WhitelistTextColor : BlacklistTextColor) : (moveToTakeReward ? BlacklistTextColor : WhitelistTextColor);
                ImGui.PushStyleColor(ImGuiCol.Text, rowColor);
                if (ImGui.Checkbox($"{modifier}##UltimatumGroupSubmenu_{listId}_{group.Id}_{modifier}", ref enabledForList))
                {
                    bool moveModifierToTakeReward = moveToTakeReward ? !enabledForList : enabledForList;
                    MoveUltimatumTakeRewardModifier(modifier, moveModifierToTakeReward);
                }
                ImGui.PopStyleColor();
            }

            ImGui.Unindent();
        }

        private static bool ShouldRenderUltimatumModifierGroup(UltimatumModifierGroupEntry group, HashSet<string> sourceSet, string filter)
        {
            bool matchesGroup = MatchesUltimatumSearch(group.DisplayName, filter);
            for (int i = 0; i < group.Members.Length; i++)
            {
                string member = group.Members[i];
                if (!sourceSet.Contains(member))
                    continue;
                if (matchesGroup || MatchesUltimatumSearch(member, filter))
                    return true;
            }

            return false;
        }

        private static UltimatumModifierGroupEntry[] BuildUltimatumModifierGroups()
        {
            Dictionary<string, List<string>> membersByBase = new(StringComparer.OrdinalIgnoreCase);
            List<string> groupOrder = [];

            for (int i = 0; i < UltimatumModifiersConstants.AllModifierNamesWithStages.Length; i++)
            {
                string modifier = UltimatumModifiersConstants.AllModifierNamesWithStages[i];
                if (!TryGetUltimatumModifierBaseName(modifier, out string baseName))
                    continue;

                if (!membersByBase.TryGetValue(baseName, out List<string>? members))
                {
                    members = [];
                    membersByBase[baseName] = members;
                    groupOrder.Add(baseName);
                }

                members.Add(modifier);
            }

            var groups = new List<UltimatumModifierGroupEntry>(groupOrder.Count);
            for (int i = 0; i < groupOrder.Count; i++)
            {
                string baseName = groupOrder[i];
                List<string> members = membersByBase[baseName];
                if (members.Count == 0)
                    continue;

                groups.Add(new UltimatumModifierGroupEntry(baseName, baseName, [.. members.Distinct(StringComparer.OrdinalIgnoreCase)]));
            }

            return [.. groups];
        }

        private static HashSet<string> BuildUltimatumTieredModifierNames()
        {
            HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < UltimatumModifierGroups.Length; i++)
            {
                // Hide tiered bases from top-level rows; they are managed via grouped submenu entries.
                result.Add(UltimatumModifierGroups[i].Id);

                string[] members = UltimatumModifierGroups[i].Members;
                for (int j = 0; j < members.Length; j++)
                {
                    result.Add(members[j]);
                }
            }

            return result;
        }

        private void SetUltimatumModifierGroupState(UltimatumModifierGroupEntry group, bool moveToTakeReward)
        {
            for (int i = 0; i < group.Members.Length; i++)
            {
                MoveUltimatumTakeRewardModifier(group.Members[i], moveToTakeReward);
            }
        }

        private static string BuildExpandedUltimatumTakeRewardRowKey(string listId, string rowId)
        {
            return $"{listId}:{rowId}";
        }

        private bool IsExpandedUltimatumTakeRewardRow(string listId, string rowId)
        {
            return string.Equals(_expandedUltimatumTakeRewardRowKey, BuildExpandedUltimatumTakeRewardRowKey(listId, rowId), StringComparison.Ordinal);
        }

        private void ToggleExpandedUltimatumTakeRewardRow(string listId, string rowId)
        {
            string rowKey = BuildExpandedUltimatumTakeRewardRowKey(listId, rowId);
            if (string.Equals(_expandedUltimatumTakeRewardRowKey, rowKey, StringComparison.Ordinal))
                _expandedUltimatumTakeRewardRowKey = string.Empty;
            else
                _expandedUltimatumTakeRewardRowKey = rowKey;
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
                ResetMechanicPriorityDefaults();
            }

            DrawMechanicPrioritySectionDescription();

            float tableWidth = Math.Min(700f, Math.Max(160f, ImGui.GetContentRegionAvail().X));
            if (!BeginSingleColumnPriorityTable("MechanicPriorityTable", "Mechanics", tableWidth))
                return;

            try
            {
                DrawMechanicPriorityRows();
            }
            finally
            {
                ImGui.EndTable();
            }
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
            _ = GetMechanicTableEntries();

            if (_mechanicToggleNodeByIdCache != null
                && _mechanicToggleNodeByIdCache.TryGetValue(mechanicId, out ToggleNode? node)
                && node != null)
            {
                return node.Value;
            }

            return true;
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
            if (MechanicPriorityEntriesById.TryGetValue(id, out MechanicPriorityEntry? found))
            {
                entry = found;
                return true;
            }

            entry = null;
            return false;
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
            RefreshItemTypeMetadataSnapshotsIfNeeded();
            return _itemTypeWhitelistMetadataSnapshot;
        }

        public IReadOnlyList<string> GetItemTypeBlacklistMetadataIdentifiers()
        {
            EnsureItemTypeFiltersInitialized();
            RefreshItemTypeMetadataSnapshotsIfNeeded();
            return _itemTypeBlacklistMetadataSnapshot;
        }

        private void RefreshItemTypeMetadataSnapshotsIfNeeded()
        {
            int signature = ComputeItemTypeMetadataSignature();
            if (signature == _itemTypeMetadataSnapshotSignature)
                return;

            _itemTypeWhitelistMetadataSnapshot = BuildItemTypeMetadataIdentifiers(
                primaryIds: ItemTypeWhitelistIds,
                primaryIsWhitelist: true,
                oppositeIds: ItemTypeBlacklistIds,
                oppositeIsWhitelist: false);

            _itemTypeBlacklistMetadataSnapshot = BuildItemTypeMetadataIdentifiers(
                primaryIds: ItemTypeBlacklistIds,
                primaryIsWhitelist: false,
                oppositeIds: ItemTypeWhitelistIds,
                oppositeIsWhitelist: true);

            _itemTypeMetadataSnapshotSignature = signature;
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
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            string term = filter.Trim();
            return name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || type.Contains(term, StringComparison.OrdinalIgnoreCase);
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
                ModTiers.TryAdd(compositeKey, defaultValue);
            }

            foreach ((string id, _, string type, int defaultValue) in AltarModsConstants.DownsideMods)
            {
                string compositeKey = BuildCompositeKey(type, id);
                ModTiers.TryAdd(compositeKey, defaultValue);
            }
            foreach ((string id, _, string type, int _) in AltarModsConstants.UpsideMods)
            {
                var compositeKey = BuildCompositeKey(type, id);
                bool isDivineOrbAlert = (type == AltarTypeMinion && id == "#% chance to drop an additional Divine Orb")
                    || (type == AltarTypeBoss && id == "Final Boss drops # additional Divine Orbs");

                if (ModAlerts.TryAdd(compositeKey, false) && isDivineOrbAlert)
                {
                    ModAlerts[compositeKey] = true;
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
