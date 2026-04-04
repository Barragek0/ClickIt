namespace ClickIt.UI.Settings.Panels
{
    internal sealed class MechanicsTablePanelRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

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

        private const string LeagueChestSubgroupMirage = "Mirage";
        private const string LeagueChestSubgroupHeist = "Heist";
        private const string LeagueChestSubgroupBlight = "Blight";
        private const string LeagueChestSubgroupBreach = "Breach";
        private const string LeagueChestSubgroupSynthesis = "Synthesis";

        public void Draw()
        {
            SettingsUiRenderHelpers.DrawInstructionText("Table rows with [v] next to them can be clicked to open submenus.");

            ImGui.Spacing();

            SettingsUiRenderHelpers.DrawSearchBar("##MechanicsSearch", "Clear##MechanicsSearchClear", ref _settings.UiState.MechanicsSearchFilter);
            if (SettingsUiRenderHelpers.DrawResetDefaultsButton("Reset Defaults##MechanicsResetDefaults"))
            {
                ResetMechanicsTableDefaults();
                _settings.UiState.ExpandedMechanicsTableRowId = string.Empty;
            }

            ImGui.Spacing();

            SettingsUiRenderHelpers.DrawDualTransferTable(
                tableId: "MechanicsFilterLists",
                leftHeader: "Click",
                rightHeader: "Don't Click",
                leftBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                drawLeft: () => DrawMechanicsTableList("Click##Mechanics", moveToClick: false, SettingsUiPalette.WhitelistTextColor),
                drawRight: () => DrawMechanicsTableList("DontClick##Mechanics", moveToClick: true, SettingsUiPalette.BlacklistTextColor));
        }

        private void DrawMechanicsTableList(string listId, bool moveToClick, Vector4 textColor)
        {
            ImGui.PushID(listId);

            IReadOnlyList<MechanicToggleTableEntry> entries = _settings.GetMechanicTableEntries();
            bool hasEntries = false;

            foreach (MechanicToggleTableEntry entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.GroupId))
                    continue;
                if (!ClickItSettings.ShouldRenderMechanicEntry(entry, moveToClick, _settings.UiState.MechanicsSearchFilter))
                    continue;

                hasEntries = true;
                bool arrowClicked = SettingsUiRenderHelpers.DrawTransferListRow(listId, entry.Id, entry.DisplayName, moveToClick, textColor);
                if (arrowClicked)
                {
                    entry.Node.Value = moveToClick;
                    _settings.UiState.ExpandedMechanicsTableRowId = string.Empty;
                    break;
                }
            }

            foreach (MechanicToggleGroupEntry group in MechanicToggleGroups)
            {
                if (!ClickItSettings.ShouldRenderMechanicGroup(group, entries, moveToClick, _settings.UiState.MechanicsSearchFilter))
                    continue;

                hasEntries = true;
                SettingsUiRenderHelpers.ExpandableTransferRowState rowState = DrawMechanicGroupRow(listId, group, moveToClick, textColor);
                if (rowState.ArrowClicked)
                {
                    ClickItSettings.SetMechanicGroupState(group.Id, entries, moveToClick);
                    _settings.UiState.ExpandedMechanicsTableRowId = string.Empty;
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

            SettingsUiRenderHelpers.DrawNoEntriesPlaceholder(hasEntries);

            ImGui.PopID();
        }

        private SettingsUiRenderHelpers.ExpandableTransferRowState DrawMechanicGroupRow(string listId, MechanicToggleGroupEntry group, bool moveToClick, Vector4 textColor)
        {
            string label = $"{group.DisplayName} [v]##{listId}_{group.Id}";
            return SettingsUiRenderHelpers.DrawExpandableTransferListRow(
                $"MoveGroup_{listId}_{group.Id}",
                label,
                IsExpandedMechanicTableRow(listId, group.Id),
                moveToClick,
                textColor);
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
                if (!SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.MechanicsSearchFilter, entry.DisplayName, group.DisplayName))
                    continue;

                bool enabled = entry.Node.Value;
                Vector4 rowColor = enabled ? SettingsUiPalette.WhitelistTextColor : SettingsUiPalette.BlacklistTextColor;
                if (SettingsUiRenderHelpers.DrawCheckbox($"{entry.DisplayName}##MechanicSubmenu_{listId}_{group.Id}_{entry.Id}", ref enabled, rowColor))
                {
                    entry.Node.Value = enabled;
                }
            }

            DrawMechanicGroupExtraSettings(group.Id);

            ImGui.Unindent();
        }

        private void DrawLeagueChestGroupSubmenu(string listId, MechanicToggleGroupEntry group, IReadOnlyList<MechanicToggleTableEntry> entries)
        {
            SettingsUiRenderHelpers.DrawWrappedText(
                "I'll add more specific league chests to this list in future releases of the plugin.",
                new Vector4(0.65f, 0.65f, 0.65f, 1f));
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

            bool searchIsEmpty = string.IsNullOrWhiteSpace(_settings.UiState.MechanicsSearchFilter);

            foreach (string subgroupName in subgroupNames)
            {
                MechanicToggleTableEntry[] subgroupEntries = leagueEntries
                    .Where(e => string.Equals(e.Subgroup, subgroupName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                bool subgroupMatchesSearch = SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.MechanicsSearchFilter, subgroupName);
                bool hasEntryMatch = subgroupEntries.Any(e => SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.MechanicsSearchFilter, e.DisplayName));
                if (!searchIsEmpty && !subgroupMatchesSearch && !hasEntryMatch)
                    continue;

                bool isOpen = ImGui.TreeNodeEx($"{subgroupName}##MechanicSubmenu_{listId}_{group.Id}_{subgroupName}", ImGuiTreeNodeFlags.DefaultOpen);
                if (!isOpen)
                    continue;

                for (int i = 0; i < subgroupEntries.Length; i++)
                {
                    MechanicToggleTableEntry entry = subgroupEntries[i];
                    if (!searchIsEmpty && !subgroupMatchesSearch && !SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.MechanicsSearchFilter, entry.DisplayName))
                        continue;

                    DrawMechanicSubmenuCheckbox(listId, group.Id, entry);
                }

                ImGui.TreePop();
            }

            MechanicToggleTableEntry[] ungroupedEntries = leagueEntries.Where(static e => string.IsNullOrWhiteSpace(e.Subgroup)).ToArray();
            for (int i = 0; i < ungroupedEntries.Length; i++)
            {
                MechanicToggleTableEntry entry = ungroupedEntries[i];
                if (!searchIsEmpty
                    && !SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.MechanicsSearchFilter, entry.DisplayName)
                    && !SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.MechanicsSearchFilter, group.DisplayName))
                {
                    continue;
                }

                DrawMechanicSubmenuCheckbox(listId, group.Id, entry);
            }
        }

        private void DrawMechanicSubmenuCheckbox(string listId, string groupId, MechanicToggleTableEntry entry)
        {
            bool enabled = entry.Node.Value;
            Vector4 rowColor = enabled ? SettingsUiPalette.WhitelistTextColor : SettingsUiPalette.BlacklistTextColor;
            if (SettingsUiRenderHelpers.DrawCheckbox($"{entry.DisplayName}##MechanicSubmenu_{listId}_{groupId}_{entry.Id}", ref enabled, rowColor))
            {
                entry.Node.Value = enabled;
            }
        }

        private void DrawMechanicGroupExtraSettings(string groupId)
        {
            if (string.Equals(groupId, "basic-chests", StringComparison.OrdinalIgnoreCase))
            {
                DrawChestDropSettleSettings(new ChestDropSettleSettingsDescriptor("Basic Chest", "BasicChests", _settings.PauseAfterOpeningBasicChests, _settings.PauseAfterOpeningBasicChestsInitialDelayMs, _settings.PauseAfterOpeningBasicChestsPollIntervalMs, _settings.PauseAfterOpeningBasicChestsQuietWindowMs));
                return;
            }

            if (string.Equals(groupId, "league-chests", StringComparison.OrdinalIgnoreCase))
            {
                DrawChestDropSettleSettings(new ChestDropSettleSettingsDescriptor("League Mechanic Chest", "LeagueChests", _settings.PauseAfterOpeningLeagueChests, _settings.PauseAfterOpeningLeagueChestsInitialDelayMs, _settings.PauseAfterOpeningLeagueChestsPollIntervalMs, _settings.PauseAfterOpeningLeagueChestsQuietWindowMs));
            }
        }

        private void DrawChestDropSettleSettings(ChestDropSettleSettingsDescriptor descriptor)
        {
            ImGui.Spacing();
            SettingsUiRenderHelpers.DrawToggleNodeControl($"Wait for Drops to Settle##{descriptor.IdPrefix}PauseEnabled", descriptor.PauseNode, $"When enabled, ClickIt waits for new loot labels after opening a {descriptor.LabelPrefix} before resuming clicks.");
            SettingsUiRenderHelpers.DrawToggleAndRangeNodeControls(
                $"Allow Nearby Mechanics while Waiting##{descriptor.IdPrefix}AllowNearbyMechanics",
                _settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle,
                "When enabled, nearby mechanics around the opened chest can still be clicked while drops are settling.",
                $"Nearby mechanic distance##{descriptor.IdPrefix}AllowNearbyMechanicsDistance",
                _settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettleDistance,
                0,
                100,
                "Maximum distance from the opened chest where mechanics are still allowed during settle wait.");
            SettingsUiRenderHelpers.DrawRangeNodeControl($"Initial delay (ms)##{descriptor.IdPrefix}InitialDelayMs", descriptor.InitialDelayNode, 100, 1500, "How long to wait after click confirmation before checking for new labels.");
            SettingsUiRenderHelpers.DrawRangeNodeControl($"Poll interval (ms)##{descriptor.IdPrefix}PollIntervalMs", descriptor.PollIntervalNode, 50, 500, "How frequently ClickIt checks ItemsOnGroundLabels for newly added drops.");
            SettingsUiRenderHelpers.DrawRangeNodeControl($"Quiet window (ms)##{descriptor.IdPrefix}QuietWindowMs", descriptor.QuietWindowNode, 100, 2000, "Loot is considered settled after this many milliseconds pass without new labels.");
        }

        private void ResetMechanicsTableDefaults()
        {
            foreach (MechanicToggleTableEntry entry in _settings.GetMechanicTableEntries())
            {
                entry.Node.Value = entry.DefaultEnabled;
            }
        }

        private bool IsExpandedMechanicTableRow(string listId, string rowId)
            => string.Equals(_settings.UiState.ExpandedMechanicsTableRowId, SettingsUiRenderHelpers.BuildExpandedRowKey(listId, rowId), StringComparison.Ordinal);

        private void ToggleExpandedMechanicTableRow(string listId, string rowId)
        {
            _settings.UiState.ExpandedMechanicsTableRowId = SettingsUiRenderHelpers.ToggleExpandedRowKey(_settings.UiState.ExpandedMechanicsTableRowId, listId, rowId);
        }

    }
}