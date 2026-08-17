namespace ClickIt.Features.Click
{
    /// <summary>
    /// Owns the inventory-full warning text overlay. Sequence-dirty-tracked internally (the
    /// warning timestamp refreshes only when the cached inventory snapshot sequence changes).
    /// </summary>
    internal sealed class InventoryFullWarningOverlay : IOverlay
    {
        private const string InventoryFullWarningText = "Your inventory is full";
        private const string InventoryLayoutUnreliableNotesPrefix = "Inventory layout unreliable";
        private const int NotFullNoFitMinFreeCellsToSuppressWarning = 12;
        private const int InventoryFullWarningHoldMs = 10_000;
        private const int InventoryFullWarningTextSize = 48;
        private const float PlayerFeetWarningOffsetY = 50f;
        private static readonly Vector2[] BoldTextOffsets =
        [
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, -1f),
            new Vector2(0f, 1f)
        ];

        private readonly AreaService? _areaService;
        private readonly Func<InventoryDebugSnapshot>? _getLatestInventoryDebug;
        private long _lastInventoryFullBlockedTimestampMs;
        private long _lastProcessedInventoryDebugSequence = long.MinValue;

        public InventoryFullWarningOverlay(AreaService? areaService, Func<InventoryDebugSnapshot>? getLatestInventoryDebug)
        {
            _areaService = areaService;
            _getLatestInventoryDebug = getLatestInventoryDebug;
        }

        public string Name => "InventoryFullWarning";

        public RenderSection Section => RenderSection.InventoryFullWarning;

        public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.None;

        public TimingChannel? RefreshTimingChannel => null;

        public ProcessingSection ProcessingSection => ProcessingSection.Unknown;

        public bool IsEnabled(ClickItSettings settings)
            => true;

        public void Refresh(OverlayRefreshContext ctx)
        {
        }

        public void Draw(OverlayRenderContext ctx)
        {
            long now = Environment.TickCount64;
            InventoryDebugSnapshot snapshot = _getLatestInventoryDebug?.Invoke()
                ?? InventoryDebugSnapshot.Empty;
            if (ShouldRefreshInventoryFullWarningTimestamp(_lastProcessedInventoryDebugSequence, snapshot.Sequence, snapshot)
                && ShouldShowInventoryPickupBlockedWarning(snapshot))
            {
                _lastInventoryFullBlockedTimestampMs = now;
            }

            _lastProcessedInventoryDebugSequence = snapshot.Sequence;

            if (!ShouldShowInventoryFullWarning(now, _lastInventoryFullBlockedTimestampMs))
                return;

            RectangleF windowRect = ctx.WindowArea;
            RectangleF leftTertiary = _areaService?.FlaskTertiaryRectangle ?? RectangleF.Empty;
            RectangleF rightTertiary = _areaService?.SkillsTertiaryRectangle ?? RectangleF.Empty;
            Vector2? playerFeetScreen = TryResolvePlayerFeetWarningPosition(ctx.GameController);
            Vector2 pos = ResolveInventoryFullWarningPosition(windowRect, leftTertiary, rightTertiary, playerFeetScreen);

            EnqueueBoldWarningText(ctx.DrawQueue, pos);
        }

        private void EnqueueBoldWarningText(DeferredDrawQueue drawQueue, Vector2 centerPosition)
        {
            for (int i = 0; i < BoldTextOffsets.Length; i++)
            {
                Vector2 offsetPosition = centerPosition + BoldTextOffsets[i];
                drawQueue.EnqueueText(
                    InventoryFullWarningText,
                    offsetPosition,
                    Color.Black,
                    InventoryFullWarningTextSize,
                    FontAlign.Center);
            }

            drawQueue.EnqueueText(
                InventoryFullWarningText,
                centerPosition,
                Color.OrangeRed,
                InventoryFullWarningTextSize,
                FontAlign.Center);
        }

        internal static bool ShouldShowInventoryPickupBlockedWarning(InventoryDebugSnapshot snapshot)
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

        private static bool ShouldSuppressNotFullNoFitWarning(InventoryDebugSnapshot snapshot)
        {
            if (snapshot.InventoryFull || !snapshot.UsedCellOccupancy || snapshot.CapacityCells <= 0)
                return false;

            int freeCells = snapshot.CapacityCells - SystemMath.Max(0, snapshot.OccupiedCells);
            return freeCells >= NotFullNoFitMinFreeCellsToSuppressWarning;
        }

        internal static bool ShouldRefreshInventoryFullWarningTimestamp(long lastProcessedSequence, long currentSequence, InventoryDebugSnapshot snapshot)
            => snapshot.HasData && currentSequence != lastProcessedSequence;

        internal static bool ShouldShowInventoryFullWarning(long now, long lastTriggeredTimestampMs)
        {
            if (lastTriggeredTimestampMs <= 0)
                return false;

            long elapsed = now - lastTriggeredTimestampMs;
            return elapsed is >= 0 and <= InventoryFullWarningHoldMs;
        }

        internal static Vector2? TryResolvePlayerFeetWarningPosition(GameController? gameController)
        {
            if (gameController?.Game?.IngameState?.Camera == null)
                return null;

            Entity? player = gameController.Player ?? gameController.Game.IngameState.Data?.LocalPlayer;
            if (player == null)
                return null;

            // WorldToScreen projects from WORLD coordinates (PosNum) — the same space the world renderers and the original plugin feed it. Player.GridPosNum is grid space and would draw the warning at the wrong screen spot.
            NumVector2 screenRaw = gameController.Game.IngameState.Camera.WorldToScreen(player.PosNum);
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
            float top = SystemMath.Min(leftTertiary.Y, rightTertiary.Y);
            float bottom = SystemMath.Max(leftTertiary.Height, rightTertiary.Height);
            float y = top + ((bottom - top) * 0.5f);

            return new Vector2(betweenX, y);
        }
    }
}
