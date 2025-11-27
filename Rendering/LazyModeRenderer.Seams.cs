using ExileCore;
using ClickIt.Services;
using System.Windows.Forms;
using SharpDX;

namespace ClickIt.Rendering
{
    public partial class LazyModeRenderer
    {
        // Test seam helper: allows tests to force-isRitualActive and inject an InputHandler
        internal (SharpDX.Color color, string line1, string line2, string line3) ComposeLazyModeStatusForTests(
            bool hasRestrictedItems,
            bool hotkeyHeld,
            bool lazyModeDisableHeld,
            bool mouseButtonBlocks,
            bool leftClickBlocks,
            bool rightClickBlocks,
            bool isRitualActive,
            GameController? gameController,
            Keys clickLabelKey,
            Utils.InputHandler? inputHandler)
        {
            // Mirror the original ComposeLazyModeStatus logic but use the forced isRitualActive value
            if (hasRestrictedItems)
            {
                return hotkeyHeld
                    ? (SharpDX.Color.LawnGreen, "Blocking overridden by hotkey.", string.Empty, string.Empty)
                    : (SharpDX.Color.Red, "Locked chest or tree detected.", $"Hold {clickLabelKey} to click them.", string.Empty);
            }

            if (lazyModeDisableHeld)
            {
                return (SharpDX.Color.Red, "Lazy mode disabled by hotkey.", "Release to resume lazy clicking.", string.Empty);
            }

            if (mouseButtonBlocks)
            {
                string buttonName = leftClickBlocks && rightClickBlocks
                    ? "both mouse buttons"
                    : leftClickBlocks ? "Left mouse button" : "Right mouse button";

                return (SharpDX.Color.Red, $"{buttonName} held.", "Release to resume lazy clicking.", string.Empty);
            }

            if (isRitualActive)
            {
                return hotkeyHeld
                    ? (SharpDX.Color.LawnGreen, "Blocking overridden by hotkey.", string.Empty, string.Empty)
                    : (SharpDX.Color.Red, "Ritual in progress.", "Complete it to resume lazy clicking.", string.Empty);
            }

            bool canActuallyClick = inputHandler?.CanClick(gameController, false, isRitualActive) ?? false;
            if (!canActuallyClick)
            {
                return (SharpDX.Color.Red, inputHandler?.GetCanClickFailureReason(gameController) ?? "Clicking disabled.", string.Empty, string.Empty);
            }

            return (SharpDX.Color.LawnGreen, string.Empty, string.Empty, string.Empty);
        }
        
        // Static helper so tests can call without relying on instance instance-method lookup with primary-ctor partials
        internal static (SharpDX.Color color, string line1, string line2, string line3) ComposeLazyModeStatusForTests(LazyModeRenderer renderer,
            bool hasRestrictedItems,
            bool hotkeyHeld,
            bool lazyModeDisableHeld,
            bool mouseButtonBlocks,
            bool leftClickBlocks,
            bool rightClickBlocks,
            bool isRitualActive,
            ExileCore.GameController? gameController,
            System.Windows.Forms.Keys clickLabelKey,
            Utils.InputHandler? inputHandler)
        {
            return renderer.ComposeLazyModeStatusForTests(hasRestrictedItems, hotkeyHeld, lazyModeDisableHeld, mouseButtonBlocks, leftClickBlocks, rightClickBlocks, isRitualActive, gameController, clickLabelKey, inputHandler);
        }
    }
}
