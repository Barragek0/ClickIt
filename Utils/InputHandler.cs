using ExileCore;
using System.Diagnostics;
using ExileCore.Shared.Cache;
using System.Runtime.InteropServices;
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
        private const string HeistBlueprintPathMarker = "Items/Heist/HeistBlueprint";
        private const string HeistBlueprintCurrencyPathMarker = "Items/Currency/Heist/Blueprint";
        private const string HeistBlueprintNamePrefix = "Blueprint:";
        private const string RoguesMarkerPathMarker = "Items/Heist/HeistCoin";
        private const string RoguesMarkerName = "Rogue's Marker";

        private readonly ClickItSettings _settings = settings;
        private readonly Random _random = new();
        private readonly ErrorHandler? _errorHandler = errorHandler;
        private readonly PerformanceMonitor _performanceMonitor = performanceMonitor;
        private long _lastClickTimestampMs = 0;
        private long _successfulClickSequence = 0;
        private long _lastToggleItemsTimestampMs = 0;
        private bool _lazyModeDisableToggled;
        private bool _lazyModeDisableKeyWasDown;
        private bool _clickHotkeyToggled;
        private bool _clickHotkeyWasDown;

        public long GetSuccessfulClickSequence()
        {
            return Interlocked.Read(ref _successfulClickSequence);
        }

        public bool IsLabelFullyOverlapped(LabelOnGround label, IReadOnlyList<LabelOnGround>? allLabels)
        {
            bool avoidOverlapsEnabled = _settings.AvoidOverlappingLabelClickPoints?.Value != false;
            if (!avoidOverlapsEnabled)
                return false;

            if (!LabelUtils.TryGetLabelRect(label, out RectangleF rect))
                return false;

            Vector2 preferredPoint = rect.Center;
            if (label.ItemOnGround.Type == EntityType.Chest)
            {
                preferredPoint.Y -= _settings.ChestHeightOffset;
            }

            List<RectangleF> potentialBlockers = CollectPotentialBlockingLabelRects(label, rect, allLabels);
            if (potentialBlockers.Count == 0)
                return false;

            // Fast path: if any probe point is visibly unblocked, skip expensive full overlap resolution.
            if (HasUnblockedOverlapProbePoint(rect, preferredPoint, potentialBlockers))
                return false;

            List<RectangleF> blockedAreas = BuildIntersectionOverlaps(rect, potentialBlockers);
            return !TryResolveVisibleClickPoint(rect, preferredPoint, blockedAreas, out _);
        }

        public Vector2 CalculateClickPosition(LabelOnGround label, Vector2 windowTopLeft, IReadOnlyList<LabelOnGround>? allLabels = null)
        {
            if (!LabelUtils.TryGetLabelRect(label, out RectangleF rect))
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

            if (!LabelUtils.TryGetLabelRect(label, out RectangleF rect))
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
    }
}
