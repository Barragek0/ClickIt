namespace ClickIt.UI.Settings.Panels
{
    internal sealed class UltimatumSettingsPanelRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly record struct TakeRewardListRenderProgress(bool HasEntries, bool ShouldStopRendering);

        private static UltimatumModifierGroupEntry[] UltimatumModifierGroups => UltimatumModifierGroupCatalog.Groups;
        private static HashSet<string> UltimatumTieredModifierNames => UltimatumModifierGroupCatalog.TieredModifierNames;

        public void DrawModifierTablePanel(bool embedded = false)
        {
            _settings.EnsureUltimatumModifiersInitialized();

            DrawModifierPriorityControls();

            if (!embedded)
                DrawModifierPriorityInstructions();

            DrawModifierPriorityTable();
        }

        private void DrawModifierPriorityControls()
        {

            SettingsUiRenderHelpers.DrawSearchBar("##UltimatumSearch", "Clear##UltimatumSearchClear", ref _settings.UiState.UltimatumSearchFilter);
            if (SettingsUiRenderHelpers.DrawResetDefaultsButton("Reset Defaults##UltimatumResetDefaults"))
                _settings.ResetUltimatumModifierPriorityDefaults();

            ImGui.Spacing();
        }

        private static void DrawModifierPriorityInstructions()
        {
            SettingsUiRenderHelpers.DrawInstructionText("Priority: top row is highest, bottom row is lowest.");
            SettingsUiRenderHelpers.DrawWrappedText(
                "Example: if this table has Resistant Monsters above Reduced Recovery above Ruin, and those three are offered, Resistant Monsters is selected.",
                new Vector4(0.7f, 0.7f, 0.7f, 1f));
            ImGui.Spacing();
        }

        private void DrawModifierPriorityTable()
        {
            float tableWidth = SystemMath.Min(600f, SystemMath.Max(100f, ImGui.GetContentRegionAvail().X));
            if (!SettingsUiRenderHelpers.BeginSingleColumnPriorityTable("UltimatumModifierPriorityTable", "Modifiers", tableWidth))
                return;

            try
            {
                DrawModifierPriorityRows();
            }
            finally
            {
                ImGui.EndTable();
            }
        }

        private void DrawModifierPriorityRows()
        {
            for (int i = 0; i < _settings.UltimatumModifierPriority.Count; i++)
            {
                string modifier = _settings.UltimatumModifierPriority[i];
                if (!ShouldRenderModifierPriorityRow(modifier))
                    continue;

                DrawModifierPriorityRow(i, modifier);
            }
        }

        private bool ShouldRenderModifierPriorityRow(string modifier)
            => SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.UltimatumSearchFilter, modifier);

        private bool DrawModifierPriorityRow(int index, string modifier)
        {
            BeginModifierPriorityRow(index);

            if (TryMoveModifierPriority(index, moveUp: true))
                return true;

            ImGui.SameLine();

            if (TryMoveModifierPriority(index, moveUp: false))
                return true;

            ImGui.SameLine();
            _ = SettingsUiRenderHelpers.DrawSelectableText(
                $"{modifier}##UltimatumModifier_{index}",
                false,
                ImGuiSelectableFlags.None,
                new Vector4(0.95f, 0.95f, 0.95f, 1f),
                new NumVector2(0, 0));

            DrawModifierPriorityDescriptionWhenHovered(modifier);
            return false;
        }

        private void BeginModifierPriorityRow(int index)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);

            Vector4 priorityColor = SettingsUiRenderHelpers.GetUltimatumPriorityRowColor(index, _settings.UltimatumModifierPriority.Count);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(priorityColor));
        }

        private bool TryMoveModifierPriority(int index, bool moveUp)
        {
            int targetIndex = moveUp ? index - 1 : index + 1;
            ImGuiDir direction = moveUp ? ImGuiDir.Up : ImGuiDir.Down;
            string buttonId = moveUp ? $"UltimatumUp_{index}" : $"UltimatumDown_{index}";
            bool enabled = moveUp
                ? index > 0
                : index < _settings.UltimatumModifierPriority.Count - 1;

            if (!SettingsUiRenderHelpers.DrawArrowButton(direction, buttonId, enabled))
                return false;

            (_settings.UltimatumModifierPriority[index], _settings.UltimatumModifierPriority[targetIndex]) =
                (_settings.UltimatumModifierPriority[targetIndex], _settings.UltimatumModifierPriority[index]);
            return true;
        }

        private static void DrawModifierPriorityDescriptionWhenHovered(string modifier)
        {
            if (!ImGui.IsItemHovered())
                return;

            string description = UltimatumModifiersConstants.GetDescription(modifier);
            if (string.IsNullOrWhiteSpace(description))
                return;

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            SettingsUiRenderHelpers.DrawWrappedText(description, new Vector4(0.65f, 0.65f, 0.65f, 1f));
        }

        public void DrawTakeRewardModifierTablePanel(bool embedded = false)
        {
            _settings.EnsureUltimatumTakeRewardModifiersInitialized();

            _ = UltimatumGruelingGauntletDetectionStore.TryGet(out _);
            if (!embedded)
            {
                SettingsUiRenderHelpers.DrawInstructionText("This table only does anything when Gruelling Gauntlet is allocated.");
                SettingsUiRenderHelpers.DrawInstructionText("Rows with [v] can be clicked to open stage-specific submenu options.");
                ImGui.Spacing();
            }

            SettingsUiRenderHelpers.DrawSearchBar("##UltimatumTakeRewardSearch", "Clear##UltimatumTakeRewardSearchClear", ref _settings.UiState.UltimatumTakeRewardSearchFilter);
            if (SettingsUiRenderHelpers.DrawResetDefaultsButton("Reset Defaults##UltimatumTakeRewardResetDefaults"))
                _settings.ResetUltimatumTakeRewardModifierDefaults();


            ImGui.Spacing();

            SettingsUiRenderHelpers.DrawDualTransferTable(
                tableId: "UltimatumTakeRewardLists",
                leftHeader: "Take Reward",
                rightHeader: "Keep Going",
                leftBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                drawLeft: () => DrawTakeRewardList("TakeReward##Ultimatum", _settings.UltimatumTakeRewardModifierNames, moveToTakeReward: false, textColor: SettingsUiPalette.BlacklistTextColor),
                drawRight: () => DrawTakeRewardList("Continue##Ultimatum", _settings.UltimatumContinueModifierNames, moveToTakeReward: true, textColor: SettingsUiPalette.WhitelistTextColor));
        }

        private void DrawTakeRewardList(string id, HashSet<string> sourceSet, bool moveToTakeReward, Vector4 textColor)
        {
            ImGui.PushID(id);

            string filter = _settings.UiState.UltimatumTakeRewardSearchFilter;
            TakeRewardListRenderProgress ungroupedProgress = DrawUngroupedTakeRewardModifierRows(id, sourceSet, filter, moveToTakeReward, textColor);
            if (ungroupedProgress.ShouldStopRendering)
            {
                ImGui.PopID();
                return;
            }

            TakeRewardListRenderProgress groupedProgress = DrawGroupedTakeRewardModifierRows(id, sourceSet, filter, moveToTakeReward, textColor);
            if (groupedProgress.ShouldStopRendering)
            {
                ImGui.PopID();
                return;
            }

            SettingsUiRenderHelpers.DrawNoEntriesPlaceholder(ungroupedProgress.HasEntries || groupedProgress.HasEntries);
            ImGui.PopID();
        }

        private TakeRewardListRenderProgress DrawUngroupedTakeRewardModifierRows(string listId, HashSet<string> sourceSet, string filter, bool moveToTakeReward, Vector4 textColor)
        {
            bool hasEntries = false;
            foreach (string modifier in UltimatumModifiersConstants.AllModifierNamesWithStages)
            {
                if (!ShouldRenderUngroupedTakeRewardModifier(sourceSet, filter, modifier))
                    continue;

                hasEntries = true;
                if (TryHandleTakeRewardModifierRow(listId, modifier, moveToTakeReward, textColor))
                    return new TakeRewardListRenderProgress(hasEntries, ShouldStopRendering: true);
            }

            return new TakeRewardListRenderProgress(hasEntries, ShouldStopRendering: false);
        }

        private TakeRewardListRenderProgress DrawGroupedTakeRewardModifierRows(string listId, HashSet<string> sourceSet, string filter, bool moveToTakeReward, Vector4 textColor)
        {
            bool hasEntries = false;
            foreach (UltimatumModifierGroupEntry group in UltimatumModifierGroups)
            {
                if (!ShouldRenderUltimatumModifierGroup(group, sourceSet, filter))
                    continue;

                hasEntries = true;
                if (TryHandleTakeRewardModifierGroupRow(listId, group, moveToTakeReward, textColor))
                    return new TakeRewardListRenderProgress(hasEntries, ShouldStopRendering: true);

                DrawExpandedUltimatumModifierGroupSubmenu(listId, group, moveToTakeReward);
            }

            return new TakeRewardListRenderProgress(hasEntries, ShouldStopRendering: false);
        }

        private static bool ShouldRenderUngroupedTakeRewardModifier(HashSet<string> sourceSet, string filter, string modifier)
            => !UltimatumTieredModifierNames.Contains(modifier)
                && sourceSet.Contains(modifier)
                && SettingsUiRenderHelpers.MatchesSearch(filter, modifier);

        private void DrawExpandedUltimatumModifierGroupSubmenu(string listId, UltimatumModifierGroupEntry group, bool moveToTakeReward)
        {
            if (!IsExpandedUltimatumTakeRewardRow(listId, group.Id))
                return;

            DrawUltimatumModifierGroupSubmenu(listId, group, moveToTakeReward);
        }

        private bool TryHandleTakeRewardModifierRow(string listId, string modifier, bool moveToTakeReward, Vector4 textColor)
        {
            bool arrowClicked = SettingsUiRenderHelpers.DrawTransferListRow(listId, modifier, modifier, moveToTakeReward, textColor);
            DrawTakeRewardDescriptionWhenHovered(modifier);
            if (!arrowClicked)
                return false;

            MoveUltimatumTakeRewardModifier(modifier, moveToTakeReward);
            ClearExpandedUltimatumTakeRewardRow();
            return true;
        }

        private bool TryHandleTakeRewardModifierGroupRow(string listId, UltimatumModifierGroupEntry group, bool moveToTakeReward, Vector4 textColor)
        {
            SettingsUiRenderHelpers.ExpandableTransferRowState rowState = DrawUltimatumModifierGroupRow(listId, group, moveToTakeReward, textColor);
            if (rowState.ArrowClicked)
            {
                SetUltimatumModifierGroupState(group, moveToTakeReward);
                ClearExpandedUltimatumTakeRewardRow();
                return true;
            }

            if (rowState.RowClicked)
                ToggleExpandedUltimatumTakeRewardRow(listId, group.Id);

            if (rowState.RowHovered || rowState.ArrowHovered)
                DrawTakeRewardDescription(group.DisplayName);

            return false;
        }

        private static void DrawTakeRewardDescriptionWhenHovered(string modifier)
        {
            if (ImGui.IsItemHovered())
                DrawTakeRewardDescription(modifier);
        }

        private static void DrawTakeRewardDescription(string modifier)
        {
            string description = UltimatumModifiersConstants.GetDescription(modifier);
            if (!string.IsNullOrWhiteSpace(description))
                SettingsUiRenderHelpers.DrawWrappedText(description, new Vector4(0.65f, 0.65f, 0.65f, 1f), 1f);
        }

        private SettingsUiRenderHelpers.ExpandableTransferRowState DrawUltimatumModifierGroupRow(string listId, UltimatumModifierGroupEntry group, bool moveToTakeReward, Vector4 textColor)
        {
            string label = $"{group.DisplayName} [v]##{listId}_{group.Id}";
            return SettingsUiRenderHelpers.DrawExpandableTransferListRow(
                $"MoveUltimatumGroup_{listId}_{group.Id}",
                label,
                IsExpandedUltimatumTakeRewardRow(listId, group.Id),
                moveToTakeReward,
                textColor);
        }

        private void DrawUltimatumModifierGroupSubmenu(string listId, UltimatumModifierGroupEntry group, bool moveToTakeReward)
        {
            ImGui.Indent();

            foreach (string modifier in UltimatumModifiersConstants.AllModifierNamesWithStages)
            {
                if (!ShouldRenderUltimatumGroupSubmenuModifier(group, modifier))
                    continue;

                DrawUltimatumGroupSubmenuModifierRow(listId, group.Id, modifier, moveToTakeReward);
            }

            ImGui.Unindent();
        }

        private bool ShouldRenderUltimatumGroupSubmenuModifier(UltimatumModifierGroupEntry group, string modifier)
            => group.Members.Contains(modifier, StringComparer.OrdinalIgnoreCase)
                && SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.UltimatumTakeRewardSearchFilter, modifier);

        private void DrawUltimatumGroupSubmenuModifierRow(string listId, string groupId, string modifier, bool moveToTakeReward)
        {
            (bool enabledForList, Vector4 rowColor) = ResolveUltimatumGroupSubmenuModifierRowState(modifier, moveToTakeReward);
            if (!SettingsUiRenderHelpers.DrawCheckbox($"{modifier}##UltimatumGroupSubmenu_{listId}_{groupId}_{modifier}", ref enabledForList, rowColor))
                return;

            bool moveModifierToTakeReward = moveToTakeReward ? !enabledForList : enabledForList;
            MoveUltimatumTakeRewardModifier(modifier, moveModifierToTakeReward);
        }

        private (bool EnabledForList, Vector4 RowColor) ResolveUltimatumGroupSubmenuModifierRowState(string modifier, bool moveToTakeReward)
        {
            bool isTakeReward = _settings.UltimatumTakeRewardModifierNames.Contains(modifier);
            bool enabledForList = moveToTakeReward ? !isTakeReward : isTakeReward;
            Vector4 rowColor = enabledForList
                ? (moveToTakeReward ? SettingsUiPalette.WhitelistTextColor : SettingsUiPalette.BlacklistTextColor)
                : (moveToTakeReward ? SettingsUiPalette.BlacklistTextColor : SettingsUiPalette.WhitelistTextColor);

            return (enabledForList, rowColor);
        }

        private static bool ShouldRenderUltimatumModifierGroup(UltimatumModifierGroupEntry group, HashSet<string> sourceSet, string filter)
        {
            bool matchesGroup = SettingsUiRenderHelpers.MatchesSearch(filter, group.DisplayName);
            for (int i = 0; i < group.Members.Length; i++)
            {
                string member = group.Members[i];
                if (!sourceSet.Contains(member))
                    continue;
                if (matchesGroup || SettingsUiRenderHelpers.MatchesSearch(filter, member))
                    return true;
            }

            return false;
        }

        private void SetUltimatumModifierGroupState(UltimatumModifierGroupEntry group, bool moveToTakeReward)
        {
            for (int i = 0; i < group.Members.Length; i++)
                MoveUltimatumTakeRewardModifier(group.Members[i], moveToTakeReward);

        }

        private void MoveUltimatumTakeRewardModifier(string modifierName, bool moveToTakeReward)
        {
            HashSet<string> source = moveToTakeReward ? _settings.UltimatumContinueModifierNames : _settings.UltimatumTakeRewardModifierNames;
            HashSet<string> target = moveToTakeReward ? _settings.UltimatumTakeRewardModifierNames : _settings.UltimatumContinueModifierNames;

            source.Remove(modifierName);
            target.Add(modifierName);
        }

        private void ClearExpandedUltimatumTakeRewardRow()
            => _settings.UiState.ExpandedUltimatumTakeRewardRowKey = string.Empty;

        private bool IsExpandedUltimatumTakeRewardRow(string listId, string rowId)
            => string.Equals(_settings.UiState.ExpandedUltimatumTakeRewardRowKey, SettingsUiRenderHelpers.BuildExpandedRowKey(listId, rowId), StringComparison.Ordinal);

        private void ToggleExpandedUltimatumTakeRewardRow(string listId, string rowId)
        {
            _settings.UiState.ExpandedUltimatumTakeRewardRowKey = SettingsUiRenderHelpers.ToggleExpandedRowKey(_settings.UiState.ExpandedUltimatumTakeRewardRowKey, listId, rowId);
        }

    }
}