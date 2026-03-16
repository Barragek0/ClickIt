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
        private const int BuffsAndDebuffsRectanglesRefreshIntervalMs = 500;
        private const int QuestTrackerRectanglesHoldLastGoodMs = 200;
        private const float SideCompanionHeightRatio = 0.65f;
        private const float SideCompanionWidthRatio = 1f;

        private RectangleF _fullScreenRectangle;
        private RectangleF _healthAndFlaskRectangle;
        private RectangleF _manaAndSkillsRectangle;
        private RectangleF _healthSquareRectangle;
        private RectangleF _flaskRectangle;
        private RectangleF _skillsRectangle;
        private RectangleF _manaSquareRectangle;
        private RectangleF _buffsAndDebuffsRectangle;
        private readonly List<RectangleF> _buffsAndDebuffsRectangles = [];
        private RectangleF _chatPanelBlockedRectangle;
        private RectangleF _mapPanelBlockedRectangle;
        private RectangleF _xpBarBlockedRectangle;
        private RectangleF _altarBlockedRectangle;
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
        public RectangleF SkillsRectangle => _skillsRectangle;
        public RectangleF ManaSquareRectangle => _manaSquareRectangle;
        public RectangleF BuffsAndDebuffsRectangle => _buffsAndDebuffsRectangle;
        public IReadOnlyList<RectangleF> BuffsAndDebuffsRectangles => _buffsAndDebuffsRectangles;
        public RectangleF ChatPanelBlockedRectangle => _chatPanelBlockedRectangle;
        public RectangleF MapPanelBlockedRectangle => _mapPanelBlockedRectangle;
        public RectangleF XpBarBlockedRectangle => _xpBarBlockedRectangle;
        public RectangleF AltarBlockedRectangle => _altarBlockedRectangle;
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

            (_healthSquareRectangle, _flaskRectangle) = SplitBottomAnchoredRectangleFromLeft(leftCombined, SideCompanionHeightRatio);
            (_manaSquareRectangle, _skillsRectangle) = SplitBottomAnchoredRectangleFromRight(rightCombined, SideCompanionHeightRatio);

            RefreshQuestTrackerAreas(gameController, now);

            _chatPanelBlockedRectangle = ResolveChatPanelBlockedRectangle(gameController);
            _mapPanelBlockedRectangle = ShouldUpdateMapPanelBlockedRectangle(IsInTownOrHideout(gameController))
                ? ResolveMapPanelBlockedRectangle(gameController)
                : RectangleF.Empty;
            _xpBarBlockedRectangle = ResolveXpBarBlockedRectangle(gameController);
            _altarBlockedRectangle = ResolveAltarBlockedRectangle(gameController);
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

        internal static (RectangleF primarySquare, RectangleF secondaryCompanion) SplitBottomAnchoredRectangleFromRight(
            RectangleF source,
            float secondaryHeightRatio)
        {
            return SplitBottomAnchoredRectangle(source, secondaryHeightRatio, anchorLeft: false);
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
                return point.PointInRectangle(_fullScreenRectangle)
                    && !point.PointInRectangle(_healthSquareRectangle)
                    && !point.PointInRectangle(_flaskRectangle)
                    && !point.PointInRectangle(_skillsRectangle)
                    && !point.PointInRectangle(_manaSquareRectangle)
                    && !point.PointInRectangle(_buffsAndDebuffsRectangle)
                    && !PointInAnyRectangle(point, _buffsAndDebuffsRectangles)
                    && !PointInAnyRectangle(point, _questTrackerBlockedRectangles)
                    && !point.PointInRectangle(_chatPanelBlockedRectangle)
                    && !point.PointInRectangle(_mapPanelBlockedRectangle)
                    && !point.PointInRectangle(_xpBarBlockedRectangle)
                    && !point.PointInRectangle(_altarBlockedRectangle);
            }
        }

        public bool PointIsInClickableArea(GameController? gameController, Vector2 point)
        {
            if (gameController != null)
                UpdateScreenAreas(gameController);
            return PointIsInClickableArea(point);
        }

        private static bool PointInAnyRectangle(Vector2 point, List<RectangleF> rectangles)
        {
            for (int i = 0; i < rectangles.Count; i++)
            {
                if (point.PointInRectangle(rectangles[i]))
                    return true;
            }

            return false;
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
            try
            {
                if (gameController == null)
                    return long.MinValue;

                dynamic game = gameController.Game;
                return Convert.ToInt64(game.CurrentAreaHash);
            }
            catch
            {
                return long.MinValue;
            }
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

        private static RectangleF ResolveAltarBlockedRectangle(GameController gameController)
         => ResolveRectangleFromNodePath(TryGetIngameUiProperty(gameController, "GameUI"), 7, 17);

        private static RectangleF ResolveRectangleFromNodePath(object? root, params int[] childPath)
        {
            if (root == null || childPath == null || childPath.Length == 0)
                return RectangleF.Empty;

            object? current = root;
            for (int i = 0; i < childPath.Length; i++)
            {
                if (!TryGetChildNode(current, childPath[i], out current) || current == null)
                    return RectangleF.Empty;
            }

            if (!TryGetClientRect(current, out RectangleF rect))
                return RectangleF.Empty;

            return rect.Width > 1f && rect.Height > 1f ? rect : RectangleF.Empty;
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

            try
            {
                dynamic ui = gameController.IngameState.IngameUi;
                return propertyName switch
                {
                    "QuestTracker" => ui.QuestTracker,
                    "ChatPanel" => ui.ChatPanel,
                    "Map" => ui.Map,
                    "GameUI" => ui.GameUI,
                    "Root" => ui.Root,
                    _ => null,
                };
            }
            catch
            {
                return null;
            }
        }
    }
}