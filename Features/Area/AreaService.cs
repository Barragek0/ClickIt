using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Features.Area
{
    public class AreaService
    {
        private readonly AreaBlockedSnapshotProvider _blockedSnapshotProvider = new();
        private readonly BlockedAreaEvaluatorPipeline _blockedAreaEvaluatorPipeline;

        public AreaService()
        {
            _blockedAreaEvaluatorPipeline = new BlockedAreaEvaluatorPipeline(
            [
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.HealthSquareRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.FlaskRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.FlaskTertiaryRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.SkillsRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.SkillsTertiaryRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.ManaSquareRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.BuffsAndDebuffsRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInAnyBlockedUiRectangle(point, snapshot.BuffsAndDebuffsRectangles, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInAnyBlockedUiRectangle(point, snapshot.QuestTrackerBlockedRectangles, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.ChatPanelBlockedRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.MapPanelBlockedRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.XpBarBlockedRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.MirageBlockedRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.AltarBlockedRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.RitualBlockedRectangle, snapshot.FullScreenRectangle),
                (snapshot, point) => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, snapshot.SentinelBlockedRectangle, snapshot.FullScreenRectangle)
            ]);
        }

        public RectangleF FullScreenRectangle => _blockedSnapshotProvider.CurrentSnapshot.FullScreenRectangle;
        public RectangleF HealthAndFlaskRectangle => _blockedSnapshotProvider.CurrentSnapshot.HealthAndFlaskRectangle;
        public RectangleF ManaAndSkillsRectangle => _blockedSnapshotProvider.CurrentSnapshot.ManaAndSkillsRectangle;
        public RectangleF HealthSquareRectangle => _blockedSnapshotProvider.CurrentSnapshot.HealthSquareRectangle;
        public RectangleF FlaskRectangle => _blockedSnapshotProvider.CurrentSnapshot.FlaskRectangle;
        public RectangleF FlaskTertiaryRectangle => _blockedSnapshotProvider.CurrentSnapshot.FlaskTertiaryRectangle;
        public RectangleF SkillsRectangle => _blockedSnapshotProvider.CurrentSnapshot.SkillsRectangle;
        public RectangleF SkillsTertiaryRectangle => _blockedSnapshotProvider.CurrentSnapshot.SkillsTertiaryRectangle;
        public RectangleF ManaSquareRectangle => _blockedSnapshotProvider.CurrentSnapshot.ManaSquareRectangle;
        public RectangleF BuffsAndDebuffsRectangle => _blockedSnapshotProvider.CurrentSnapshot.BuffsAndDebuffsRectangle;
        public IReadOnlyList<RectangleF> BuffsAndDebuffsRectangles => _blockedSnapshotProvider.CurrentSnapshot.BuffsAndDebuffsRectangles;
        public RectangleF ChatPanelBlockedRectangle => _blockedSnapshotProvider.CurrentSnapshot.ChatPanelBlockedRectangle;
        public RectangleF MapPanelBlockedRectangle => _blockedSnapshotProvider.CurrentSnapshot.MapPanelBlockedRectangle;
        public RectangleF XpBarBlockedRectangle => _blockedSnapshotProvider.CurrentSnapshot.XpBarBlockedRectangle;
        public RectangleF MirageBlockedRectangle => _blockedSnapshotProvider.CurrentSnapshot.MirageBlockedRectangle;
        public RectangleF AltarBlockedRectangle => _blockedSnapshotProvider.CurrentSnapshot.AltarBlockedRectangle;
        public RectangleF RitualBlockedRectangle => _blockedSnapshotProvider.CurrentSnapshot.RitualBlockedRectangle;
        public RectangleF SentinelBlockedRectangle => _blockedSnapshotProvider.CurrentSnapshot.SentinelBlockedRectangle;
        public IReadOnlyList<RectangleF> QuestTrackerBlockedRectangles => _blockedSnapshotProvider.CurrentSnapshot.QuestTrackerBlockedRectangles;

        internal void ApplyBlockedSnapshot(AreaBlockedSnapshot snapshot)
            => _blockedSnapshotProvider.ApplySnapshot(snapshot);

        public void UpdateScreenAreas(GameController gameController)
            => _blockedSnapshotProvider.UpdateScreenAreas(gameController);

        internal static bool ShouldUpdateMapPanelBlockedRectangle(bool isInTownOrHideout)
            => AreaBlockedSnapshotProvider.ShouldUpdateMapPanelBlockedRectangle(isInTownOrHideout);

        internal static (RectangleF primarySquare, RectangleF secondaryCompanion) SplitBottomAnchoredRectangleFromLeft(
            RectangleF source,
            float secondaryHeightRatio)
        {
            return AreaBlockedSnapshotProvider.SplitBottomAnchoredRectangleFromLeft(source, secondaryHeightRatio);
        }

        internal static (RectangleF primarySquare, RectangleF secondaryCompanion, RectangleF tertiaryCompanion) SplitBottomAnchoredThreeRectanglesFromLeft(
            RectangleF source,
            float secondaryHeightRatio,
            float tertiaryHeightRatio,
            float tertiaryWidthRatio)
        {
            return AreaBlockedSnapshotProvider.SplitBottomAnchoredThreeRectanglesFromLeft(source, secondaryHeightRatio, tertiaryHeightRatio, tertiaryWidthRatio);
        }

        internal static (RectangleF primarySquare, RectangleF secondaryCompanion) SplitBottomAnchoredRectangleFromRight(
            RectangleF source,
            float secondaryHeightRatio)
        {
            return AreaBlockedSnapshotProvider.SplitBottomAnchoredRectangleFromRight(source, secondaryHeightRatio);
        }

        internal static (RectangleF primarySquare, RectangleF secondaryCompanion, RectangleF tertiaryCompanion) SplitBottomAnchoredThreeRectanglesFromRight(
            RectangleF source,
            float secondaryHeightRatio,
            float tertiaryHeightRatio,
            float tertiaryWidthRatio)
        {
            return AreaBlockedSnapshotProvider.SplitBottomAnchoredThreeRectanglesFromRight(source, secondaryHeightRatio, tertiaryHeightRatio, tertiaryWidthRatio);
        }

        public bool PointIsInClickableArea(Vector2 point)
        {
            AreaBlockedSnapshot snapshot = _blockedSnapshotProvider.CurrentSnapshot;
            if (!point.PointInRectangle(snapshot.FullScreenRectangle))
                return false;

            return !IsBlockedByAreaEvaluatorPipeline(snapshot, point);
        }

        private bool IsBlockedByAreaEvaluatorPipeline(AreaBlockedSnapshot snapshot, Vector2 point)
            => _blockedAreaEvaluatorPipeline.IsBlocked(snapshot, point);

        public bool PointIsInClickableArea(GameController? gameController, Vector2 point)
        {
            if (gameController != null)
                UpdateScreenAreas(gameController);
            return PointIsInClickableArea(point);
        }
    }
}