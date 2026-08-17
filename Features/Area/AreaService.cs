namespace ClickIt.Features.Area
{
    public class AreaService(ClickItSettings? settings = null)
    {
        private readonly ClickItSettings? _settings = settings;
        private readonly AreaBlockedSnapshotProvider _blockedSnapshotProvider = new();

        private readonly BlockedAreaEvaluatorPipeline _blockedAreaEvaluatorPipeline = new(
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
        public long? BlockedUiRefreshAgeMs => ResolveRefreshAgeMs(Environment.TickCount64, _blockedSnapshotProvider.CurrentSnapshot.LastBlockedUiRectanglesRefreshTimestampMs);
        public long? BuffsAndDebuffsRefreshAgeMs => ResolveRefreshAgeMs(Environment.TickCount64, _blockedSnapshotProvider.CurrentSnapshot.LastBuffsAndDebuffsRectanglesRefreshTimestampMs);
        public int ConfiguredBlockedUiRefreshIntervalMs => ResolveBlockedUiRefreshIntervalMs();

        internal void ApplyBlockedSnapshot(AreaBlockedSnapshot snapshot)
            => _blockedSnapshotProvider.ApplySnapshot(snapshot);

        public void UpdateScreenAreas(GameController gameController, bool forceBlockedUiRefresh = false)
            => _blockedSnapshotProvider.UpdateScreenAreas(gameController, ResolveBlockedUiRefreshIntervalMs(), forceBlockedUiRefresh);

        internal static bool IsInMap(GameController? gameController)
        {
            AreaInstance? area = gameController?.Area?.CurrentArea;
            return area != null && !area.IsHideout && !area.IsTown;
        }

        public bool PointIsInClickableArea(Vector2 point)
        {
            AreaBlockedSnapshot snapshot = _blockedSnapshotProvider.CurrentSnapshot;
            if (!BlockedAreaGeometryEngine.PointInUiRectangle(point, snapshot.FullScreenRectangle))
                return false;

            return !IsBlockedByAreaEvaluatorPipeline(snapshot, point);
        }

        private bool IsBlockedByAreaEvaluatorPipeline(AreaBlockedSnapshot snapshot, Vector2 point)
            => _blockedAreaEvaluatorPipeline.IsBlocked(snapshot, point);

        public bool PointIsInClickableArea(GameController? gameController, Vector2 point, bool forceBlockedUiRefresh = false)
        {
            // The blocked-rect snapshot is rebuilt on its own 250ms cadence; when it is still fresh the update is a pure lock + area-hash read that cannot change the result, so skip it. The click-point resolution probes the grid many times per resolution.
            if (gameController != null && (forceBlockedUiRefresh || IsBlockedUiSnapshotStale()))
                UpdateScreenAreas(gameController, forceBlockedUiRefresh);
            return PointIsInClickableArea(point);
        }

        private bool IsBlockedUiSnapshotStale()
        {
            AreaBlockedSnapshot snapshot = _blockedSnapshotProvider.CurrentSnapshot;
            long now = Environment.TickCount64;
            int interval = ResolveBlockedUiRefreshIntervalMs();
            return snapshot.LastBlockedUiRectanglesRefreshTimestampMs <= 0
                || now - snapshot.LastBlockedUiRectanglesRefreshTimestampMs >= interval
                || snapshot.LastBuffsAndDebuffsRectanglesRefreshTimestampMs <= 0
                || now - snapshot.LastBuffsAndDebuffsRectanglesRefreshTimestampMs >= interval;
        }

        internal static long? ResolveRefreshAgeMs(long now, long lastRefreshTimestampMs)
        {
            if (lastRefreshTimestampMs <= 0)
                return null;

            long elapsed = now - lastRefreshTimestampMs;
            return elapsed < 0 ? 0 : elapsed;
        }

        private int ResolveBlockedUiRefreshIntervalMs()
            => SystemMath.Max(50, _settings?.BlockedUiRefreshIntervalMs?.Value ?? AreaBlockedSnapshotProvider.DefaultBlockedUiRectanglesRefreshIntervalMs);
    }
}