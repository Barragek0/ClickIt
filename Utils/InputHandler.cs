using ExileCore;
using System.Diagnostics;
using ExileCore.Shared.Cache;
using System.Runtime.InteropServices;
using System.Text;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
using ExileCore.PoEMemory;
using System.Windows.Forms;
namespace ClickIt.Utils
{
    public partial class InputHandler(ClickItSettings settings, PerformanceMonitor performanceMonitor, ErrorHandler? errorHandler = null)
    {
        private const string HeistContractPathMarker = "Metadata/Items/Heist/Contracts/";
        private const string HeistContractNamePrefix = "Contract:";

        private readonly ClickItSettings _settings = settings;
        private readonly Random _random = new();
        private readonly ErrorHandler? _errorHandler = errorHandler;
        private readonly PerformanceMonitor _performanceMonitor = performanceMonitor;
        private long _lastClickTimestampMs = 0;
        private long _successfulClickSequence = 0;
        private long _lastToggleItemsTimestampMs = 0;
        private bool _lazyModeDisableToggled;
        private bool _lazyModeDisableKeyWasDown;

        public long GetSuccessfulClickSequence()
        {
            return Interlocked.Read(ref _successfulClickSequence);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private static bool TryGetLabelClientRect(LabelOnGround? label, out RectangleF rect)
        {
            rect = default;

            Element? element = label?.Label;
            if (element == null || !element.IsValid)
                return false;

            object? maybeRect = element.GetClientRect();
            if (maybeRect is not RectangleF r)
                return false;

            if (r.Width <= 0 || r.Height <= 0)
                return false;

            rect = r;
            return true;
        }

        private static bool TryGetIntersection(RectangleF a, RectangleF b, out RectangleF intersection)
        {
            intersection = default;

            float left = Math.Max(a.Left, b.Left);
            float top = Math.Max(a.Top, b.Top);
            float right = Math.Min(a.Right, b.Right);
            float bottom = Math.Min(a.Bottom, b.Bottom);

            if (right <= left || bottom <= top)
                return false;

            intersection = new RectangleF(left, top, right - left, bottom - top);
            return true;
        }

        private static bool IsPointInsideRect(Vector2 point, RectangleF rect)
        {
            return point.X >= rect.Left
                && point.X <= rect.Right
                && point.Y >= rect.Top
                && point.Y <= rect.Bottom;
        }

        private static bool IsPointBlocked(Vector2 point, IReadOnlyList<RectangleF> blockedAreas)
        {
            for (int i = 0; i < blockedAreas.Count; i++)
            {
                if (IsPointInsideRect(point, blockedAreas[i]))
                    return true;
            }

            return false;
        }

        private static bool IsPointClickable(Vector2 point, Func<Vector2, bool>? isClickableArea)
        {
            return isClickableArea == null || isClickableArea(point);
        }

        private static RectangleF GetVirtualScreenBounds()
        {
            var vs = SystemInformation.VirtualScreen;
            return new RectangleF(vs.Left, vs.Top, vs.Width, vs.Height);
        }

        internal static bool IsSafeAutomationPoint(Vector2 point, RectangleF gameWindowRect, RectangleF virtualScreenRect)
        {
            if (!IsPointInsideRect(point, virtualScreenRect))
                return false;

            if (gameWindowRect.Width <= 0 || gameWindowRect.Height <= 0)
                return true;

            if (!TryGetIntersection(gameWindowRect, virtualScreenRect, out RectangleF allowedRect))
                return false;

            return IsPointInsideRect(point, allowedRect);
        }

        private static bool TryValidateAutomationScreenPoint(Vector2 point, GameController? gameController, out string reason)
        {
            RectangleF virtualScreenRect = GetVirtualScreenBounds();
            if (!IsPointInsideRect(point, virtualScreenRect))
            {
                reason = $"outside virtual screen bounds {virtualScreenRect}";
                return false;
            }

            RectangleF gameWindowRect = gameController?.Window?.GetWindowRectangleTimeCache ?? RectangleF.Empty;
            if (!IsSafeAutomationPoint(point, gameWindowRect, virtualScreenRect))
            {
                reason = $"outside safe game window bounds {gameWindowRect}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static Vector2 ClampPointToRect(Vector2 point, RectangleF rect)
        {
            return new Vector2(
                Math.Clamp(point.X, rect.Left, rect.Right),
                Math.Clamp(point.Y, rect.Top, rect.Bottom));
        }

        internal static bool IsHeistContractWorldItem(string? itemPath, string? renderName)
        {
            bool byPath = !string.IsNullOrWhiteSpace(itemPath)
                && itemPath.IndexOf(HeistContractPathMarker, StringComparison.OrdinalIgnoreCase) >= 0;
            if (byPath)
                return true;

            return !string.IsNullOrWhiteSpace(renderName)
                && renderName.StartsWith(HeistContractNamePrefix, StringComparison.OrdinalIgnoreCase);
        }

        internal static Vector2 ResolvePreferredLabelPoint(RectangleF rect, EntityType itemType, int chestHeightOffset, string? itemPath, string? renderName)
        {
            Vector2 preferredPoint = rect.Center;

            if (itemType == EntityType.Chest)
            {
                preferredPoint.Y -= chestHeightOffset;
            }

            // Contract labels often render under dropped items; bias lower label area to avoid clicking nearby item hitboxes.
            if (itemType == EntityType.WorldItem && IsHeistContractWorldItem(itemPath, renderName))
            {
                float safeLowerY = rect.Top + (rect.Height * 0.84f);
                preferredPoint.Y = Math.Clamp(safeLowerY, rect.Top + 1f, rect.Bottom - 1f);
            }

            return preferredPoint;
        }

        internal static bool TryResolveVisibleClickPoint(RectangleF targetRect, Vector2 preferredPoint, IReadOnlyList<RectangleF> blockedAreas, out Vector2 resolvedPoint)
        {
            Vector2 clampedPreferred = ClampPointToRect(preferredPoint, targetRect);
            if (blockedAreas == null || blockedAreas.Count == 0 || !IsPointBlocked(clampedPreferred, blockedAreas))
            {
                resolvedPoint = clampedPreferred;
                return true;
            }

            const int cols = 7;
            const int rows = 5;
            float stepX = targetRect.Width / cols;
            float stepY = targetRect.Height / rows;

            Vector2 best = clampedPreferred;
            float bestDistanceSq = float.MaxValue;

            for (int y = 0; y < rows; y++)
            {
                float sampleY = targetRect.Top + ((y + 0.5f) * stepY);
                for (int x = 0; x < cols; x++)
                {
                    float sampleX = targetRect.Left + ((x + 0.5f) * stepX);
                    Vector2 candidate = new(sampleX, sampleY);
                    if (IsPointBlocked(candidate, blockedAreas))
                        continue;

                    float dx = candidate.X - clampedPreferred.X;
                    float dy = candidate.Y - clampedPreferred.Y;
                    float distanceSq = dx * dx + dy * dy;
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        best = candidate;
                    }
                }
            }

            if (bestDistanceSq < float.MaxValue)
            {
                resolvedPoint = best;
                return true;
            }

            resolvedPoint = clampedPreferred;
            return false;
        }

        internal static bool TryResolveVisibleClickablePoint(
            RectangleF targetRect,
            Vector2 preferredPoint,
            IReadOnlyList<RectangleF> blockedAreas,
            Func<Vector2, bool>? isClickableArea,
            out Vector2 resolvedPoint)
        {
            Vector2 clampedPreferred = ClampPointToRect(preferredPoint, targetRect);
            if ((blockedAreas == null || blockedAreas.Count == 0 || !IsPointBlocked(clampedPreferred, blockedAreas))
                && IsPointClickable(clampedPreferred, isClickableArea))
            {
                resolvedPoint = clampedPreferred;
                return true;
            }

            const int cols = 7;
            const int rows = 5;
            float stepX = targetRect.Width / cols;
            float stepY = targetRect.Height / rows;

            Vector2 best = clampedPreferred;
            float bestDistanceSq = float.MaxValue;

            for (int y = 0; y < rows; y++)
            {
                float sampleY = targetRect.Top + ((y + 0.5f) * stepY);
                for (int x = 0; x < cols; x++)
                {
                    float sampleX = targetRect.Left + ((x + 0.5f) * stepX);
                    Vector2 candidate = new(sampleX, sampleY);

                    if (blockedAreas != null && blockedAreas.Count > 0 && IsPointBlocked(candidate, blockedAreas))
                        continue;

                    if (!IsPointClickable(candidate, isClickableArea))
                        continue;

                    float dx = candidate.X - clampedPreferred.X;
                    float dy = candidate.Y - clampedPreferred.Y;
                    float distanceSq = dx * dx + dy * dy;
                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        best = candidate;
                    }
                }
            }

            if (bestDistanceSq < float.MaxValue)
            {
                resolvedPoint = best;
                return true;
            }

            resolvedPoint = clampedPreferred;
            return false;
        }

        internal static Vector2 ResolveVisibleClickPoint(RectangleF targetRect, Vector2 preferredPoint, IReadOnlyList<RectangleF> blockedAreas)
        {
            TryResolveVisibleClickPoint(targetRect, preferredPoint, blockedAreas, out Vector2 resolvedPoint);
            return resolvedPoint;
        }

        private static List<RectangleF> CollectBlockingOverlaps(LabelOnGround targetLabel, RectangleF targetRect, IReadOnlyList<LabelOnGround>? allLabels)
        {
            List<RectangleF> blockedAreas = new();
            if (allLabels == null || allLabels.Count == 0)
                return blockedAreas;

            for (int i = 0; i < allLabels.Count; i++)
            {
                LabelOnGround? other = allLabels[i];
                if (other == null || ReferenceEquals(other, targetLabel))
                    continue;

                if (!TryGetLabelClientRect(other, out RectangleF otherRect))
                    continue;

                if (TryGetIntersection(targetRect, otherRect, out RectangleF overlap))
                {
                    blockedAreas.Add(overlap);
                }
            }

            return blockedAreas;
        }

        public bool IsLabelFullyOverlapped(LabelOnGround label, IReadOnlyList<LabelOnGround>? allLabels)
        {
            bool avoidOverlapsEnabled = _settings.AvoidOverlappingLabelClickPoints?.Value != false;
            if (!avoidOverlapsEnabled)
                return false;

            if (!TryGetLabelClientRect(label, out RectangleF rect))
                return false;

            Vector2 preferredPoint = rect.Center;
            if (label.ItemOnGround.Type == EntityType.Chest)
            {
                preferredPoint.Y -= _settings.ChestHeightOffset;
            }

            List<RectangleF> blockedAreas = CollectBlockingOverlaps(label, rect, allLabels);
            return !TryResolveVisibleClickPoint(rect, preferredPoint, blockedAreas, out _);
        }

        public Vector2 CalculateClickPosition(LabelOnGround label, Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels = null)
        {
            if (!TryGetLabelClientRect(label, out RectangleF rect))
            {
                throw new InvalidOperationException("Label element is invalid");
            }

            var item = label.ItemOnGround;
            Vector2 preferredPoint = ResolvePreferredLabelPoint(
                rect,
                item?.Type ?? EntityType.WorldItem,
                _settings.ChestHeightOffset,
                item?.Path,
                item?.RenderName);

            Vector2 resolvedPoint = preferredPoint;
            bool avoidOverlapsEnabled = _settings.AvoidOverlappingLabelClickPoints?.Value != false;
            if (avoidOverlapsEnabled)
            {
                List<RectangleF> blockedAreas = CollectBlockingOverlaps(label, rect, allLabels);
                resolvedPoint = ResolveVisibleClickPoint(rect, preferredPoint, blockedAreas);
            }

            float jitterRange = 2f;
            float jitterX = (float)(_random.NextDouble() * (jitterRange * 2) - jitterRange);
            float jitterY = (float)(_random.NextDouble() * (jitterRange * 2) - jitterRange);

            Vector2 jitteredPoint = resolvedPoint + new Vector2(jitterX, jitterY);
            if (!IsPointInsideRect(jitteredPoint, rect))
            {
                jitteredPoint = resolvedPoint;
            }

            return jitteredPoint + windowTopLeft;
        }

        public bool TryCalculateClickPosition(
            LabelOnGround label,
            Vector2 windowTopLeft,
            IReadOnlyList<LabelOnGround>? allLabels,
            Func<Vector2, bool>? isClickableArea,
            out Vector2 clickPosition)
        {
            clickPosition = default;

            if (!TryGetLabelClientRect(label, out RectangleF rect))
                return false;

            var item = label.ItemOnGround;
            Vector2 preferredPoint = ResolvePreferredLabelPoint(
                rect,
                item?.Type ?? EntityType.WorldItem,
                _settings.ChestHeightOffset,
                item?.Path,
                item?.RenderName);

            List<RectangleF> blockedAreas = [];
            bool avoidOverlapsEnabled = _settings.AvoidOverlappingLabelClickPoints?.Value != false;
            if (avoidOverlapsEnabled)
            {
                blockedAreas = CollectBlockingOverlaps(label, rect, allLabels);
            }

            if (!TryResolveVisibleClickablePoint(rect, preferredPoint, blockedAreas, isClickableArea, out Vector2 resolvedPoint))
                return false;

            float jitterRange = 2f;
            float jitterX = (float)(_random.NextDouble() * (jitterRange * 2) - jitterRange);
            float jitterY = (float)(_random.NextDouble() * (jitterRange * 2) - jitterRange);

            Vector2 jitteredPoint = resolvedPoint + new Vector2(jitterX, jitterY);
            if (!IsPointInsideRect(jitteredPoint, rect) || !IsPointClickable(jitteredPoint, isClickableArea))
            {
                jitteredPoint = resolvedPoint;
            }

            clickPosition = jitteredPoint + windowTopLeft;
            return true;
        }

        public bool TriggerToggleItems()
        {
            if (!_settings.ToggleItems.Value)
                return false;

            int intervalMs = Math.Max(100, _settings.ToggleItemsIntervalMs.Value);
            long now = Environment.TickCount64;

            if (_lastToggleItemsTimestampMs > 0)
            {
                long elapsed = now - _lastToggleItemsTimestampMs;
                if (elapsed >= 0 && elapsed < intervalMs)
                    return false;
            }

            Keyboard.KeyPress(_settings.ToggleItemsHotkey, 20);
            Keyboard.KeyPress(_settings.ToggleItemsHotkey, 20);
            _lastToggleItemsTimestampMs = now;
            return true;
        }

        public int GetToggleItemsPostClickBlockMs()
        {
            return Math.Max(0, _settings.ToggleItemsPostToggleClickBlockMs.Value);
        }

        private bool IsInToggleItemsPostClickBlockWindow()
        {
            int blockMs = GetToggleItemsPostClickBlockMs();
            if (blockMs <= 0 || _lastToggleItemsTimestampMs <= 0)
                return false;

            long elapsed = Environment.TickCount64 - _lastToggleItemsTimestampMs;
            return elapsed >= 0 && elapsed < blockMs;
        }
        public bool CanClick(GameController gameController, bool hasLazyModeRestrictedItemsOnScreen = false, bool isRitualActive = false)
        {
            if (gameController == null) return false;

            bool keyState = IsClickKeyStateActive(hasLazyModeRestrictedItemsOnScreen);
            bool clickHotkeyHeld = IsClickHotkeyHeld();

            return keyState &&
                IsPOEActive(gameController) &&
                (_settings?.BlockOnOpenLeftRightPanel?.Value != true || !IsPanelOpen(gameController)) &&
                !IsInTownOrHideout(gameController) &&
                (!isRitualActive || clickHotkeyHeld) &&
                !IsInToggleItemsPostClickBlockWindow() &&
                !IsBlockedByUiOrEscapeState(gameController);
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
        {
            return _settings?.ClickLabelKey != null && Input.GetKeyState(_settings.ClickLabelKey.Value);
        }

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
            if (uiState?.AtlasPanel?.IsVisible ?? false)
                return "Atlas panel is open.";
            if (uiState?.AtlasTreePanel?.IsVisible ?? false)
                return "Atlas tree panel is open.";
            if (uiState?.TreePanel?.IsVisible ?? false)
                return "Passive tree panel is open.";
            if ((uiState?.UltimatumPanel?.IsVisible ?? false) && !_settings.IsOtherUltimatumClickEnabled())
                return "Ultimatum panel is open (Click Ultimatum Choices is disabled).";
            if (uiState?.BetrayalWindow?.IsVisible ?? false)
                return "Betrayal window is open.";
            if (uiState?.SyndicatePanel?.IsVisible ?? false)
                return "Syndicate panel is open.";
            if (uiState?.SyndicateTree?.IsVisible ?? false)
                return "Syndicate tree panel is open.";
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

        public bool IsClickHotkeyPressed(TimeCache<List<LabelOnGround>>? cachedLabels, Services.LabelFilterService? labelFilterService)
        {
            bool hotkeyHeld = Input.GetKeyState(_settings.ClickLabelKey.Value);
            if (!_settings.LazyMode.Value)
            {
                return hotkeyHeld;
            }

            var labels = cachedLabels?.Value;
            bool hasRestricted = labelFilterService?.HasLazyModeRestrictedItemsOnScreen(labels) ?? false;
            bool disableKeyHeld = IsLazyModeDisableActiveForCurrentInputState();
            var (_, _, mouseButtonBlocks) = GetMouseButtonBlockingState(_settings, Input.GetKeyState);

            if (hotkeyHeld)
            {
                return true;
            }

            return !hasRestricted && !disableKeyHeld && !mouseButtonBlocks;
        }

        public bool IsLazyModeDisableActiveForCurrentInputState()
        {
            if (!_settings.LazyMode.Value)
            {
                _lazyModeDisableToggled = false;
            }

            bool toggleMode = _settings.IsLazyModeDisableHotkeyToggleModeEnabled();
            bool keyDown = Input.GetKeyState(_settings.LazyModeDisableKey.Value);
            return ResolveLazyModeDisableActive(toggleMode, keyDown, ref _lazyModeDisableToggled, ref _lazyModeDisableKeyWasDown);
        }

        public static bool ResolveLazyModeDisableActive(bool toggleModeEnabled, bool disableKeyPressed, ref bool toggledState, ref bool wasPressedLastFrame)
        {
            if (!toggleModeEnabled)
            {
                wasPressedLastFrame = disableKeyPressed;
                return disableKeyPressed;
            }

            if (disableKeyPressed && !wasPressedLastFrame)
            {
                toggledState = !toggledState;
            }

            wasPressedLastFrame = disableKeyPressed;
            return toggledState;
        }

        public static (bool leftClickBlocks, bool rightClickBlocks, bool mouseButtonBlocks)
            GetMouseButtonBlockingState(ClickItSettings settings, Func<Keys, bool> keyStateProvider)
        {
            if (settings == null || keyStateProvider == null)
                return (false, false, false);

            bool leftClickBlocks = settings.DisableLazyModeLeftClickHeld.Value && keyStateProvider(Keys.LButton);
            bool rightClickBlocks = settings.DisableLazyModeRightClickHeld.Value && keyStateProvider(Keys.RButton);
            return (leftClickBlocks, rightClickBlocks, leftClickBlocks || rightClickBlocks);
        }

        private static bool IsPOEActive(GameController gameController)
        {
            return gameController.Window.IsForeground();
        }

        private static bool IsPanelOpen(GameController gameController)
        {
            if (gameController == null) return false;
            var ui = gameController.IngameState?.IngameUi;
            if (ui == null) return false;
            return ui.OpenLeftPanel.Address != 0 || ui.OpenRightPanel.Address != 0;
        }
        private static bool IsInTownOrHideout(GameController gameController)
        {
            if (gameController == null) return false;
            var area = gameController.Area?.CurrentArea;
            if (area == null) return false;
            return area.IsHideout || area.IsTown;
        }

    }
}
