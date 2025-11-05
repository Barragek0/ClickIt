using ExileCore;
using ExileCore.Shared.Helpers;
using SharpDX;

namespace ClickIt.Services
{
    /// <summary>
    /// Handles screen area calculations and clickable area detection
    /// </summary>
    public class AreaService
    {
        private RectangleF _fullScreenRectangle;
        private RectangleF _healthAndFlaskRectangle;
        private RectangleF _manaAndSkillsRectangle;
        private RectangleF _buffsAndDebuffsRectangle;

        public RectangleF FullScreenRectangle => _fullScreenRectangle;
        public RectangleF HealthAndFlaskRectangle => _healthAndFlaskRectangle;
        public RectangleF ManaAndSkillsRectangle => _manaAndSkillsRectangle;
        public RectangleF BuffsAndDebuffsRectangle => _buffsAndDebuffsRectangle;

        public void UpdateScreenAreas(GameController gameController)
        {
            RectangleF winRect = gameController.Window.GetWindowRectangleTimeCache;

            // Only update if window size or position changed
            if (_fullScreenRectangle.Width != winRect.Width || _fullScreenRectangle.Height != winRect.Height ||
                _fullScreenRectangle.X != winRect.X || _fullScreenRectangle.Y != winRect.Y)
            {
                _fullScreenRectangle = new RectangleF(winRect.X, winRect.Y, winRect.Width, winRect.Height);

                _healthAndFlaskRectangle = new RectangleF(
                    (float)(winRect.BottomLeft.X / 3),
                    (float)(winRect.BottomLeft.Y / 5 * 3.92),
                    (float)(winRect.BottomLeft.X + (winRect.BottomRight.X / 3.4)),
                    winRect.BottomLeft.Y);

                _manaAndSkillsRectangle = new RectangleF(
                    (float)(winRect.BottomRight.X / 3 * 2.12),
                    (float)(winRect.BottomLeft.Y / 5 * 3.92),
                    winRect.BottomRight.X,
                    winRect.BottomRight.Y);

                _buffsAndDebuffsRectangle = new RectangleF(
                    winRect.TopLeft.X,
                    winRect.TopLeft.Y,
                    winRect.TopRight.X / 2,
                    winRect.TopLeft.Y + 120);
            }
        }

        public bool PointIsInClickableArea(Vector2 point)
        {
            return point.PointInRectangle(_fullScreenRectangle) &&
                   !point.PointInRectangle(_healthAndFlaskRectangle) &&
                   !point.PointInRectangle(_manaAndSkillsRectangle) &&
                   !point.PointInRectangle(_buffsAndDebuffsRectangle);
        }
    }
}