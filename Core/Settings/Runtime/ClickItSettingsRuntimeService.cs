namespace ClickIt.Core.Settings.Runtime
{
    internal static class ClickItSettingsRuntimeService
    {
        internal static bool IsLazyModeDisableHotkeyToggleModeEnabled(ClickItSettings settings)
            => settings.LazyModeDisableKeyToggleMode?.Value == true;

        internal static bool IsClickHotkeyToggleModeEnabled(ClickItSettings settings)
            => settings.ClickHotkeyToggleMode?.Value == true;

        internal static bool IsInitialUltimatumClickEnabled(ClickItSettings settings)
            => settings.ClickInitialUltimatum?.Value == true;

        internal static bool IsOtherUltimatumClickEnabled(ClickItSettings settings)
            => settings.ClickUltimatumChoices?.Value == true;

        internal static bool IsUltimatumTakeRewardButtonClickEnabled(ClickItSettings settings)
            => settings.ClickUltimatumTakeRewardButton?.Value != false;

        internal static IReadOnlyList<string> GetMechanicPriorityOrder(ClickItSettings settings)
        {
            ClickItSettingsRuntimeCacheState runtimeCache = settings.TransientState.RuntimeCache;

            // Cache-first: skip the full sanitize on the steady-state hot path; it only runs when the settings changed (snapshot mismatch).
            if (settings.MechanicPriorityOrder != null && HasMatchingMechanicPrioritySnapshot(settings))
            {
                return runtimeCache.MechanicPrioritySnapshot;
            }

            SettingsDefaultsService.EnsureMechanicPrioritiesInitialized(settings);
            runtimeCache.MechanicPrioritySnapshot = [.. settings.MechanicPriorityOrder];
            return runtimeCache.MechanicPrioritySnapshot;
        }

        internal static IReadOnlyCollection<string> GetMechanicPriorityIgnoreDistanceIds(ClickItSettings settings)
        {
            ClickItSettingsRuntimeCacheState runtimeCache = settings.TransientState.RuntimeCache;

            // Cache-first: skip the full sanitize on the steady-state hot path; it only runs when the settings changed (snapshot mismatch).
            if (settings.MechanicPriorityIgnoreDistanceIds != null && HasMatchingMechanicIgnoreDistanceSnapshot(settings))
            {
                return runtimeCache.MechanicIgnoreDistanceSnapshot;
            }

            SettingsDefaultsService.EnsureMechanicPrioritiesInitialized(settings);
            runtimeCache.MechanicIgnoreDistanceSnapshot = [.. settings.MechanicPriorityIgnoreDistanceIds.OrderBy(static x => x, ClickItSettings.PriorityComparer)];
            return runtimeCache.MechanicIgnoreDistanceSnapshot;
        }

        internal static IReadOnlyDictionary<string, int> GetMechanicPriorityIgnoreDistanceWithinById(ClickItSettings settings)
        {
            ClickItSettingsRuntimeCacheState runtimeCache = settings.TransientState.RuntimeCache;

            // Cache-first: skip the full sanitize on the steady-state hot path; it only runs when the settings changed (snapshot mismatch).
            if (settings.MechanicPriorityIgnoreDistanceWithinById != null && HasMatchingMechanicIgnoreDistanceWithinSnapshot(settings))
            {
                return runtimeCache.MechanicIgnoreDistanceWithinMapSnapshot;
            }

            SettingsDefaultsService.EnsureMechanicPrioritiesInitialized(settings);
            runtimeCache.MechanicIgnoreDistanceWithinSnapshot = [.. settings.MechanicPriorityIgnoreDistanceWithinById.OrderBy(static x => x.Key, ClickItSettings.PriorityComparer)];
            runtimeCache.MechanicIgnoreDistanceWithinMapSnapshot = new Dictionary<string, int>(
                runtimeCache.MechanicIgnoreDistanceWithinSnapshot.ToDictionary(static x => x.Key, static x => x.Value, ClickItSettings.PriorityComparer),
                ClickItSettings.PriorityComparer);
            return runtimeCache.MechanicIgnoreDistanceWithinMapSnapshot;
        }

        private static bool HasMatchingMechanicPrioritySnapshot(ClickItSettings settings)
        {
            string[] snapshot = settings.TransientState.RuntimeCache.MechanicPrioritySnapshot;
            if (snapshot.Length != settings.MechanicPriorityOrder.Count)
                return false;

            for (int i = 0; i < settings.MechanicPriorityOrder.Count; i++)
            {
                if (!string.Equals(snapshot[i], settings.MechanicPriorityOrder[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static bool HasMatchingMechanicIgnoreDistanceSnapshot(ClickItSettings settings)
        {
            string[] snapshot = settings.TransientState.RuntimeCache.MechanicIgnoreDistanceSnapshot;
            if (snapshot.Length != settings.MechanicPriorityIgnoreDistanceIds.Count)
                return false;

            // The cached snapshot is the sorted form of the settings set; verify each entry against the settings' own HashSet (O(n), no per-entry scans) instead of materializing a fresh ordered copy on the cache-hit path.
            foreach (string id in snapshot)
            {
                if (!settings.MechanicPriorityIgnoreDistanceIds.Contains(id))
                    return false;
            }

            return true;
        }

        private static bool HasMatchingMechanicIgnoreDistanceWithinSnapshot(ClickItSettings settings)
        {
            KeyValuePair<string, int>[] snapshot = settings.TransientState.RuntimeCache.MechanicIgnoreDistanceWithinSnapshot;
            if (snapshot.Length != settings.MechanicPriorityIgnoreDistanceWithinById.Count)
                return false;

            // The cached snapshot is the sorted form of the settings map; verify each entry against the settings' own Dictionary (O(n) lookups) instead of per-entry scans on the cache-hit path.
            foreach (KeyValuePair<string, int> entry in snapshot)
            {
                if (!settings.MechanicPriorityIgnoreDistanceWithinById.TryGetValue(entry.Key, out int value)
                    || value != entry.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
