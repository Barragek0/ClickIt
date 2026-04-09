namespace ClickIt.UI.Debug
{
    internal sealed class DebugFrameRenderer(AreaService? areaService, DeferredFrameQueue deferredFrameQueue)
    {
        private readonly AreaService? _areaService = areaService;
        private readonly DeferredFrameQueue _deferredFrameQueue = deferredFrameQueue;

        public void Render(ClickItSettings settings)
        {
            if (_areaService == null || !settings.DebugShowFrames)
                return;

            RectangleF healthSquare = _areaService.HealthSquareRectangle;
            RectangleF flaskRect = _areaService.FlaskRectangle;
            RectangleF flaskTertiaryRect = _areaService.FlaskTertiaryRectangle;
            RectangleF manaSquare = _areaService.ManaSquareRectangle;
            RectangleF skillsRect = _areaService.SkillsRectangle;
            RectangleF skillsTertiaryRect = _areaService.SkillsTertiaryRectangle;

            if (IsEmptyRect(healthSquare) || IsEmptyRect(flaskRect) || IsEmptyRect(flaskTertiaryRect))
            {
                (healthSquare, flaskRect, flaskTertiaryRect) = AreaService.SplitBottomAnchoredThreeRectanglesFromLeft(
                    _areaService.HealthAndFlaskRectangle,
                    0.6f,
                    0.85f,
                    1f);
            }

            if (IsEmptyRect(manaSquare) || IsEmptyRect(skillsRect) || IsEmptyRect(skillsTertiaryRect))
            {
                (manaSquare, skillsRect, skillsTertiaryRect) = AreaService.SplitBottomAnchoredThreeRectanglesFromRight(
                    _areaService.ManaAndSkillsRectangle,
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

            _deferredFrameQueue.Enqueue(_areaService.FullScreenRectangle, Color.LightSkyBlue, 1);
            _deferredFrameQueue.Enqueue(healthSquareDraw, Color.Red, 1);
            _deferredFrameQueue.Enqueue(flaskRectDraw, Color.Red, 1);
            _deferredFrameQueue.Enqueue(flaskTertiaryRectDraw, Color.Red, 1);
            _deferredFrameQueue.Enqueue(skillsRectDraw, Color.DeepSkyBlue, 1);
            _deferredFrameQueue.Enqueue(skillsTertiaryRectDraw, Color.DeepSkyBlue, 1);
            _deferredFrameQueue.Enqueue(manaSquareDraw, Color.DeepSkyBlue, 1);

            IReadOnlyList<RectangleF> buffsAndDebuffsRects = _areaService.BuffsAndDebuffsRectangles;
            if (buffsAndDebuffsRects.Count > 0)
            {
                for (int i = 0; i < buffsAndDebuffsRects.Count; i++)
                {
                    _deferredFrameQueue.Enqueue(buffsAndDebuffsRects[i], Color.Plum, 1);
                }
            }
            else
            {
                _deferredFrameQueue.Enqueue(_areaService.BuffsAndDebuffsRectangle, Color.Plum, 1);
            }

            _deferredFrameQueue.Enqueue(_areaService.ChatPanelBlockedRectangle, Color.Green, 1);
            _deferredFrameQueue.Enqueue(_areaService.MapPanelBlockedRectangle, Color.Pink, 1);
            _deferredFrameQueue.Enqueue(_areaService.XpBarBlockedRectangle, Color.Orange, 1);
            _deferredFrameQueue.Enqueue(_areaService.MirageBlockedRectangle, Color.Cyan, 1);
            _deferredFrameQueue.Enqueue(_areaService.AltarBlockedRectangle, Color.Gold, 1);
            _deferredFrameQueue.Enqueue(_areaService.RitualBlockedRectangle, Color.LawnGreen, 1);
            _deferredFrameQueue.Enqueue(_areaService.SentinelBlockedRectangle, Color.LightCoral, 1);

            IReadOnlyList<RectangleF> questTrackerRects = _areaService.QuestTrackerBlockedRectangles;
            for (int i = 0; i < questTrackerRects.Count; i++)
            {
                _deferredFrameQueue.Enqueue(questTrackerRects[i], Color.MediumPurple, 1);
            }
        }

        private static bool IsEmptyRect(RectangleF rect)
            => rect.X == 0f && rect.Y == 0f && rect.Width == 0f && rect.Height == 0f;

        private static RectangleF ToDrawRectangleFromLtrb(RectangleF rect)
        {
            float width = SystemMath.Max(0f, rect.Width - rect.X);
            float height = SystemMath.Max(0f, rect.Height - rect.Y);
            return new RectangleF(rect.X, rect.Y, width, height);
        }
    }
}