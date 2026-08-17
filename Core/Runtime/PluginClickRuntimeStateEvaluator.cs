namespace ClickIt.Core.Runtime
{
    internal readonly record struct PluginClickRuntimeStateSnapshot(
        bool LazyModeEnabled,
        bool LazyModeDisableActive,
        bool HasLazyModeRestrictedItems,
        bool IsRitualActive,
        bool PoeForeground,
        bool UseLazyModeTiming,
        bool ShowLazyModeTarget);

    internal readonly record struct PluginClickGateDecision(
        bool ReadyByTime,
        bool CanClick,
        bool ShouldCancelOffscreenPathing,
        string FailureReason)
    {
        internal bool IsBlocked
            => !ReadyByTime || !CanClick;
    }

    internal readonly record struct PluginClickReadinessDecision(
        bool ReadyByTime,
        bool CanClick)
    {
        internal bool IsBlocked
            => !ReadyByTime || !CanClick;
    }

    internal readonly record struct PluginClickFrequencyTargetDecision(
        double ClickTargetMs,
        double LazyModeTargetMs,
        bool ShowLazyModeTarget)
    {
        internal double TargetIntervalMs
            => ShowLazyModeTarget ? LazyModeTargetMs : ClickTargetMs;
    }

    internal static class PluginClickRuntimeStateEvaluator
    {
        // Cached per-service restricted-items delegate so the per-frame hotkey check does not allocate a closure on every call. The service reference is stable for a plugin lifetime; the cache re-keys when it changes (tests).
        private static LazyModeBlockerService? _hotkeyRestrictedService;
        private static Func<IReadOnlyList<LabelOnGround>?, bool>? _hotkeyRestrictedLookup;

        internal static bool ResolveHotkeyActive(PluginServices services)
            => ResolveHotkeyActive(services.InputHandler, services.CachedLabels, services.LazyModeBlockerService);

        internal static bool ResolveHotkeyActive(
            InputHandler? inputHandler,
            TimeCache<List<LabelOnGround>>? cachedLabels,
            LazyModeBlockerService? lazyModeBlockerService)
            => inputHandler?.IsClickHotkeyPressed(cachedLabels, ResolveRestrictedLookup(lazyModeBlockerService)) ?? false;

        private static Func<IReadOnlyList<LabelOnGround>?, bool> ResolveRestrictedLookup(LazyModeBlockerService? lazyModeBlockerService)
        {
            if (ReferenceEquals(lazyModeBlockerService, _hotkeyRestrictedService) && _hotkeyRestrictedLookup != null)
                return _hotkeyRestrictedLookup;

            Func<IReadOnlyList<LabelOnGround>?, bool> lookup = labels => ResolveHasLazyModeRestrictedItems(lazyModeBlockerService, labels);
            _hotkeyRestrictedService = lazyModeBlockerService;
            _hotkeyRestrictedLookup = lookup;
            return lookup;
        }

        internal static bool ResolveHasLazyModeRestrictedItems(
            LazyModeBlockerService? lazyModeBlockerService,
            IReadOnlyList<LabelOnGround>? labels)
            => lazyModeBlockerService?.HasRestrictedItemsOnScreen(labels) ?? false;

        internal static bool ResolveIsRitualActive(GameController? gameController)
            => EntityHelpers.IsRitualActive(gameController);

        internal static bool ResolvePoeForeground(GameController? gameController)
        {
            try
            {
                return gameController?.Window?.IsForeground() == true;
            }
            catch
            {
                return false;
            }
        }

        internal static PluginClickRuntimeStateSnapshot ResolveSnapshot(
            ClickItSettings? settings,
            InputHandler? inputHandler,
            LazyModeBlockerService? lazyModeBlockerService,
            GameController? gameController,
            IReadOnlyList<LabelOnGround>? labels = null)
        {
            if (settings == null)
                return default;

            bool lazyModeEnabled = settings.LazyMode.Value;
            bool lazyModeDisableActive = lazyModeEnabled
                && ResolveLazyModeDisableActive(settings, inputHandler);
            IReadOnlyList<LabelOnGround>? effectiveLabels = labels
                ?? (IReadOnlyList<LabelOnGround>?)gameController?.IngameState?.IngameUi?.ItemsOnGroundLabelsVisible;

            return BuildSnapshot(
                lazyModeEnabled,
                lazyModeDisableActive,
                ResolveHasLazyModeRestrictedItems(lazyModeBlockerService, effectiveLabels),
                ResolveIsRitualActive(gameController),
                ResolvePoeForeground(gameController));
        }

        internal static PluginClickRuntimeStateSnapshot ResolveSnapshot(
            ClickItSettings? settings,
            InputHandler? inputHandler,
            GameController? gameController,
            PluginLazyModeContextSnapshot lazyModeContext)
        {
            if (settings == null)
                return default;

            bool lazyModeEnabled = settings.LazyMode.Value;
            bool lazyModeDisableActive = lazyModeEnabled
                && ResolveLazyModeDisableActive(settings, inputHandler);

            return BuildSnapshot(
                lazyModeEnabled,
                lazyModeDisableActive,
                lazyModeContext.HasLazyModeRestrictedItems,
                lazyModeContext.IsRitualActive,
                ResolvePoeForeground(gameController));
        }

        internal static bool ResolveLazyModeDisableActive(ClickItSettings settings, InputHandler? inputHandler)
        {
            if (inputHandler != null)
                return inputHandler.IsLazyModeDisableActiveForCurrentInputState();

            return Input.GetKeyState(settings.LazyModeDisableKeyBinding);
        }

        internal static bool ResolveUseLazyModeTiming(
            bool lazyModeEnabled,
            bool hasLazyModeRestrictedItems,
            bool isRitualActive)
            => lazyModeEnabled && !hasLazyModeRestrictedItems && !isRitualActive;

        internal static bool ShouldCancelOffscreenPathingForInputRelease(bool lazyModeEnabled, bool clickHotkeyHeld)
            => !lazyModeEnabled && !clickHotkeyHeld;

        internal static bool ShouldEvaluateRitualState(bool lazyModeEnabled, bool clickHotkeyActive)
            => lazyModeEnabled || !clickHotkeyActive;

        internal static bool ShouldEvaluateLazyModeRestrictedItems(bool lazyModeEnabled)
            => lazyModeEnabled;

        internal static bool ShouldRunManualUiHoverCoroutine(bool manualUiHoverEnabled, bool lazyModeEnabled)
            => manualUiHoverEnabled && !lazyModeEnabled;

        internal static bool ResolveManualUiHoverMode(ClickItSettings? settings, bool clickHotkeyActive)
            => ResolveManualUiHoverMode(
                settings?.ClickOnManualUiHoverOnly?.Value == true,
                settings?.LazyMode?.Value == true,
                clickHotkeyActive);

        internal static bool ResolveManualUiHoverMode(
            bool manualUiHoverEnabled,
            bool lazyModeEnabled,
            bool clickHotkeyActive)
            => ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled, lazyModeEnabled) && !clickHotkeyActive;

        internal static bool ResolveShowLazyModeTarget(
            bool useLazyModeTiming,
            bool lazyModeDisableActive,
            bool poeForeground)
            => useLazyModeTiming && !lazyModeDisableActive && poeForeground;

        internal static PluginClickFrequencyTargetDecision ResolveFrequencyTargetDecision(ClickItSettings settings, PluginClickRuntimeStateSnapshot runtimeState)
            => new(
                ClickTargetMs: settings.ClickFrequencyTarget.Value,
                LazyModeTargetMs: settings.LazyModeClickLimiting.Value,
                ShowLazyModeTarget: runtimeState.ShowLazyModeTarget);

        internal static PluginClickGateDecision ResolveRegularClickGateDecision(
            InputHandler? inputHandler,
            GameController? gameController,
            PluginClickRuntimeStateSnapshot runtimeState,
            bool hotkeyActive,
            long elapsedMilliseconds,
            double targetTime)
        {
            bool readyByTime = elapsedMilliseconds >= targetTime;
            bool canClick = inputHandler != null
                && gameController != null
                && inputHandler.CanClick(
                    gameController,
                    runtimeState.HasLazyModeRestrictedItems,
                    runtimeState.IsRitualActive);
            bool hotkeyHeld = inputHandler == null || hotkeyActive;

            return new PluginClickGateDecision(
                ReadyByTime: readyByTime,
                CanClick: canClick,
                ShouldCancelOffscreenPathing: ShouldCancelOffscreenPathingForInputRelease(runtimeState.LazyModeEnabled, hotkeyHeld),
                FailureReason: ResolveRegularClickFailureReason(inputHandler, gameController, canClick));
        }

        internal static PluginClickReadinessDecision ResolveManualUiHoverGateDecision(
            InputHandler? inputHandler,
            GameController? gameController,
            long elapsedMilliseconds,
            double targetTime)
            => new(
                ReadyByTime: elapsedMilliseconds >= targetTime,
                CanClick: inputHandler != null
                    && gameController != null
                    && inputHandler.CanClickWithoutInputState(gameController));

        internal static string ResolveRegularClickFailureReason(
            InputHandler? inputHandler,
            GameController? gameController,
            bool canClick)
            => canClick
                ? "Timer gating"
                : inputHandler != null && gameController != null
                    ? inputHandler.GetCanClickFailureReason(gameController)
                    : "Unknown click blocker";

        internal static PluginClickRuntimeStateSnapshot BuildSnapshot(
            bool lazyModeEnabled,
            bool lazyModeDisableActive,
            bool hasLazyModeRestrictedItems,
            bool isRitualActive,
            bool poeForeground)
        {
            bool useLazyModeTiming = ResolveUseLazyModeTiming(lazyModeEnabled, hasLazyModeRestrictedItems, isRitualActive);
            bool showLazyModeTarget = ResolveShowLazyModeTarget(useLazyModeTiming, lazyModeDisableActive, poeForeground);
            return new PluginClickRuntimeStateSnapshot(
                LazyModeEnabled: lazyModeEnabled,
                LazyModeDisableActive: lazyModeDisableActive,
                HasLazyModeRestrictedItems: hasLazyModeRestrictedItems,
                IsRitualActive: isRitualActive,
                PoeForeground: poeForeground,
                UseLazyModeTiming: useLazyModeTiming,
                ShowLazyModeTarget: showLazyModeTarget);
        }
    }
}