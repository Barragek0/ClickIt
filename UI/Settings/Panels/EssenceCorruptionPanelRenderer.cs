namespace ClickIt.UI.Settings.Panels
{
    internal sealed class EssenceCorruptionPanelRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

        public void DrawPanel(bool embedded = false)
        {
            SettingsDefaultsService.EnsureEssenceCorruptionFiltersInitialized(_settings);

            SettingsUiRenderHelpers.DrawSearchBar("##EssenceSearch", "Clear##EssenceSearchClear", ref _settings.UiState.EssenceSearchFilter);
            if (SettingsUiRenderHelpers.DrawResetDefaultsButton("Reset Defaults##EssenceResetDefaults"))
            {
                SettingsDefaultsService.ResetEssenceCorruptionDefaults(_settings);
            }

            ImGui.Spacing();

            SettingsUiRenderHelpers.DrawDualTransferTable(
                tableId: "EssenceCorruptionLists",
                leftHeader: "Corrupt",
                rightHeader: "Don't Corrupt",
                leftBackground: new Vector4(0.6f, 0.2f, 0.2f, 0.3f),
                rightBackground: new Vector4(0.2f, 0.6f, 0.2f, 0.3f),
                drawLeft: () => DrawEssenceCorruptionList("Corrupt##Essence", _settings.EssenceCorruptNames, moveToCorrupt: false, textColor: new Vector4(0.8f, 0.4f, 0.4f, 1.0f)),
                drawRight: () => DrawEssenceCorruptionList("DontCorrupt##Essence", _settings.EssenceDontCorruptNames, moveToCorrupt: true, textColor: new Vector4(0.4f, 0.8f, 0.4f, 1.0f)));
        }

        private void DrawEssenceCorruptionList(string id, HashSet<string> sourceSet, bool moveToCorrupt, Vector4 textColor)
        {
            ImGui.PushID(id);

            bool hasEntries = false;
            foreach (string essenceName in ClickItSettings.EssenceAllTableNames)
            {
                if (!sourceSet.Contains(essenceName))
                    continue;
                if (!SettingsUiRenderHelpers.MatchesSearch(_settings.UiState.EssenceSearchFilter, essenceName))
                    continue;

                hasEntries = true;
                bool arrowClicked = SettingsUiRenderHelpers.DrawTransferListRow(id, essenceName, essenceName, moveToCorrupt, textColor);

                if (arrowClicked)
                {
                    MoveEssenceName(essenceName, moveToCorrupt);
                    break;
                }
            }

            SettingsUiRenderHelpers.DrawNoEntriesPlaceholder(hasEntries);

            ImGui.PopID();
        }

        private void MoveEssenceName(string essenceName, bool moveToCorrupt)
        {
            HashSet<string> source = moveToCorrupt ? _settings.EssenceDontCorruptNames : _settings.EssenceCorruptNames;
            HashSet<string> target = moveToCorrupt ? _settings.EssenceCorruptNames : _settings.EssenceDontCorruptNames;

            source.Remove(essenceName);
            target.Add(essenceName);
        }
    }
}