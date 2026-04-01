using ClickIt.Definitions;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using System.Linq;
using System.Numerics;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private const string LeagueChestSubgroupMirage = "Mirage";
        private const string LeagueChestSubgroupHeist = "Heist";
        private const string LeagueChestSubgroupBlight = "Blight";
        private const string LeagueChestSubgroupBreach = "Breach";
        private const string LeagueChestSubgroupSynthesis = "Synthesis";

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

        private readonly struct MechanicGroupRowRenderState(bool rowClicked, bool arrowClicked)
        {
            public bool RowClicked { get; } = rowClicked;
            public bool ArrowClicked { get; } = arrowClicked;
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

            DrawSearchBar("##MechanicsSearch", "Clear##MechanicsSearchClear", ref UiState.MechanicsSearchFilter);
            if (DrawResetDefaultsButton("Reset Defaults##MechanicsResetDefaults"))
            {
                ResetMechanicsTableDefaults();
                UiState.ExpandedMechanicsTableRowId = string.Empty;
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
                if (!ShouldRenderMechanicEntry(entry, moveToClick, UiState.MechanicsSearchFilter))
                    continue;

                hasEntries = true;
                bool arrowClicked = DrawTransferListRow(listId, entry.Id, entry.DisplayName, moveToClick, textColor);
                if (arrowClicked)
                {
                    entry.Node.Value = moveToClick;
                    UiState.ExpandedMechanicsTableRowId = string.Empty;
                    break;
                }
            }

            foreach (MechanicToggleGroupEntry group in MechanicToggleGroups)
            {
                if (!ShouldRenderMechanicGroup(group, entries, moveToClick, UiState.MechanicsSearchFilter))
                    continue;

                hasEntries = true;
                MechanicGroupRowRenderState rowState = DrawMechanicGroupRow(listId, group, moveToClick, textColor);
                if (rowState.ArrowClicked)
                {
                    SetMechanicGroupState(group.Id, entries, moveToClick);
                    UiState.ExpandedMechanicsTableRowId = string.Empty;
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
                if (!MatchesMechanicsSearch(entry.DisplayName, UiState.MechanicsSearchFilter) && !MatchesMechanicsSearch(group.DisplayName, UiState.MechanicsSearchFilter))
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

            bool searchIsEmpty = string.IsNullOrWhiteSpace(UiState.MechanicsSearchFilter);

            foreach (string subgroupName in subgroupNames)
            {
                MechanicToggleTableEntry[] subgroupEntries = leagueEntries
                    .Where(e => string.Equals(e.Subgroup, subgroupName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                bool subgroupMatchesSearch = MatchesMechanicsSearch(subgroupName, UiState.MechanicsSearchFilter);
                bool hasEntryMatch = subgroupEntries.Any(e => MatchesMechanicsSearch(e.DisplayName, UiState.MechanicsSearchFilter));
                if (!searchIsEmpty && !subgroupMatchesSearch && !hasEntryMatch)
                    continue;

                bool isOpen = ImGui.TreeNodeEx($"{subgroupName}##MechanicSubmenu_{listId}_{group.Id}_{subgroupName}", ImGuiTreeNodeFlags.DefaultOpen);
                if (!isOpen)
                    continue;

                for (int i = 0; i < subgroupEntries.Length; i++)
                {
                    MechanicToggleTableEntry entry = subgroupEntries[i];
                    if (!searchIsEmpty && !subgroupMatchesSearch && !MatchesMechanicsSearch(entry.DisplayName, UiState.MechanicsSearchFilter))
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
                    && !MatchesMechanicsSearch(entry.DisplayName, UiState.MechanicsSearchFilter)
                    && !MatchesMechanicsSearch(group.DisplayName, UiState.MechanicsSearchFilter))
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

        internal static bool ShouldRenderMechanicEntry(MechanicToggleTableEntry entry, bool moveToClick, string filter)
        {
            bool inSourceSet = moveToClick ? !entry.Node.Value : entry.Node.Value;
            return inSourceSet && MatchesMechanicsSearch(entry.DisplayName, filter);
        }

        internal static bool ShouldRenderMechanicGroup(MechanicToggleGroupEntry group, IReadOnlyList<MechanicToggleTableEntry> entries, bool moveToClick, string filter)
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
            return string.Equals(UiState.ExpandedMechanicsTableRowId, BuildExpandedMechanicTableRowKey(listId, rowId), StringComparison.Ordinal);
        }

        private void ToggleExpandedMechanicTableRow(string listId, string rowId)
        {
            string rowKey = BuildExpandedMechanicTableRowKey(listId, rowId);
            if (string.Equals(UiState.ExpandedMechanicsTableRowId, rowKey, StringComparison.Ordinal))
            {
                UiState.ExpandedMechanicsTableRowId = string.Empty;
            }
            else
            {
                UiState.ExpandedMechanicsTableRowId = rowKey;
            }
        }

        internal static void SetMechanicGroupState(string groupId, IReadOnlyList<MechanicToggleTableEntry> entries, bool enabled)
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

        internal IReadOnlyList<MechanicToggleTableEntry> GetMechanicTableEntries()
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
    }
}
