using ClickIt.Definitions;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private readonly struct UltimatumGroupRowRenderState(bool rowClicked, bool arrowClicked, bool isHovered)
        {
            public bool RowClicked { get; } = rowClicked;
            public bool ArrowClicked { get; } = arrowClicked;
            public bool IsHovered { get; } = isHovered;
        }

        private static UltimatumModifierGroupEntry[] UltimatumModifierGroups => UltimatumModifierGroupCatalog.Groups;
        private static HashSet<string> UltimatumTieredModifierNames => UltimatumModifierGroupCatalog.TieredModifierNames;

        private void DrawUltimatumModifierTablePanel()
        {
            EnsureUltimatumModifiersInitialized();

            DrawSearchBar("##UltimatumSearch", "Clear##UltimatumSearchClear", ref UiState.UltimatumSearchFilter);
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
                    if (!MatchesUltimatumSearch(modifier, UiState.UltimatumSearchFilter))
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

            bool hasDetection = Services.Click.Runtime.UltimatumGruelingGauntletDetectionStore.TryGet(out bool isGruelingGauntletActive);
            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "This table only does anything when Gruelling Gauntlet is allocated.");

            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.35f, 1f), "Rows with [v] can be clicked to open stage-specific submenu options.");
            ImGui.Spacing();

            DrawSearchBar("##UltimatumTakeRewardSearch", "Clear##UltimatumTakeRewardSearchClear", ref UiState.UltimatumTakeRewardSearchFilter);
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
                if (!MatchesUltimatumSearch(modifier, UiState.UltimatumTakeRewardSearchFilter))
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
                    UiState.ExpandedUltimatumTakeRewardRowKey = string.Empty;
                    ImGui.PopID();
                    return;
                }
            }

            foreach (UltimatumModifierGroupEntry group in UltimatumModifierGroups)
            {
                if (!ShouldRenderUltimatumModifierGroup(group, sourceSet, UiState.UltimatumTakeRewardSearchFilter))
                    continue;

                hasEntries = true;
                UltimatumGroupRowRenderState rowState = DrawUltimatumModifierGroupRow(id, group, moveToTakeReward, textColor);
                if (rowState.ArrowClicked)
                {
                    SetUltimatumModifierGroupState(group, moveToTakeReward);
                    UiState.ExpandedUltimatumTakeRewardRowKey = string.Empty;
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
                if (!MatchesUltimatumSearch(modifier, UiState.UltimatumTakeRewardSearchFilter))
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

        private void SetUltimatumModifierGroupState(UltimatumModifierGroupEntry group, bool moveToTakeReward)
        {
            for (int i = 0; i < group.Members.Length; i++)
            {
                MoveUltimatumTakeRewardModifier(group.Members[i], moveToTakeReward);
            }
        }

        private void MoveUltimatumTakeRewardModifier(string modifierName, bool moveToTakeReward)
        {
            HashSet<string> source = moveToTakeReward ? UltimatumContinueModifierNames : UltimatumTakeRewardModifierNames;
            HashSet<string> target = moveToTakeReward ? UltimatumTakeRewardModifierNames : UltimatumContinueModifierNames;

            source.Remove(modifierName);
            target.Add(modifierName);
        }

        private static string BuildExpandedUltimatumTakeRewardRowKey(string listId, string rowId)
        {
            return $"{listId}:{rowId}";
        }

        private bool IsExpandedUltimatumTakeRewardRow(string listId, string rowId)
        {
            return string.Equals(UiState.ExpandedUltimatumTakeRewardRowKey, BuildExpandedUltimatumTakeRewardRowKey(listId, rowId), StringComparison.Ordinal);
        }

        private void ToggleExpandedUltimatumTakeRewardRow(string listId, string rowId)
        {
            string rowKey = BuildExpandedUltimatumTakeRewardRowKey(listId, rowId);
            if (string.Equals(UiState.ExpandedUltimatumTakeRewardRowKey, rowKey, StringComparison.Ordinal))
                UiState.ExpandedUltimatumTakeRewardRowKey = string.Empty;
            else
                UiState.ExpandedUltimatumTakeRewardRowKey = rowKey;
        }

        private static bool MatchesUltimatumSearch(string modifier, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            return modifier.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
