using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared;
using ClickIt.Components;
using ClickIt.Utils;
using SharpDX;
using ExileCore.PoEMemory;

#nullable enable

namespace ClickIt.Services
{
    public class ClickService
    {
        private readonly ClickItSettings settings;
        private readonly GameController gameController;
        private readonly Action<string, int> logMessage;
        private readonly Action<string, int> logError;
        private readonly AltarService altarService;
        private readonly WeightCalculator weightCalculator;
        private readonly Rendering.AltarDisplayRenderer altarDisplayRenderer;
        private readonly Random random;
        private readonly Func<Vector2, string?, bool> pointIsInClickableArea;
        private readonly Action<bool> safeBlockInput;

        public ClickService(
            ClickItSettings settings,
            GameController gameController,
            Action<string, int> logMessage,
            Action<string, int> logError,
            AltarService altarService,
            WeightCalculator weightCalculator,
            Rendering.AltarDisplayRenderer altarDisplayRenderer,
            Random random,
            Func<Vector2, string?, bool> pointIsInClickableArea,
            Action<bool> safeBlockInput)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
            this.logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
            this.logError = logError ?? throw new ArgumentNullException(nameof(logError));
            this.altarService = altarService ?? throw new ArgumentNullException(nameof(altarService));
            this.weightCalculator = weightCalculator ?? throw new ArgumentNullException(nameof(weightCalculator));
            this.altarDisplayRenderer = altarDisplayRenderer ?? throw new ArgumentNullException(nameof(altarDisplayRenderer));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.pointIsInClickableArea = pointIsInClickableArea ?? throw new ArgumentNullException(nameof(pointIsInClickableArea));
            this.safeBlockInput = safeBlockInput ?? throw new ArgumentNullException(nameof(safeBlockInput));
        }

        public IEnumerator ProcessAltarClicking()
        {
            var altarSnapshot = altarService.GetAltarComponentsReadOnly();
            if (altarSnapshot.Count == 0)
            {
                yield break;
            }

            bool clickEater = settings.ClickEaterAltars;
            bool clickExarch = settings.ClickExarchAltars;
            bool leftHanded = settings.LeftHanded;

            // Pre-filter altars to avoid repeated checks
            var altarsToClick = altarSnapshot.Where(altar => ShouldClickAltar(altar, clickEater, clickExarch)).ToList();

            foreach (PrimaryAltarComponent altar in altarsToClick)
            {
                Element? boxToClick = GetAltarElementToClick(altar);
                if (boxToClick != null)
                {
                    yield return ClickAltarElement(boxToClick, leftHanded);
                }
            }
        }

        public bool ShouldClickAltar(PrimaryAltarComponent altar, bool clickEater, bool clickExarch)
        {
            // First check if altar type is enabled
            bool isEnabledType = (altar.AltarType == ClickIt.AltarType.EaterOfWorlds && clickEater) ||
                                (altar.AltarType == ClickIt.AltarType.SearingExarch && clickExarch);

            if (!isEnabledType)
                return false;

            // Use cached validation
            if (!altar.IsValidCached())
            {
                logMessage("Skipping altar - Validation failed", 5);
                return false;
            }

            if (!altar.TopMods.Element.IsValid || !altar.BottomMods.Element.IsValid)
            {
                logMessage("Skipping altar - Elements are not valid", 5);
                return false;
            }

            // Check for unmatched mods
            if (altar.TopMods.HasUnmatchedMods || altar.BottomMods.HasUnmatchedMods)
            {
                logMessage("Skipping altar - Unmatched mods present", 5);
                return false;
            }

            // Use cached weights
            var altarWeights = altar.GetCachedWeights(pc => weightCalculator.CalculateAltarWeights(pc));
            if (!altarWeights.HasValue)
            {
                logMessage("Skipping altar - Weight calculation failed", 5);
                return false;
            }

            // Use cached rectangles
            var (topModsRect, bottomModsRect) = altar.GetCachedRects();
            Vector2 topModsTopLeft = topModsRect.TopLeft;

            // Check if we can determine a valid choice
            Element? boxToClick = altarDisplayRenderer.DetermineAltarChoice(altar, altarWeights.Value, topModsRect, bottomModsRect, topModsTopLeft);

            if (boxToClick == null)
            {
                logMessage("Skipping altar - No valid choice could be determined", 5);
                return false;
            }

            // Final check: ensure the choice is clickable
            if (!pointIsInClickableArea(boxToClick.GetClientRect().Center, altar.AltarType.ToString()) ||
                !boxToClick.IsVisible)
            {
                logMessage("Skipping altar - Choice is not clickable or visible", 5);
                return false;
            }

            return true;
        }

        public Element? GetAltarElementToClick(PrimaryAltarComponent altar)
        {
            // All validation is now done in ShouldClickAltar, so we can proceed directly
            var altarWeights = altar.GetCachedWeights(pc => weightCalculator.CalculateAltarWeights(pc));
            if (!altarWeights.HasValue) return null;

            var (topModsRect, bottomModsRect) = altar.GetCachedRects();
            Vector2 topModsTopLeft = topModsRect.TopLeft;

            // We know this will succeed since ShouldClickAltar already validated it
            Element? boxToClick = altarDisplayRenderer.DetermineAltarChoice(altar, altarWeights.Value, topModsRect, bottomModsRect, topModsTopLeft);

            return boxToClick;
        }

        private IEnumerator ClickAltarElement(Element element, bool leftHanded)
        {
            // Element validation already done in ShouldClickAltar, skip redundant check
            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Vector2 clickPos = element.GetClientRect().Center + windowTopLeft;

            if (settings.BlockUserInput.Value)
            {
                safeBlockInput(true);
            }

            ExileCore.Input.SetCursorPos(clickPos);
            yield return new WaitTime(20);

            // Quick final validation before click
            if (!element.IsValid)
            {
                logError("CRITICAL: Altar element became invalid during click delay", 10);
                safeBlockInput(false);
                yield break;
            }

            if (leftHanded)
            {
                Mouse.RightClick();
            }
            else
            {
                Mouse.LeftClick();
            }

            safeBlockInput(false);
            yield return new WaitTime(random.Next(60, 70));
        }
    }
}
