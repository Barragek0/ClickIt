namespace ClickIt.Features.Area
{
    public class AreaService(ClickItSettings? settings = null)
    {
        private readonly ClickItSettings? _settings = settings;
        private readonly AreaBlockedSnapshotProvider _blockedSnapshotProvider = new();

        private static readonly Func<AreaBlockedSnapshot, RectangleF>[] s_blockedRectangles =
        [
            snapshot => snapshot.HealthSquareRectangle,
            snapshot => snapshot.FlaskRectangle,
            snapshot => snapshot.FlaskTertiaryRectangle,
            snapshot => snapshot.SkillsRectangle,
            snapshot => snapshot.SkillsTertiaryRectangle,
            snapshot => snapshot.ManaSquareRectangle,
            snapshot => snapshot.BuffsAndDebuffsRectangle,
            snapshot => snapshot.ChatPanelBlockedRectangle,
            snapshot => snapshot.MapPanelBlockedRectangle,
            snapshot => snapshot.XpBarBlockedRectangle,
        ];

        private static readonly Func<AreaBlockedSnapshot, IReadOnlyList<RectangleF>>[] s_blockedCollections =
        [
            snapshot => snapshot.BuffsAndDebuffsRectangles,
            snapshot => snapshot.QuestTrackerBlockedRectangles,
            snapshot => snapshot.ButtonRectangles,
        ];

        public RectangleF FullScreenRectangle => _blockedSnapshotProvider.CurrentSnapshot.FullScreenRectangle;
        public RectangleF HealthAndFlaskRectangle => _blockedSnapshotProvider.CurrentSnapshot.HealthAndFlaskRectangle;
        public RectangleF ManaAndSkillsRectangle => _blockedSnapshotProvider.CurrentSnapshot.ManaAndSkillsRectangle;
        public RectangleF HealthSquareRectangle => _blockedSnapshotProvider.CurrentSnapshot.HealthSquareRectangle;
        public RectangleF FlaskRectangle => _blockedSnapshotProvider.CurrentSnapshot.FlaskRectangle;
        public RectangleF FlaskTertiaryRectangle => _blockedSnapshotProvider.CurrentSnapshot.FlaskTertiaryRectangle;
        public RectangleF SkillsRectangle => _blockedSnapshotProvider.CurrentSnapshot.SkillsRectangle;
        public RectangleF SkillsTertiaryRectangle => _blockedSnapshotProvider.CurrentSnapshot.SkillsTertiaryRectangle;
        public RectangleF ManaSquareRectangle => _blockedSnapshotProvider.CurrentSnapshot.ManaSquareRectangle;
        public RectangleF BuffsRectangle => _blockedSnapshotProvider.CurrentSnapshot.BuffsRectangle;
        public RectangleF DebuffsRectangle => _blockedSnapshotProvider.CurrentSnapshot.DebuffsRectangle;
        public RectangleF BuffsAndDebuffsRectangle => _blockedSnapshotProvider.CurrentSnapshot.BuffsAndDebuffsRectangle;
        public IReadOnlyList<RectangleF> BuffsAndDebuffsRectangles => _blockedSnapshotProvider.CurrentSnapshot.BuffsAndDebuffsRectangles;
        public RectangleF ChatPanelBlockedRectangle => _blockedSnapshotProvider.CurrentSnapshot.ChatPanelBlockedRectangle;
        public RectangleF MapPanelBlockedRectangle => _blockedSnapshotProvider.CurrentSnapshot.MapPanelBlockedRectangle;
        public RectangleF XpBarBlockedRectangle => _blockedSnapshotProvider.CurrentSnapshot.XpBarBlockedRectangle;
        public IReadOnlyList<RectangleF> QuestTrackerBlockedRectangles => _blockedSnapshotProvider.CurrentSnapshot.QuestTrackerBlockedRectangles;
        public IReadOnlyList<RectangleF> ButtonRectangles => _blockedSnapshotProvider.CurrentSnapshot.ButtonRectangles;
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
        {
            foreach (var select in s_blockedRectangles)
                if (BlockedAreaHitTestEngine.PointInBlockedUiRectangle(point, select(snapshot), snapshot.FullScreenRectangle))
                    return true;
            foreach (var select in s_blockedCollections)
                if (BlockedAreaHitTestEngine.PointInAnyBlockedUiRectangle(point, select(snapshot), snapshot.FullScreenRectangle))
                    return true;
            return false;
        }

        internal bool IsClientRectangleClickable(GameController? controller, RectangleF region)
        {
            if (controller != null && IsBlockedUiSnapshotStale())
                UpdateScreenAreas(controller);

            AreaBlockedSnapshot snapshot = _blockedSnapshotProvider.CurrentSnapshot;
            RectangleF window = snapshot.FullScreenRectangle;
            if (!(region.Width > 0 && region.Height > 0 && region.Left >= 0 && region.Top >= 0
                && region.Right <= window.Width && region.Bottom <= window.Height))
                return false;

            foreach (var select in s_blockedRectangles)
                if (Overlaps(region, select(snapshot)))
                    return false;
            foreach (var select in s_blockedCollections)
                foreach (RectangleF rectangle in select(snapshot))
                    if (Overlaps(region, rectangle))
                        return false;
            return true;
        }

        private static bool Overlaps(RectangleF first, RectangleF second)
            => second.Width > 0 && second.Height > 0
                && first.Left <= second.Right && first.Right >= second.Left
                && first.Top <= second.Bottom && first.Bottom >= second.Top;

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
