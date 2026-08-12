namespace ClickIt.Shared.Game
{
    internal readonly record struct RecommendedSettingChange(string Name, string CurrentText, string NewText);

    // Opt-in helper that writes ClickIt's recommended ExileAPI performance settings into the LIVE
    // CorePerformanceSettings nodes. Editing ExileAPI's settings.json directly does not work while
    // running (the values are already loaded in memory and ExileAPI rewrites the file on shutdown),
    // but the live nodes take effect immediately and are persisted by ExileAPI on exit.
    internal static class ExileCorePerformanceApplier
    {
        private const int RecommendedTargetFps = 70;
        private const int DefaultThreadCount = 8;

        private static Func<GameController?>? s_gameControllerProvider;
        private static bool s_suppressSetupUntilReload;

        internal static void SetGameControllerProvider(Func<GameController?>? provider)
            => s_gameControllerProvider = provider;

        // Set by the debug performance box so a reset only takes effect after ExileAPI reloads.
        internal static void SetSuppressSetupUntilReload(bool value)
            => s_suppressSetupUntilReload = value;

        internal static bool SuppressSetupUntilReload => s_suppressSetupUntilReload;

        // Returns null when the current values cannot be read, an empty list when everything is
        // already at the recommended values, or the list of changes that apply would make.
        internal static List<RecommendedSettingChange>? GetRecommendedChanges()
        {
            GameController? gameController = s_gameControllerProvider?.Invoke();
            if (gameController == null)
                return null;

            try
            {
                CorePerformanceSettings? performance = gameController.Settings?.CoreSettings?.PerformanceSettings;
                if (performance == null)
                    return null;

                List<RecommendedSettingChange> changes = [];
                AddBoolChange(changes, "Coroutine Multi Threading", performance.CoroutineMultiThreading.Value, true);
                AddBoolChange(changes, "Parse Entities in Multi Thread", performance.ParseEntitiesInMultiThread.Value, true);
                AddIntChange(changes, "Threads count", performance.Threads.Value, ResolveRecommendedThreadCount());
                AddIntChange(changes, "Target FPS", performance.TargetFps.Value, RecommendedTargetFps);
                AddIntChange(changes, "Target Parallel Coroutine FPS", performance.TargetParallelCoroutineFps.Value, RecommendedTargetFps);
                AddIntChange(changes, "Entities Fps", performance.EntitiesFps.Value, RecommendedTargetFps);
                AddBoolChange(changes, "Parse Server Entities", performance.ParseServerEntities.Value, true);
                return changes;
            }
            catch
            {
                return null;
            }
        }

        internal static bool TryApplyRecommended()
        {
            GameController? gameController = s_gameControllerProvider?.Invoke();
            if (gameController == null)
                return false;

            try
            {
                ExileCoreCoreSettings? coreSettings = gameController.Settings?.CoreSettings;
                CorePerformanceSettings? performance = coreSettings?.PerformanceSettings;
                if (performance == null)
                    return false;

                performance.CoroutineMultiThreading.Value = true;
                performance.ParseEntitiesInMultiThread.Value = true;
                performance.Threads.Value = ResolveRecommendedThreadCount();
                performance.TargetFps.Value = RecommendedTargetFps;
                performance.TargetParallelCoroutineFps.Value = RecommendedTargetFps;
                performance.EntitiesFps.Value = RecommendedTargetFps;
                performance.ParseServerEntities.Value = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static int ResolveRecommendedThreadCount()
        {
            int count = Environment.ProcessorCount;
            return count >= 1 ? count : DefaultThreadCount;
        }

        private static void AddBoolChange(List<RecommendedSettingChange> changes, string name, bool current, bool recommended)
        {
            if (current == recommended)
                return;
            changes.Add(new RecommendedSettingChange(name, current ? "ON" : "OFF", recommended ? "ON" : "OFF"));
        }

        private static void AddIntChange(List<RecommendedSettingChange> changes, string name, int current, int recommended)
        {
            if (current == recommended)
                return;
            changes.Add(new RecommendedSettingChange(name, current.ToString(), recommended.ToString()));
        }
    }
}
