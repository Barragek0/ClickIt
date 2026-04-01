using ClickIt.Utils;
using ClickIt.Services.Area;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.Shared.Helpers;
using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ClickIt.Services
{
    public class AreaService
    {
        private const int BlockedUiRectanglesRefreshIntervalMs = 10_000;
        private const int BuffsAndDebuffsRectanglesRefreshIntervalMs = 1_000;
        private const int QuestTrackerRectanglesHoldLastGoodMs = 1_000;
        private const float SideCompanionHeightRatio = 0.625f;
        private const float SideCompanionWidthRatio = 0.555f;
        private const float SideTertiaryCompanionHeightRatio = 0.85f;
        private const float SideTertiaryCompanionWidthRatio = 0.79f;

        private RectangleF _fullScreenRectangle;
        private RectangleF _healthAndFlaskRectangle;
        private RectangleF _manaAndSkillsRectangle;
        private RectangleF _healthSquareRectangle;
        private RectangleF _flaskRectangle;
        private RectangleF _flaskTertiaryRectangle;
        private RectangleF _skillsRectangle;
        private RectangleF _skillsTertiaryRectangle;
        private RectangleF _manaSquareRectangle;
        private RectangleF _buffsAndDebuffsRectangle;
        private readonly List<RectangleF> _buffsAndDebuffsRectangles = [];
        private RectangleF _chatPanelBlockedRectangle;
        private RectangleF _mapPanelBlockedRectangle;
        private RectangleF _xpBarBlockedRectangle;
        private RectangleF _mirageBlockedRectangle;
        private RectangleF _altarBlockedRectangle;
        private RectangleF _ritualBlockedRectangle;
        private RectangleF _sentinelBlockedRectangle;
        private readonly List<RectangleF> _questTrackerBlockedRectangles = [];
        private long _lastQuestTrackerRectanglesSuccessTimestampMs;
        private long _lastBlockedUiRectanglesRefreshTimestampMs;
        private long _lastBuffsAndDebuffsRectanglesRefreshTimestampMs;
        private long _lastKnownAreaHash = long.MinValue;

        private readonly object _screenAreasLock = new();
        private readonly BlockedAreaEvaluatorPipeline _blockedAreaEvaluatorPipeline;

        public AreaService()
        {
            _blockedAreaEvaluatorPipeline = new BlockedAreaEvaluatorPipeline(
            [
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _healthSquareRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _flaskRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _flaskTertiaryRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _skillsRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _skillsTertiaryRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _manaSquareRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _buffsAndDebuffsRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInAnyBlockedUiRectangle(point, _buffsAndDebuffsRectangles, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInAnyBlockedUiRectangle(point, _questTrackerBlockedRectangles, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _chatPanelBlockedRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _mapPanelBlockedRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _xpBarBlockedRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _mirageBlockedRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _altarBlockedRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _ritualBlockedRectangle, _fullScreenRectangle),
                point => BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, _sentinelBlockedRectangle, _fullScreenRectangle)
            ]);
        }

        public RectangleF FullScreenRectangle => _fullScreenRectangle;
        public RectangleF HealthAndFlaskRectangle => _healthAndFlaskRectangle;
        public RectangleF ManaAndSkillsRectangle => _manaAndSkillsRectangle;
        public RectangleF HealthSquareRectangle => _healthSquareRectangle;
        public RectangleF FlaskRectangle => _flaskRectangle;
        public RectangleF FlaskTertiaryRectangle => _flaskTertiaryRectangle;
        public RectangleF SkillsRectangle => _skillsRectangle;
        public RectangleF SkillsTertiaryRectangle => _skillsTertiaryRectangle;
        public RectangleF ManaSquareRectangle => _manaSquareRectangle;
        public RectangleF BuffsAndDebuffsRectangle => _buffsAndDebuffsRectangle;
        public IReadOnlyList<RectangleF> BuffsAndDebuffsRectangles => _buffsAndDebuffsRectangles;
        public RectangleF ChatPanelBlockedRectangle => _chatPanelBlockedRectangle;
        public RectangleF MapPanelBlockedRectangle => _mapPanelBlockedRectangle;
        public RectangleF XpBarBlockedRectangle => _xpBarBlockedRectangle;
        public RectangleF MirageBlockedRectangle => _mirageBlockedRectangle;
        public RectangleF AltarBlockedRectangle => _altarBlockedRectangle;
        public RectangleF RitualBlockedRectangle => _ritualBlockedRectangle;
        public RectangleF SentinelBlockedRectangle => _sentinelBlockedRectangle;
        public IReadOnlyList<RectangleF> QuestTrackerBlockedRectangles => _questTrackerBlockedRectangles;

        internal void ApplyBlockedSnapshot(AreaBlockedSnapshot snapshot)
        {
            using (LockManager.AcquireStatic(_screenAreasLock))
            {
                _fullScreenRectangle = snapshot.FullScreenRectangle;
                _healthAndFlaskRectangle = snapshot.HealthAndFlaskRectangle;
                _manaAndSkillsRectangle = snapshot.ManaAndSkillsRectangle;
                _healthSquareRectangle = snapshot.HealthSquareRectangle;
                _flaskRectangle = snapshot.FlaskRectangle;
                _flaskTertiaryRectangle = snapshot.FlaskTertiaryRectangle;
                _skillsRectangle = snapshot.SkillsRectangle;
                _skillsTertiaryRectangle = snapshot.SkillsTertiaryRectangle;
                _manaSquareRectangle = snapshot.ManaSquareRectangle;
                _buffsAndDebuffsRectangle = snapshot.BuffsAndDebuffsRectangle;
                _chatPanelBlockedRectangle = snapshot.ChatPanelBlockedRectangle;
                _mapPanelBlockedRectangle = snapshot.MapPanelBlockedRectangle;
                _xpBarBlockedRectangle = snapshot.XpBarBlockedRectangle;
                _mirageBlockedRectangle = snapshot.MirageBlockedRectangle;
                _altarBlockedRectangle = snapshot.AltarBlockedRectangle;
                _ritualBlockedRectangle = snapshot.RitualBlockedRectangle;
                _sentinelBlockedRectangle = snapshot.SentinelBlockedRectangle;

                _buffsAndDebuffsRectangles.Clear();
                _buffsAndDebuffsRectangles.AddRange(snapshot.BuffsAndDebuffsRectangles);

                _questTrackerBlockedRectangles.Clear();
                _questTrackerBlockedRectangles.AddRange(snapshot.QuestTrackerBlockedRectangles);
            }
        }

        public void UpdateScreenAreas(GameController gameController)
        {
            using (LockManager.AcquireStatic(_screenAreasLock))
            {
                long now = Environment.TickCount64;
                bool areaChanged = HasAreaChanged(gameController);

                if (BlockedAreaRefreshScheduler.ShouldRefresh(
                    now,
                    _lastBlockedUiRectanglesRefreshTimestampMs,
                    BlockedUiRectanglesRefreshIntervalMs,
                    forceRefresh: areaChanged))
                {
                    RefreshMainBlockedAreas(gameController, now);
                    _lastBlockedUiRectanglesRefreshTimestampMs = now;
                }

                if (BlockedAreaRefreshScheduler.ShouldRefresh(
                    now,
                    _lastBuffsAndDebuffsRectanglesRefreshTimestampMs,
                    BuffsAndDebuffsRectanglesRefreshIntervalMs))
                {
                    RefreshBuffAndDebuffAreas(gameController);
                    _lastBuffsAndDebuffsRectanglesRefreshTimestampMs = now;
                }
            }
        }

        private bool HasAreaChanged(GameController gameController)
        {
            long currentAreaHash = ResolveCurrentAreaHash(gameController);
            bool changed = AreaChangeRules.HasAreaHashChanged(currentAreaHash, _lastKnownAreaHash);
            if (changed)
                _lastKnownAreaHash = currentAreaHash;
            return changed;
        }

        private void RefreshMainBlockedAreas(GameController gameController, long now)
        {
            RectangleF winRect = gameController.Window.GetWindowRectangleTimeCache;
            _fullScreenRectangle = new RectangleF(winRect.X, winRect.Y, winRect.Width, winRect.Height);

            RectangleF leftCombined = new RectangleF(
                winRect.BottomLeft.X / 3f,
                winRect.BottomLeft.Y / 5f * 3.92f,
                winRect.BottomLeft.X + (winRect.BottomRight.X / 3.4f),
                winRect.BottomLeft.Y);

            RectangleF rightCombined = new RectangleF(
                winRect.BottomRight.X / 3f * 2.12f,
                winRect.BottomLeft.Y / 5f * 3.92f,
                winRect.BottomRight.X,
                winRect.BottomRight.Y);

            _healthAndFlaskRectangle = leftCombined;
            _manaAndSkillsRectangle = rightCombined;

            (_healthSquareRectangle, _flaskRectangle, _flaskTertiaryRectangle) = SplitBottomAnchoredThreeRectanglesFromLeft(
                leftCombined,
                SideCompanionHeightRatio,
                SideTertiaryCompanionHeightRatio,
                SideTertiaryCompanionWidthRatio);
            (_manaSquareRectangle, _skillsRectangle, _skillsTertiaryRectangle) = SplitBottomAnchoredThreeRectanglesFromRight(
                rightCombined,
                SideCompanionHeightRatio,
                SideTertiaryCompanionHeightRatio,
                SideTertiaryCompanionWidthRatio);

            RefreshQuestTrackerAreas(gameController, now);

            _chatPanelBlockedRectangle = AreaBlockedRectangleResolver.ResolveChatPanelBlockedRectangle(gameController);
            _mapPanelBlockedRectangle = ShouldUpdateMapPanelBlockedRectangle(IsInTownOrHideout(gameController))
                ? AreaBlockedRectangleResolver.ResolveMapPanelBlockedRectangle(gameController)
                : RectangleF.Empty;
            _xpBarBlockedRectangle = AreaBlockedRectangleResolver.ResolveXpBarBlockedRectangle(gameController);
            _mirageBlockedRectangle = AreaBlockedRectangleResolver.ResolveMirageBlockedRectangle(gameController);
            _altarBlockedRectangle = AreaBlockedRectangleResolver.ResolveAltarBlockedRectangle(gameController);
            _ritualBlockedRectangle = AreaBlockedRectangleResolver.ResolveRitualBlockedRectangle(gameController);
            _sentinelBlockedRectangle = AreaBlockedRectangleResolver.ResolveSentinelBlockedRectangle(gameController);
        }

        private void RefreshQuestTrackerAreas(GameController gameController, long now)
        {
            List<RectangleF> current = AreaBlockedRectangleCollectionResolver.ResolveQuestTrackerBlockedRectangles(gameController);
            if (current.Count > 0)
            {
                _questTrackerBlockedRectangles.Clear();
                _questTrackerBlockedRectangles.AddRange(current);
                _lastQuestTrackerRectanglesSuccessTimestampMs = now;
                return;
            }

            if (!BlockedAreaRefreshScheduler.ShouldRetainQuestTrackerRectanglesOnEmptyRead(
                _questTrackerBlockedRectangles.Count,
                now,
                _lastQuestTrackerRectanglesSuccessTimestampMs,
                QuestTrackerRectanglesHoldLastGoodMs))
            {
                _questTrackerBlockedRectangles.Clear();
            }
        }

        private void RefreshBuffAndDebuffAreas(GameController gameController)
        {
            List<RectangleF> rectangles = AreaBlockedRectangleCollectionResolver.ResolveBuffsAndDebuffsBlockedRectangles(gameController);
            _buffsAndDebuffsRectangles.Clear();
            _buffsAndDebuffsRectangles.AddRange(rectangles);
            _buffsAndDebuffsRectangle = _buffsAndDebuffsRectangles.Count > 0
                ? _buffsAndDebuffsRectangles[0]
                : RectangleF.Empty;
        }

        internal static bool ShouldUpdateMapPanelBlockedRectangle(bool isInTownOrHideout) => !isInTownOrHideout;

        internal static (RectangleF primarySquare, RectangleF secondaryCompanion) SplitBottomAnchoredRectangleFromLeft(
            RectangleF source,
            float secondaryHeightRatio)
        {
            return SplitBottomAnchoredRectangle(source, secondaryHeightRatio, anchorLeft: true);
        }

        internal static (RectangleF primarySquare, RectangleF secondaryCompanion, RectangleF tertiaryCompanion) SplitBottomAnchoredThreeRectanglesFromLeft(
            RectangleF source,
            float secondaryHeightRatio,
            float tertiaryHeightRatio,
            float tertiaryWidthRatio)
        {
            (RectangleF primary, RectangleF secondary) = SplitBottomAnchoredRectangle(source, secondaryHeightRatio, anchorLeft: true);
            RectangleF tertiary = BuildLinkedBottomRectangle(secondary, tertiaryHeightRatio, tertiaryWidthRatio, anchorLeft: true);
            return (primary, secondary, tertiary);
        }

        internal static (RectangleF primarySquare, RectangleF secondaryCompanion) SplitBottomAnchoredRectangleFromRight(
            RectangleF source,
            float secondaryHeightRatio)
        {
            return SplitBottomAnchoredRectangle(source, secondaryHeightRatio, anchorLeft: false);
        }

        internal static (RectangleF primarySquare, RectangleF secondaryCompanion, RectangleF tertiaryCompanion) SplitBottomAnchoredThreeRectanglesFromRight(
            RectangleF source,
            float secondaryHeightRatio,
            float tertiaryHeightRatio,
            float tertiaryWidthRatio)
        {
            (RectangleF primary, RectangleF secondary) = SplitBottomAnchoredRectangle(source, secondaryHeightRatio, anchorLeft: false);
            RectangleF tertiary = BuildLinkedBottomRectangle(secondary, tertiaryHeightRatio, tertiaryWidthRatio, anchorLeft: false);
            return (primary, secondary, tertiary);
        }

        private static RectangleF BuildLinkedBottomRectangle(
            RectangleF source,
            float heightRatio,
            float widthRatio,
            bool anchorLeft)
            => BlockedAreaGeometryEngine.BuildLinkedBottomRectangle(source, heightRatio, widthRatio, anchorLeft);

        private static (RectangleF primarySquare, RectangleF secondaryCompanion) SplitBottomAnchoredRectangle(
            RectangleF source,
            float secondaryHeightRatio,
            bool anchorLeft)
            => BlockedAreaGeometryEngine.SplitBottomAnchoredRectangle(source, secondaryHeightRatio, anchorLeft, SideCompanionWidthRatio);

        public bool PointIsInClickableArea(Vector2 point)
        {
            using (LockManager.AcquireStatic(_screenAreasLock))
            {
                if (!point.PointInRectangle(_fullScreenRectangle))
                    return false;

                return !IsBlockedByAreaEvaluatorPipeline(point);
            }
        }

        private bool IsBlockedByAreaEvaluatorPipeline(Vector2 point)
            => _blockedAreaEvaluatorPipeline.IsBlocked(point);

        public bool PointIsInClickableArea(GameController? gameController, Vector2 point)
        {
            if (gameController != null)
                UpdateScreenAreas(gameController);
            return PointIsInClickableArea(point);
        }

        private static bool IsInTownOrHideout(GameController? gameController)
        {
            var area = gameController?.Area?.CurrentArea;
            return area != null && (area.IsHideout || area.IsTown);
        }

        private static long ResolveCurrentAreaHash(GameController? gameController)
        {
            return AreaUiSnapshotReader.TryReadCurrentAreaHash(gameController, out long areaHash)
                ? areaHash
                : long.MinValue;
        }

    }
}