namespace ClickIt.Features.Area
{
    internal sealed class AreaBlockedSnapshotProvider
    {
        private const int BlockedUiRectanglesRefreshIntervalMs = 10_000;
        private const int BuffsAndDebuffsRectanglesRefreshIntervalMs = 1_000;
        private const int QuestTrackerRectanglesHoldLastGoodMs = 1_000;
        private const float SideCompanionHeightRatio = 0.625f;
        private const float SideCompanionWidthRatio = 0.555f;
        private const float SideTertiaryCompanionHeightRatio = 0.85f;
        private const float SideTertiaryCompanionWidthRatio = 0.79f;

        private readonly AreaBlockedState _blockedState = new();
        private readonly object _syncLock = new();
        private AreaBlockedSnapshot _currentSnapshot = new();

        internal AreaBlockedSnapshot CurrentSnapshot => Volatile.Read(ref _currentSnapshot);

        internal void ApplySnapshot(AreaBlockedSnapshot snapshot)
        {
            using (LockManager.AcquireStatic(_syncLock))
            {
                _blockedState.ApplySnapshot(snapshot);
                PublishCurrentSnapshot();
            }
        }

        internal void UpdateScreenAreas(GameController gameController)
        {
            using (LockManager.AcquireStatic(_syncLock))
            {
                long now = Environment.TickCount64;
                bool areaChanged = HasAreaChanged(gameController);
                bool snapshotChanged = false;

                if (BlockedAreaRefreshScheduler.ShouldRefresh(
                    now,
                    _blockedState.LastBlockedUiRectanglesRefreshTimestampMs,
                    BlockedUiRectanglesRefreshIntervalMs,
                    forceRefresh: areaChanged))
                {
                    RefreshMainBlockedAreas(gameController, now);
                    _blockedState.LastBlockedUiRectanglesRefreshTimestampMs = now;
                    snapshotChanged = true;
                }

                if (BlockedAreaRefreshScheduler.ShouldRefresh(
                    now,
                    _blockedState.LastBuffsAndDebuffsRectanglesRefreshTimestampMs,
                    BuffsAndDebuffsRectanglesRefreshIntervalMs))
                {
                    RefreshBuffAndDebuffAreas(gameController);
                    _blockedState.LastBuffsAndDebuffsRectanglesRefreshTimestampMs = now;
                    snapshotChanged = true;
                }

                if (snapshotChanged)
                    PublishCurrentSnapshot();
            }
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

        private void PublishCurrentSnapshot()
        {
            Volatile.Write(ref _currentSnapshot, _blockedState.CreateSnapshot());
        }

        private bool HasAreaChanged(GameController gameController)
        {
            long currentAreaHash = ResolveCurrentAreaHash(gameController);
            bool changed = AreaChangeRules.HasAreaHashChanged(currentAreaHash, _blockedState.LastKnownAreaHash);
            if (changed)
                _blockedState.LastKnownAreaHash = currentAreaHash;

            return changed;
        }

        private void RefreshMainBlockedAreas(GameController gameController, long now)
        {
            RectangleF winRect = gameController.Window.GetWindowRectangleTimeCache;
            _blockedState.FullScreenRectangle = new RectangleF(winRect.X, winRect.Y, winRect.Width, winRect.Height);

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

            _blockedState.HealthAndFlaskRectangle = leftCombined;
            _blockedState.ManaAndSkillsRectangle = rightCombined;

            (_blockedState.HealthSquareRectangle, _blockedState.FlaskRectangle, _blockedState.FlaskTertiaryRectangle) = SplitBottomAnchoredThreeRectanglesFromLeft(
                leftCombined,
                SideCompanionHeightRatio,
                SideTertiaryCompanionHeightRatio,
                SideTertiaryCompanionWidthRatio);
            (_blockedState.ManaSquareRectangle, _blockedState.SkillsRectangle, _blockedState.SkillsTertiaryRectangle) = SplitBottomAnchoredThreeRectanglesFromRight(
                rightCombined,
                SideCompanionHeightRatio,
                SideTertiaryCompanionHeightRatio,
                SideTertiaryCompanionWidthRatio);

            RefreshQuestTrackerAreas(gameController, now);

            _blockedState.ChatPanelBlockedRectangle = AreaBlockedRectangleResolver.ResolveChatPanelBlockedRectangle(gameController);
            _blockedState.MapPanelBlockedRectangle = ShouldUpdateMapPanelBlockedRectangle(IsInTownOrHideout(gameController))
                ? AreaBlockedRectangleResolver.ResolveMapPanelBlockedRectangle(gameController)
                : RectangleF.Empty;
            _blockedState.XpBarBlockedRectangle = AreaBlockedRectangleResolver.ResolveXpBarBlockedRectangle(gameController);
            _blockedState.MirageBlockedRectangle = AreaBlockedRectangleResolver.ResolveMirageBlockedRectangle(gameController);
            _blockedState.AltarBlockedRectangle = AreaBlockedRectangleResolver.ResolveAltarBlockedRectangle(gameController);
            _blockedState.RitualBlockedRectangle = AreaBlockedRectangleResolver.ResolveRitualBlockedRectangle(gameController);
            _blockedState.SentinelBlockedRectangle = AreaBlockedRectangleResolver.ResolveSentinelBlockedRectangle(gameController);
        }

        private void RefreshQuestTrackerAreas(GameController gameController, long now)
        {
            List<RectangleF> current = AreaBlockedRectangleCollectionResolver.ResolveQuestTrackerBlockedRectangles(gameController);
            ApplyQuestTrackerRectangles(_blockedState, current, now);
        }

        private void RefreshBuffAndDebuffAreas(GameController gameController)
        {
            List<RectangleF> rectangles = AreaBlockedRectangleCollectionResolver.ResolveBuffsAndDebuffsBlockedRectangles(gameController);
            ApplyBuffAndDebuffRectangles(_blockedState, rectangles);
        }

        internal static void ApplyQuestTrackerRectangles(AreaBlockedState blockedState, IReadOnlyList<RectangleF> current, long now)
        {
            if (current.Count > 0)
            {
                blockedState.QuestTrackerBlockedRectangles.Clear();
                blockedState.QuestTrackerBlockedRectangles.AddRange(current);
                blockedState.LastQuestTrackerRectanglesSuccessTimestampMs = now;
                return;
            }

            if (!BlockedAreaRefreshScheduler.ShouldRetainQuestTrackerRectanglesOnEmptyRead(
                blockedState.QuestTrackerBlockedRectangles.Count,
                now,
                blockedState.LastQuestTrackerRectanglesSuccessTimestampMs,
                QuestTrackerRectanglesHoldLastGoodMs))
            {
                blockedState.QuestTrackerBlockedRectangles.Clear();
            }
        }

        internal static void ApplyBuffAndDebuffRectangles(AreaBlockedState blockedState, IReadOnlyList<RectangleF> rectangles)
        {
            blockedState.BuffsAndDebuffsRectangles.Clear();
            blockedState.BuffsAndDebuffsRectangles.AddRange(rectangles);
            blockedState.BuffsAndDebuffsRectangle = blockedState.BuffsAndDebuffsRectangles.Count > 0
                ? blockedState.BuffsAndDebuffsRectangles[0]
                : RectangleF.Empty;
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