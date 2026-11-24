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
        private readonly Func<string?>? _getHarvestCurrencyWarning;
        private long _lastInventoryFullBlockedTimestampMs;
        private long _lastProcessedInventoryDebugSequence = long.MinValue;
        private sealed record DelveSupplies(int? Flares, int? Dynamite, long Timestamp);
        private DelveSupplies? _delveSupplies;

        public InventoryFullWarningOverlay(AreaService? areaService, Func<InventoryDebugSnapshot>? getLatestInventoryDebug,
            Func<string?>? getHarvestCurrencyWarning = null)
        {
            _areaService = areaService;
            _getLatestInventoryDebug = getLatestInventoryDebug;
            _getHarvestCurrencyWarning = getHarvestCurrencyWarning;
        }

        /// <summary>
        /// The essence-corruption currency warning, set once the click port that owns the corruption state exists.
        ///
        /// Kept as a settable property rather than a constructor argument because the overlays are registered before the
        /// click domain is assembled; the harvest warning can be passed in because its owner lives in the core domain.
        /// </summary>
        internal Func<string?>? EssenceCurrencyWarning { get; set; }

        public string Name => "InventoryFullWarning";

        public RenderSection Section => RenderSection.InventoryFullWarning;

        public OverlayRefreshPolicy RefreshPolicy => OverlayRefreshPolicy.Throttled(250);

        public TimingChannel? RefreshTimingChannel => null;

        public ProcessingSection ProcessingSection => ProcessingSection.Unknown;

        public bool IsEnabled(ClickItSettings settings)
            => true;

        public void Refresh(OverlayRefreshContext ctx)
        {
            int? flares = null;
            int? dynamite = null;
            try
            {
                if (ctx.Settings.ShowLowDelveFlaresWarning.Value || ctx.Settings.ShowLowDelveDynamiteWarning.Value)
                {
                    object? skillBar = ctx.GameController?.IngameState?.IngameUi?.SkillBar;
                    if (ctx.Settings.ShowLowDelveFlaresWarning.Value)
                        flares = ReadDelveSupplyCount(skillBar, 2);
                    if (ctx.Settings.ShowLowDelveDynamiteWarning.Value)
                        dynamite = ReadDelveSupplyCount(skillBar, 3);
                }
            }
            catch
            {
                // Unavailable UI must clear old counts, including during area transitions.
                flares = null;
                dynamite = null;
            }
            Volatile.Write(ref _delveSupplies, new DelveSupplies(flares, dynamite, Environment.TickCount64));
        }

        internal static int? ReadDelveSupplyCount(object? skillBar, int supplyIndex)
        {
            if (!DynamicAccess.TryGetChildAtIndex(skillBar, 13, out object? container)
                || !DynamicAccess.TryGetChildAtIndex(container, 0, out object? row)
                || !DynamicAccess.TryGetChildAtIndex(row, supplyIndex, out object? supply)
                || !DynamicAccess.TryGetChildAtIndex(supply, 1, out object? label)
                || !DynamicAccess.TryReadString(label, static element => element.Text, out string text))
                return null;
            int slash = text.IndexOf('/');
            return slash > 0 && int.TryParse(text.AsSpan(0, slash).Trim(), out int count) && count >= 0 ? count : null;
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

            bool showInventoryWarning = ShouldShowInventoryFullWarning(now, _lastInventoryFullBlockedTimestampMs);
            string? harvestWarning = _getHarvestCurrencyWarning?.Invoke();
            string? essenceWarning = EssenceCurrencyWarning?.Invoke();
            DelveSupplies? supplies = Volatile.Read(ref _delveSupplies);
            bool fresh = supplies != null && now - supplies.Timestamp is >= 0 and <= 1000;
            int? flareCount = fresh && ctx.Settings.ShowLowDelveFlaresWarning.Value ? supplies!.Flares : null;
            int? dynamiteCount = fresh && ctx.Settings.ShowLowDelveDynamiteWarning.Value ? supplies!.Dynamite : null;
            bool showFlareWarning = IsLowDelveSupply(flareCount, ctx.Settings.LowDelveFlaresThreshold.Value);
            bool showDynamiteWarning = IsLowDelveSupply(dynamiteCount, ctx.Settings.LowDelveDynamiteThreshold.Value);
            if (!showInventoryWarning && harvestWarning == null && essenceWarning == null && !showFlareWarning && !showDynamiteWarning)
                return;

            RectangleF windowRect = ctx.WindowArea;
            RectangleF leftTertiary = _areaService?.FlaskTertiaryRectangle ?? RectangleF.Empty;
            RectangleF rightTertiary = _areaService?.SkillsTertiaryRectangle ?? RectangleF.Empty;
            Vector2? playerFeetScreen = TryResolvePlayerFeetWarningPosition(ctx.GameController);
            Vector2 pos = ResolveInventoryFullWarningPosition(windowRect, leftTertiary, rightTertiary, playerFeetScreen);

            if (showInventoryWarning)
                EnqueueBoldWarningText(ctx.DrawQueue, pos, InventoryFullWarningText, InventoryFullWarningTextSize);

            if (harvestWarning != null)
            {
                Vector2 harvestPos = showInventoryWarning ? pos + new Vector2(0, InventoryFullWarningTextSize) : pos;
                EnqueueBoldWarningText(ctx.DrawQueue, harvestPos, harvestWarning, 32);
            }

            if (essenceWarning != null)
            {
                // Stacks under whichever lines are already drawn, so all the warnings stay readable together.
                float offsetY = showInventoryWarning ? InventoryFullWarningTextSize : 0f;
                if (harvestWarning != null)
                    offsetY += 32f;

                EnqueueBoldWarningText(ctx.DrawQueue, pos + new Vector2(0, offsetY), essenceWarning, 32);
            }

            float supplyOffset = (showInventoryWarning ? InventoryFullWarningTextSize : 0f)
                + (harvestWarning != null ? 32f : 0f) + (essenceWarning != null ? 32f : 0f);
            if (showFlareWarning)
            {
                EnqueueBoldWarningText(ctx.DrawQueue, pos + new Vector2(0, supplyOffset), $"Low Flares: {flareCount}", 32);
                supplyOffset += 32f;
            }
            if (showDynamiteWarning)
                EnqueueBoldWarningText(ctx.DrawQueue, pos + new Vector2(0, supplyOffset), $"Low Dynamite: {dynamiteCount}", 32);
        }

        internal static bool IsLowDelveSupply(int? count, int threshold) => count.HasValue && count.Value < SystemMath.Clamp(threshold, 1, 10);

        private static void EnqueueBoldWarningText(DeferredDrawQueue drawQueue, Vector2 centerPosition, string text, int textSize)
        {
            for (int i = 0; i < BoldTextOffsets.Length; i++)
            {
                Vector2 offsetPosition = centerPosition + BoldTextOffsets[i];
                drawQueue.EnqueueText(
                    text,
                    offsetPosition,
                    Color.Black,
                    textSize,
                    FontAlign.Center);
            }

            drawQueue.EnqueueText(
                text,
                centerPosition,
                Color.OrangeRed,
                textSize,
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
