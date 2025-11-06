using System;
using System.Collections;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ClickIt.Utils;

using SharpDX;

#nullable enable

namespace ClickIt.Services
{
    /// <summary>
    /// Handles the complex clicking logic and label processing
    /// </summary>
    public class ClickService
    {
        private readonly ClickItSettings settings;
        private readonly GameController gameController;
        private readonly Action<string, float> logMessage;
        private readonly Func<LabelOnGround?> cachedLabels;
        private readonly InputHandler inputHandler;
        private readonly Func<Entity?> getShrine;
        private readonly Func<bool> groundItemsVisible;

        public ClickService(
            ClickItSettings settings,
            GameController gameController,
            Action<string, float> logMessage,
            Func<LabelOnGround?> cachedLabels,
            InputHandler inputHandler,
            Func<Entity?> getShrine,
            Func<bool> groundItemsVisible)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
            this.logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
            this.cachedLabels = cachedLabels ?? throw new ArgumentNullException(nameof(cachedLabels));
            this.inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
            this.getShrine = getShrine ?? throw new ArgumentNullException(nameof(getShrine));
            this.groundItemsVisible = groundItemsVisible ?? throw new ArgumentNullException(nameof(groundItemsVisible));
        }

        public IEnumerator ProcessClickLabel(Element? altar = null)
        {
            if (altar != null)
            {
                return ProcessAltarClick(altar);
            }

            return ProcessStandardClick();
        }

        private IEnumerator ProcessAltarClick(Element altar)
        {
            logMessage("Processing altar click", 5);

            if (!inputHandler.CanClick(gameController))
            {
                yield break;
            }

            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            SharpDX.Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            // For altars, use the center of the element directly
            SharpDX.Vector2 clickPos = altar.GetClientRect().Center + windowTopLeft;

            inputHandler.PerformClick(clickPos);
            yield return new WaitTime(new Random().Next(50, 150));
        }

        private IEnumerator ProcessStandardClick()
        {
            if (!inputHandler.CanClick(gameController))
            {
                yield break;
            }

            if (!groundItemsVisible())
            {
                yield break;
            }

            LabelOnGround? nextLabel = cachedLabels();
            if (nextLabel == null)
            {
                logMessage("Next label is null", 5);
                yield break;
            }

            Entity item = nextLabel.ItemOnGround;
            if (item?.DistancePlayer > settings.ClickDistance)
            {
                logMessage($"Item too far: {item?.DistancePlayer} > {settings.ClickDistance}", 5);
                yield break;
            }

            RectangleF windowArea = gameController.Window.GetWindowRectangleTimeCache;
            SharpDX.Vector2 windowTopLeft = new(windowArea.X, windowArea.Y);
            Element labelElement = nextLabel.Label;

            if (ProcessSpecialCases(nextLabel, labelElement, windowTopLeft))
            {
                yield break;
            }

            // Handle regular clicking
            ProcessRegularClick(nextLabel, windowTopLeft);
            yield return new WaitTime(new Random().Next(50, 150));
        }

        private bool ProcessSpecialCases(LabelOnGround nextLabel, Element labelElement, SharpDX.Vector2 windowTopLeft)
        {
            Entity item = nextLabel.ItemOnGround;
            string path = item.Path ?? "";
            EntityType type = item.Type;

            // Handle essences
            if (settings.ClickEssences && Services.ElementService.GetElementByString(labelElement, "The monster is imprisoned by powerful Essences.") != null)
            {
                logMessage("Found essence, processing...", 5);
                ProcessEssence(nextLabel, windowTopLeft);
                return true;
            }

            // Handle sulphite
            if (settings.ClickSulphiteVeins && Services.ElementService.GetElementByString(labelElement, "Interact to acquire Voltaxic Sulphite") != null)
            {
                logMessage("Found sulphite vein", 5);
                ProcessRegularClick(nextLabel, windowTopLeft);
                return true;
            }

            // Handle shrines
            if (settings.ClickShrines && type == EntityType.Shrine)
            {
                Entity? shrine = getShrine();
                if (shrine != null)
                {
                    logMessage("Found shrine", 5);
                    ProcessRegularClick(nextLabel, windowTopLeft);
                    return true;
                }
            }

            return false;
        }

        private void ProcessEssence(LabelOnGround nextLabel, SharpDX.Vector2 windowTopLeft)
        {
            // This would need the essence processing logic extracted as well
            // For now, delegate to regular click processing
            ProcessRegularClick(nextLabel, windowTopLeft);
        }

        private void ProcessRegularClick(LabelOnGround nextLabel, SharpDX.Vector2 windowTopLeft)
        {
            if (nextLabel?.Label?.GetClientRect() == null)
            {
                logMessage("Label client rect is null", 5);
                return;
            }

            SharpDX.Vector2 clickPos = inputHandler.CalculateClickPosition(nextLabel, windowTopLeft);
            inputHandler.PerformClick(clickPos);
        }
    }
}