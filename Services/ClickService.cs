using System.Collections;
using ExileCore;
using ExileCore.Shared;
using ExileCore.Shared.Cache;
using ClickIt.Components;
using ClickIt.Utils;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace ClickIt.Services
{

    public partial class ClickService(
        ClickItSettings settings,
        GameController gameController,
        ErrorHandler errorHandler,
        AltarService altarService,
        WeightCalculator weightCalculator,
        Rendering.AltarDisplayRenderer altarDisplayRenderer,
        Func<Vector2, string, bool> pointIsInClickableArea,
        InputHandler inputHandler,
        LabelFilterService labelFilterService,
        ShrineService shrineService,
        PathfindingService pathfindingService,
        Func<bool> groundItemsVisible,
        TimeCache<List<LabelOnGround>> cachedLabels,
        PerformanceMonitor performanceMonitor,
        Action<string, int>? freezeDebugTelemetrySnapshot = null)
        : Observability.IClickTelemetryPublisher
    {
        private readonly ClickItSettings settings = settings ?? throw new ArgumentNullException(nameof(settings));
        private readonly GameController gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
        private readonly ErrorHandler errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        private readonly AltarService altarService = altarService ?? throw new ArgumentNullException(nameof(altarService));
        private readonly WeightCalculator weightCalculator = weightCalculator ?? throw new ArgumentNullException(nameof(weightCalculator));
        private readonly Rendering.AltarDisplayRenderer altarDisplayRenderer = altarDisplayRenderer ?? throw new ArgumentNullException(nameof(altarDisplayRenderer));
        private readonly Func<Vector2, string, bool> pointIsInClickableArea = pointIsInClickableArea ?? throw new ArgumentNullException(nameof(pointIsInClickableArea));
        private readonly InputHandler inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
        private readonly LabelFilterService labelFilterService = labelFilterService ?? throw new ArgumentNullException(nameof(labelFilterService));
        private readonly ShrineService shrineService = shrineService ?? throw new ArgumentNullException(nameof(shrineService));
        private readonly PathfindingService pathfindingService = pathfindingService ?? throw new ArgumentNullException(nameof(pathfindingService));
        private readonly Func<bool> groundItemsVisible = groundItemsVisible ?? throw new ArgumentNullException(nameof(groundItemsVisible));
        private readonly TimeCache<List<LabelOnGround>> cachedLabels = cachedLabels;
        private readonly PerformanceMonitor performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        private readonly Action<string, int>? freezeDebugTelemetrySnapshot = freezeDebugTelemetrySnapshot;
        private readonly ChestLootSettlementState chestLootSettlementState = new();
        private readonly ClickRuntimeState _runtimeState = new();
        private long _gruelingGauntletPassiveCacheTimestampMs;
        private bool _gruelingGauntletPassiveCachedValue;
        private bool _gruelingGauntletPassiveCacheHasValue;

        // Thread safety lock to prevent race conditions during element access
        private readonly object _elementAccessLock = new();
        internal object GetElementAccessLock()
        {
            return _elementAccessLock;
        }

        // Helper to avoid allocating debug message strings when debug logging is disabled
        private void DebugLog(Func<string> messageFactory)
        {
            if (settings.DebugMode?.Value != true)
                return;

            string message = messageFactory();
            SetLatestRuntimeDebugLog(message);

            if (settings.LogMessages?.Value == true)
            {
                errorHandler.LogMessage(message);
            }
        }

        private bool IsClickableInEitherSpace(Vector2 clientPoint, string path)
        {
            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            return IsClickableInEitherSpace(clientPoint, windowTopLeft, pointIsInClickableArea, path);
        }

        internal void CancelOffscreenPathingState()
        {
            OffscreenPathing.ClearStickyOffscreenTarget();
            pathfindingService.ClearLatestPath();
        }

        internal void CancelPostChestLootSettlementState()
        {
            ChestLootSettlement.ClearPendingChestOpenConfirmation();
            ChestLootSettlement.ClearPostChestLootSettlementWatch();
        }

        internal static bool IsClickableInEitherSpace(
            Vector2 clientPoint,
            Vector2 windowTopLeft,
            Func<Vector2, string, bool> clickabilityCheck,
            string path)
        {
            return clickabilityCheck(clientPoint, path)
                || clickabilityCheck(clientPoint + windowTopLeft, path);
        }

        private bool EnsureCursorInsideGameWindowForClick(string outsideWindowLogMessage)
        {
            if (settings.VerifyCursorInGameWindowBeforeClick?.Value == true && !IsCursorInsideGameWindow())
            {
                DebugLog(() => outsideWindowLogMessage);
                return false;
            }

            return true;
        }

        private void PerformLockedClick(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
            using (LockManager.AcquireStatic(_elementAccessLock))
            {
                inputHandler.PerformClick(clickPos, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);
            }
        }

        private void PerformLockedHoldClick(
            Vector2 clickPos,
            int holdDurationMs,
            Element? expectedElement,
            GameController? controller,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
            using (LockManager.AcquireStatic(_elementAccessLock))
            {
                inputHandler.PerformClickAndHold(clickPos, holdDurationMs, expectedElement, controller, forceUiHoverVerification, allowWhenHotkeyInactive, avoidCursorMove);
            }
        }

        internal bool ExecuteLabelInteraction(
            Vector2 clickPos,
            Element? expectedElement,
            GameController? controller,
            bool useHoldClick,
            int holdDurationMs = 0,
            bool forceUiHoverVerification = false,
            bool allowWhenHotkeyInactive = false,
            bool avoidCursorMove = false)
        {
            return useHoldClick
                ? PerformLabelHoldClick(
                    clickPos,
                    expectedElement,
                    controller,
                    holdDurationMs,
                    forceUiHoverVerification,
                    allowWhenHotkeyInactive,
                    avoidCursorMove)
                : PerformLabelClick(
                    clickPos,
                    expectedElement,
                    controller,
                    forceUiHoverVerification,
                    allowWhenHotkeyInactive,
                    avoidCursorMove);
        }

        private bool IsCursorInsideGameWindow()
        {
            try
            {
                var winRect = gameController?.Window.GetWindowRectangleTimeCache;
                if (winRect == null) return true;
                var cursor = Mouse.GetCursorPosition();
                return cursor.X >= winRect.Value.X && cursor.Y >= winRect.Value.Y && cursor.X <= winRect.Value.X + winRect.Value.Width && cursor.Y <= winRect.Value.Y + winRect.Value.Height;
            }
            catch
            {
                // If we cannot determine the cursor/window bounds assume it's fine so we don't block clicks unexpectedly
                return true;
            }
        }

        internal static void ClearThreadLocalStorageForCurrentThread()
        {
            _threadGroundLabelEntityAddresses?.Clear();
            _threadGroundLabelEntityAddresses = null;

            _threadSkillBarEntriesBuffer?.Clear();
            _threadSkillBarEntriesBuffer = null;
        }

        internal static void ClearThreadLocalStorageForTests()
        {
            ClearThreadLocalStorageForCurrentThread();
        }

        private void HoldDebugTelemetryAfterSuccessfulInteraction(string reason)
        {
            if (settings.DebugMode?.Value != true || settings.RenderDebug?.Value != true)
                return;

            int holdDurationMs = Math.Max(0, settings.DebugFreezeSuccessfulInteractionMs?.Value ?? 0);
            if (holdDurationMs <= 0)
                return;

            freezeDebugTelemetrySnapshot?.Invoke(reason, holdDurationMs);
        }

    }
}


