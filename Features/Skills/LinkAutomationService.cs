namespace ClickIt.Features.Skills;

internal sealed class LinkAutomationService(
    ClickItSettings settings,
    GameController gameController,
    InputHandler inputHandler,
    Func<Vector2, bool> isPointClickable,
    Action<string> logMessage,
    bool destructive = false)
{
    private readonly bool _destructive = destructive;
    private readonly ClickItSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly GameController _gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
    private readonly InputHandler _inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
    private readonly Func<Vector2, bool> _isPointClickable = isPointClickable ?? throw new ArgumentNullException(nameof(isPointClickable));
    private readonly Action<string> _logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
    private readonly List<Entity> _mercenarySnapshot = [];
    private readonly List<Entity> _animateGuardianSnapshot = [];
    private readonly List<Entity> _spectreSnapshot = [];
    private readonly HashSet<long> _locallyOwnedCompanionAddresses = [];
    private readonly Dictionary<long, int> _castAttemptsByEntityId = [];
    private SkillAutomationTelemetrySnapshot _debugSnapshot = SkillAutomationTelemetrySnapshot.Empty;

    internal SkillAutomationTelemetrySnapshot GetDebugSnapshot()
        => Volatile.Read(ref _debugSnapshot);

    internal void Update()
    {
        bool castOnMercenaries = (_destructive ? _settings.CastDestructiveLinkOnMercenaries : _settings.CastFlameLinkOnMercenaries)?.Value == true;
        bool castOnAnimateGuardian = (_destructive ? _settings.CastDestructiveLinkOnAnimateGuardian : _settings.CastFlameLinkOnAnimateGuardian)?.Value == true;
        bool castOnSpectres = (_destructive ? _settings.CastDestructiveLinkOnSpectres : _settings.CastFlameLinkOnSpectres)?.Value == true;
        SkillAutomationTelemetrySnapshot debug = GetDebugSnapshot() with
        {
            ServiceAvailable = true,
            CastOnMercenaries = castOnMercenaries,
            CastOnAnimateGuardian = castOnAnimateGuardian,
            CastOnSpectres = castOnSpectres,
            AutomationAllowed = false,
            Status = "Checking settings",
            SkillBinding = string.Empty,
            PositionCheck = string.Empty,
            HasTargetPosition = false,
            TargetsWithCastAttempts = _castAttemptsByEntityId.Count
        };

        if (!castOnMercenaries && !castOnAnimateGuardian && !castOnSpectres)
        {
            PublishDebugSnapshot(debug with { Status = "Disabled in settings" });
            return;
        }

        bool? isHideout = AreaClassification.IsSafeArea(_gameController);
        if (!LinkAutomationPolicy.ShouldRunInArea(isHideout))
        {
            PublishDebugSnapshot(debug with { Status = isHideout.HasValue ? "Paused in hideout" : "Waiting for current area state" });
            return;
        }

        if (!_inputHandler.CanPerformBackgroundAutomation(_gameController) || Mouse.DisableNativeInput)
        {
            PublishDebugSnapshot(debug with { Status = "Waiting for game input safety gate" });
            return;
        }

        debug = debug with { AutomationAllowed = true, Status = "Searching locally owned targets" };
        PublishDebugSnapshot(debug);
        EntityEventHub hub = EntityEventHub.Instance;
        hub.EnsureSubscribed(_gameController);
        if ((!castOnMercenaries || hub.Mercenaries.Count == 0)
            && (!castOnAnimateGuardian || hub.AnimateGuardians.Count == 0)
            && (!castOnSpectres || hub.Spectres.Count == 0))
        {
            _castAttemptsByEntityId.Clear();
            // EntityEventHub rate-limits this shared retained-cache reseed to once every two seconds.
            hub.Reseed();
        }

        if (castOnMercenaries)
            hub.Mercenaries.CopyTo(_mercenarySnapshot);
        else
            _mercenarySnapshot.Clear();
        if (castOnAnimateGuardian)
            hub.AnimateGuardians.CopyTo(_animateGuardianSnapshot);
        else
            _animateGuardianSnapshot.Clear();
        if (castOnSpectres)
            hub.Spectres.CopyTo(_spectreSnapshot);
        else
            _spectreSnapshot.Clear();

        if (!TryRefreshLocallyOwnedCompanionAddresses())
        {
            PublishDebugSnapshot(debug with { Status = "Waiting for local companion ownership data" });
            return;
        }

        debug = debug with
        {
            MercenaryCount = CountLocallyOwnedTargets(_mercenarySnapshot),
            AnimateGuardianCount = CountLocallyOwnedTargets(_animateGuardianSnapshot),
            SpectreCount = CountLocallyOwnedTargets(_spectreSnapshot),
            TargetsWithCastAttempts = _castAttemptsByEntityId.Count
        };

        Entity? target = LinkAutomationPolicy.SelectNextMissingTarget(
            _mercenarySnapshot,
            _animateGuardianSnapshot,
            castOnMercenaries,
            castOnAnimateGuardian,
            ReadTargetBuffState,
            IsLocallyOwnedCompanion,
            _spectreSnapshot,
            castOnSpectres);
        if (target == null)
        {
            PublishDebugSnapshot(debug with
            {
                Status = _destructive ? "No locally owned tracked target is missing Destructive Link" : "No locally owned tracked target is missing Flame Link"
            });
            return;
        }

        long targetId = SafeReadEntityId(target);
        _castAttemptsByEntityId.TryGetValue(targetId, out int previousAttempts);
        debug = debug with
        {
            Status = _destructive ? "Target is missing Destructive Link" : "Target is missing Flame Link",
            TargetEntityId = targetId,
            TargetPath = ReadTargetPath(target),
            TargetBuffState = LinkTargetBuffState.Missing,
            TargetCastAttempts = previousAttempts
        };
        PublishDebugSnapshot(debug);

        object? skillBar = _gameController.IngameState?.IngameUi?.SkillBar;
        if (!SkillBarBindingResolver.TryFindReadySkillBinding(skillBar, _destructive ? "destructive_link" : "flame_link", out SkillBinding binding, out string diagnostic)
            && !(_destructive && SkillBarBindingResolver.TryFindReadySkillBinding(skillBar, "critical_link", out binding, out diagnostic)))
        {
            DebugLog($"{(_destructive ? "Destructive Link" : "Flame Link")} target is missing its buff, but no ready key was found ({diagnostic}).");
            PublishDebugSnapshot(debug with { Status = _destructive ? "No ready Destructive Link binding" : "No ready Flame Link binding", SkillBinding = diagnostic });
            return;
        }

        debug = debug with { SkillBinding = $"{binding.InternalName} ({binding.BoundKey})" };
        string runtimeState = SkillBarBindingResolver.TryResolveSkillRuntimeState(
            binding.Entry,
            out bool isUsing,
            out bool? allowedToCast,
            out bool? canBeUsed)
                ? $"isUsing={isUsing}, allowedToCast={FormatRuntimeFlag(allowedToCast)}, canBeUsed={FormatRuntimeFlag(canBeUsed)}"
                : "unavailable";
        debug = debug with
        {
            SkillRuntimeState = runtimeState,
            Status = "Runtime flags recorded; continuing target checks"
        };
        PublishDebugSnapshot(debug);

        // The projection reads Entity.PosNum in world space and returns game-client coordinates. Validate that point before converting it to desktop coordinates.
        if (Keyboard.IsKeyDown(binding.BoundKey)
            || !EntityScreenProjection.TryProjectEntityScreen(_gameController, target, out Vector2 targetClientPosition))
        {
            PublishDebugSnapshot(debug with { Status = "Target projection unavailable or skill key held", PositionCheck = "Projection/key check failed" });
            return;
        }

        RectangleF window = GameWindowRectResolver.Resolve(_gameController);
        if (!LinkAutomationPolicy.TryResolveAbsoluteScreenPoint(targetClientPosition, window, out Vector2 targetScreenPosition)
            || !_isPointClickable(targetClientPosition))
        {
            PublishDebugSnapshot(debug with
            {
                Status = "Target position is outside the clickable game area",
                PositionCheck = "Client bounds or clickable-area validation failed",
                TargetClientPosition = targetClientPosition,
                HasTargetPosition = true
            });
            return;
        }

        if (!LabelClickPointSearch.TryValidateAutomationScreenPoint(targetScreenPosition, _gameController, out _))
        {
            PublishDebugSnapshot(debug with
            {
                Status = "Desktop click position validation failed",
                PositionCheck = "Screen point validation failed",
                TargetClientPosition = targetClientPosition,
                TargetScreenPosition = targetScreenPosition,
                HasTargetPosition = true
            });
            return;
        }

        SystemDrawingPoint cursorBefore = Mouse.GetCursorPosition();
        if (_settings.VerifyCursorInGameWindowBeforeClick?.Value == true && !IsInsideWindow(cursorBefore, window))
        {
            PublishDebugSnapshot(debug with { Status = "Skipped: cursor is outside the game window", PositionCheck = "Cursor verification failed", TargetClientPosition = targetClientPosition, TargetScreenPosition = targetScreenPosition, HasTargetPosition = true });
            return;
        }
        debug = debug with
        {
            Status = "Moving cursor to verified target position",
            PositionCheck = "Client bounds, clickable area, and desktop point passed",
            TargetClientPosition = targetClientPosition,
            TargetScreenPosition = targetScreenPosition,
            HasTargetPosition = true
        };
        PublishDebugSnapshot(debug);
        if (GameInputSafety.ShouldBlockInput(_gameController, _settings))
        {
            PublishDebugSnapshot(debug with { Status = "Skipped: game is paused" });
            return;
        }
        Input.SetCursorPos(new NumVector2(targetScreenPosition.X, targetScreenPosition.Y));
        PublishDebugSnapshot(debug with { Status = _destructive ? "Cursor move requested; casting Destructive Link" : "Cursor move requested; casting Flame Link" });

        bool attempted = false;
        try
        {
            ClickPipelineTiming.Sleep(10);
            if (GameInputSafety.ShouldBlockInput(_gameController, _settings))
            {
                PublishDebugSnapshot(debug with { Status = "Skipped: game is paused" });
                return;
            }
            attempted = true;
            Keyboard.KeyPress(binding.BoundKey, SkillBarBindingResolver.KeyTapDelayMs);
        }
        finally
        {
            // Skill automation always restores the user's cursor; the lazy-mode cursor setting only applies to ClickIt's item-click flow.
            if (!GameInputSafety.ShouldBlockInput(_gameController, _settings))
                Input.SetCursorPos(new NumVector2(cursorBefore.X, cursorBefore.Y));
            if (attempted)
                RecordCastAttempt(targetId);
        }

        int attempts = _castAttemptsByEntityId[targetId];
        PublishDebugSnapshot(debug with
        {
            Status = "Cast sent; waiting for target buff",
            TargetCastAttempts = attempts,
            LastAction = $"Pressed {binding.BoundKey} for entity {targetId}",
            PositionCheck = "Client bounds, clickable area, and desktop point passed",
            TargetClientPosition = targetClientPosition,
            TargetScreenPosition = targetScreenPosition,
            HasTargetPosition = true
        });

        DebugLog($"Used {(_destructive ? "Destructive Link" : "Flame Link")} on target {targetId} with key {binding.BoundKey} (attempt {attempts}); waiting for its target buff before selecting another target.");
    }

    private bool TryRefreshLocallyOwnedCompanionAddresses()
    {
        _locallyOwnedCompanionAddresses.Clear();

        Entity? player = _gameController.Player;
        if (player == null || !DynamicAccess.TryGetComponent<Actor>(player, out Actor? actor) || actor == null)
            return false;

        List<DeployedObject>? deployedObjects;
        try { deployedObjects = actor.DeployedObjects; }
        catch { return false; }
        if (deployedObjects == null)
            return false;

        int deployedObjectCount;
        try { deployedObjectCount = deployedObjects.Count; }
        catch { return false; }

        // Allied paths include other players' companions. Trust only the deployed entity references on the local player's Actor component.
        for (int deployedIndex = 0; deployedIndex < deployedObjectCount; deployedIndex++)
        {
            try
            {
                Entity? deployedEntity = deployedObjects[deployedIndex]?.Entity;
                if (DynamicAccess.TryReadEntityAddress(deployedEntity, out long address) && address > 0)
                    _locallyOwnedCompanionAddresses.Add(address);
            }
            catch
            {
                // A stale skill entry cannot establish ownership; skip it and only trust the remaining readable player-owned references.
            }
        }

        return true;
    }

    private int CountLocallyOwnedTargets(List<Entity> targets)
    {
        int count = 0;
        for (int i = 0; i < targets.Count; i++)
            if (IsLocallyOwnedCompanion(targets[i]))
                count++;
        return count;
    }

    private bool IsLocallyOwnedCompanion(Entity entity)
        => DynamicAccess.TryReadEntityAddress(entity, out long address)
            && address > 0
            && _locallyOwnedCompanionAddresses.Contains(address);

    private void RecordCastAttempt(long entityId)
    {
        if (entityId <= 0)
            return;

        if (_castAttemptsByEntityId.Count >= 64 && !_castAttemptsByEntityId.ContainsKey(entityId))
            _castAttemptsByEntityId.Clear();

        _castAttemptsByEntityId.TryGetValue(entityId, out int attempts);
        if (attempts < int.MaxValue)
            _castAttemptsByEntityId[entityId] = attempts + 1;
    }

    private static bool IsInsideWindow(SystemDrawingPoint point, RectangleF window)
        => point.X >= window.Left
            && point.Y >= window.Top
            && point.X <= window.Right
            && point.Y <= window.Bottom;

    private LinkTargetBuffState ReadTargetBuffState(Entity entity)
    {
        try
        {
            if (!entity.IsValid
                || !entity.IsTargetable
                || !DynamicAccess.TryGetComponent<Buffs>(entity, out Buffs? buffs)
                || buffs == null)
                return LinkTargetBuffState.Unknown;

            List<Buff> buffsList = buffs.BuffsList;
            if (buffsList == null)
                return LinkTargetBuffState.Unknown;

            for (int i = 0; i < buffsList.Count; i++)
            {
                Buff? buff = buffsList[i];
                if (buff == null)
                    continue;

                string? name;
                try { name = buff.Name; }
                catch { return LinkTargetBuffState.Unknown; }
                if (LinkAutomationPolicy.IsTargetBuffName(name, _destructive))
                    return LinkTargetBuffState.Present;
            }

            return LinkTargetBuffState.Missing;
        }
        catch
        {
            return LinkTargetBuffState.Unknown;
        }
    }

    private void DebugLog(string message)
    {
        if (_settings.DebugMode?.Value == true)
            _logMessage(message);
    }

    private static long SafeReadEntityId(Entity entity)
    {
        try { return entity.Id; }
        catch { return 0; }
    }

    private void PublishDebugSnapshot(SkillAutomationTelemetrySnapshot snapshot)
        => Volatile.Write(ref _debugSnapshot, snapshot);

    private static string FormatRuntimeFlag(bool? value)
        => value.HasValue ? value.Value.ToString() : "unknown";

    private static string ReadTargetPath(Entity entity)
    {
        try { return entity.Path ?? string.Empty; }
        catch { return "<unavailable>"; }
    }
}
