namespace ClickIt.Shared.Input
{
    public class InputHandler(ClickItSettings settings, PerformanceMonitor performanceMonitor, ErrorHandler? errorHandler = null)
    {
        private readonly ClickItSettings _settings = settings;
        private readonly ErrorHandler? _errorHandler = errorHandler;
        private readonly PerformanceMonitor _performanceMonitor = performanceMonitor;
        private readonly ToggleItemsController _toggleItemsController = new(settings, Keyboard.KeyPress);
        private readonly InputHotkeyStateService _hotkeyStateService = new(settings);
        private readonly LabelClickPointResolver _labelClickPointResolver = new(settings);
        private InteractionExecutor? _interactionExecutor;

        private InteractionExecutor InteractionExecutor
            => _interactionExecutor ??= new InteractionExecutor(
                _settings,
                _performanceMonitor,
                () => IsClickHotkeyActiveForCurrentInputState(),
                _errorHandler);

        public long GetSuccessfulClickSequence()
            => InteractionExecutor.GetSuccessfulClickSequence();

        public bool IsLabelFullyOverlapped(LabelOnGround label, IReadOnlyList<LabelOnGround>? allLabels)
            => _labelClickPointResolver.IsLabelFullyOverlapped(label, allLabels);

        public Vector2 CalculateClickPosition(LabelOnGround label, Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels = null)
            => _labelClickPointResolver.CalculateClickPosition(label, windowTopLeft, allLabels);

        public bool TryCalculateClickPosition(
            LabelOnGround label,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels,
            Func<Vector2, bool>? isClickableArea,
            out Vector2 clickPosition)
            => _labelClickPointResolver.TryCalculateClickPosition(label, windowTopLeft, allLabels, isClickableArea, out clickPosition);

        public bool TriggerToggleItems()
            => _toggleItemsController.TriggerToggleItems();

        public int GetToggleItemsPostClickBlockMs()
            => _toggleItemsController.GetToggleItemsPostClickBlockMs();

        public void PerformClick(
            Vector2 position,
            Element? expectedElement = null,
            GameController? gameController = null,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
            => InteractionExecutor.PerformClick(position, expectedElement, gameController, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);

        public void PerformClickAndHold(
            Vector2 position,
            int holdDurationMs,
            Element? expectedElement = null,
            GameController? gameController = null,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
            => InteractionExecutor.PerformClickAndHold(position, holdDurationMs, expectedElement, gameController, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);

        public Element? HoverAndGetUIHover(Vector2 screenPoint, GameController? gameController, int delayMs = -1)
            => InteractionExecutor.HoverAndGetUIHover(screenPoint, gameController, delayMs);

        public bool CanClick(GameController gameController, bool hasLazyModeRestrictedItemsOnScreen = false, bool isRitualActive = false)
        {
            if (gameController == null)
                return false;

            bool keyState = IsClickKeyStateActive(hasLazyModeRestrictedItemsOnScreen);
            bool clickHotkeyHeld = IsClickHotkeyHeld();

            return keyState
                && IsPOEActive(gameController)
                && (_settings?.BlockOnOpenLeftRightPanel?.Value != true || !IsPanelOpen(gameController))
                && !IsInTownOrHideout(gameController)
                && (!isRitualActive || clickHotkeyHeld)
                && !IsInToggleItemsPostClickBlockWindow()
                && !IsBlockedByUiOrEscapeState(gameController);
        }

        private bool IsClickKeyStateActive(bool hasLazyModeRestrictedItemsOnScreen)
        {
            bool lazyModeActive = _settings?.LazyMode != null
                && _settings.LazyMode.Value
                && !hasLazyModeRestrictedItemsOnScreen
                && !IsLazyModeDisableActiveForCurrentInputState();

            return lazyModeActive || IsClickHotkeyHeld();
        }

        private bool IsClickHotkeyHeld()
            => IsClickHotkeyActiveForCurrentInputState();

        public bool IsClickHotkeyActiveForCurrentInputState()
            => _hotkeyStateService.IsClickHotkeyActive(Keyboard.IsKeyDown);

        private bool IsBlockedByUiOrEscapeState(GameController gameController)
        {
            if (gameController.Game.IsEscapeState)
                return true;

            return GetUiBlockingReason(gameController) != null;
        }

        private string? GetUiBlockingReason(GameController? gameController)
        {
            var uiState = gameController?.IngameState?.IngameUi;

            if (uiState?.ChatTitlePanel?.IsVisible ?? false)
                return "Chat is open.";
            if (IsUiElementVisible(uiState, "Atlas", "AtlasPanel"))
                return "Atlas panel is open.";
            if (uiState?.AtlasTreePanel?.IsVisible ?? false)
                return "Atlas tree panel is open.";
            if (uiState?.TreePanel?.IsVisible ?? false)
                return "Passive tree panel is open.";
            if ((uiState?.UltimatumPanel?.IsVisible ?? false) && !_settings.IsOtherUltimatumClickEnabled())
                return "Ultimatum panel is open (Click Ultimatum Choices is disabled).";
            if (IsUiElementVisible(uiState, "SyndicatePanel", "BetrayalWindow"))
                return "Syndicate panel is open.";
            if (uiState?.IncursionWindow?.IsVisible ?? false)
                return "Incursion window is open.";
            if (uiState?.RitualWindow?.IsVisible ?? false)
                return "Ritual window is open.";
            if (uiState?.SanctumFloorWindow?.IsVisible ?? false)
                return "Sanctum floor window is open.";
            if (uiState?.SanctumRewardWindow?.IsVisible ?? false)
                return "Sanctum reward window is open.";
            if (uiState?.MicrotransactionShopWindow?.IsVisible ?? false)
                return "Microtransaction shop window is open.";
            if (uiState?.ResurrectPanel?.IsVisible ?? false)
                return "Resurrect panel is open.";
            if (uiState?.NpcDialog?.IsVisible ?? false)
                return "NPC dialog is open.";

            return null;
        }

        private static bool IsUiElementVisible(object? uiState, params string[] propertyNames)
        {
            if (uiState == null)
                return false;

            Type uiStateType = uiState.GetType();
            for (int i = 0; i < propertyNames.Length; i++)
            {
                object? element = uiStateType.GetProperty(propertyNames[i])?.GetValue(uiState);
                if (element?.GetType().GetProperty("IsVisible")?.GetValue(element) is true)
                    return true;
            }

            return false;
        }

        public string GetCanClickFailureReason(GameController gameController)
        {
            if (gameController?.Window?.IsForeground() == false)
                return "PoE not in focus.";

            var area = gameController?.Area?.CurrentArea;
            if (_settings.BlockOnOpenLeftRightPanel.Value)
            {
                var ui = gameController?.IngameState?.IngameUi;
                if (ui?.OpenLeftPanel?.Address != 0 || ui?.OpenRightPanel?.Address != 0)
                    return "Panel is open.";
            }

            if (area?.IsTown == true || area?.IsHideout == true)
                return "In town/hideout.";

            if (IsInToggleItemsPostClickBlockWindow())
                return "Waiting after Toggle Item View.";

            string? uiReason = GetUiBlockingReason(gameController);
            if (!string.IsNullOrEmpty(uiReason))
                return uiReason;

            if (gameController?.Game?.IsEscapeState == true)
                return "Escape menu is open.";

            return "Clicking disabled.";
        }

        public bool CanClickWithoutInputState(GameController gameController)
        {
            if (gameController == null)
                return false;

            return IsPOEActive(gameController)
                && (_settings?.BlockOnOpenLeftRightPanel?.Value != true || !IsPanelOpen(gameController))
                && !IsInTownOrHideout(gameController)
                && !IsInToggleItemsPostClickBlockWindow()
                && !IsBlockedByUiOrEscapeState(gameController);
        }

        public bool IsClickHotkeyPressed(TimeCache<List<LabelOnGround>>? cachedLabels, LabelFilterService? labelFilterService)
        {
            bool hotkeyHeld = IsClickHotkeyHeld();
            if (!_settings.LazyMode.Value)
            {
                return hotkeyHeld;
            }

            var labels = cachedLabels?.Value;
            bool hasRestricted = labelFilterService?.HasLazyModeRestrictedItemsOnScreen(labels) ?? false;
            bool disableKeyHeld = IsLazyModeDisableActiveForCurrentInputState();
            var (_, _, mouseButtonBlocks) = GetMouseButtonBlockingState(_settings, Keyboard.IsKeyDown);

            if (hotkeyHeld)
            {
                return true;
            }

            return !hasRestricted && !disableKeyHeld && !mouseButtonBlocks;
        }

        public bool IsLazyModeDisableActiveForCurrentInputState()
            => _hotkeyStateService.IsLazyModeDisableActive(Keyboard.IsKeyDown);

        public static bool ResolveLazyModeDisableActive(bool toggleModeEnabled, bool disableKeyPressed, ref bool toggledState, ref bool wasPressedLastFrame)
            => InputHotkeyStateService.ResolveLazyModeDisableActive(toggleModeEnabled, disableKeyPressed, ref toggledState, ref wasPressedLastFrame);

        public static bool ResolveClickHotkeyActive(bool toggleModeEnabled, bool hotkeyPressed, ref bool toggledState, ref bool wasPressedLastFrame)
            => InputHotkeyStateService.ResolveClickHotkeyActive(toggleModeEnabled, hotkeyPressed, ref toggledState, ref wasPressedLastFrame);

        public static (bool leftClickBlocks, bool rightClickBlocks, bool mouseButtonBlocks)
            GetMouseButtonBlockingState(ClickItSettings settings, Func<Keys, bool> keyStateProvider)
            => InputHotkeyStateService.GetMouseButtonBlockingState(settings, keyStateProvider);

        private static bool IsPOEActive(GameController gameController)
            => gameController.Window.IsForeground();

        private static bool IsPanelOpen(GameController gameController)
        {
            if (gameController == null)
                return false;

            var ui = gameController.IngameState?.IngameUi;
            if (ui == null)
                return false;

            return ui.OpenLeftPanel.Address != 0 || ui.OpenRightPanel.Address != 0;
        }

        private static bool IsInTownOrHideout(GameController gameController)
        {
            if (gameController == null)
                return false;

            var area = gameController.Area?.CurrentArea;
            if (area == null)
                return false;

            return area.IsHideout || area.IsTown;
        }

        private bool IsInToggleItemsPostClickBlockWindow()
            => _toggleItemsController.IsInPostClickBlockWindow();
    }
}
