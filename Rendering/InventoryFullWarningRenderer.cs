using ClickIt.Services;
using ClickIt.Utils;
using ExileCore;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Rendering
{
    internal class InventoryFullWarningRenderer(
        DeferredTextQueue deferredTextQueue,
        AreaService? areaService = null,
        Func<LabelFilterService.InventoryDebugSnapshot, long, bool>? tryAutoCopyOnWarningTrigger = null)
    {
        private const string InventoryFullWarningText = "Your inventory is full";
        private const string InventoryLayoutUnreliableNotesPrefix = "Inventory layout unreliable";
        private const int NotFullNoFitMinFreeCellsToSuppressWarning = 12;
        private const int InventoryFullWarningHoldMs = 10_000;
        private const int InventoryWarningAutoCopyThrottleMs = 1_000;
        private const int InventoryFullWarningTextSize = 48;
        private const float PlayerFeetWarningOffsetY = 50f;
        private static readonly Vector2[] BoldTextOffsets =
        [
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, -1f),
            new Vector2(0f, 1f)
        ];

        private readonly DeferredTextQueue _deferredTextQueue = deferredTextQueue ?? new DeferredTextQueue();
        private readonly AreaService? _areaService = areaService;
        private readonly Func<LabelFilterService.InventoryDebugSnapshot, long, bool>? _tryAutoCopyOnWarningTrigger = tryAutoCopyOnWarningTrigger;
        private long _lastInventoryFullBlockedTimestampMs;
        private long _lastProcessedInventoryDebugSequence = long.MinValue;
        private long _lastInventoryWarningAutoCopyAttemptTimestampMs;

        public void Render(GameController gameController)
        {

            long now = Environment.TickCount64;
            LabelFilterService.InventoryDebugSnapshot snapshot = LabelFilterService.GetLatestInventoryDebug();
            if (ShouldRefreshInventoryFullWarningTimestamp(_lastProcessedInventoryDebugSequence, snapshot.Sequence, snapshot)
                && ShouldShowInventoryPickupBlockedWarning(snapshot))
            {
                _lastInventoryFullBlockedTimestampMs = now;
                TryAutoCopyDebugSnapshot(snapshot, now);
            }

            _lastProcessedInventoryDebugSequence = snapshot.Sequence;

            if (!ShouldShowInventoryFullWarning(now, _lastInventoryFullBlockedTimestampMs))
                return;

            RectangleF windowRect = gameController.Window.GetWindowRectangleTimeCache;
            RectangleF leftTertiary = _areaService?.FlaskTertiaryRectangle ?? RectangleF.Empty;
            RectangleF rightTertiary = _areaService?.SkillsTertiaryRectangle ?? RectangleF.Empty;
            Vector2? playerFeetScreen = TryResolvePlayerFeetWarningPosition(gameController);
            Vector2 pos = ResolveInventoryFullWarningPosition(windowRect, leftTertiary, rightTertiary, playerFeetScreen);

            EnqueueBoldWarningText(pos);
        }

        private void TryAutoCopyDebugSnapshot(LabelFilterService.InventoryDebugSnapshot snapshot, long now)
        {
            if (_tryAutoCopyOnWarningTrigger == null)
                return;

            if (!ShouldAutoCopyInventoryWarning(now, _lastInventoryWarningAutoCopyAttemptTimestampMs))
                return;

            _lastInventoryWarningAutoCopyAttemptTimestampMs = now;
            try
            {
                _ = _tryAutoCopyOnWarningTrigger.Invoke(snapshot, now);
            }
            catch
            {
                // Clipboard diagnostics are best-effort and should not affect rendering behavior.
            }
        }

        private void EnqueueBoldWarningText(Vector2 centerPosition)
        {
            for (int i = 0; i < BoldTextOffsets.Length; i++)
            {
                Vector2 offsetPosition = centerPosition + BoldTextOffsets[i];
                _deferredTextQueue.Enqueue(
                    InventoryFullWarningText,
                    offsetPosition,
                    SharpDX.Color.Black,
                    InventoryFullWarningTextSize,
                    ExileCore.Shared.Enums.FontAlign.Center);
            }

            _deferredTextQueue.Enqueue(
                InventoryFullWarningText,
                centerPosition,
                SharpDX.Color.OrangeRed,
                InventoryFullWarningTextSize,
                ExileCore.Shared.Enums.FontAlign.Center);
        }

        internal static bool ShouldShowInventoryPickupBlockedWarning(LabelFilterService.InventoryDebugSnapshot snapshot)
        {
            if (!snapshot.HasData || snapshot.DecisionAllowPickup)
                return false;

            if (string.Equals(snapshot.Stage, "InventoryFullDecision", StringComparison.Ordinal))
                return true;

            if (string.Equals(snapshot.Stage, "InventoryNotFullNoFit", StringComparison.Ordinal))
            {
                if (snapshot.Notes.StartsWith(InventoryLayoutUnreliableNotesPrefix, StringComparison.Ordinal))
                    return false;

                if (ShouldSuppressNotFullNoFitWarning(snapshot))
                    return false;

                return !string.IsNullOrWhiteSpace(snapshot.GroundItemPath)
                    || !string.IsNullOrWhiteSpace(snapshot.GroundItemName);
            }

            return false;
        }

        private static bool ShouldSuppressNotFullNoFitWarning(LabelFilterService.InventoryDebugSnapshot snapshot)
        {
            if (snapshot.InventoryFull || !snapshot.UsedCellOccupancy || snapshot.CapacityCells <= 0)
                return false;

            int freeCells = snapshot.CapacityCells - Math.Max(0, snapshot.OccupiedCells);
            return freeCells >= NotFullNoFitMinFreeCellsToSuppressWarning;
        }

        internal static bool ShouldRefreshInventoryFullWarningTimestamp(long lastProcessedSequence, long currentSequence, LabelFilterService.InventoryDebugSnapshot snapshot)
            => snapshot.HasData && currentSequence != lastProcessedSequence;

        internal static bool ShouldShowInventoryFullWarning(long now, long lastTriggeredTimestampMs)
        {
            if (lastTriggeredTimestampMs <= 0)
                return false;

            long elapsed = now - lastTriggeredTimestampMs;
            return elapsed >= 0 && elapsed <= InventoryFullWarningHoldMs;
        }

        internal static bool ShouldAutoCopyInventoryWarning(long now, long lastAutoCopyAttemptTimestampMs)
        {
            if (lastAutoCopyAttemptTimestampMs <= 0)
                return true;

            long elapsed = now - lastAutoCopyAttemptTimestampMs;
            return elapsed < 0 || elapsed >= InventoryWarningAutoCopyThrottleMs;
        }

        internal static Vector2? TryResolvePlayerFeetWarningPosition(GameController gameController)
        {
            if (gameController?.Game?.IngameState?.Camera == null)
                return null;

            var player = gameController.Player ?? gameController.Game.IngameState.Data?.LocalPlayer;
            if (player == null)
                return null;

            var screenRaw = gameController.Game.IngameState.Camera.WorldToScreen(player.PosNum);
            float x = screenRaw.X;
            float y = screenRaw.Y + PlayerFeetWarningOffsetY;
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
                return null;

            return new Vector2(x, y);
        }

        internal static Vector2 ResolveInventoryFullWarningPosition(RectangleF windowRect, RectangleF leftTertiary, RectangleF rightTertiary, Vector2? playerFeetScreen)
        {
            if (playerFeetScreen.HasValue)
                return playerFeetScreen.Value;

            float centerX = windowRect.X + (windowRect.Width * 0.5f);
            float fallbackY = windowRect.Y + (windowRect.Height * 0.86f);

            bool hasLeft = leftTertiary.Width > leftTertiary.X && leftTertiary.Height > leftTertiary.Y;
            bool hasRight = rightTertiary.Width > rightTertiary.X && rightTertiary.Height > rightTertiary.Y;
            if (!hasLeft || !hasRight)
                return new Vector2(centerX, fallbackY);

            float betweenX = (leftTertiary.Width + rightTertiary.X) * 0.5f;
            float top = Math.Min(leftTertiary.Y, rightTertiary.Y);
            float bottom = Math.Max(leftTertiary.Height, rightTertiary.Height);
            float y = top + ((bottom - top) * 0.5f);

            return new Vector2(betweenX, y);
        }
    }
}