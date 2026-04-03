using ClickIt.Features.Observability;
using SharpDX;
using Color = SharpDX.Color;

namespace ClickIt.UI.Debug.Sections
{
    internal sealed class ClickingDebugOverlaySection(Debug.DebugOverlayRenderContext context)
    {
        private readonly Debug.DebugOverlayRenderContext _context = context;

        public int RenderRuntimeDebugLogOverlay(ref int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Debug Log Overlay ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            if (_context.Plugin is not ClickIt clickIt)
            {
                _context.DeferredTextQueue.Enqueue("Click service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            DebugTelemetrySnapshot telemetry = _context.DebugTelemetrySource.GetSnapshot();
            if (!telemetry.Click.ServiceAvailable)
            {
                _context.DeferredTextQueue.Enqueue("Click service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            var latest = telemetry.Click.RuntimeLog;
            if (!latest.HasData)
            {
                _context.DeferredTextQueue.Enqueue("No debug log messages yet", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Latest: {latest.Message}", Color.LightGray, 13, 80);

            var trail = telemetry.Click.RuntimeLogTrail;
            yPos = _context.RenderDebugTrailBlock(ref xPos, yPos, lineHeight, trail, maxRows: 10, wrapWidth: 80);
            return yPos;
        }

        public int RenderClickingDebug(ref int xPos, int yPos, int lineHeight)
        {
            _context.DeferredTextQueue.Enqueue("--- Clicking ---", new Vector2(xPos, yPos), Color.Orange, 16);
            yPos += lineHeight;

            if (_context.Plugin is not ClickIt clickIt)
            {
                _context.DeferredTextQueue.Enqueue("Click service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            DebugTelemetrySnapshot telemetry = _context.DebugTelemetrySource.GetSnapshot();
            if (!telemetry.Click.ServiceAvailable)
            {
                _context.DeferredTextQueue.Enqueue("Click service unavailable", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            ClickItSettings settings = clickIt.Settings ?? new ClickItSettings();
            if (_context.DebugTelemetrySource.TryGetFreezeState(out long remainingMs, out string freezeReason))
            {
                string freezeSummary = string.IsNullOrWhiteSpace(freezeReason)
                    ? $"Telemetry Hold Active: {remainingMs}ms remaining"
                    : $"Telemetry Hold Active: {remainingMs}ms remaining | {freezeReason}";
                yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, freezeSummary, Color.Orange, 13, 86);
            }

            _context.DeferredTextQueue.Enqueue("Click Settings Snapshot:", new Vector2(xPos, yPos), Color.LightBlue, 14);
            yPos += lineHeight;

            IReadOnlyList<string> clickSettingsLines = BuildClickSettingsDebugSnapshotLines(settings);
            for (int i = 0; i < clickSettingsLines.Count; i++)
            {
                yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, clickSettingsLines[i], Color.LightGray, 13, 86);
            }

            var snap = telemetry.Click.Click;
            if (!snap.HasData)
            {
                _context.DeferredTextQueue.Enqueue("No click data yet", new Vector2(xPos, yPos), Color.Gray, 14);
                return yPos + lineHeight;
            }

            Color stageColor = snap.Resolved && snap.ResolvedClickable ? Color.LightGreen : Color.Yellow;
            _context.DeferredTextQueue.Enqueue($"Stage: {snap.Stage}  Seq: {snap.Sequence}", new Vector2(xPos, yPos), stageColor, 14);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Mechanic: {snap.MechanicId}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Distance: {snap.Distance:0.0}", new Vector2(xPos, yPos), Color.White, 14);
            yPos += lineHeight;

            yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Path: {snap.EntityPath}", Color.LightGray, 13, 72);

            _context.DeferredTextQueue.Enqueue($"World Raw: ({snap.WorldScreenRaw.X:0.0},{snap.WorldScreenRaw.Y:0.0})", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"World Abs: ({snap.WorldScreenAbsolute.X:0.0},{snap.WorldScreenAbsolute.Y:0.0})", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Click Pos: ({snap.ResolvedClickPoint.X:0.0},{snap.ResolvedClickPoint.Y:0.0})", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Center InWnd/Clickable: {snap.CenterInWindow}/{snap.CenterClickable}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            _context.DeferredTextQueue.Enqueue($"Resolved InWnd/Clickable: {snap.ResolvedInWindow}/{snap.ResolvedClickable}", new Vector2(xPos, yPos), Color.White, 13);
            yPos += lineHeight;

            yPos = _context.EnqueueWrappedDebugLine(ref xPos, yPos, lineHeight, $"Resolved: {snap.Resolved}  Note: {snap.Notes}", Color.LightGray, 13, 72);

            var trail = telemetry.Click.ClickTrail;
            yPos = _context.RenderDebugTrailBlock(ref xPos, yPos, lineHeight, trail, maxRows: 8, wrapWidth: 78);

            return yPos;
        }

        internal static IReadOnlyList<string> BuildClickSettingsDebugSnapshotLines(ClickItSettings settings)
        {
            settings ??= new ClickItSettings();

            string toggleLine = string.Join(", ",
            [
                $"hotkeyToggle:{settings.ClickHotkeyToggleMode.Value}",
                $"manualCursor:{settings.ClickOnManualUiHoverOnly.Value}",
                $"lazyMode:{settings.LazyMode.Value}",
                $"leftHanded:{settings.LeftHanded.Value}"
            ]);

            string coreClickLine = string.Join(", ",
            [
                $"radius:{settings.ClickDistance.Value}",
                $"freqTarget:{settings.ClickFrequencyTarget.Value}ms",
                $"verifyCursorInWindow:{settings.VerifyCursorInGameWindowBeforeClick.Value}",
                $"verifyUiHoverNonLazy:{settings.VerifyUIHoverWhenNotLazy.Value}",
                $"avoidOverlap:{settings.AvoidOverlappingLabelClickPoints.Value}"
            ]);

            string inputSafetyLine = string.Join(", ",
            [
                $"blockPanels:{settings.BlockOnOpenLeftRightPanel.Value}",
                $"toggleItems:{settings.ToggleItems.Value}",
                $"toggleItemsInterval:{settings.ToggleItemsIntervalMs.Value}ms",
                $"postToggleBlock:{settings.ToggleItemsPostToggleClickBlockMs.Value}ms"
            ]);

            string pathingLine = string.Join(", ",
            [
                $"walkOffscreen:{settings.WalkTowardOffscreenLabels.Value}",
                $"prioritizeOnscreen:{settings.PrioritizeOnscreenClickableMechanicsOverPathfinding.Value}",
                $"pathBudget:{settings.OffscreenPathfindingSearchBudget.Value}"
            ]);

            string chestSettleLine = string.Join(", ",
            [
                $"waitBasicChestDrops:{settings.PauseAfterOpeningBasicChests.Value}",
                $"waitLeagueChestDrops:{settings.PauseAfterOpeningLeagueChests.Value}",
                $"allowNearbyDuringSettle:{settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettle.Value}",
                $"nearbySettleDist:{settings.AllowNearbyMechanicsWhileWaitingForChestDropsToSettleDistance.Value}"
            ]);

            return
            [
                toggleLine,
                coreClickLine,
                inputSafetyLine,
                pathingLine,
                chestSettleLine
            ];
        }
    }
}