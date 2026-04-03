using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.UI.Debug.Sections
{
    internal sealed class FrameDebugOverlaySection(Debug.DebugOverlayRenderContext context)
    {
        private readonly Debug.DebugOverlayRenderContext _context = context;

        public void RenderDebugFrames(ClickItSettings settings)
        {
            if (_context.AreaService == null)
                return;

            if (!settings.DebugShowFrames)
                return;

            var healthSquare = _context.AreaService.HealthSquareRectangle;
            var flaskRect = _context.AreaService.FlaskRectangle;
            var flaskTertiaryRect = _context.AreaService.FlaskTertiaryRectangle;
            var manaSquare = _context.AreaService.ManaSquareRectangle;
            var skillsRect = _context.AreaService.SkillsRectangle;
            var skillsTertiaryRect = _context.AreaService.SkillsTertiaryRectangle;

            if (IsEmptyRect(healthSquare) || IsEmptyRect(flaskRect) || IsEmptyRect(flaskTertiaryRect))
            {
                (healthSquare, flaskRect, flaskTertiaryRect) = AreaService.SplitBottomAnchoredThreeRectanglesFromLeft(
                    _context.AreaService.HealthAndFlaskRectangle,
                    0.6f,
                    0.85f,
                    1f);
            }

            if (IsEmptyRect(manaSquare) || IsEmptyRect(skillsRect) || IsEmptyRect(skillsTertiaryRect))
            {
                (manaSquare, skillsRect, skillsTertiaryRect) = AreaService.SplitBottomAnchoredThreeRectanglesFromRight(
                    _context.AreaService.ManaAndSkillsRectangle,
                    0.6f,
                    0.85f,
                    1f);
            }

            RectangleF healthSquareDraw = ToDrawRectangleFromLtrb(healthSquare);
            RectangleF flaskRectDraw = ToDrawRectangleFromLtrb(flaskRect);
            RectangleF flaskTertiaryRectDraw = ToDrawRectangleFromLtrb(flaskTertiaryRect);
            RectangleF skillsRectDraw = ToDrawRectangleFromLtrb(skillsRect);
            RectangleF skillsTertiaryRectDraw = ToDrawRectangleFromLtrb(skillsTertiaryRect);
            RectangleF manaSquareDraw = ToDrawRectangleFromLtrb(manaSquare);

            _context.DeferredFrameQueue.Enqueue(_context.AreaService.FullScreenRectangle, Color.LightSkyBlue, 1);
            _context.DeferredFrameQueue.Enqueue(healthSquareDraw, Color.Red, 1);
            _context.DeferredFrameQueue.Enqueue(flaskRectDraw, Color.Red, 1);
            _context.DeferredFrameQueue.Enqueue(flaskTertiaryRectDraw, Color.Red, 1);
            _context.DeferredFrameQueue.Enqueue(skillsRectDraw, Color.DeepSkyBlue, 1);
            _context.DeferredFrameQueue.Enqueue(skillsTertiaryRectDraw, Color.DeepSkyBlue, 1);
            _context.DeferredFrameQueue.Enqueue(manaSquareDraw, Color.DeepSkyBlue, 1);

            var buffsAndDebuffsRects = _context.AreaService.BuffsAndDebuffsRectangles;
            if (buffsAndDebuffsRects.Count > 0)
            {
                for (int i = 0; i < buffsAndDebuffsRects.Count; i++)
                {
                    _context.DeferredFrameQueue.Enqueue(buffsAndDebuffsRects[i], Color.Plum, 1);
                }
            }
            else
            {
                _context.DeferredFrameQueue.Enqueue(_context.AreaService.BuffsAndDebuffsRectangle, Color.Plum, 1);
            }

            _context.DeferredFrameQueue.Enqueue(_context.AreaService.ChatPanelBlockedRectangle, Color.Green, 1);
            _context.DeferredFrameQueue.Enqueue(_context.AreaService.MapPanelBlockedRectangle, Color.Pink, 1);
            _context.DeferredFrameQueue.Enqueue(_context.AreaService.XpBarBlockedRectangle, Color.Orange, 1);
            _context.DeferredFrameQueue.Enqueue(_context.AreaService.MirageBlockedRectangle, Color.Cyan, 1);
            _context.DeferredFrameQueue.Enqueue(_context.AreaService.AltarBlockedRectangle, Color.Gold, 1);
            _context.DeferredFrameQueue.Enqueue(_context.AreaService.RitualBlockedRectangle, Color.LawnGreen, 1);
            _context.DeferredFrameQueue.Enqueue(_context.AreaService.SentinelBlockedRectangle, Color.LightCoral, 1);

            var questTrackerRects = _context.AreaService.QuestTrackerBlockedRectangles;
            for (int i = 0; i < questTrackerRects.Count; i++)
            {
                _context.DeferredFrameQueue.Enqueue(questTrackerRects[i], Color.MediumPurple, 1);
            }
        }

        private static bool IsEmptyRect(RectangleF rect)
            => rect.X == 0f && rect.Y == 0f && rect.Width == 0f && rect.Height == 0f;

        private static RectangleF ToDrawRectangleFromLtrb(RectangleF rect)
        {
            float width = Math.Max(0f, rect.Width - rect.X);
            float height = Math.Max(0f, rect.Height - rect.Y);
            return new RectangleF(rect.X, rect.Y, width, height);
        }
    }
}