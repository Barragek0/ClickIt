namespace ClickIt.Features.Click.Application;

/// <summary>Uses AutoAugury's recommendations to select cards in the Fates panels.</summary>
internal sealed class AutoAuguryFatesClickService
{
    private const string AutoAuguryPluginTypeName = "AutoAugury.AutoAugury";

    private const int RetryDelayMs = 150;

    private const int SelectionSettleMs = 400;

    private const int OpeningSettleMs = 100;

    private const int PanelCloseTimeoutMs = 3000;

    private const int MostChoiceAttempts = 5;

    private const int PanelAbsenceGraceMs = 500;

    private const int PlacementConfirmTimeoutMs = 1500;

    private const int MostPlacementRetries = 2;

    // Child indexes for the offer row, collection row, and confirm button under the panel body.
    private const int PanelBodyIndex = 2;
    private const int OfferRowIndex = 6;
    private const int CollectionRowIndex = 7;
    private const int ConfirmButtonIndex = 10;

    private static readonly BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly ClickItSettings _settings;
    private readonly PluginManager? _pluginManager;
    private readonly GameController _gameController;
    private readonly Func<Vector2, string, bool> _isClickableInEitherSpace;
    private readonly Func<Vector2, Element, Func<Element?, bool>?, bool> _performClick;
    private readonly Action _recordClickInterval;
    private readonly Action<string> _debugLog;
    private readonly Stopwatch _retryTimer = new();
    private readonly Stopwatch _phaseTimer = new();

    private Type? _pluginType;
    private FieldInfo? _scoresField;
    private FieldInfo? _panelStateField;
    private FieldInfo? _fatesPanelField;
    private FieldInfo? _panelElementField;
    private FieldInfo? _guideField;
    private MethodInfo? _bestOfMethod;
    private PropertyInfo? _choosingFateProperty;
    private PropertyInfo? _invokingFatesProperty;
    private PropertyInfo? _confirmButtonProperty;
    private PropertyInfo? _offeredProperty;
    private PropertyInfo? _scoreCardProperty;
    private PropertyInfo? _scoreValueProperty;
    private PropertyInfo? _guideNextProperty;
    private PropertyInfo? _guideAdviceProperty;
    private PropertyInfo? _adviceSlotsProperty;
    private PropertyInfo? _pickCardProperty;
    private PropertyInfo? _cardNameProperty;
    private PropertyInfo? _cardRectProperty;

    private FatesClickPhase _phase;

    private long _panelAddress;

    private long? _panelOpenedAt;

    private long? _panelAbsentSince;

    private bool _gaveUp;

    private RectangleF _pendingPlacementBounds;
    private int _placedCountAtPlacement;

    private int _placementAttempts;

    private int _placementRetries;

    private readonly List<RectangleF> _skippedPlacementBounds = [];

    private int _confirmAttempts;

    private RectangleF _selectedCardBounds;

    internal AutoAuguryFatesClickService(
        ClickItSettings settings,
        PluginManager? pluginManager,
        GameController gameController,
        Func<Vector2, string, bool> isClickableInEitherSpace,
        Func<Vector2, Element, Func<Element?, bool>?, bool> performClick,
        Action recordClickInterval,
        Action<string> debugLog)
    {
        _settings = settings;
        _pluginManager = pluginManager;
        _gameController = gameController;
        _isClickableInEitherSpace = isClickableInEitherSpace;
        _performClick = performClick;
        _recordClickInterval = recordClickInterval;
        _debugLog = debugLog;
    }

    internal bool IsPluginAvailable
    {
        get
        {
            object? plugin = FindEnabledPlugin();
            return plugin != null && IsPluginSettingEnabled(plugin) && HasChoiceContract && HasInvocationContract;
        }
    }

    internal bool TryHandlePanel()
    {
        if (!_settings.ClickFates.Value || !_settings.ClickRecommendedFates.Value)
        {
            ResetPhase();
            return false;
        }

        object? plugin = FindEnabledPlugin();
        bool available = plugin != null && IsPluginSettingEnabled(plugin) && HasChoiceContract && HasInvocationContract;
        _settings.TransientState.RuntimeCache.AutoAuguryPluginAvailable = available;
        if (!available)
        {
            _settings.ClickRecommendedFates.Value = false;
            ResetPhase();
            return false;
        }

        if (!TryReadPanelForPlugin(plugin!, out AutoAuguryFatesPanelRead read))
        {
            // A failed read can be one-tick flicker; keep an in-progress attempt while the panel element remains visible.
            bool elementGone = !IsLiveFatesPanelVisible(plugin!);
            if (HandlePanelAbsence(elementGone))
                return true;

            ResetPhase();
            return false;
        }

        if (!read.ChoosingFate && !read.InvokingFates)
        {
            // Hold the click tick through a transient closed read so the regular path cannot click another card.
            if (IsLiveFatesPanelVisible(plugin!))
                return true;

            return HandlePanelAbsence(panelElementGone: true);
        }

        _panelAbsentSince = null;

        if (read.Panel == null)
            return true;

        if (_panelAddress != read.Panel.Address)
        {
            _gaveUp = false;
            _panelOpenedAt = Environment.TickCount64;
            ResetPhase();
        }

        _panelAddress = read.Panel.Address;

        if (_gaveUp)
            return true;

        return read.InvokingFates ? HandleInvocation(read) : HandleChoice(read);
    }

    private bool IsLiveFatesPanelVisible(object plugin)
    {
        try
        {
            object? fatesPanel = _fatesPanelField?.GetValue(plugin);
            Element? cachedPanel = _panelElementField?.GetValue(fatesPanel) as Element;
            return IsFatesPanelVisible(cachedPanel) || IsFatesPanelVisible(_gameController.IngameState?.UIHoverTooltip);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFatesPanelVisible(Element? panel)
    {
        if (panel is not { IsValid: true, IsVisible: true })
            return false;
        Element? title = ResolveElementAtPath(panel, 2, 3);
        return title is { IsVisible: true } && (title.Text == "Fates" || title.Text == "Invoke Fates");
    }

    internal static bool IsWithinOpeningSettle(long? openedAt, long now)
        => openedAt is { } since && now - since < OpeningSettleMs;

    private bool HandlePanelAbsence(bool panelElementGone)
    {
        if (_phase == FatesClickPhase.Idle && !_gaveUp)
        {
            ForgetPanelOpening();
            return false;
        }

        long now = Environment.TickCount64;
        _panelAbsentSince ??= now;

        if (ShouldHoldAbsentPanel(_phase == FatesClickPhase.AwaitClose, panelElementGone, _panelAbsentSince, now))
            return true;

        _gaveUp = false;
        ForgetPanelOpening();
        ResetPhase();
        return false;
    }

    internal static bool ShouldHoldAbsentPanel(bool confirmSent, bool panelElementGone, long? absentSince, long now)
        => !(confirmSent && panelElementGone) && IsWithinPanelAbsenceGrace(absentSince, now);

    private void ForgetPanelOpening()
    {
        _panelAddress = 0;
        _panelOpenedAt = null;
    }

    internal static bool IsWithinPanelAbsenceGrace(long? absentSince, long now)
        => absentSince is not { } since || now - since < PanelAbsenceGraceMs;

    internal static AutoAuguryRecommendedFate? SelectChoice(IReadOnlyList<AutoAuguryRecommendedFate> recommendations)
    {
        return recommendations.Count == 0 ? null : recommendations[0];
    }

    internal bool TryReadPanelForPlugin(object plugin, out AutoAuguryFatesPanelRead read)
    {
        if (_pluginType != plugin.GetType())
            CacheReflectionMembers(plugin.GetType());

        read = AutoAuguryFatesPanelRead.Closed;

        try
        {
            object? panelState = _panelStateField?.GetValue(plugin);
            bool choosingFate = ReadBool(panelState, _choosingFateProperty);
            bool invokingFates = ReadBool(panelState, _invokingFatesProperty);
            if (!choosingFate && !invokingFates)
                return true;

            object? fatesPanel = _fatesPanelField?.GetValue(plugin);
            Element? panel = _panelElementField?.GetValue(fatesPanel) as Element;
            if (panel != null && (!panel.IsValid || !panel.IsVisible))
                return false;

            // Offered contains the choice row, or the placed-slot row at the boss.
            read = new AutoAuguryFatesPanelRead(
                choosingFate,
                invokingFates,
                panel,
                ReadRectangle(panelState, _confirmButtonProperty),
                ReadItemCount(panelState, _offeredProperty),
                choosingFate ? ReadRecommendations(plugin) : [],
                invokingFates ? ReadNextInvocation(plugin) : null,
                invokingFates && ReadGuideIsComputed(plugin));
            return true;
        }
        catch
        {
            read = AutoAuguryFatesPanelRead.Closed;
            return false;
        }
    }

    private bool HandleChoice(AutoAuguryFatesPanelRead read)
    {
        if (_phase == FatesClickPhase.AwaitClose)
        {
            // Wait for the panel to close; do not choose again while confirm is pending.
            if (_phaseTimer.ElapsedMilliseconds < PanelCloseTimeoutMs)
                return true;

            if (_confirmAttempts >= MostChoiceAttempts)
            {
                _debugLog("AutoAugury Fate panel stayed open after "
                    + $"{_confirmAttempts} confirm attempts; leaving it for the next read.");
                _gaveUp = true;
                ResetPhase();
                return true;
            }

            // Confirm retries must not toggle the selected card.
            _phase = FatesClickPhase.AwaitConfirm;
            _retryTimer.Reset();
            return true;
        }

        if (_phase == FatesClickPhase.AwaitConfirm)
        {
            Element? selectionRow = ResolveElementAtPath(read.Panel!, PanelBodyIndex, OfferRowIndex, 0);
            Element? selected = selectionRow == null ? null : FindCardByBounds(selectionRow, _selectedCardBounds);
            Element? marker = selected == null ? null : ResolveElementAtPath(selected, 0, 0, 0);
            Element? confirm = ResolveElementAtPath(read.Panel!, PanelBodyIndex, ConfirmButtonIndex);
            if (marker is not { IsValid: true, IsVisible: true } || confirm is not { IsValid: true, IsVisible: true, IsActive: true })
                return true;

            return TryClickConfirm(read, "confirm");
        }

        if (IsActionCoolingDown())
            return true;

        // Keep the selected card; clicking it again would send a second choice.
        if (_selectedCardBounds.Width > 1f)
            return TryClickConfirm(read, "confirm");

        AutoAuguryRecommendedFate? choice = SelectChoice(read.Recommendations);
        if (choice == null)
            return true;

        Element? offeredRow = ResolveElementAtPath(read.Panel!, PanelBodyIndex, OfferRowIndex, 0);
        Element? card = offeredRow == null ? null : FindCardByBounds(offeredRow, choice.Bounds);
        Element? selectedMarker = card == null ? null : ResolveElementAtPath(card, 0, 0, 0);
        bool alreadySelected = selectedMarker is { IsValid: true, IsVisible: true };
        if (!alreadySelected && !TryClickElement(card, choice.Bounds, "recommended Fate"))
            return true;

        _selectedCardBounds = choice.Bounds;
        _phase = FatesClickPhase.AwaitConfirm;
        _phaseTimer.Restart();
        _retryTimer.Reset();
        _debugLog(read.Recommendations.Count > 1
            ? $"AutoAugury recommended Fate selected: {choice.Name} (first of {read.Recommendations.Count} equally best)."
            : $"AutoAugury recommended Fate selected: {choice.Name}.");
        return true;
    }

    private bool HandleInvocation(AutoAuguryFatesPanelRead read)
    {
        if (!HasInvocationContract)
            return true;

        if (_phase == FatesClickPhase.AwaitPlacement)
            return AwaitPlacement(read);

        if (_phase == FatesClickPhase.AwaitClose)
        {
            if (_phaseTimer.ElapsedMilliseconds < PanelCloseTimeoutMs)
                return true;

            if (_confirmAttempts >= MostChoiceAttempts)
            {
                _debugLog("AutoAugury Invoke Fates panel stayed open after "
                    + $"{_confirmAttempts} confirm attempts; leaving it for the next read.");
                _gaveUp = true;
                ResetPhase();
                return true;
            }

            // Rebuild the invocation slots before retrying confirm.
            _phase = FatesClickPhase.Idle;
            _skippedPlacementBounds.Clear();
            _retryTimer.Reset();
            return true;
        }

        if (_phase == FatesClickPhase.AwaitConfirm)
        {
            if (_phaseTimer.ElapsedMilliseconds < SelectionSettleMs)
                return true;

            return TryClickConfirm(read, "confirm");
        }

        if (IsActionCoolingDown())
            return true;

        if (ResolvePlacement(read.NextInvocation, _skippedPlacementBounds) is { } next)
        {
            // The boss panel toggles a collection card in and out of its slot; never repeat a pending click.
            if (IsPlacementInProgress(_placementAttempts, _pendingPlacementBounds, next))
                return true;

            Element? collectionRow = ResolveElementAtPath(read.Panel!, PanelBodyIndex, CollectionRowIndex, 0);
            Element? card = collectionRow == null ? null : FindCardByBounds(collectionRow, next.Bounds);
            if (!TryClickElement(card, next.Bounds, "invocation Fate"))
                return true;

            if (!BoundsMatch(_pendingPlacementBounds, next.Bounds))
                _placementRetries = 0;
            _placedCountAtPlacement = read.TopRowCount;
            _pendingPlacementBounds = next.Bounds;
            _placementAttempts = 1;
            _phase = FatesClickPhase.AwaitPlacement;
            _phaseTimer.Restart();
            _retryTimer.Reset();
            _debugLog($"AutoAugury invocation Fate clicked: {next.Name}.");
            return true;
        }

        // Confirm once the guide is computed and has no remaining card to place.
        if (!read.GuideComputed)
            return true;

        _phase = FatesClickPhase.AwaitConfirm;
        _phaseTimer.Restart();
        _retryTimer.Reset();
        return TryClickConfirm(read, "confirm");
    }

    internal static bool IsPlacementInProgress(int placementAttempts, RectangleF pendingBounds,
        AutoAuguryRecommendedFate? next)
        => placementAttempts > 0
            && pendingBounds.Width > 1f
            && next is { } card
            && BoundsMatch(pendingBounds, card.Bounds);

    internal static AutoAuguryRecommendedFate? ResolvePlacement(
        AutoAuguryRecommendedFate? next, IReadOnlyList<RectangleF> skipped)
    {
        if (next is not { } card)
            return null;

        foreach (RectangleF bounds in skipped)
            if (BoundsMatch(bounds, card.Bounds))
                return null;

        return card;
    }

    private bool AwaitPlacement(AutoAuguryFatesPanelRead read)
    {
        bool registered = read.GuideComputed && (read.NextInvocation is not { } next
            || !BoundsMatch(_pendingPlacementBounds, next.Bounds))
            || read.TopRowCount > _placedCountAtPlacement
            || PendingCardReadsPlaced(read);

        if (registered)
        {
            EndPlacementWait();
            return true;
        }

        if (_phaseTimer.ElapsedMilliseconds < PlacementConfirmTimeoutMs)
            return true;

        if (_placementRetries < MostPlacementRetries)
        {
            // Retry only after the placement timeout; the guide still frames this card.
            _placementRetries++;
            _placementAttempts = 0;
            _phase = FatesClickPhase.Idle;
            _retryTimer.Reset();
            _debugLog($"AutoAugury invocation placement did not register; retrying ({_placementRetries} of "
                + $"{MostPlacementRetries}).");
            return true;
        }

        // A late placement could still register, so leave this panel for manual review.
        _debugLog("AutoAugury invocation placement did not register; leaving the panel for manual review.");
        _gaveUp = true;
        ResetPhase();
        return true;
    }

    private void EndPlacementWait()
    {
        _phase = FatesClickPhase.Idle;
        _placedCountAtPlacement = 0;
        _pendingPlacementBounds = RectangleF.Empty;
        _placementAttempts = 0;
        _placementRetries = 0;
        _retryTimer.Reset();
    }

    private bool PendingCardReadsPlaced(AutoAuguryFatesPanelRead read)
    {
        if (_pendingPlacementBounds.Width <= 1f)
            return false;

        try
        {
            Element? collectionRow = ResolveElementAtPath(read.Panel!, PanelBodyIndex, CollectionRowIndex, 0);
            Element? placed = collectionRow == null ? null : FindCardByBounds(collectionRow, _pendingPlacementBounds);
            return placed is { IsValid: true, IsVisible: true, IsActive: false };
        }
        catch
        {
            return false;
        }
    }

    private bool TryClickConfirm(AutoAuguryFatesPanelRead read, string action)
    {
        if (_confirmAttempts >= MostChoiceAttempts)
        {
            _debugLog($"AutoAugury Fates confirm not retried: {_confirmAttempts} attempts already made.");
            return true;
        }

        if (_retryTimer.IsRunning && _retryTimer.ElapsedMilliseconds < RetryDelayMs)
            return true;

        _retryTimer.Restart();
        Element? confirm = ResolveElementAtPath(read.Panel!, PanelBodyIndex, ConfirmButtonIndex);
        if (!TryClickElement(confirm, read.ConfirmRect, action))
            return true;

        _confirmAttempts++;
        _phase = FatesClickPhase.AwaitClose;
        _phaseTimer.Restart();
        return true;
    }

    private bool IsActionCoolingDown()
    {
        if (_retryTimer.IsRunning && _retryTimer.ElapsedMilliseconds < RetryDelayMs)
            return true;

        _retryTimer.Restart();
        return false;
    }

    private List<AutoAuguryRecommendedFate> ReadRecommendations(object plugin)
    {
        List<AutoAuguryRecommendedFate> recommendations = [];
        object? scores = _scoresField?.GetValue(plugin);
        if (scores is not IEnumerable)
            return recommendations;

        if (_bestOfMethod == null || _bestOfMethod.Invoke(null, [scores]) is not IEnumerable candidates)
            return recommendations;

        foreach (object? score in candidates)
        {
            object? card = score == null ? null : _scoreCardProperty?.GetValue(score);
            if (TryReadFateCard(card, out AutoAuguryRecommendedFate? fate))
                recommendations.Add(fate with { Score = ReadScoreValue(score) });
        }

        return recommendations;
    }

    private double ReadScoreValue(object? score)
    {
        if (score == null || _scoreValueProperty is null)
            return 0d;

        try
        {
            object? value = _scoreValueProperty.GetValue(score);
            return value is double d ? d : value is float f ? f : value is int i ? i : 0d;
        }
        catch
        {
            return 0d;
        }
    }

    private AutoAuguryRecommendedFate? ReadNextInvocation(object plugin)
    {
        object? guide = _guideField?.GetValue(plugin);
        object? pick = guide == null ? null : _guideNextProperty?.GetValue(guide);
        if (pick == null)
            return null;

        object? card = _pickCardProperty?.GetValue(pick);
        return TryReadFateCard(card, out AutoAuguryRecommendedFate? fate) ? fate : null;
    }

    private bool ReadGuideIsComputed(object plugin)
    {
        object? guide = _guideField?.GetValue(plugin);
        object? advice = guide == null ? null : _guideAdviceProperty?.GetValue(guide);
        return ReadItemCount(advice, _adviceSlotsProperty) > 0;
    }

    private bool TryReadFateCard(object? card, [NotNullWhen(true)] out AutoAuguryRecommendedFate? fate)
    {
        fate = null;
        if (card == null)
            return false;

        string? name = _cardNameProperty?.GetValue(card) as string;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (_cardRectProperty?.GetValue(card) is not RectangleF bounds || bounds.Width <= 1f || bounds.Height <= 1f)
            return false;

        fate = new AutoAuguryRecommendedFate(name, bounds);
        return true;
    }

    private static bool ReadBool(object? source, PropertyInfo? property)
        => source != null && property != null && property.GetValue(source) is true;

    private static RectangleF ReadRectangle(object? source, PropertyInfo? property)
        => source != null && property != null && property.GetValue(source) is RectangleF rect ? rect : RectangleF.Empty;

    private static int ReadItemCount(object? source, PropertyInfo? property)
    {
        if (source == null || property == null)
            return 0;

        object? value = property.GetValue(source);
        return value switch
        {
            int count => count,
            ICollection collection => collection.Count,
            _ => value != null && value.GetType().GetProperty("Count", InstanceMembers)?.GetValue(value) is int count
                ? count
                : 0
        };
    }

    private void CacheReflectionMembers(Type pluginType)
    {
        ClearReflectionMembers();
        _pluginType = pluginType;

        _scoresField = pluginType.GetField("_scores", InstanceMembers);
        _panelStateField = pluginType.GetField("_panelState", InstanceMembers);
        _fatesPanelField = pluginType.GetField("_fatesPanel", InstanceMembers);
        _guideField = pluginType.GetField("_guide", InstanceMembers);
        _panelElementField = _fatesPanelField?.FieldType.GetField("_panel", InstanceMembers);

        Type? panelStateType = _panelStateField?.FieldType;
        _choosingFateProperty = panelStateType?.GetProperty("ChoosingFate", InstanceMembers);
        _invokingFatesProperty = panelStateType?.GetProperty("InvokingFates", InstanceMembers);
        _confirmButtonProperty = panelStateType?.GetProperty("ConfirmButton", InstanceMembers);
        _offeredProperty = panelStateType?.GetProperty("Offered", InstanceMembers);

        Type? scoringType = pluginType.Assembly.GetType("AutoAugury.FateScoring", throwOnError: false);
        _bestOfMethod = scoringType?.GetMethod("BestOf", StaticMembers);

        Type? scoreType = _scoresField?.FieldType.IsGenericType == true
            ? _scoresField.FieldType.GetGenericArguments()[0]
            : null;
        _scoreCardProperty = scoreType?.GetProperty("Card", InstanceMembers);
        _scoreValueProperty = scoreType?.GetProperty("Value", InstanceMembers);

        Type? guideType = _guideField?.FieldType;
        _guideNextProperty = guideType?.GetProperty("Next", InstanceMembers);
        _guideAdviceProperty = guideType?.GetProperty("Advice", InstanceMembers);
        _adviceSlotsProperty = _guideAdviceProperty?.PropertyType.GetProperty("Slots", InstanceMembers);

        Type? pickType = _guideNextProperty?.PropertyType;
        _pickCardProperty = pickType?.GetProperty("Card", InstanceMembers);

        Type? cardType = _pickCardProperty?.PropertyType ?? _scoreCardProperty?.PropertyType;
        _cardNameProperty = cardType?.GetProperty("Name", InstanceMembers);
        _cardRectProperty = cardType?.GetProperty("Rect", InstanceMembers);
    }

    private void ClearReflectionMembers()
    {
        _pluginType = null;
        _scoresField = null;
        _panelStateField = null;
        _fatesPanelField = null;
        _panelElementField = null;
        _guideField = null;
        _bestOfMethod = null;
        _choosingFateProperty = null;
        _invokingFatesProperty = null;
        _confirmButtonProperty = null;
        _offeredProperty = null;
        _scoreCardProperty = null;
        _scoreValueProperty = null;
        _guideNextProperty = null;
        _guideAdviceProperty = null;
        _adviceSlotsProperty = null;
        _pickCardProperty = null;
        _cardNameProperty = null;
        _cardRectProperty = null;
    }

    private bool HasChoiceContract
        => _scoresField != null && _panelStateField != null && _fatesPanelField != null
            && _panelElementField != null && typeof(Element).IsAssignableFrom(_panelElementField.FieldType)
            && _choosingFateProperty != null && _confirmButtonProperty != null && _offeredProperty != null
            && _scoreCardProperty != null && _scoreValueProperty != null
            && _cardNameProperty != null && _cardRectProperty != null && _bestOfMethod != null;

    private bool HasInvocationContract
        => _guideField != null && _guideNextProperty != null && _guideAdviceProperty != null
            && _adviceSlotsProperty != null && _pickCardProperty != null && _invokingFatesProperty != null;

    private object? FindEnabledPlugin()
    {
        try
        {
            foreach (PluginWrapper wrapper in _pluginManager?.Plugins ?? [])
            {
                object? plugin = wrapper.Plugin;
                if (!wrapper.IsEnable || plugin?.GetType().FullName != AutoAuguryPluginTypeName)
                    continue;

                if (_pluginType != plugin.GetType())
                    CacheReflectionMembers(plugin.GetType());

                return plugin;
            }
        }
        catch
        {
            // If plugin state changes during enumeration, deny input for this pass.
        }

        ClearReflectionMembers();
        return null;
    }

    private bool IsPluginSettingEnabled(object plugin)
    {
        try
        {
            object? settings = plugin.GetType().GetProperty("Settings", InstanceMembers)?.GetValue(plugin);
            object? enable = settings?.GetType().GetProperty("Enable", InstanceMembers)?.GetValue(settings);
            return enable?.GetType().GetProperty("Value", InstanceMembers)?.GetValue(enable) is true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryClickElement(Element? element, RectangleF expectedBounds, string action)
    {
        try
        {
            if (element == null || !element.IsValid || !element.IsVisible)
                return LogClickFailure(action, "element is missing, invalid, or hidden");

            RectangleF currentBounds = element.GetClientRectCache;
            if (currentBounds.Width <= 1f || currentBounds.Height <= 1f)
                return LogClickFailure(action, $"element has no usable rectangle ({currentBounds})");

            // Use published bounds as a staleness check when they are available.
            if (!IsPublishedBoundsUsable(expectedBounds, currentBounds))
                return LogClickFailure(action, $"element moved (expected {expectedBounds}, now {currentBounds})");

            if (!LabelClickPointSearch.TryResolveVisibleClickablePoint(
                    currentBounds,
                    currentBounds.Center,
                    [],
                    point => _isClickableInEitherSpace(point, MechanicIds.Fates),
                    out Vector2 clientPoint))
                return LogClickFailure(action, $"no visible clickable point in {currentBounds}");

            RectangleF window = _gameController.Window.GetWindowRectangleTimeCache;
            Vector2 absolutePoint = clientPoint + new Vector2(window.X, window.Y);

            // Require the cursor to reach the expected element before dispatching the click.
            long expectedAddress = element.Address;
            if (!_performClick(absolutePoint, element, hover => IsHoverOnElement(hover, expectedAddress)))
                return LogClickFailure(action, "the click dispatcher rejected the input");

            _recordClickInterval();
            _debugLog($"AutoAugury Fates {action} clicked at {absolutePoint}.");
            return true;
        }
        catch
        {
            // Stale UI or window state must fail closed.
            return LogClickFailure(action, "the element or window could not be read");
        }
    }

    private bool LogClickFailure(string action, string reason)
    {
        _debugLog($"AutoAugury Fates {action} not clicked: {reason}.");
        return false;
    }

    internal static bool IsHoverOnElement(Element? hover, long expectedAddress)
    {
        if (expectedAddress == 0)
            return false;

        for (int depth = 0; depth < 6 && hover != null; depth++, hover = hover.Parent)
            if (hover.Address == expectedAddress)
                return true;

        return false;
    }

    private static Element? FindCardByBounds(Element row, RectangleF bounds)
    {
        try
        {
            foreach (object? child in AreaUiNodeTraversal.ResolveChildNodes(row))
            {
                if (child is not Element card || !card.IsValid || !card.IsVisible)
                    continue;

                if (BoundsMatch(bounds, card.GetClientRectCache))
                    return card;
            }
        }
        catch
        {
            // The UI tree can change between reading the recommendation and resolving its element.
        }

        return null;
    }

    private static Element? ResolveElementAtPath(Element root, params int[] path)
    {
        object? current = root;
        foreach (int index in path)
            if (!AreaUiNodeTraversal.TryGetChildNode(current, index, out current) || current is not Element)
                return null;

        return current as Element;
    }

    internal static bool BoundsMatch(RectangleF expected, RectangleF actual)
        => MathF.Abs(expected.X - actual.X) <= 2f
            && MathF.Abs(expected.Y - actual.Y) <= 2f
            && MathF.Abs(expected.Width - actual.Width) <= 2f
            && MathF.Abs(expected.Height - actual.Height) <= 2f;

    internal static bool IsPublishedBoundsUsable(RectangleF published, RectangleF current)
        => published.Width <= 1f || published.Height <= 1f || BoundsMatch(published, current);

    private void ResetPhase()
    {
        _phase = FatesClickPhase.Idle;
        _panelAbsentSince = null;
        _pendingPlacementBounds = RectangleF.Empty;
        _placedCountAtPlacement = 0;
        _placementAttempts = 0;
        _placementRetries = 0;
        _skippedPlacementBounds.Clear();
        _confirmAttempts = 0;
        _selectedCardBounds = RectangleF.Empty;
        _phaseTimer.Reset();
        _retryTimer.Reset();
    }

    private enum FatesClickPhase
    {
        Idle,
        AwaitPlacement,
        AwaitConfirm,
        AwaitClose
    }
}

internal sealed record AutoAuguryRecommendedFate(string Name, RectangleF Bounds, double Score = 0d);

internal readonly record struct AutoAuguryFatesPanelRead(
    bool ChoosingFate,
    bool InvokingFates,
    Element? Panel,
    RectangleF ConfirmRect,
    int TopRowCount,
    IReadOnlyList<AutoAuguryRecommendedFate> Recommendations,
    AutoAuguryRecommendedFate? NextInvocation,
    bool GuideComputed)
{
    internal static readonly AutoAuguryFatesPanelRead Closed =
        new(false, false, null, RectangleF.Empty, 0, [], null, false);
}
