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

    internal static class PluginClickRuntimeStateEvaluator
    {
        internal static bool ResolveHotkeyActive(PluginServices services)
            => ResolveHotkeyActive(services.InputHandler, services.CachedLabels, services.LazyModeBlockerService);

        internal static bool ResolveHotkeyActive(
            InputHandler? inputHandler,
            TimeCache<List<LabelOnGround>>? cachedLabels,
            LazyModeBlockerService? lazyModeBlockerService)
            => inputHandler?.IsClickHotkeyPressed(
                cachedLabels,
                labels => ResolveHasLazyModeRestrictedItems(lazyModeBlockerService, labels)) ?? false;

        internal static bool ResolveHasLazyModeRestrictedItems(
            LazyModeBlockerService? lazyModeBlockerService,
            IReadOnlyList<LabelOnGround>? labels)
            => lazyModeBlockerService?.HasRestrictedItemsOnScreen(labels) ?? false;

        internal static bool ResolveIsRitualActive(GameController? gameController)
            => EntityHelpers.IsRitualActive(gameController);

        internal static bool ResolvePoeForeground(GameController? gameController)
            => gameController?.Window?.IsForeground() == true;

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

        internal static bool ShouldRestartClickTimerAfterSuccessfulClick(long clickSequenceBefore, long clickSequenceAfter)
            => clickSequenceAfter > clickSequenceBefore;

        internal static bool ShouldCancelOffscreenPathingForInputRelease(bool lazyModeEnabled, bool clickHotkeyHeld)
            => !lazyModeEnabled && !clickHotkeyHeld;

        internal static bool ShouldEvaluateRitualState(bool lazyModeEnabled, bool clickHotkeyActive)
            => lazyModeEnabled || !clickHotkeyActive;

        internal static bool ShouldEvaluateLazyModeRestrictedItems(bool lazyModeEnabled)
            => lazyModeEnabled;

        internal static bool ShouldRunManualUiHoverCoroutine(bool manualUiHoverEnabled, bool lazyModeEnabled)
            => manualUiHoverEnabled && !lazyModeEnabled;

        internal static bool ShouldRunManualUiHoverCoroutine(bool manualUiHoverEnabled, bool lazyModeEnabled, bool clickHotkeyActive)
            => ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled, lazyModeEnabled) && !clickHotkeyActive;

        internal static bool ShouldRunManualUiHoverCoroutine(ClickItSettings? settings)
            => ShouldRunManualUiHoverCoroutine(
                settings?.ClickOnManualUiHoverOnly?.Value == true,
                settings?.LazyMode?.Value == true);

        internal static bool ShouldSuppressRegularClickForManualUiHoverMode(bool manualUiHoverEnabled, bool lazyModeEnabled, bool clickHotkeyActive)
            => ShouldRunManualUiHoverCoroutine(manualUiHoverEnabled, lazyModeEnabled, clickHotkeyActive);

        internal static bool ResolveShowLazyModeTarget(
            bool useLazyModeTiming,
            bool lazyModeDisableActive,
            bool poeForeground)
            => useLazyModeTiming && !lazyModeDisableActive && poeForeground;

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