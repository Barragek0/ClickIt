using ClickIt.Utils;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;
using SharpDX;
using System;
using System.Threading;
#nullable enable
namespace ClickIt.Utils
{
    public class InputHandler
    {
        private readonly ClickItSettings _settings;
        private readonly Random _random;
        private readonly Action<bool>? _safeBlockInput;

        public InputHandler(ClickItSettings settings, Action<bool>? safeBlockInput = null)
        {
            _settings = settings;
            _random = new Random();
            _safeBlockInput = safeBlockInput;
        }
        public Vector2 CalculateClickPosition(LabelOnGround label, Vector2 windowTopLeft)
        {
            Vector2 offset = new(_random.Next(0, 5),
                label.ItemOnGround.Type == EntityType.Chest ?
                -_random.Next(_settings.ChestHeightOffset, _settings.ChestHeightOffset + 2) :
                _random.Next(0, 5));
            return label.Label.GetClientRect().Center + windowTopLeft + offset;
        }
        public void TriggerToggleItems()
        {
            if (_settings.ToggleItems.Value && _random.Next(0, 20) == 0)
            {
#pragma warning disable CS0618
                Keyboard.KeyPress(_settings.ToggleItemsHotkey, 20);
                Keyboard.KeyPress(_settings.ToggleItemsHotkey, 20);
#pragma warning restore CS0618
            }
        }
        public bool CanClick(GameController gameController)
        {
#pragma warning disable CS0618
            return ExileCore.Input.GetKeyState(_settings.ClickLabelKey.Value) &&
#pragma warning restore CS0618
                   IsPOEActive(gameController) &&
                   (!_settings.BlockOnOpenLeftRightPanel || !IsPanelOpen(gameController)) &&
                   !IsInTownOrHideout(gameController) &&
                   !gameController.IngameState.IngameUi.ChatTitlePanel.IsVisible;
        }
        private static bool IsPOEActive(GameController gameController)
        {
            return gameController.Window.IsForeground();
        }
        private static bool IsPanelOpen(GameController gameController)
        {
            return gameController.IngameState.IngameUi.OpenLeftPanel.Address != 0 ||
                   gameController.IngameState.IngameUi.OpenRightPanel.Address != 0;
        }
        private static bool IsInTownOrHideout(GameController gameController)
        {
            return gameController.Area.CurrentArea.IsHideout || gameController.Area.CurrentArea.IsTown;
        }

        public void PerformClick(Vector2 position)
        {
            if (_settings.BlockUserInput.Value && _safeBlockInput != null)
            {
                _safeBlockInput(true);
            }
            Input.SetCursorPos(position);
            if (_settings.LeftHanded.Value)
                Mouse.RightClick();
            else
                Mouse.LeftClick();
            if (_settings.BlockUserInput.Value && _safeBlockInput != null)
            {
                _safeBlockInput(false);
            }
        }
    }
}
