using ClickIt.Utils;
using ExileCore;
using ExileCore.Shared.Helpers;
using SharpDX;
using RectangleF = SharpDX.RectangleF;
namespace ClickIt.Services
{
    public class AreaService
    {
        private RectangleF _fullScreenRectangle;
        private RectangleF _healthAndFlaskRectangle;
        private RectangleF _manaAndSkillsRectangle;
        private RectangleF _buffsAndDebuffsRectangle;

        // Thread safety lock for screen area updates
        private readonly object _screenAreasLock = new();
        public RectangleF FullScreenRectangle => _fullScreenRectangle;
        public RectangleF HealthAndFlaskRectangle => _healthAndFlaskRectangle;
        public RectangleF ManaAndSkillsRectangle => _manaAndSkillsRectangle;
        public RectangleF BuffsAndDebuffsRectangle => _buffsAndDebuffsRectangle;
        public void UpdateScreenAreas(GameController gameController)
        {
            using (LockManager.AcquireStatic(_screenAreasLock))
            {
                RectangleF winRect = gameController.Window.GetWindowRectangleTimeCache;
                // Compare with a small tolerance to avoid floating-point equality issues
                const float EPS = 0.5f;
                static bool RectsDiffer(RectangleF a, RectangleF b, float eps)
                {
                    return Math.Abs(a.Width - b.Width) > eps || Math.Abs(a.Height - b.Height) > eps ||
                           Math.Abs(a.X - b.X) > eps || Math.Abs(a.Y - b.Y) > eps;
                }

                if (RectsDiffer(_fullScreenRectangle, winRect, EPS))
                {
                    _fullScreenRectangle = new RectangleF(winRect.X, winRect.Y, winRect.Width, winRect.Height);
                    _healthAndFlaskRectangle = new RectangleF(
                        winRect.BottomLeft.X / 3f,
                        winRect.BottomLeft.Y / 5f * 3.92f,
                        winRect.BottomLeft.X + (winRect.BottomRight.X / 3.4f),
                        winRect.BottomLeft.Y);
                    _manaAndSkillsRectangle = new RectangleF(
                        winRect.BottomRight.X / 3f * 2.12f,
                        winRect.BottomLeft.Y / 5f * 3.92f,
                        winRect.BottomRight.X,
                        winRect.BottomRight.Y);
                    _buffsAndDebuffsRectangle = new RectangleF(
                        winRect.TopLeft.X,
                        winRect.TopLeft.Y,
                        winRect.TopRight.X / 2f,
                        winRect.TopLeft.Y + 120f);
                }
            }
        }
        public bool PointIsInClickableArea(Vector2 point)
        {
            using (LockManager.AcquireStatic(_screenAreasLock))
            {
                return point.PointInRectangle(_fullScreenRectangle) &&
                       !point.PointInRectangle(_healthAndFlaskRectangle) &&
                       !point.PointInRectangle(_manaAndSkillsRectangle) &&
                       !point.PointInRectangle(_buffsAndDebuffsRectangle);
            }
        }
    }
}
