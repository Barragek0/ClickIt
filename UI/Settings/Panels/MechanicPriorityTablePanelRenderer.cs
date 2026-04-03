using ExileCore.Shared.Nodes;
using ImGuiNET;
using System.Numerics;

namespace ClickIt.UI.Settings.Panels
{
    internal sealed class MechanicPriorityTablePanelRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        private readonly struct VisibleMechanicPriorityRow(int orderIndex, string mechanicId, MechanicPriorityEntry entry)
        {
            public int OrderIndex { get; } = orderIndex;
            public string MechanicId { get; } = mechanicId;
            public MechanicPriorityEntry Entry { get; } = entry;
        }

        public void Draw()
        {
            SettingsDefaultsService.EnsureMechanicPrioritiesInitialized(_settings);

            if (SettingsUiRenderHelpers.DrawResetDefaultsButton("Reset Defaults##MechanicPriorityResetDefaults"))
            {
                SettingsDefaultsService.ResetMechanicPriorityDefaults(_settings);
            }

            DrawMechanicPrioritySectionDescription();

            float tableWidth = Math.Min(700f, Math.Max(160f, ImGui.GetContentRegionAvail().X));
            if (!SettingsUiRenderHelpers.BeginSingleColumnPriorityTable("MechanicPriorityTable", "Mechanics", tableWidth))
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

        private List<VisibleMechanicPriorityRow> GetVisibleMechanicPriorityRows()
        {
            List<VisibleMechanicPriorityRow> rows = [];
            for (int orderIndex = 0; orderIndex < _settings.MechanicPriorityOrder.Count; orderIndex++)
            {
                string mechanicId = _settings.MechanicPriorityOrder[orderIndex];
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
            _ = _settings.GetMechanicTableEntries();
            Dictionary<string, ToggleNode>? nodesById = _settings.TransientState.RuntimeCache.MechanicToggleNodeByIdCache;

            if (nodesById != null
                && nodesById.TryGetValue(mechanicId, out ToggleNode? node)
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

            Vector4 priorityColor = SettingsUiRenderHelpers.GetUltimatumPriorityRowColor(visibleIndex, visibleRows.Count);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(priorityColor));

            if (SettingsUiRenderHelpers.DrawArrowButton(ImGuiDir.Up, $"MechanicPriorityUp_{mechanicId}_{orderIndex}", enabled: visibleIndex > 0))
            {
                int targetOrderIndex = visibleRows[visibleIndex - 1].OrderIndex;
                (_settings.MechanicPriorityOrder[orderIndex], _settings.MechanicPriorityOrder[targetOrderIndex]) = (_settings.MechanicPriorityOrder[targetOrderIndex], _settings.MechanicPriorityOrder[orderIndex]);
                return true;
            }

            ImGui.SameLine();
            if (SettingsUiRenderHelpers.DrawArrowButton(ImGuiDir.Down, $"MechanicPriorityDown_{mechanicId}_{orderIndex}", enabled: visibleIndex < visibleRows.Count - 1))
            {
                int targetOrderIndex = visibleRows[visibleIndex + 1].OrderIndex;
                (_settings.MechanicPriorityOrder[orderIndex], _settings.MechanicPriorityOrder[targetOrderIndex]) = (_settings.MechanicPriorityOrder[targetOrderIndex], _settings.MechanicPriorityOrder[orderIndex]);
                return true;
            }

            ImGui.SameLine();
            bool isExpanded = string.Equals(_settings.UiState.ExpandedMechanicPriorityRowId, mechanicId, StringComparison.OrdinalIgnoreCase);
            bool rowClicked = ImGui.Selectable($"{entry.DisplayName}##MechanicPriority_{mechanicId}", isExpanded, ImGuiSelectableFlags.AllowDoubleClick, new Vector2(0, 0));
            if (rowClicked)
                _settings.UiState.ExpandedMechanicPriorityRowId = isExpanded ? string.Empty : mechanicId;

            return false;
        }

        private void DrawMechanicPriorityExpandedOptions(string mechanicId)
        {
            if (!string.Equals(_settings.UiState.ExpandedMechanicPriorityRowId, mechanicId, StringComparison.OrdinalIgnoreCase))
                return;

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.12f, 0.85f)));
            ImGui.Indent(34f);

            bool ignoreDistance = _settings.MechanicPriorityIgnoreDistanceIds.Contains(mechanicId);
            if (ImGui.Checkbox($"Ignore Distance##IgnoreDistance_{mechanicId}", ref ignoreDistance))
            {
                if (ignoreDistance)
                    _settings.MechanicPriorityIgnoreDistanceIds.Add(mechanicId);
                else
                    _settings.MechanicPriorityIgnoreDistanceIds.Remove(mechanicId);
            }

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));
            ImGui.TextWrapped("When enabled, this mechanic bypasses distance sorting and is resolved from configured priority order.");
            ImGui.PopStyleColor();

            if (ignoreDistance)
            {
                int currentWithin = _settings.MechanicPriorityIgnoreDistanceWithinById.TryGetValue(mechanicId, out int configuredWithin)
                    ? configuredWithin
                    : ClickItSettings.MechanicIgnoreDistanceWithinDefault;
                ImGui.SetNextItemWidth(400f);
                if (ImGui.SliderInt($"Ignore Distance Within##IgnoreDistanceWithin_{mechanicId}", ref currentWithin, ClickItSettings.MechanicIgnoreDistanceWithinMin, ClickItSettings.MechanicIgnoreDistanceWithinMax))
                {
                    _settings.MechanicPriorityIgnoreDistanceWithinById[mechanicId] = currentWithin;
                }

                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1f));
                ImGui.TextWrapped("Ignore Distance applies only while this mechanic is within the configured distance from the player.");
                ImGui.PopStyleColor();
            }

            ImGui.Unindent(34f);
        }

        private static bool TryGetMechanicPriorityEntry(string id, out MechanicPriorityEntry? entry)
        {
            if (MechanicPriorityCatalog.EntriesById.TryGetValue(id, out MechanicPriorityEntry? found))
            {
                entry = found;
                return true;
            }

            entry = null;
            return false;
        }

    }
}