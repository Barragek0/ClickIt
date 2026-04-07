namespace ClickIt.UI.Settings.Panels
{
    internal sealed class MechanicsTablePanelRenderer(
        ClickItSettings settings,
        MechanicsEmbeddedSettingsPanelRenderer embeddedSettingsPanelRenderer)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly MechanicsEmbeddedSettingsPanelRenderer _embeddedSettingsPanelRenderer = embeddedSettingsPanelRenderer;

        private const string ClickMechanicsListId = "Click##Mechanics";
        private const string DontClickMechanicsListId = "DontClick##Mechanics";
        private const float ExpandedColumnWeight = 0.72f;
        private const float CollapsedColumnWeight = 0.28f;
        private readonly record struct MechanicsListRenderProgress(bool HasEntries, bool ShouldStopRendering);

        private static readonly MechanicToggleGroupEntry[] MechanicToggleGroups =
        [
            new("heist", "Heist"),
            new("doors", "Doors"),
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

        private static readonly HashSet<string> ComplexMechanicRowIds = new(StringComparer.OrdinalIgnoreCase)
        {
            MechanicIds.Items,
            MechanicIds.Essences,
            MechanicIds.Strongboxes,
            "heist",
            "doors",
            "basic-chests",
            "delve",
            "ultimatum",
            "altars"
        };

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

            FocusedMechanicsColumn? focusedColumn = GetFocusedMechanicsColumn();
            if (focusedColumn is not null)
            {
                DrawFocusedMechanicsTable(focusedColumn.Value);
                return;
            }

            (float leftColumnWeight, float rightColumnWeight) = GetMechanicsTableColumnWeights();

            SettingsUiRenderHelpers.DrawDualTransferTable(
                tableId: "MechanicsFilterLists",
                leftHeader: "Click",
                rightHeader: "Don't Click",
                leftBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                drawLeft: () => DrawMechanicsTableList(ClickMechanicsListId, moveToClick: false, SettingsUiPalette.WhitelistTextColor),
                drawRight: () => DrawMechanicsTableList(DontClickMechanicsListId, moveToClick: true, SettingsUiPalette.BlacklistTextColor),
                leftColumnWeight: leftColumnWeight,
                rightColumnWeight: rightColumnWeight);
        }

        private readonly record struct FocusedMechanicsColumn(string TableId, string Header, Vector4 Background, string ListId, bool MoveToClick, Vector4 TextColor);

        private void DrawFocusedMechanicsTable(FocusedMechanicsColumn focusedColumn)
        {
            SettingsUiRenderHelpers.DrawSingleTransferTable(
                tableId: focusedColumn.TableId,
                header: focusedColumn.Header,
                background: focusedColumn.Background,
                drawContent: () => DrawMechanicsTableList(focusedColumn.ListId, focusedColumn.MoveToClick, focusedColumn.TextColor));
        }

        private FocusedMechanicsColumn? GetFocusedMechanicsColumn()
        {
            if (!_settings.UiState.MechanicsAltarWeightTablesExpanded)
                return null;

            string expandedKey = _settings.UiState.ExpandedMechanicsTableRowId;
            if (string.IsNullOrWhiteSpace(expandedKey))
                return null;

            int separatorIndex = expandedKey.IndexOf(':');
            if (separatorIndex <= 0 || separatorIndex >= expandedKey.Length - 1)
                return null;

            string listId = expandedKey[..separatorIndex];
            string rowId = expandedKey[(separatorIndex + 1)..];
            if (!string.Equals(rowId, "altars", StringComparison.OrdinalIgnoreCase))
                return null;

            if (string.Equals(listId, ClickMechanicsListId, StringComparison.Ordinal))
            {
                return new FocusedMechanicsColumn(
                    "MechanicsFilterLists",
                    "Click",
                    new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                    ClickMechanicsListId,
                    MoveToClick: false,
                    SettingsUiPalette.WhitelistTextColor);
            }

            if (string.Equals(listId, DontClickMechanicsListId, StringComparison.Ordinal))
            {
                return new FocusedMechanicsColumn(
                    "MechanicsFilterLists",
                    "Don't Click",
                    new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                    DontClickMechanicsListId,
                    MoveToClick: true,
                    SettingsUiPalette.BlacklistTextColor);
            }

            return null;
        }

        private void DrawMechanicsTableList(string listId, bool moveToClick, Vector4 textColor)
        {
            ImGui.PushID(listId);

            IReadOnlyList<MechanicToggleTableEntry> entries = _settings.GetMechanicTableEntries();
            MechanicsListRenderProgress entryProgress = DrawStandaloneMechanicEntries(listId, entries, moveToClick, textColor);
            if (entryProgress.ShouldStopRendering)
            {
                ImGui.PopID();
                return;
            }

            MechanicsListRenderProgress groupProgress = DrawMechanicGroupRows(listId, entries, moveToClick, textColor);
            if (groupProgress.ShouldStopRendering)
            {
                ImGui.PopID();
                return;
            }

            SettingsUiRenderHelpers.DrawNoEntriesPlaceholder(entryProgress.HasEntries || groupProgress.HasEntries);

            ImGui.PopID();
        }

        private MechanicsListRenderProgress DrawStandaloneMechanicEntries(
            string listId,
            IReadOnlyList<MechanicToggleTableEntry> entries,
            bool moveToClick,
            Vector4 textColor)
        {
            bool hasEntries = false;

            foreach (MechanicToggleTableEntry entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.GroupId))
                    continue;
                if (!ClickItSettings.ShouldRenderMechanicEntry(entry, moveToClick, _settings.UiState.MechanicsSearchFilter))
                    continue;

                hasEntries = true;
                if (TryDrawMechanicEntryWithSubmenu(listId, entry, moveToClick, textColor, out bool shouldStopRendering))
                {
                    if (shouldStopRendering)
                        return new MechanicsListRenderProgress(hasEntries, ShouldStopRendering: true);

                    continue;
                }

                bool arrowClicked = SettingsUiRenderHelpers.DrawTransferListRow(listId, entry.Id, entry.DisplayName, moveToClick, textColor);
                if (!arrowClicked)
                    continue;

                entry.Node.Value = moveToClick;
                _settings.UiState.ExpandedMechanicsTableRowId = string.Empty;
                return new MechanicsListRenderProgress(hasEntries, ShouldStopRendering: true);
            }

            return new MechanicsListRenderProgress(hasEntries, ShouldStopRendering: false);
        }

        private MechanicsListRenderProgress DrawMechanicGroupRows(
            string listId,
            IReadOnlyList<MechanicToggleTableEntry> entries,
            bool moveToClick,
            Vector4 textColor)
        {
            bool hasEntries = false;

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
                    return new MechanicsListRenderProgress(hasEntries, ShouldStopRendering: true);
                }

                if (rowState.RowClicked)
                    ToggleExpandedMechanicTableRow(listId, group.Id);

                if (IsExpandedMechanicTableRow(listId, group.Id))
                    DrawMechanicGroupSubmenu(listId, group, entries);
            }

            return new MechanicsListRenderProgress(hasEntries, ShouldStopRendering: false);
        }

        private bool TryDrawMechanicEntryWithSubmenu(string listId, MechanicToggleTableEntry entry, bool moveToClick, Vector4 textColor, out bool shouldStopRendering)
        {
            shouldStopRendering = false;

            if (!HasMechanicEntrySubmenu(entry.Id))
                return false;

            SettingsUiRenderHelpers.ExpandableTransferRowState rowState = DrawMechanicEntryRow(listId, entry, moveToClick, textColor);
            if (rowState.ArrowClicked)
            {
                entry.Node.Value = moveToClick;
                _settings.UiState.ExpandedMechanicsTableRowId = string.Empty;
                shouldStopRendering = true;
                return true;
            }

            if (rowState.RowClicked)
            {
                ToggleExpandedMechanicTableRow(listId, entry.Id);
            }

            if (IsExpandedMechanicTableRow(listId, entry.Id))
            {
                DrawMechanicEntrySubmenu(entry.Id);
            }

            return true;
        }

        private SettingsUiRenderHelpers.ExpandableTransferRowState DrawMechanicEntryRow(string listId, MechanicToggleTableEntry entry, bool moveToClick, Vector4 textColor)
        {
            string label = $"{entry.DisplayName} [v]##{listId}_{entry.Id}";
            return SettingsUiRenderHelpers.DrawExpandableTransferListRow(
                $"Move_{listId}_{entry.Id}",
                label,
                IsExpandedMechanicTableRow(listId, entry.Id),
                moveToClick,
                textColor);
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
                _embeddedSettingsPanelRenderer.DrawMechanicGroupExtraSettings(group.Id);
                ImGui.Unindent();
                return;
            }

            List<MechanicToggleTableEntry> groupedEntries = CollectMechanicGroupEntries(entries, group.Id, out bool hasSubgroups);
            if (hasSubgroups)
            {
                DrawGroupedMechanicSubmenu(listId, group, groupedEntries);
                _embeddedSettingsPanelRenderer.DrawMechanicGroupExtraSettings(group.Id);
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

            _embeddedSettingsPanelRenderer.DrawMechanicGroupExtraSettings(group.Id);

            ImGui.Unindent();
        }

        private void DrawGroupedMechanicSubmenu(string listId, MechanicToggleGroupEntry group, IReadOnlyList<MechanicToggleTableEntry> entries)
        {
            DrawMechanicSubgroups(
                listId,
                group.Id,
                group.DisplayName,
                entries,
                subgroupName => _embeddedSettingsPanelRenderer.DrawMechanicSubgroupExtraSettings(group.Id, subgroupName));
        }

        private void DrawLeagueChestGroupSubmenu(string listId, MechanicToggleGroupEntry group, IReadOnlyList<MechanicToggleTableEntry> entries)
        {
            SettingsUiRenderHelpers.DrawWrappedText(
                "I'll add more specific league chests to this list in future releases of the plugin.",
                new Vector4(0.65f, 0.65f, 0.65f, 1f));
            ImGui.Spacing();

            List<MechanicToggleTableEntry> leagueEntries = CollectMechanicGroupEntries(entries, group.Id, out _);
            DrawMechanicSubgroups(listId, group.Id, group.DisplayName, leagueEntries);
        }

        private void DrawMechanicSubgroups(
            string listId,
            string groupId,
            string groupDisplayName,
            IReadOnlyList<MechanicToggleTableEntry> entries,
            Action<string>? drawSubgroupExtraSettings = null)
        {
            string[] subgroupNames = GetOrderedSubgroupNames(entries);
            bool searchIsEmpty = string.IsNullOrWhiteSpace(_settings.UiState.MechanicsSearchFilter);

            foreach (string subgroupName in subgroupNames)
            {
                MechanicToggleTableEntry[] subgroupEntries = GetSubgroupEntries(entries, subgroupName);
                if (!TryBeginMechanicSubgroupTree(listId, groupId, subgroupName, subgroupEntries, searchIsEmpty, out bool subgroupMatchesSearch))
                    continue;

                DrawMechanicSubgroupEntries(listId, groupId, subgroupEntries, searchIsEmpty, subgroupMatchesSearch);
                drawSubgroupExtraSettings?.Invoke(subgroupName);
                ImGui.TreePop();
            }

            DrawUngroupedMechanicEntries(listId, groupId, groupDisplayName, entries, searchIsEmpty);
        }

        private static List<MechanicToggleTableEntry> CollectMechanicGroupEntries(
            IReadOnlyList<MechanicToggleTableEntry> entries,
            string groupId,
            out bool hasSubgroups)
        {
            var groupedEntries = new List<MechanicToggleTableEntry>();
            hasSubgroups = false;

            for (int i = 0; i < entries.Count; i++)
            {
                MechanicToggleTableEntry entry = entries[i];
                if (!string.Equals(entry.GroupId, groupId, StringComparison.OrdinalIgnoreCase))
                    continue;

                groupedEntries.Add(entry);
                if (!hasSubgroups && !string.IsNullOrWhiteSpace(entry.Subgroup))
                    hasSubgroups = true;
            }

            return groupedEntries;
        }

        private static string[] GetOrderedSubgroupNames(IReadOnlyList<MechanicToggleTableEntry> entries)
        {
            var subgroupNames = new List<string>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < entries.Count; i++)
            {
                string? subgroupName = entries[i].Subgroup;
                if (string.IsNullOrWhiteSpace(subgroupName) || !seenNames.Add(subgroupName))
                    continue;

                subgroupNames.Add(subgroupName);
            }

            subgroupNames.Sort(StringComparer.OrdinalIgnoreCase);
            return [.. subgroupNames];
        }

        private static MechanicToggleTableEntry[] GetSubgroupEntries(IReadOnlyList<MechanicToggleTableEntry> entries, string subgroupName)
        {
            var subgroupEntries = new List<MechanicToggleTableEntry>();

            for (int i = 0; i < entries.Count; i++)
            {
                MechanicToggleTableEntry entry = entries[i];
                if (string.Equals(entry.Subgroup, subgroupName, StringComparison.OrdinalIgnoreCase))
                    subgroupEntries.Add(entry);
            }

            return [.. subgroupEntries];
        }

        private bool TryBeginMechanicSubgroupTree(
            string listId,
            string groupId,
            string subgroupName,
            MechanicToggleTableEntry[] subgroupEntries,
            bool searchIsEmpty,
            out bool subgroupMatchesSearch)
        {
            subgroupMatchesSearch = SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.MechanicsSearchFilter, subgroupName);
            bool hasEntryMatch = HasMechanicSubgroupEntryMatch(subgroupEntries);
            if (!searchIsEmpty && !subgroupMatchesSearch && !hasEntryMatch)
                return false;

            return ImGui.TreeNodeEx($"{subgroupName}##MechanicSubmenu_{listId}_{groupId}_{subgroupName}", ImGuiTreeNodeFlags.DefaultOpen);
        }

        private bool HasMechanicSubgroupEntryMatch(IReadOnlyList<MechanicToggleTableEntry> subgroupEntries)
        {
            for (int i = 0; i < subgroupEntries.Count; i++)
            {
                if (SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.MechanicsSearchFilter, subgroupEntries[i].DisplayName))
                    return true;
            }

            return false;
        }

        private void DrawMechanicSubgroupEntries(
            string listId,
            string groupId,
            MechanicToggleTableEntry[] subgroupEntries,
            bool searchIsEmpty,
            bool subgroupMatchesSearch)
        {
            for (int i = 0; i < subgroupEntries.Length; i++)
            {
                MechanicToggleTableEntry entry = subgroupEntries[i];
                if (!searchIsEmpty && !subgroupMatchesSearch && !SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.MechanicsSearchFilter, entry.DisplayName))
                    continue;

                DrawMechanicSubmenuCheckbox(listId, groupId, entry);
            }
        }

        private void DrawUngroupedMechanicEntries(
            string listId,
            string groupId,
            string groupDisplayName,
            IReadOnlyList<MechanicToggleTableEntry> entries,
            bool searchIsEmpty)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                MechanicToggleTableEntry entry = entries[i];
                if (!string.IsNullOrWhiteSpace(entry.Subgroup))
                    continue;

                if (!searchIsEmpty
                    && !SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.MechanicsSearchFilter, entry.DisplayName)
                    && !SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.MechanicsSearchFilter, groupDisplayName))
                {
                    continue;
                }

                DrawMechanicSubmenuCheckbox(listId, groupId, entry);
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

            if (string.Equals(entry.Id, MechanicIds.HeistHazards, StringComparison.OrdinalIgnoreCase))
            {
                SettingsUiRenderHelpers.DrawInlineTooltip("Hazards are objects that block your path and must be destroyed to get past.");
            }
        }

        private void ResetMechanicsTableDefaults()
        {
            foreach (MechanicToggleTableEntry entry in _settings.GetMechanicTableEntries())
            {
                entry.Node.Value = entry.DefaultEnabled;
            }
        }

        private bool HasMechanicEntrySubmenu(string entryId)
            => string.Equals(entryId, MechanicIds.Items, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entryId, MechanicIds.Essences, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entryId, MechanicIds.Strongboxes, StringComparison.OrdinalIgnoreCase);

        private void DrawMechanicEntrySubmenu(string entryId)
            => _embeddedSettingsPanelRenderer.DrawMechanicEntrySubmenu(entryId);

        private (float LeftColumnWeight, float RightColumnWeight) GetMechanicsTableColumnWeights()
        {
            string expandedKey = _settings.UiState.ExpandedMechanicsTableRowId;
            if (string.IsNullOrWhiteSpace(expandedKey))
                return (0.5f, 0.5f);

            int separatorIndex = expandedKey.IndexOf(':');
            if (separatorIndex <= 0 || separatorIndex >= expandedKey.Length - 1)
                return (0.5f, 0.5f);

            string listId = expandedKey[..separatorIndex];
            string rowId = expandedKey[(separatorIndex + 1)..];
            if (!ComplexMechanicRowIds.Contains(rowId))
                return (0.5f, 0.5f);

            if (string.Equals(rowId, "altars", StringComparison.OrdinalIgnoreCase)
                && _settings.UiState.MechanicsAltarWeightTablesExpanded)
            {
                if (string.Equals(listId, ClickMechanicsListId, StringComparison.Ordinal))
                    return (1f, 0f);

                if (string.Equals(listId, DontClickMechanicsListId, StringComparison.Ordinal))
                    return (0f, 1f);
            }

            if (string.Equals(listId, ClickMechanicsListId, StringComparison.Ordinal))
                return (ExpandedColumnWeight, CollapsedColumnWeight);

            if (string.Equals(listId, DontClickMechanicsListId, StringComparison.Ordinal))
                return (CollapsedColumnWeight, ExpandedColumnWeight);

            return (0.5f, 0.5f);
        }

        private bool IsExpandedMechanicTableRow(string listId, string rowId)
            => string.Equals(_settings.UiState.ExpandedMechanicsTableRowId, SettingsUiRenderHelpers.BuildExpandedRowKey(listId, rowId), StringComparison.Ordinal);

        private void ToggleExpandedMechanicTableRow(string listId, string rowId)
        {
            _settings.UiState.ExpandedMechanicsTableRowId = SettingsUiRenderHelpers.ToggleExpandedRowKey(_settings.UiState.ExpandedMechanicsTableRowId, listId, rowId);
        }

    }
}