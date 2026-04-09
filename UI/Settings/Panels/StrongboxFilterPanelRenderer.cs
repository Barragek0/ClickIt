namespace ClickIt.UI.Settings.Panels
{
    internal sealed class StrongboxFilterPanelRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        public void DrawPanel(bool embedded = false)
        {
            SettingsDefaultsService.EnsureStrongboxFiltersInitialized(_settings);

            SettingsUiRenderHelpers.DrawSearchBar("##StrongboxSearch", "Clear##StrongboxSearchClear", ref _settings.UiState.StrongboxSearchFilter);
            if (SettingsUiRenderHelpers.DrawResetDefaultsButton("Reset Defaults##StrongboxResetDefaults"))
                SettingsDefaultsService.ResetStrongboxFilterDefaults(_settings);


            ImGui.Spacing();

            SettingsUiRenderHelpers.DrawDualTransferTable(
                tableId: "StrongboxFilterLists",
                leftHeader: "Click",
                rightHeader: "Don't Click",
                leftBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                drawLeft: () => DrawStrongboxFilterList("Click##Strongbox", _settings.StrongboxClickIds, moveToClick: false, textColor: SettingsUiPalette.WhitelistTextColor),
                drawRight: () => DrawStrongboxFilterList("DontClick##Strongbox", _settings.StrongboxDontClickIds, moveToClick: true, textColor: SettingsUiPalette.BlacklistTextColor));
        }

        private void DrawStrongboxFilterList(string id, HashSet<string> sourceSet, bool moveToClick, Vector4 textColor)
        {
            ImGui.PushID(id);

            bool hasEntries = false;
            foreach (ClickItSettings.StrongboxFilterEntry entry in ClickItSettings.StrongboxTableEntries)
            {
                if (!sourceSet.Contains(entry.Id))
                    continue;
                if (!MatchesStrongboxSearch(entry, _settings.UiState.StrongboxSearchFilter))
                    continue;

                hasEntries = true;
                bool arrowClicked = SettingsUiRenderHelpers.DrawTransferListRow(id, entry.Id, entry.DisplayName, moveToClick, textColor);

                if (arrowClicked)
                {
                    MoveStrongboxFilter(entry.Id, moveToClick);
                    break;
                }
            }

            SettingsUiRenderHelpers.DrawNoEntriesPlaceholder(hasEntries);

            ImGui.PopID();
        }

        private void MoveStrongboxFilter(string strongboxId, bool moveToClick)
        {
            HashSet<string> source = moveToClick ? _settings.StrongboxDontClickIds : _settings.StrongboxClickIds;
            HashSet<string> target = moveToClick ? _settings.StrongboxClickIds : _settings.StrongboxDontClickIds;

            source.Remove(strongboxId);
            target.Add(strongboxId);
        }

        private static bool MatchesStrongboxSearch(ClickItSettings.StrongboxFilterEntry entry, string filter)
        {
            return SettingsUiRenderHelpers.MatchesSearch(
                filter,
                entry.MetadataIdentifiers.Prepend(entry.DisplayName));
        }
    }
}