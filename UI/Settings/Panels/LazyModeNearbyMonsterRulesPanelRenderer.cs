namespace ClickIt.UI.Settings.Panels
{
    internal sealed class LazyModeNearbyMonsterRulesPanelRenderer(ClickItSettings settings)
    {
        private readonly ClickItSettings _settings = settings;

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

        public void Draw()
        {
            SettingsNormalizationService.EnsureLazyModeNearbyMonsterFiltersInitialized(_settings);

            DrawRuleRows([
                new LazyModeNearbyMonsterRuleDescriptor(
                    "Normal",
                    "Normal",
                    () => _settings.LazyModeNormalMonsterBlockCount,
                    () => _settings.LazyModeNormalMonsterBlockDistance,
                    (count, distance) =>
                    {
                        _settings.LazyModeNormalMonsterBlockCount = count;
                        _settings.LazyModeNormalMonsterBlockDistance = distance;
                    }),
                new LazyModeNearbyMonsterRuleDescriptor(
                    "Magic",
                    "Magic",
                    () => _settings.LazyModeMagicMonsterBlockCount,
                    () => _settings.LazyModeMagicMonsterBlockDistance,
                    (count, distance) =>
                    {
                        _settings.LazyModeMagicMonsterBlockCount = count;
                        _settings.LazyModeMagicMonsterBlockDistance = distance;
                    }),
                new LazyModeNearbyMonsterRuleDescriptor(
                    "Rare",
                    "Rare",
                    () => _settings.LazyModeRareMonsterBlockCount,
                    () => _settings.LazyModeRareMonsterBlockDistance,
                    (count, distance) =>
                    {
                        _settings.LazyModeRareMonsterBlockCount = count;
                        _settings.LazyModeRareMonsterBlockDistance = distance;
                    }),
                new LazyModeNearbyMonsterRuleDescriptor(
                    "Unique",
                    "Unique",
                    () => _settings.LazyModeUniqueMonsterBlockCount,
                    () => _settings.LazyModeUniqueMonsterBlockDistance,
                    (count, distance) =>
                    {
                        _settings.LazyModeUniqueMonsterBlockCount = count;
                        _settings.LazyModeUniqueMonsterBlockDistance = distance;
                    })
            ]);

            ImGui.Spacing();
            ImGui.TextDisabled("Set count to 0 to disable a specific rarity rule.");
        }

        private static void DrawRuleRows(IReadOnlyList<LazyModeNearbyMonsterRuleDescriptor> rows)
        {
            foreach (LazyModeNearbyMonsterRuleDescriptor row in rows)
                DrawRuleRow(
        row.RowId,
        row.RarityLabel,
        row.GetCount(),
        row.GetDistance(),
        row.Apply);

        }

        private static void DrawRuleRow(string rowId, string rarityLabel, int currentCount, int currentDistance, Action<int, int> apply)
        {
            int count = currentCount;
            int distance = currentDistance;

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Disable Lazy Mode when");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(70f);
            bool changed = ImGui.InputInt($"##LazyModeNearbyMonsterCount{rowId}", ref count, 1, 10);
            count = SettingsNormalizationService.SanitizeLazyModeNearbyMonsterCount(count);

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted($"{rarityLabel} Monsters are within");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(80f);
            changed |= ImGui.InputInt($"##LazyModeNearbyMonsterDistance{rowId}", ref distance, 1, 10);
            distance = SettingsNormalizationService.SanitizeLazyModeNearbyMonsterDistance(distance);

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Distance");

            if (changed || count != currentCount || distance != currentDistance)
                apply(count, distance);

        }
    }
}