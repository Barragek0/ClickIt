using ImGuiNET;
using System;

namespace ClickIt
{
    public partial class ClickItSettings
    {
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

        private void DrawLazyModeNearbyMonsterRulesPanel()
        {
            SettingsNormalizationService.EnsureLazyModeNearbyMonsterFiltersInitialized(this);

            DrawLazyModeNearbyMonsterRuleRows([
                new LazyModeNearbyMonsterRuleDescriptor(
                    "Normal",
                    "Normal",
                    () => LazyModeNormalMonsterBlockCount,
                    () => LazyModeNormalMonsterBlockDistance,
                    (count, distance) =>
                    {
                        LazyModeNormalMonsterBlockCount = count;
                        LazyModeNormalMonsterBlockDistance = distance;
                    }),
                new LazyModeNearbyMonsterRuleDescriptor(
                    "Magic",
                    "Magic",
                    () => LazyModeMagicMonsterBlockCount,
                    () => LazyModeMagicMonsterBlockDistance,
                    (count, distance) =>
                    {
                        LazyModeMagicMonsterBlockCount = count;
                        LazyModeMagicMonsterBlockDistance = distance;
                    }),
                new LazyModeNearbyMonsterRuleDescriptor(
                    "Rare",
                    "Rare",
                    () => LazyModeRareMonsterBlockCount,
                    () => LazyModeRareMonsterBlockDistance,
                    (count, distance) =>
                    {
                        LazyModeRareMonsterBlockCount = count;
                        LazyModeRareMonsterBlockDistance = distance;
                    }),
                new LazyModeNearbyMonsterRuleDescriptor(
                    "Unique",
                    "Unique",
                    () => LazyModeUniqueMonsterBlockCount,
                    () => LazyModeUniqueMonsterBlockDistance,
                    (count, distance) =>
                    {
                        LazyModeUniqueMonsterBlockCount = count;
                        LazyModeUniqueMonsterBlockDistance = distance;
                    })
            ]);

            ImGui.Spacing();
            ImGui.TextDisabled("Set count to 0 to disable a specific rarity rule.");
        }

        private void DrawLazyModeNearbyMonsterRuleRows(IReadOnlyList<LazyModeNearbyMonsterRuleDescriptor> rows)
        {
            foreach (LazyModeNearbyMonsterRuleDescriptor row in rows)
            {
                DrawLazyModeNearbyMonsterRuleRow(
                    row.RowId,
                    row.RarityLabel,
                    row.GetCount(),
                    row.GetDistance(),
                    row.Apply);
            }
        }

        private void DrawLazyModeNearbyMonsterRuleRow(string rowId, string rarityLabel, int currentCount, int currentDistance, Action<int, int> apply)
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
            {
                apply(count, distance);
            }
        }
    }
}