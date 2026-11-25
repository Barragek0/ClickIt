namespace ClickIt.Features.Click.Application;

internal sealed class AutoAuguryFatesClickService(
    ClickItSettings settings,
    PluginManager? pluginManager,
    GameController gameController,
    Func<Vector2, string, bool> isClickable,
    Func<Vector2, Func<Element?, bool>, bool, bool, bool> performClick,
    Action recordClickInterval,
    Action<string> debugLog)
{
    private const int InterfaceVersion = 2;
    private const int AbsenceGraceMs = 100;
    private const int OpeningSettleMs = 100;
    private readonly ClickItSettings _settings = settings;
    private readonly PluginManager? _pluginManager = pluginManager;
    private readonly GameController _gameController = gameController;
    private readonly Func<Vector2, string, bool> _isClickable = isClickable;
    private readonly Func<Vector2, Func<Element?, bool>, bool, bool, bool> _performClick = performClick;
    private readonly Action _recordClickInterval = recordClickInterval;
    private readonly Action<string> _debugLog = debugLog;
    private PendingClick? _pending;
    private long? _absentSince;
    private long _panelAddress;
    private long _panelOpenedAt;
    private string _lastWaitReason = string.Empty;
    private long _lastWaitLoggedAt;

    internal readonly record struct Advice(bool Ready, bool AtBoss, string Fate, RectangleF CardRect, RectangleF ConfirmRect);
    internal readonly record struct Card(string Fate, RectangleF Rect, bool Selected);
    internal readonly record struct PendingClick(bool AtBoss, bool Confirm, string Fate, RectangleF Rect, int SelectedCount);

    internal bool IsPluginAvailable => TryGetBridge(out _, out _);

    private bool TryGetBridge(
        out Func<(bool Ready, bool AtBoss, string Fate, RectangleF CardRect, RectangleF ConfirmRect)>? advice,
        out Func<IReadOnlyList<(string Fate, RectangleF CardRect, bool Selected)>>? order)
    {
        advice = null;
        order = null;
        try
        {
            if (!(_pluginManager?.Plugins.Any(static wrapper => wrapper.IsEnable
                    && wrapper.Plugin?.GetType().FullName == "AutoAugury.AutoAugury") ?? false))
                return false;

            var bridge = _gameController.PluginBridge;
            if (bridge?.GetMethod<Func<int>>("AutoAugury.InterfaceVersion")?.Invoke() != InterfaceVersion)
                return false;
            advice = bridge.GetMethod<Func<(bool, bool, string, RectangleF, RectangleF)>>("AutoAugury.Advice");
            order = bridge.GetMethod<Func<IReadOnlyList<(string, RectangleF, bool)>>>("AutoAugury.Order");
            return advice != null && order != null;
        }
        catch { return false; }
    }

    internal bool TryHandlePanel()
    {
        if (!_settings.ClickFates.Value || !_settings.ClickRecommendedFates.Value)
        {
            _pending = null;
            _absentSince = null;
            _panelAddress = 0;
            return false;
        }

        if (!TryGetBridge(out var getAdvice, out var getOrder))
        {
            LogWait("AutoAugury bridge unavailable or incompatible");
            return _pending != null || IsFatesWindowVisible();
        }

        try
        {
            var published = getAdvice!();
            Advice advice = new(published.Ready, published.AtBoss, published.Fate, published.CardRect, published.ConfirmRect);
            if (!advice.Ready)
            {
                LogWait("waiting for AutoAugury advice");
                if (IsFatesWindowVisible())
                {
                    _absentSince = null;
                    return true;
                }
                if (_pending == null)
                {
                    _panelAddress = 0;
                    return false;
                }
                _absentSince ??= Environment.TickCount64;
                if (Environment.TickCount64 - _absentSince < AbsenceGraceMs)
                    return true;
                _pending = null;
                _absentSince = null;
                _panelAddress = 0;
                return false;
            }

            _absentSince = null;
            Element? panel = GetFatesWindow();
            if (panel is not { IsValid: true, IsVisible: true } || panel.Address == 0)
            {
                LogWait("Fates window unavailable");
                return true;
            }
            if (_panelAddress != panel.Address)
            {
                _panelAddress = panel.Address;
                _panelOpenedAt = Environment.TickCount64;
                _pending = null;
            }
            if (IsWithinOpeningSettle(_panelOpenedAt, Environment.TickCount64))
                return true;
            IReadOnlyList<(string Fate, RectangleF CardRect, bool Selected)> publishedOrder = getOrder!();
            if (publishedOrder == null || publishedOrder.Count > 64)
                return true;
            Card[] cards = publishedOrder.Select(static item => new Card(item.Fate, item.CardRect, item.Selected)).ToArray();
            if (_pending is { } pending)
            {
                if (!HasPlacementAdvanced(pending, advice, cards))
                {
                    LogWait("waiting for selection or panel closure after click");
                    return true;
                }
                _pending = null;
            }

            if (!TryChooseAction(advice, cards, out Card target, out bool confirm))
            {
                LogWait("bridge advice and ordered cards are not ready or disagree");
                return true;
            }

            RectangleF rect = confirm ? advice.ConfirmRect : target.Rect;
            if (!IsUsableRect(rect))
            {
                LogWait("recommended rectangle unavailable");
                return true;
            }
            Element? targetElement = ResolveTarget(panel, advice.AtBoss, confirm, rect);
            if (targetElement is not { IsValid: true, IsVisible: true } || targetElement.Address == 0
                || (confirm && !targetElement.IsActive))
            {
                LogWait("recommended card or confirm element unavailable");
                return true;
            }
            Vector2 point = rect.Center;
            if (!_isClickable(point, MechanicIds.Fates) || _gameController.Window?.IsForeground() != true)
            {
                LogWait("recommended point blocked or game window not foreground");
                return true;
            }

            RectangleF window = GameWindowRectResolver.Resolve(_gameController);
            Vector2 absolute = point + new Vector2(window.X, window.Y);
            PendingClick next = new(advice.AtBoss, confirm, target.Fate ?? string.Empty, rect,
                cards.Count(static card => card.Selected));
            long targetAddress = targetElement.Address;
            string failure = "input dispatcher rejected click before hover validation";
            // Fate cards and their confirmation remain interactive while the paused game displays this UI.
            Func<Element?, bool> validateSettledHover = hover =>
            {
                SystemDrawingPoint cursor = Mouse.GetCursorPosition();
                RectangleF currentWindow = GameWindowRectResolver.Resolve(_gameController);
                if (_gameController.Window?.IsForeground() != true || !_isClickable(point, MechanicIds.Fates))
                {
                    failure = "window or clickable area changed";
                    return false;
                }
                if (currentWindow.X != window.X || currentWindow.Y != window.Y
                    || MathF.Abs(cursor.X - absolute.X) > 2 || MathF.Abs(cursor.Y - absolute.Y) > 2)
                {
                    failure = "window or cursor moved";
                    return false;
                }
                if ((advice.AtBoss || confirm) && Input.GetKeyState(Keys.ControlKey))
                {
                    failure = "Control held during ordinary click";
                    return false;
                }
                if (!IsHoverOnElement(hover, targetAddress))
                {
                    failure = $"hover mismatch (expected=0x{targetAddress:X}, actual=0x{hover?.Address ?? 0:X})";
                    return false;
                }
                if (!targetElement.IsValid || !targetElement.IsVisible || (confirm && !targetElement.IsActive)
                    || !BoundsMatch(targetElement.GetClientRectCache, rect))
                {
                    failure = "target element changed or moved";
                    return false;
                }
                var latest = getAdvice();
                var latestOrder = getOrder();
                if (!latest.Ready || latest.AtBoss != advice.AtBoss || latestOrder == null || latestOrder.Count > 64)
                {
                    failure = "bridge advice became unavailable";
                    return false;
                }
                Card[] current = latestOrder.Select(static item => new Card(item.Fate, item.CardRect, item.Selected)).ToArray();
                failure = "bridge recommendation or selection changed";
                return TryChooseAction(new Advice(latest.Ready, latest.AtBoss, latest.Fate, latest.CardRect, latest.ConfirmRect),
                    current, out Card currentTarget, out bool currentConfirm)
                    && currentConfirm == confirm
                    && BoundsMatch(confirm ? latest.ConfirmRect : currentTarget.Rect, rect)
                    && (confirm || string.Equals(currentTarget.Fate, target.Fate, StringComparison.Ordinal));
            };
            bool sent = _performClick(absolute, validateSettledHover, !advice.AtBoss && !confirm, true);
            if (sent)
            {
                _pending = next;
                _recordClickInterval();
                _debugLog($"AutoAugury: {(confirm ? "confirm" : target.Fate)} clicked.");
            }
            else
                LogWait(failure);
            return true;
        }
        catch (Exception ex)
        {
            LogWait($"bridge or UI read failed: {ex.GetType().Name}");
            return true;
        }
    }

    private bool IsFatesWindowVisible()
    {
        try
        {
            return GetFatesWindow() is { IsValid: true, IsVisible: true };
        }
        catch { return false; }
    }

    private Element? GetFatesWindow()
    {
        object? ingameUi = _gameController.IngameState?.IngameUi;
        if (DynamicAccess.TryGetDynamicValue(ingameUi, static ui => ui.AuguryFateWindow, out object? window)
            && window is Element { IsValid: true, IsVisible: true } typed)
            return typed;

        Element? tooltip = _gameController.IngameState?.UIHoverTooltip;
        if (tooltip is not { IsValid: true, IsVisible: true }
            || !AreaUiNodeTraversal.TryGetChildNode(tooltip, 2, out object? body)
            || !AreaUiNodeTraversal.TryGetChildNode(body, 3, out object? rawTitle)
            || rawTitle is not Element title)
            return null;
        return title.Text is "Fates" or "Invoke Fates" ? tooltip : null;
    }

    internal static bool IsWithinOpeningSettle(long openedAt, long now)
        => now < openedAt || now - openedAt < OpeningSettleMs;

    private void LogWait(string reason)
    {
        long now = Environment.TickCount64;
        if (reason == _lastWaitReason && now - _lastWaitLoggedAt < 1000)
            return;
        _lastWaitReason = reason;
        _lastWaitLoggedAt = now;
        _debugLog($"AutoAugury: {reason}.");
    }

    internal static bool TryChooseAction(Advice advice, IReadOnlyList<Card> cards, out Card target, out bool confirm)
    {
        target = default;
        confirm = false;
        if (!advice.Ready || (!advice.AtBoss && cards.Count != 1))
            return false;
        foreach (Card card in cards)
        {
            if (card.Selected)
                continue;
            if (string.IsNullOrWhiteSpace(card.Fate) || !IsUsableRect(card.Rect)
                || !string.Equals(card.Fate, advice.Fate, StringComparison.Ordinal)
                || !BoundsMatch(card.Rect, advice.CardRect))
                return false;
            target = card;
            return true;
        }
        confirm = true;
        return string.IsNullOrEmpty(advice.Fate) && IsUsableRect(advice.ConfirmRect);
    }

    internal static bool HasPlacementAdvanced(PendingClick pending, Advice advice, IReadOnlyList<Card> cards)
    {
        if (pending.Confirm || !pending.AtBoss || !advice.AtBoss)
            return false;
        return cards.Count(static card => card.Selected) > pending.SelectedCount;
    }

    private static bool IsUsableRect(RectangleF rect)
        => float.IsFinite(rect.X) && float.IsFinite(rect.Y) && float.IsFinite(rect.Width) && float.IsFinite(rect.Height)
            && rect.Width > 1 && rect.Height > 1;

    internal static bool BoundsMatch(RectangleF a, RectangleF b)
        => MathF.Abs(a.X - b.X) <= 2 && MathF.Abs(a.Y - b.Y) <= 2
            && MathF.Abs(a.Width - b.Width) <= 2 && MathF.Abs(a.Height - b.Height) <= 2;

    private static Element? ResolveTarget(Element panel, bool atBoss, bool confirm, RectangleF expected)
    {
        object? current = panel;
        int[] path = confirm ? [2, 10] : [2, atBoss ? 7 : 6, 0];
        foreach (int index in path)
            if (!AreaUiNodeTraversal.TryGetChildNode(current, index, out current) || current is not Element)
                return null;
        if (confirm)
            return current is Element button && BoundsMatch(button.GetClientRectCache, expected) ? button : null;
        foreach (object? child in AreaUiNodeTraversal.ResolveChildNodes(current))
            if (child is Element card && card.IsValid && card.IsVisible && BoundsMatch(card.GetClientRectCache, expected))
                return card;
        return null;
    }

    internal static bool IsHoverOnElement(Element? hover, long expectedAddress)
    {
        if (expectedAddress == 0)
            return false;
        for (int depth = 0; depth < 8 && hover != null; depth++, hover = hover.Parent)
            if (hover.Address == expectedAddress)
                return true;
        return false;
    }
}
