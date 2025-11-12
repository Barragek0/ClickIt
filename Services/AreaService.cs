using ExileCore;
using ExileCore.Shared.Helpers;
using SharpDX;
namespace ClickIt.Services
{
    public class AreaService
    {
        private RectangleF _fullScreenRectangle;
        private RectangleF _healthAndFlaskRectangle;
        private RectangleF _manaAndSkillsRectangle;
        private RectangleF _buffsAndDebuffsRectangle;

        // Thread safety lock for screen area updates
        private readonly object _screenAreasLock = new object();
        public RectangleF FullScreenRectangle => _fullScreenRectangle;
        public RectangleF HealthAndFlaskRectangle => _healthAndFlaskRectangle;
        public RectangleF ManaAndSkillsRectangle => _manaAndSkillsRectangle;
        public RectangleF BuffsAndDebuffsRectangle => _buffsAndDebuffsRectangle;
        public void UpdateScreenAreas(GameController gameController)
        {
            var gm = global::ClickIt.Utils.LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_screenAreasLock))
                {
                    RectangleF winRect = gameController.Window.GetWindowRectangleTimeCache;
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
                return;
            }

            RectangleF winRectNoLock = gameController.Window.GetWindowRectangleTimeCache;
            if (_fullScreenRectangle.Width != winRectNoLock.Width || _fullScreenRectangle.Height != winRectNoLock.Height ||
                _fullScreenRectangle.X != winRectNoLock.X || _fullScreenRectangle.Y != winRectNoLock.Y)
            {
                _fullScreenRectangle = new RectangleF(winRectNoLock.X, winRectNoLock.Y, winRectNoLock.Width, winRectNoLock.Height);
                _healthAndFlaskRectangle = new RectangleF(
                    (float)(winRectNoLock.BottomLeft.X / 3),
                    (float)(winRectNoLock.BottomLeft.Y / 5 * 3.92),
                    (float)(winRectNoLock.BottomLeft.X + (winRectNoLock.BottomRight.X / 3.4)),
                    winRectNoLock.BottomLeft.Y);
                _manaAndSkillsRectangle = new RectangleF(
                    (float)(winRectNoLock.BottomRight.X / 3 * 2.12),
                    (float)(winRectNoLock.BottomLeft.Y / 5 * 3.92),
                    winRectNoLock.BottomRight.X,
                    winRectNoLock.BottomRight.Y);
                _buffsAndDebuffsRectangle = new RectangleF(
                    winRectNoLock.TopLeft.X,
                    winRectNoLock.TopLeft.Y,
                    winRectNoLock.TopRight.X / 2,
                    winRectNoLock.TopLeft.Y + 120);
            }
        }
        public bool PointIsInClickableArea(Vector2 point)
        {
            var gm = global::ClickIt.Utils.LockManager.Instance;
            if (gm != null)
            {
                using (gm.Acquire(_screenAreasLock))
                {
                    return point.PointInRectangle(_fullScreenRectangle) &&
                           !point.PointInRectangle(_healthAndFlaskRectangle) &&
                           !point.PointInRectangle(_manaAndSkillsRectangle) &&
                           !point.PointInRectangle(_buffsAndDebuffsRectangle);
                }
            }

            return point.PointInRectangle(_fullScreenRectangle) &&
                   !point.PointInRectangle(_healthAndFlaskRectangle) &&
                   !point.PointInRectangle(_manaAndSkillsRectangle) &&
                   !point.PointInRectangle(_buffsAndDebuffsRectangle);
        }
    }
}
