using ClickIt.Definitions;
using ImGuiNET;
using System.Numerics;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private void DrawEssenceCorruptionTablePanel()
        {
            EnsureEssenceCorruptionFiltersInitialized();

            ImGui.SetNextItemOpen(false, ImGuiCond.Once);
            bool sectionOpen = ImGui.TreeNode("Corruption Filters");
            DrawInlineTooltip("Configure which Screaming, Shrieking, and Deafening essences should be corrupted. Use arrows to move entries between Corrupt and Don't Corrupt lists.");
            if (!sectionOpen)
                return;

            try
            {
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
            finally
            {
                ImGui.TreePop();
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

            ImGui.SetNextItemOpen(false, ImGuiCond.Once);
            bool sectionOpen = ImGui.TreeNode("Strongbox Filters");
            DrawInlineTooltip("Configure which strongboxes should be clicked. Use arrows to move entries between Click and Don't Click lists.");
            if (!sectionOpen)
                return;

            try
            {
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
            finally
            {
                ImGui.TreePop();
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

            ImGui.SetNextItemOpen(false, ImGuiCond.Once);
            bool sectionOpen = ImGui.TreeNode("Modifier Priorities");
            DrawInlineTooltip("Top rows are preferred first. Example: if the options are Resistant Monsters, Reduced Recovery, and Ruin, the plugin picks whichever appears highest in this table.");
            if (!sectionOpen)
                return;

            try
            {
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
            finally
            {
                ImGui.TreePop();
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
    }
}