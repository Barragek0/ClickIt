using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums; // FontAlign
using SharpDX;

namespace ClickIt
{
    public partial class ClickIt
    {
        private bool PointIsInClickableArea(Vector2 point, string? path = null)
        {
            State.AreaService?.UpdateScreenAreas(GameController);
            return State.AreaService?.PointIsInClickableArea(point) ?? false;
        }

        private void RenderInternal()
        {
            bool debugMode = Settings.DebugMode;
            bool renderDebug = Settings.RenderDebug;
            bool hasDebugRendering = debugMode && renderDebug;

            int altarCount = State.AltarService?.GetAltarComponents()?.Count ?? 0;
            bool hasAltars = altarCount > 0;

            bool hasLazyModeIndicator = Settings.LazyMode.Value;

            if (!hasDebugRendering && !hasAltars && !hasLazyModeIndicator)
            {
                return; // Skip all timer operations for no-op renders
            }

            // Start timing only when actually rendering
            State.PerformanceMonitor?.StartRenderTiming();
            State.PerformanceMonitor?.UpdateFPS();

            // Render lazy mode indicator if enabled
            if (Settings.LazyMode.Value)
            {
                RenderLazyModeIndicator();
            }

            if (hasDebugRendering)
            {
                State.DebugRenderer?.RenderDebugFrames(Settings);
                if (State.DebugRenderer != null && State.PerformanceMonitor != null)
                {
                    State.DebugRenderer.RenderDetailedDebugInfo(Settings, State.PerformanceMonitor);
                }
            }

            if (hasAltars)
            {
                RenderAltarComponents();
            }

            State.PerformanceMonitor?.StopRenderTiming();

            // Flush deferred text rendering to prevent freezes
            // Use no-op logger to prevent recursive logging during render loop
            State.DeferredTextQueue?.Flush(Graphics, (msg, frame) => { });
            State.DeferredFrameQueue?.Flush(Graphics, (msg, frame) => { });
        }

        private void RenderAltarComponents()
        {
            State.AltarDisplayRenderer?.RenderAltarComponents();
        }

        /// <summary>
        /// Check if a ritual is currently active by looking for RitualBlocker entities
        /// </summary>
        private bool IsRitualActive()
        {
            return Utils.EntityHelpers.IsRitualActive(GameController);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3776:Methods should not be too complex", Justification = "Refactor later — keep behavior identical while splitting files")]
        private void RenderLazyModeIndicator()
        {
            if (State.DeferredTextQueue == null) return;
            var windowRect = GameController.Window.GetWindowRectangleTimeCache;
            float centerX = windowRect.Width / 2f;
            float topY = 60f; // Small margin from top

            // Check if lazy mode is restricted
            var allLabels = State.CachedLabels?.Value ?? [];
            bool hasRestrictedItems = State.LabelFilterService?.HasLazyModeRestrictedItemsOnScreen(allLabels) ?? false;

            // Check if a ritual is active
            bool isRitualActive = IsRitualActive();

            // Check if primary mouse button is held (prevents lazy clicking)
            bool leftButtonHeld = Input.GetKeyState(Keys.LButton);
            bool rightButtonHeld = Input.GetKeyState(Keys.RButton);
            bool leftClickBlocks = Settings.DisableLazyModeLeftClickHeld.Value && leftButtonHeld;
            bool rightClickBlocks = Settings.DisableLazyModeRightClickHeld.Value && rightButtonHeld;
            bool mouseButtonBlocks = leftClickBlocks || rightClickBlocks;

            // Check if hotkey is currently held
            bool hotkeyHeld = Input.GetKeyState(Settings.ClickLabelKey.Value);

            // Check if lazy mode disable key is currently held
            bool lazyModeDisableHeld = Input.GetKeyState(Settings.LazyModeDisableKey.Value);

            // Determine display state and messages
            SharpDX.Color textColor;
            string line1 = "", line2 = "", line3 = "";


            if (hasRestrictedItems)
            {
                if (hotkeyHeld)
                {
                    textColor = SharpDX.Color.LawnGreen;
                    line1 = "Blocking overridden by hotkey.";
                    line2 = "";
                }
                else
                {
                    textColor = SharpDX.Color.Red;
                    line1 = "Locked strongbox, chest or tree detected.";
                    string hotkeyName = Settings.ClickLabelKey.Value.ToString();
                    line2 = $"Hold {hotkeyName} to click them.";
                }
            }
            else if (!hasRestrictedItems && lazyModeDisableHeld)
            {
                textColor = SharpDX.Color.Red;
                line1 = "Lazy mode disabled by hotkey.";
                line2 = "Release to resume lazy clicking.";
            }
            else if (mouseButtonBlocks)
            {
                textColor = SharpDX.Color.Red;
                string buttonName = "";
                if (leftClickBlocks && rightClickBlocks) buttonName = "both mouse buttons";
                else if (leftClickBlocks) buttonName = "Left mouse button";
                else buttonName = "Right mouse button";
                line1 = $"{buttonName} held.";
                line2 = "Release to resume lazy clicking.";
            }
            else if (isRitualActive)
            {
                if (hotkeyHeld)
                {
                    textColor = SharpDX.Color.LawnGreen;
                    line1 = "Blocking overridden by hotkey.";
                    line2 = "";
                }
                else
                {
                    textColor = SharpDX.Color.Red;
                    line1 = "Ritual in progress.";
                    line2 = "Complete it to resume lazy clicking.";
                }
            }
            else
            {
                // Check if CanClick would actually allow clicking
                bool canActuallyClick = State.InputHandler?.CanClick(GameController, false, isRitualActive) ?? false;

                if (!canActuallyClick)
                {
                    textColor = SharpDX.Color.Red;
                    line1 = GetCanClickFailureReason();
                }
                else
                {
                    textColor = SharpDX.Color.LawnGreen;
                    // No additional lines for green state
                }
            }

            // Render the lazy mode indicator
            RenderLazyModeText(centerX, topY, textColor, line1, line2, line3);
        }

        private void RenderLazyModeText(float centerX, float topY, SharpDX.Color color, string line1, string line2, string line3)
        {
            const string LAZY_MODE_TEXT = "Lazy Mode";
            State.DeferredTextQueue?.Enqueue(LAZY_MODE_TEXT, new Vector2(centerX, topY), color, 36, FontAlign.Center);

            if (string.IsNullOrEmpty(line1)) return;

            float lineHeight = 36 * 1.2f;
            float secondLineY = topY + lineHeight;
            State.DeferredTextQueue?.Enqueue(line1, new Vector2(centerX, secondLineY), color, 24, FontAlign.Center);

            if (string.IsNullOrEmpty(line2)) return;

            float thirdLineY = secondLineY + lineHeight;
            State.DeferredTextQueue?.Enqueue(line2, new Vector2(centerX, thirdLineY), color, 24, FontAlign.Center);

            if (string.IsNullOrEmpty(line3)) return;

            float fourthLineY = thirdLineY + lineHeight;
            State.DeferredTextQueue?.Enqueue(line3, new Vector2(centerX, fourthLineY), color, 24, FontAlign.Center);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3776:Methods should not be too complex", Justification = "Refactor later — keep behavior identical while splitting files")]
        private string GetCanClickFailureReason()
        {
            if (GameController?.Window?.IsForeground() == false)
                return "PoE not in focus.";

            if (Settings.BlockOnOpenLeftRightPanel.Value &&
                (GameController?.IngameState?.IngameUi?.OpenLeftPanel?.Address != 0 ||
                 GameController?.IngameState?.IngameUi?.OpenRightPanel?.Address != 0))
                return "Panel is open.";

            if (GameController?.Area?.CurrentArea?.IsTown == true ||
                GameController?.Area?.CurrentArea?.IsHideout == true)
                return "In town/hideout.";

            if (GameController?.IngameState?.IngameUi?.ChatTitlePanel?.IsVisible == true)
                return "Chat is open.";

            if (GameController?.IngameState?.IngameUi?.AtlasPanel?.IsVisible == true)
                return "Atlas panel is open.";

            if (GameController?.IngameState?.IngameUi?.AtlasTreePanel?.IsVisible == true)
                return "Atlas tree panel is open.";

            if (GameController?.IngameState?.IngameUi?.TreePanel?.IsVisible == true)
                return "Passive tree panel is open.";

            if (GameController?.IngameState?.IngameUi?.UltimatumPanel?.IsVisible == true)
                return "Ultimatum panel is open.";

            if (GameController?.IngameState?.IngameUi?.BetrayalWindow?.IsVisible == true)
                return "Betrayal window is open.";

            if (GameController?.IngameState?.IngameUi?.SyndicatePanel?.IsVisible == true)
                return "Syndicate panel is open.";

            if (GameController?.IngameState?.IngameUi?.SyndicateTree?.IsVisible == true)
                return "Syndicate tree panel is open.";

            if (GameController?.IngameState?.IngameUi?.IncursionWindow?.IsVisible == true)
                return "Incursion window is open.";

            if (GameController?.IngameState?.IngameUi?.RitualWindow?.IsVisible == true)
                return "Ritual window is open.";

            if (GameController?.IngameState?.IngameUi?.SanctumFloorWindow?.IsVisible == true)
                return "Sanctum floor window is open.";

            if (GameController?.IngameState?.IngameUi?.SanctumRewardWindow?.IsVisible == true)
                return "Sanctum reward window is open.";

            if (GameController?.IngameState?.IngameUi?.MicrotransactionShopWindow?.IsVisible == true)
                return "Microtransaction shop window is open.";

            if (GameController?.IngameState?.IngameUi?.ResurrectPanel?.IsVisible == true)
                return "Resurrect panel is open.";

            if (GameController?.IngameState?.IngameUi?.NpcDialog?.IsVisible == true)
                return "NPC dialog is open.";

            if (GameController?.IngameState?.IngameUi?.KalandraTabletWindow?.IsVisible == true)
                return "Kalandra tablet window is open.";

            if (GameController?.Game?.IsEscapeState == true)
                return "Escape menu is open.";

            return "Clicking disabled.";
        }
    }
}
