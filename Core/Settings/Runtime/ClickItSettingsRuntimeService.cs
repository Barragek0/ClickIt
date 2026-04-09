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

        internal static bool IsAnyUltimatumClickEnabled(ClickItSettings settings)
            => IsInitialUltimatumClickEnabled(settings) || IsOtherUltimatumClickEnabled(settings);

        internal static bool IsUltimatumTakeRewardButtonClickEnabled(ClickItSettings settings)
            => settings.ClickUltimatumTakeRewardButton?.Value != false;

        internal static bool IsAnyDetailedDebugSectionEnabled(ClickItSettings settings)
        {
            return settings.DebugShowStatus
                || settings.DebugShowGameState
                || settings.DebugShowPerformance
                || settings.DebugShowClickFrequencyTarget
                || settings.DebugShowAltarDetection
                || settings.DebugShowAltarService
                || settings.DebugShowLabels
                || settings.DebugShowInventoryPickup
                || settings.DebugShowHoveredItemMetadata
                || settings.DebugShowPathfinding
                || settings.DebugShowUltimatum
                || settings.DebugShowClicking
                || settings.DebugShowRuntimeDebugLogOverlay
                || settings.DebugShowRecentErrors;
        }

        internal static bool IsOnlyPathfindingDetailedDebugSectionEnabled(ClickItSettings settings)
        {
            return settings.DebugShowPathfinding
                && !settings.DebugShowStatus
                && !settings.DebugShowGameState
                && !settings.DebugShowPerformance
                && !settings.DebugShowClickFrequencyTarget
                && !settings.DebugShowAltarDetection
                && !settings.DebugShowAltarService
                && !settings.DebugShowLabels
                && !settings.DebugShowInventoryPickup
                && !settings.DebugShowHoveredItemMetadata
                && !settings.DebugShowUltimatum
                && !settings.DebugShowClicking
                && !settings.DebugShowRuntimeDebugLogOverlay
                && !settings.DebugShowRecentErrors;
        }

        internal static IReadOnlyList<string> GetMechanicPriorityOrder(ClickItSettings settings)
        {
            SettingsDefaultsService.EnsureMechanicPrioritiesInitialized(settings);
            ClickItSettingsRuntimeCacheState runtimeCache = settings.TransientState.RuntimeCache;

            if (HasMatchingMechanicPrioritySnapshot(settings))
            {
                return runtimeCache.MechanicPrioritySnapshot;
            }

            runtimeCache.MechanicPrioritySnapshot = [.. settings.MechanicPriorityOrder];
            return runtimeCache.MechanicPrioritySnapshot;
        }

        internal static IReadOnlyCollection<string> GetMechanicPriorityIgnoreDistanceIds(ClickItSettings settings)
        {
            SettingsDefaultsService.EnsureMechanicPrioritiesInitialized(settings);
            ClickItSettingsRuntimeCacheState runtimeCache = settings.TransientState.RuntimeCache;

            if (HasMatchingMechanicIgnoreDistanceSnapshot(settings))
            {
                return runtimeCache.MechanicIgnoreDistanceSnapshot;
            }

            runtimeCache.MechanicIgnoreDistanceSnapshot = [.. settings.MechanicPriorityIgnoreDistanceIds.OrderBy(static x => x, ClickItSettings.PriorityComparer)];
            return runtimeCache.MechanicIgnoreDistanceSnapshot;
        }

        internal static IReadOnlyDictionary<string, int> GetMechanicPriorityIgnoreDistanceWithinById(ClickItSettings settings)
        {
            SettingsDefaultsService.EnsureMechanicPrioritiesInitialized(settings);
            ClickItSettingsRuntimeCacheState runtimeCache = settings.TransientState.RuntimeCache;

            if (HasMatchingMechanicIgnoreDistanceWithinSnapshot(settings))
            {
                return runtimeCache.MechanicIgnoreDistanceWithinMapSnapshot;
            }

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

            string[] current = [.. settings.MechanicPriorityIgnoreDistanceIds.OrderBy(static x => x, ClickItSettings.PriorityComparer)];
            for (int i = 0; i < current.Length; i++)
            {
                if (!string.Equals(current[i], snapshot[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static bool HasMatchingMechanicIgnoreDistanceWithinSnapshot(ClickItSettings settings)
        {
            KeyValuePair<string, int>[] snapshot = settings.TransientState.RuntimeCache.MechanicIgnoreDistanceWithinSnapshot;
            if (snapshot.Length != settings.MechanicPriorityIgnoreDistanceWithinById.Count)
                return false;

            KeyValuePair<string, int>[] current = [.. settings.MechanicPriorityIgnoreDistanceWithinById.OrderBy(static x => x.Key, ClickItSettings.PriorityComparer)];
            for (int i = 0; i < current.Length; i++)
            {
                if (!string.Equals(current[i].Key, snapshot[i].Key, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (current[i].Value != snapshot[i].Value)
                    return false;
            }

            return true;
        }
    }
}