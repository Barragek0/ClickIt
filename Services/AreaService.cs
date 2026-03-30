using ClickIt.Utils;
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

        private readonly record struct BlockedAreaEvaluationContext(AreaService Service, Vector2 Point);

        private interface IBlockedAreaEvaluator
        {
            bool IsBlocked(in BlockedAreaEvaluationContext context);
        }

        private sealed class BlockedAreaEvaluator(Func<AreaService, Vector2, bool> isBlocked) : IBlockedAreaEvaluator
        {
            private readonly Func<AreaService, Vector2, bool> _isBlocked = isBlocked;

            public bool IsBlocked(in BlockedAreaEvaluationContext context)
                => _isBlocked(context.Service, context.Point);
        }

        private static readonly IBlockedAreaEvaluator[] BlockedAreaEvaluators =
        [
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._healthSquareRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._flaskRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._flaskTertiaryRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._skillsRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._skillsTertiaryRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._manaSquareRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._buffsAndDebuffsRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInAnyBlockedUiRectangle(point, service._buffsAndDebuffsRectangles)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInAnyBlockedUiRectangle(point, service._questTrackerBlockedRectangles)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._chatPanelBlockedRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._mapPanelBlockedRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._xpBarBlockedRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._mirageBlockedRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._altarBlockedRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._ritualBlockedRectangle)),
            new BlockedAreaEvaluator(static (service, point) => service.PointInBlockedUiRectangle(point, service._sentinelBlockedRectangle))
        ];

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

        public void UpdateScreenAreas(GameController gameController)
        {
            using (LockManager.AcquireStatic(_screenAreasLock))
            {
                long now = Environment.TickCount64;
                bool areaChanged = HasAreaChanged(gameController);

                if (ShouldRefreshBlockedUiRectangles(
                    now,
                    _lastBlockedUiRectanglesRefreshTimestampMs,
                    BlockedUiRectanglesRefreshIntervalMs,
                    forceRefresh: areaChanged))
                {
                    RefreshMainBlockedAreas(gameController, now);
                    _lastBlockedUiRectanglesRefreshTimestampMs = now;
                }

                if (ShouldRefreshBlockedUiRectangles(
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
            bool changed = HasAreaHashChanged(currentAreaHash, _lastKnownAreaHash);
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

            _chatPanelBlockedRectangle = ResolveChatPanelBlockedRectangle(gameController);
            _mapPanelBlockedRectangle = ShouldUpdateMapPanelBlockedRectangle(IsInTownOrHideout(gameController))
                ? ResolveMapPanelBlockedRectangle(gameController)
                : RectangleF.Empty;
            _xpBarBlockedRectangle = ResolveXpBarBlockedRectangle(gameController);
            _mirageBlockedRectangle = ResolveMirageBlockedRectangle(gameController);
            _altarBlockedRectangle = ResolveAltarBlockedRectangle(gameController);
            _ritualBlockedRectangle = ResolveRitualBlockedRectangle(gameController);
            _sentinelBlockedRectangle = ResolveSentinelBlockedRectangle(gameController);
        }

        private void RefreshQuestTrackerAreas(GameController gameController, long now)
        {
            List<RectangleF> current = ResolveQuestTrackerBlockedRectangles(gameController);
            if (current.Count > 0)
            {
                _questTrackerBlockedRectangles.Clear();
                _questTrackerBlockedRectangles.AddRange(current);
                _lastQuestTrackerRectanglesSuccessTimestampMs = now;
                return;
            }

            if (!ShouldRetainQuestTrackerRectanglesOnEmptyRead(
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
            List<RectangleF> rectangles = ResolveBuffsAndDebuffsBlockedRectangles(gameController);
            _buffsAndDebuffsRectangles.Clear();
            _buffsAndDebuffsRectangles.AddRange(rectangles);
            _buffsAndDebuffsRectangle = _buffsAndDebuffsRectangles.Count > 0
                ? _buffsAndDebuffsRectangles[0]
                : RectangleF.Empty;
        }

        internal static bool ShouldUpdateMapPanelBlockedRectangle(bool isInTownOrHideout) => !isInTownOrHideout;

        internal static bool RectsDiffer(RectangleF a, RectangleF b, float eps)
        {
            return Math.Abs(a.Width - b.Width) > eps
                || Math.Abs(a.Height - b.Height) > eps
                || Math.Abs(a.X - b.X) > eps
                || Math.Abs(a.Y - b.Y) > eps;
        }

        internal static bool ShouldRefreshBlockedUiRectangles(
            long now,
            long lastRefreshTimestampMs,
            int refreshIntervalMs,
            bool forceRefresh = false)
        {
            if (forceRefresh || refreshIntervalMs <= 0 || lastRefreshTimestampMs <= 0)
                return true;

            long elapsed = now - lastRefreshTimestampMs;
            return elapsed < 0 || elapsed >= refreshIntervalMs;
        }

        internal static bool HasAreaHashChanged(long currentAreaHash, long lastKnownAreaHash)
        {
            if (currentAreaHash == long.MinValue)
                return false;
            if (lastKnownAreaHash == long.MinValue)
                return true;
            return currentAreaHash != lastKnownAreaHash;
        }

        internal static bool ShouldRetainQuestTrackerRectanglesOnEmptyRead(
            int currentRectangleCount,
            long now,
            long lastSuccessTimestampMs,
            int holdLastGoodMs)
        {
            if (currentRectangleCount <= 0 || lastSuccessTimestampMs <= 0 || holdLastGoodMs <= 0)
                return false;

            long elapsed = now - lastSuccessTimestampMs;
            return elapsed >= 0 && elapsed <= holdLastGoodMs;
        }

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
        {
            float left = source.X;
            float top = source.Y;
            float right = source.Width;
            float bottom = source.Height;

            float sourceWidth = right - left;
            float sourceHeight = bottom - top;
            if (sourceWidth <= 0f || sourceHeight <= 0f)
                return RectangleF.Empty;

            float linkedWidth = sourceWidth * Math.Max(0f, widthRatio);
            float linkedHeight = sourceHeight * Math.Clamp(heightRatio, 0f, 1f);
            if (linkedWidth <= 0f || linkedHeight <= 0f)
                return RectangleF.Empty;

            return anchorLeft
                ? new RectangleF(right, bottom - linkedHeight, right + linkedWidth, bottom)
                : new RectangleF(left - linkedWidth, bottom - linkedHeight, left, bottom);
        }

        private static (RectangleF primarySquare, RectangleF secondaryCompanion) SplitBottomAnchoredRectangle(
            RectangleF source,
            float secondaryHeightRatio,
            bool anchorLeft)
        {
            float left = source.X;
            float top = source.Y;
            float right = source.Width;
            float bottom = source.Height;

            float totalWidth = right - left;
            float totalHeight = bottom - top;
            if (totalWidth <= 0f || totalHeight <= 0f)
                return (RectangleF.Empty, RectangleF.Empty);

            float squareSize = Math.Min(totalHeight, totalWidth);
            float companionWidth = Math.Max(0f, totalWidth - squareSize) * SideCompanionWidthRatio;
            float companionHeight = totalHeight * Math.Clamp(secondaryHeightRatio, 0f, 1f);

            RectangleF primarySquare = anchorLeft
                ? new RectangleF(left, bottom - squareSize, left + squareSize, bottom)
                : new RectangleF(right - squareSize, bottom - squareSize, right, bottom);

            if (companionWidth <= 0f || companionHeight <= 0f)
                return (primarySquare, RectangleF.Empty);

            RectangleF secondary = anchorLeft
                ? new RectangleF(primarySquare.Width, bottom - companionHeight, primarySquare.Width + companionWidth, bottom)
                : new RectangleF(primarySquare.X - companionWidth, bottom - companionHeight, primarySquare.X, bottom);

            return (primarySquare, secondary);
        }

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
        {
            BlockedAreaEvaluationContext context = new(this, point);
            for (int i = 0; i < BlockedAreaEvaluators.Length; i++)
            {
                if (BlockedAreaEvaluators[i].IsBlocked(context))
                    return true;
            }

            return false;
        }

        public bool PointIsInClickableArea(GameController? gameController, Vector2 point)
        {
            if (gameController != null)
                UpdateScreenAreas(gameController);
            return PointIsInClickableArea(point);
        }

        private bool PointInAnyBlockedUiRectangle(Vector2 point, List<RectangleF> rectangles)
        {
            for (int i = 0; i < rectangles.Count; i++)
            {
                if (PointInBlockedUiRectangle(point, rectangles[i]))
                    return true;
            }

            return false;
        }

        private bool PointInBlockedUiRectangle(Vector2 point, RectangleF rect)
        {
            if (PointInUiRectangleAnyRepresentation(point, rect))
                return true;

            Vector2 windowTopLeft = new(_fullScreenRectangle.X, _fullScreenRectangle.Y);
            if (windowTopLeft.X == 0f && windowTopLeft.Y == 0f)
                return false;

            Vector2 clientPoint = point - windowTopLeft;
            return PointInUiRectangleAnyRepresentation(clientPoint, rect);
        }

        private static bool PointInUiRectangleAnyRepresentation(Vector2 point, RectangleF rect)
        {
            return PointInUiRectangle(point, rect) || point.PointInRectangle(rect);
        }

        private static bool PointInUiRectangle(Vector2 point, RectangleF rect)
        {
            if (rect.Width <= 0f || rect.Height <= 0f)
                return false;

            float right = rect.X + rect.Width;
            float bottom = rect.Y + rect.Height;
            return point.X >= rect.X
                && point.X <= right
                && point.Y >= rect.Y
                && point.Y <= bottom;
        }

        private static bool IsInTownOrHideout(GameController? gameController)
        {
            var area = gameController?.Area?.CurrentArea;
            return area != null && (area.IsHideout || area.IsTown);
        }

        private static List<RectangleF> ResolveBuffsAndDebuffsBlockedRectangles(GameController gameController)
        {
            var blocked = new List<RectangleF>(2);
            object? root = TryGetIngameUiProperty(gameController, "Root");
            if (!TryGetChildNode(root, 1, out object? child1) || child1 == null)
                return blocked;

            TryAddValidRectFromChild(child1, 23, blocked);
            TryAddValidRectFromChild(child1, 24, blocked);
            return blocked;
        }

        private static void TryAddValidRectFromChild(object source, int childIndex, List<RectangleF> output)
        {
            if (!TryGetChildNode(source, childIndex, out object? node) || node == null)
                return;

            if (TryGetClientRect(node, out RectangleF rect) && rect.Width > 1f && rect.Height > 1f)
                output.Add(rect);
        }

        private static long ResolveCurrentAreaHash(GameController? gameController)
        {
            if (gameController == null)
                return long.MinValue;

            return TryReadCurrentAreaHash(gameController.Game, out long areaHash)
                ? areaHash
                : long.MinValue;
        }

        private static List<RectangleF> ResolveQuestTrackerBlockedRectangles(GameController gameController)
        {
            var blocked = new List<RectangleF>();
            object? root = TryGetIngameUiProperty(gameController, "QuestTracker");
            if (!TryGetChildNode(root, 0, out object? child0) || child0 == null)
                return blocked;
            if (!TryGetChildNode(child0, 0, out object? rowsRoot) || rowsRoot == null)
                return blocked;

            List<object?> children = ResolveChildNodes(rowsRoot);
            for (int i = 0; i < children.Count; i++)
            {
                if (!TryGetChildNode(children[i], 1, out object? clickableContainer) || clickableContainer == null)
                    continue;

                if (!TryGetClientRect(clickableContainer, out RectangleF clickableRect))
                    continue;

                if (clickableRect.Width > 1f && clickableRect.Height > 1f)
                    blocked.Add(clickableRect);
            }

            return blocked;
        }

        private static RectangleF ResolveChatPanelBlockedRectangle(GameController gameController)
            => ResolveRectangleFromNodePath(TryGetIngameUiProperty(gameController, "ChatPanel"), 1, 2, 2);

        private static RectangleF ResolveMapPanelBlockedRectangle(GameController gameController)
            => ResolveRectangleFromNodePath(TryGetIngameUiProperty(gameController, "Map"), 2, 1);

        private static RectangleF ResolveXpBarBlockedRectangle(GameController gameController)
         => ResolveRectangleFromNodePath(TryGetIngameUiProperty(gameController, "GameUI"), 0);

        private static RectangleF ResolveMirageBlockedRectangle(GameController gameController)
         => ResolveVisibleRectangleFromNodePath(TryGetIngameUiProperty(gameController, "GameUI"), 7, 17);

        private static RectangleF ResolveAltarBlockedRectangle(GameController gameController)
         => ResolveVisibleRectangleFromNodePath(TryGetIngameUiProperty(gameController, "GameUI"), 7, 16);

        private static RectangleF ResolveRitualBlockedRectangle(GameController gameController)
         => ResolveVisibleRectangleFromNodePath(TryGetIngameUiProperty(gameController, "GameUI"), 7, 18, 0);

        private static RectangleF ResolveSentinelBlockedRectangle(GameController gameController)
         => ResolveRectangleFromNodePath(TryGetIngameUiProperty(gameController, "GameUI"), 7, 18, 2, 0);

        private static RectangleF ResolveVisibleRectangleFromNodePath(object? root, params int[] childPath)
        {
            return TryResolveRectangleFromNodePath(root, requireVisibleElement: true, childPath: childPath, out RectangleF rect)
                ? rect
                : RectangleF.Empty;
        }

        internal static bool ShouldUseVisibleUiBlockedRectangle(bool elementIsValid, bool elementIsVisible)
            => elementIsValid && elementIsVisible;

        private static RectangleF ResolveRectangleFromNodePath(object? root, params int[] childPath)
        {
            return TryResolveRectangleFromNodePath(root, requireVisibleElement: false, childPath: childPath, out RectangleF rect)
                ? rect
                : RectangleF.Empty;
        }

        private static bool TryResolveRectangleFromNodePath(object? root, bool requireVisibleElement, int[]? childPath, out RectangleF rect)
        {
            rect = RectangleF.Empty;
            if (root == null || childPath == null || childPath.Length == 0)
                return false;

            object? current = root;
            for (int i = 0; i < childPath.Length; i++)
            {
                if (!TryGetChildNode(current, childPath[i], out current) || current == null)
                    return false;
            }

            if (requireVisibleElement)
            {
                if (current is not Element element)
                    return false;

                if (!ShouldUseVisibleUiBlockedRectangle(element.IsValid, element.IsVisible))
                    return false;
            }

            if (!TryGetClientRect(current, out RectangleF resolvedRect))
                return false;

            if (resolvedRect.Width <= 1f || resolvedRect.Height <= 1f)
                return false;

            rect = resolvedRect;

            return true;
        }

        private static List<object?> ResolveChildNodes(object source)
        {
            var children = new List<object?>();
            for (int i = 0; i < 256; i++)
            {
                if (!TryGetChildNode(source, i, out object? child) || child == null)
                    break;

                children.Add(child);
            }

            return children;
        }

        private static bool TryGetClientRect(object source, out RectangleF rect)
        {
            rect = RectangleF.Empty;
            if (source is not Element element)
                return false;

            rect = element.GetClientRect();
            return true;
        }

        private static bool TryGetChildNode(object? source, int index, out object? child)
        {
            child = null;
            if (source is not Element element || index < 0)
                return false;

            child = element.GetChildAtIndex(index);
            return child != null;
        }

        private static object? TryGetIngameUiProperty(GameController? gameController, string propertyName)
        {
            if (gameController?.IngameState?.IngameUi == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            return propertyName switch
            {
                "QuestTracker" => TryGetIngameUiMember(gameController.IngameState.IngameUi, static ui => ui.QuestTracker),
                "ChatPanel" => TryGetIngameUiMember(gameController.IngameState.IngameUi, static ui => ui.ChatPanel),
                "Map" => TryGetIngameUiMember(gameController.IngameState.IngameUi, static ui => ui.Map),
                "GameUI" => TryGetIngameUiMember(gameController.IngameState.IngameUi, static ui => ui.GameUI),
                "Root" => TryGetIngameUiMember(gameController.IngameState.IngameUi, static ui => ui.Root),
                _ => null,
            };
        }

        private static bool TryReadCurrentAreaHash(object? game, out long areaHash)
        {
            areaHash = long.MinValue;
            if (!DynamicAccess.TryGetDynamicValue(game, static source => source.CurrentAreaHash, out object? rawAreaHash)
                || rawAreaHash == null)
            {
                return false;
            }

            try
            {
                areaHash = Convert.ToInt64(rawAreaHash);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object? TryGetIngameUiMember(object? ingameUi, Func<dynamic, object?> accessor)
        {
            return DynamicAccess.TryGetDynamicValue(ingameUi, accessor, out object? value)
                ? value
                : null;
        }
    }
}