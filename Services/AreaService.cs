using ClickIt.Utils;
using ExileCore;
using System.Collections.Generic;
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
        private const float SideCompanionHeightRatio = 0.6f;
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
        private RectangleF _gameUiPanelBlockedRectangle;
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
        public RectangleF GameUiPanelBlockedRectangle => _gameUiPanelBlockedRectangle;
        public IReadOnlyList<RectangleF> QuestTrackerBlockedRectangles => _questTrackerBlockedRectangles;

        public void UpdateScreenAreas(GameController gameController)
        {
            using (LockManager.AcquireStatic(_screenAreasLock))
            {
                long now = Environment.TickCount64;
                long currentAreaHash = ResolveCurrentAreaHash(gameController);
                bool areaChanged = HasAreaHashChanged(currentAreaHash, _lastKnownAreaHash);

                if (areaChanged)
                {
                    _lastKnownAreaHash = currentAreaHash;
                }

                if (ShouldRefreshBlockedUiRectangles(
                    now,
                    _lastBlockedUiRectanglesRefreshTimestampMs,
                    BlockedUiRectanglesRefreshIntervalMs,
                    forceRefresh: areaChanged))
                {
                    RectangleF winRect = gameController.Window.GetWindowRectangleTimeCache;

                    _fullScreenRectangle = new RectangleF(winRect.X, winRect.Y, winRect.Width, winRect.Height);
                    RectangleF leftCombinedRectangle = new RectangleF(
                        winRect.BottomLeft.X / 3f,
                        winRect.BottomLeft.Y / 5f * 3.92f,
                        winRect.BottomLeft.X + (winRect.BottomRight.X / 3.4f),
                        winRect.BottomLeft.Y);
                    RectangleF rightCombinedRectangle = new RectangleF(
                        winRect.BottomRight.X / 3f * 2.12f,
                        winRect.BottomLeft.Y / 5f * 3.92f,
                        winRect.BottomRight.X,
                        winRect.BottomRight.Y);

                    _healthAndFlaskRectangle = leftCombinedRectangle;
                    _manaAndSkillsRectangle = rightCombinedRectangle;

                    (_healthSquareRectangle, _flaskRectangle) =
                        SplitBottomAnchoredRectangleFromLeft(leftCombinedRectangle, SideCompanionHeightRatio);
                    (_manaSquareRectangle, _skillsRectangle) =
                        SplitBottomAnchoredRectangleFromRight(rightCombinedRectangle, SideCompanionHeightRatio);

                    List<RectangleF> resolvedQuestTrackerRects = ResolveQuestTrackerBlockedRectangles(gameController);
                    if (resolvedQuestTrackerRects.Count > 0)
                    {
                        _questTrackerBlockedRectangles.Clear();
                        _questTrackerBlockedRectangles.AddRange(resolvedQuestTrackerRects);
                        _lastQuestTrackerRectanglesSuccessTimestampMs = now;
                    }
                    else if (!ShouldRetainQuestTrackerRectanglesOnEmptyRead(
                        currentRectangleCount: _questTrackerBlockedRectangles.Count,
                        now,
                        _lastQuestTrackerRectanglesSuccessTimestampMs,
                        QuestTrackerRectanglesHoldLastGoodMs))
                    {
                        _questTrackerBlockedRectangles.Clear();
                    }

                    _chatPanelBlockedRectangle = ResolveChatPanelBlockedRectangle(gameController);
                    bool isInTownOrHideout = IsInTownOrHideout(gameController);
                    _mapPanelBlockedRectangle = ShouldUpdateMapPanelBlockedRectangle(isInTownOrHideout)
                        ? ResolveMapPanelBlockedRectangle(gameController)
                        : RectangleF.Empty;
                    _gameUiPanelBlockedRectangle = ResolveGameUiPanelBlockedRectangle(gameController);
                    _lastBlockedUiRectanglesRefreshTimestampMs = now;
                }

                if (ShouldRefreshBlockedUiRectangles(
                    now,
                    _lastBuffsAndDebuffsRectanglesRefreshTimestampMs,
                    BuffsAndDebuffsRectanglesRefreshIntervalMs))
                {
                    List<RectangleF> resolvedBuffRects = ResolveBuffsAndDebuffsBlockedRectangles(gameController);
                    _buffsAndDebuffsRectangles.Clear();
                    _buffsAndDebuffsRectangles.AddRange(resolvedBuffRects);
                    _buffsAndDebuffsRectangle = _buffsAndDebuffsRectangles.Count > 0
                        ? _buffsAndDebuffsRectangles[0]
                        : RectangleF.Empty;
                    _lastBuffsAndDebuffsRectanglesRefreshTimestampMs = now;
                }
            }
        }

        internal static bool ShouldUpdateMapPanelBlockedRectangle(bool isInTownOrHideout)
        {
            return !isInTownOrHideout;
        }

        internal static bool RectsDiffer(RectangleF a, RectangleF b, float eps)
        {
            return Math.Abs(a.Width - b.Width) > eps || Math.Abs(a.Height - b.Height) > eps ||
                   Math.Abs(a.X - b.X) > eps || Math.Abs(a.Y - b.Y) > eps;
        }

        internal static bool ShouldRefreshBlockedUiRectangles(
            long now,
            long lastRefreshTimestampMs,
            int refreshIntervalMs,
            bool forceRefresh = false)
        {
            if (forceRefresh)
                return true;
            if (refreshIntervalMs <= 0)
                return true;
            if (lastRefreshTimestampMs <= 0)
                return true;

            long elapsedMs = now - lastRefreshTimestampMs;
            return elapsedMs < 0 || elapsedMs >= refreshIntervalMs;
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

            long elapsedMs = now - lastSuccessTimestampMs;
            return elapsedMs >= 0 && elapsedMs <= holdLastGoodMs;
        }

        internal static (RectangleF primarySquare, RectangleF secondaryCompanion) SplitBottomAnchoredRectangleFromLeft(
            RectangleF source,
            float secondaryHeightRatio)
        {
            float left = source.X;
            float top = source.Y;
            float right = source.Width;
            float bottom = source.Height;

            float totalWidth = right - left;
            float totalHeight = bottom - top;
            if (totalWidth <= 0f || totalHeight <= 0f)
                return (RectangleF.Empty, RectangleF.Empty);

            float clampedRatio = Math.Clamp(secondaryHeightRatio, 0f, 1f);
            float squareSize = Math.Min(totalHeight, totalWidth);
            float companionMaxWidth = Math.Max(0f, totalWidth - squareSize);
            float companionWidth = companionMaxWidth * SideCompanionWidthRatio;
            float companionHeight = totalHeight * clampedRatio;

            float squareRight = left + squareSize;

            RectangleF primarySquare = new RectangleF(
                left,
                bottom - squareSize,
                squareRight,
                bottom);

            RectangleF secondaryCompanion = companionWidth <= 0f || companionHeight <= 0f
                ? RectangleF.Empty
                : new RectangleF(
                    squareRight,
                    bottom - companionHeight,
                    squareRight + companionWidth,
                    bottom);

            return (primarySquare, secondaryCompanion);
        }

        internal static (RectangleF primarySquare, RectangleF secondaryCompanion) SplitBottomAnchoredRectangleFromRight(
            RectangleF source,
            float secondaryHeightRatio)
        {
            float left = source.X;
            float top = source.Y;
            float right = source.Width;
            float bottom = source.Height;

            float totalWidth = right - left;
            float totalHeight = bottom - top;
            if (totalWidth <= 0f || totalHeight <= 0f)
                return (RectangleF.Empty, RectangleF.Empty);

            float clampedRatio = Math.Clamp(secondaryHeightRatio, 0f, 1f);
            float squareSize = Math.Min(totalHeight, totalWidth);
            float companionMaxWidth = Math.Max(0f, totalWidth - squareSize);
            float companionWidth = companionMaxWidth * SideCompanionWidthRatio;
            float companionHeight = totalHeight * clampedRatio;

            RectangleF primarySquare = new RectangleF(
                right - squareSize,
                bottom - squareSize,
                right,
                bottom);

            // Explicit anchor: secondary bottom-right attaches to primary bottom-left.
            float companionRight = primarySquare.X;
            float companionLeft = companionRight - companionWidth;

            RectangleF secondaryCompanion = companionWidth <= 0f || companionHeight <= 0f
                ? RectangleF.Empty
                : new RectangleF(
                    companionLeft,
                    bottom - companionHeight,
                    companionRight,
                    bottom);

            return (primarySquare, secondaryCompanion);
        }

        public bool PointIsInClickableArea(Vector2 point)
        {
            using (LockManager.AcquireStatic(_screenAreasLock))
            {
                return point.PointInRectangle(_fullScreenRectangle) &&
                      !point.PointInRectangle(_healthSquareRectangle) &&
                      !point.PointInRectangle(_flaskRectangle) &&
                      !point.PointInRectangle(_skillsRectangle) &&
                      !point.PointInRectangle(_manaSquareRectangle) &&
                      !point.PointInRectangle(_buffsAndDebuffsRectangle) &&
                        !PointInAnyRectangle(point, _buffsAndDebuffsRectangles) &&
                      !PointInAnyRectangle(point, _questTrackerBlockedRectangles) &&
                      !point.PointInRectangle(_chatPanelBlockedRectangle) &&
                      !point.PointInRectangle(_mapPanelBlockedRectangle) &&
                      !point.PointInRectangle(_gameUiPanelBlockedRectangle);
            }
        }

        public bool PointIsInClickableArea(GameController? gameController, Vector2 point)
        {
            if (gameController != null)
            {
                UpdateScreenAreas(gameController);
            }

            return PointIsInClickableArea(point);
        }

        private static bool PointInAnyRectangle(Vector2 point, List<RectangleF> rectangles)
        {
            if (rectangles.Count == 0)
                return false;

            for (int i = 0; i < rectangles.Count; i++)
            {
                if (point.PointInRectangle(rectangles[i]))
                    return true;
            }

            return false;
        }

        private static bool IsInTownOrHideout(GameController? gameController)
        {
            if (gameController == null)
                return false;

            var area = gameController.Area?.CurrentArea;
            if (area == null)
                return false;

            return area.IsHideout || area.IsTown;
        }

        private static List<RectangleF> ResolveBuffsAndDebuffsBlockedRectangles(GameController gameController)
        {
            var blockedRectangles = new List<RectangleF>(2);
            object? root = TryGetIngameUiProperty(gameController, "Root");
            if (!TryGetChildNode(root, 1, out object? child1) || child1 == null)
                return blockedRectangles;

            if (TryGetChildNode(child1, 23, out object? child23)
                && child23 != null
                && TryGetClientRect(child23, out RectangleF rect23)
                && rect23.Width > 1f
                && rect23.Height > 1f)
            {
                blockedRectangles.Add(rect23);
            }

            if (TryGetChildNode(child1, 24, out object? child24)
                && child24 != null
                && TryGetClientRect(child24, out RectangleF rect24)
                && rect24.Width > 1f
                && rect24.Height > 1f)
            {
                blockedRectangles.Add(rect24);
            }

            return blockedRectangles;
        }

        private static long ResolveCurrentAreaHash(GameController? gameController)
        {
            if (gameController == null)
                return long.MinValue;

            try
            {
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
            var blockedRectangles = new List<RectangleF>();
            object? root = TryGetIngameUiProperty(gameController, "QuestTracker");
            if (!TryGetChildNode(root, 0, out object? child0) || child0 == null)
                return blockedRectangles;
            if (!TryGetChildNode(child0, 0, out object? child00) || child00 == null)
                return blockedRectangles;

            List<object?> children = ResolveChildNodes(child00);
            if (children.Count == 0)
                return blockedRectangles;

            for (int i = 0; i < children.Count; i++)
            {
                object? row = children[i];
                if (!TryGetChildNode(row, 1, out object? clickableContainer) || clickableContainer == null)
                    continue;

                if (!TryGetClientRect(clickableContainer, out RectangleF clickableRect))
                    continue;

                if (clickableRect.Width <= 1f || clickableRect.Height <= 1f)
                    continue;

                blockedRectangles.Add(clickableRect);
            }

            return blockedRectangles;
        }

        private static RectangleF ResolveChatPanelBlockedRectangle(GameController gameController)
        {
            object? root = TryGetIngameUiProperty(gameController, "ChatPanel");
            if (!TryGetChildNode(root, 1, out object? child1) || child1 == null)
                return RectangleF.Empty;
            if (!TryGetChildNode(child1, 2, out object? child12) || child12 == null)
                return RectangleF.Empty;
            if (!TryGetChildNode(child12, 2, out object? target) || target == null)
                return RectangleF.Empty;

            return TryGetClientRect(target, out RectangleF rect) && rect.Width > 1f && rect.Height > 1f
                ? rect
                : RectangleF.Empty;
        }

        private static RectangleF ResolveMapPanelBlockedRectangle(GameController gameController)
        {
            object? root = TryGetIngameUiProperty(gameController, "Map");
            if (!TryGetChildNode(root, 2, out object? child2) || child2 == null)
                return RectangleF.Empty;
            if (!TryGetChildNode(child2, 1, out object? target) || target == null)
                return RectangleF.Empty;

            return TryGetClientRect(target, out RectangleF rect) && rect.Width > 1f && rect.Height > 1f
                ? rect
                : RectangleF.Empty;
        }

        private static RectangleF ResolveGameUiPanelBlockedRectangle(GameController gameController)
        {
            object? ingameUi = gameController?.IngameState?.IngameUi;
            object? root = TryGetIngameUiProperty(gameController, "GameUI");
            if (root == null)
            {
                _ = TryGetChildNode(ingameUi, 0, out root);
            }

            if (!TryGetChildNode(root, 0, out object? target) || target == null)
                return RectangleF.Empty;

            return TryGetClientRect(target, out RectangleF rect) && rect.Width > 1f && rect.Height > 1f
                ? rect
                : RectangleF.Empty;
        }

        private static List<object?> ResolveChildNodes(object source)
        {
            var children = new List<object?>();
            if (source == null)
                return children;

            // Fallback for APIs exposing only index-based child access.
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
            if (source == null)
                return false;

            if (source is Element element)
            {
                rect = element.GetClientRect();
                return true;
            }

            return false;
        }

        private static bool TryGetChildNode(object? source, int index, out object? child)
        {
            child = null;
            if (source == null || index < 0)
                return false;

            if (source is Element element)
            {
                child = element.GetChildAtIndex(index);
                return child != null;
            }

            return false;
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
