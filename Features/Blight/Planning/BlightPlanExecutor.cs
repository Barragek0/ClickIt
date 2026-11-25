namespace ClickIt.Features.Blight.Planning;

internal sealed class BlightPlanExecutor
{
    private long _phaseStartTimestamp;
    private int _consecutiveFailures;
    private NumVector2 _lastPlayerGridPos;
    private int _stationaryTicks;

    private bool _stopPlayerSawMovement;

    // Timestamp of the last build-icon click (OpenMenu BUILD).  The build icon is a TOGGLE — a re-click while the sub-menu is still opening closes it again — so the executor waits for the sub-menu to appear instead of re-clicking every tick.
    private long _lastBuildMenuClickTimestampMs;
    private const long MenuSubMenuWaitMs = 500;

    private const int MaxWalkWaitTicksBeforeSkip = 25;
    private int _walkWaitTicks;
    private NumVector2 _walkWaitStepPos;

    // Foundation label resolution is the most expensive per-tick read; cache per labels-list reference (the labels refresh ~50ms, bounding staleness).
    private IReadOnlyList<LabelOnGround>? _cachedLabels;
    private NumVector2 _cachedStepPos;
    private bool _hasCachedLabel;
    private Element? _cachedLabelElement;

    internal enum Phase { Walking, StopPlayer, OpenMenu, SelectTower, SelectSpecialization, WaitVerify, Done }

    private Phase _phase;
    private Phase _lastLoggedPhase = (Phase)(-1);
    private int _verifyTimeoutMs = 2000;

    private const int MinStationaryTicksBeforeBuild = 3;

    // Kind.None placeholder when the debug-stage throttle is not due — avoids a per-tick string alloc.
    private const string WaitingDebugMessage = "Waiting for build/upgrade...";

    private readonly Lock _planLock = new();

    internal BlightPlan? CurrentPlan { get; private set; }
    internal int CurrentCursor { get; private set; }

    // Render thread reads plan + cursor together (BlightOverlay draws the current step); the pair is published under the same lock the writers use so a SetPlan/AdvanceCursor mid-read can never expose a new plan with a stale cursor.
    internal (BlightPlan? Plan, int Cursor) GetPlanSnapshot()
    {
        lock (_planLock)
            return (CurrentPlan, CurrentCursor);
    }

    internal void SetPlan(BlightPlan plan)
    {
        lock (_planLock)
        {
            CurrentPlan = plan.WithCurrentStepIndex(0);
            CurrentCursor = 0;
        }
        _phase = Phase.Walking;
        _consecutiveFailures = 0;
        _stationaryTicks = 0;
        _lastBuildMenuClickTimestampMs = 0;
    }

    // True while a just-clicked build icon still needs time for the sub-menu to appear — re-clicking the toggle would close the menu.  Pure so the toggle-race guard is unit-testable.
    internal static bool ShouldWaitForBuildSubMenu(long lastBuildMenuClickTimestampMs, long nowMs, long waitMs)
        => lastBuildMenuClickTimestampMs != 0 && nowMs - lastBuildMenuClickTimestampMs < waitMs;

    internal BlightBuildAction Tick(
        GameController? gc,
        IReadOnlyList<LabelOnGround>? labels,
        BlightService service)
    {
        if (CurrentPlan == null || CurrentPlan.IsComplete)
            return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Plan complete or not set");

        BlightPlanStep? step = CurrentPlan.CurrentStep;
        if (step == null)
            return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "No current step");

        BlightPlanStep s = step.Value;
        BlightCachedTower? tower = BlightHelpers.FindTowerAt(service.KnownTowers, s.FoundationPosition);
        if (tower == null)
            return Fail($"Foundation not found at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");

        // Un-throttled executor skeleton: log every phase transition with the step, so the dump shows the exact executor flow (walk -> stop -> open -> select/spec -> verify) without the 10/sec Recent Stages throttle dropping entries.
        if (_phase != _lastLoggedPhase)
        {
            _lastLoggedPhase = _phase;
            service.AddExecutorEvent($"PHASE {_phase} step={s.ActionLabel} {s.TowerType} lvl{s.TargetLevel} at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0}) towerLvl={tower.UpgradeLevel}");
        }

        // Verify reads the entity cache (tower rank), not the label, so a transient label gap after a click can't stall confirmation.
        if (_phase == Phase.WaitVerify)
        {
            int delayMs = s.Action == BlightPlanAction.Build
                ? service.BlightTowerBuildDelayMs
                : service.BlightTowerUpgradeDelayMs;

            double elapsed = (Stopwatch.GetTimestamp() - _phaseStartTimestamp) * 1000.0 / Stopwatch.Frequency;
            if (elapsed < delayMs)
            {
                // Kind.None — the message is never published; only build it when the debug stage throttle is due so the per-tick waiting loop does not allocate a string each tick.
                string waitMsg = service.IsDebugStageDue
                    ? $"Waiting for {(s.Action == BlightPlanAction.Build ? "build" : "upgrade")}... ({elapsed:F0}ms)"
                    : WaitingDebugMessage;
                return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: waitMsg);
            }

            (int currentRank, int cachedLevel, int effectiveRank) = GetTowerRank(service, s.FoundationPosition);
            string? verifyDatId = service.GetTowerDatIdAt(s.FoundationPosition);
            if (service.IsDebugStageDue)
                service.AddDebugStage($"Executor: VERIFY -> rank={currentRank} cached={cachedLevel} effective={effectiveRank} target={s.TargetLevel} datId={verifyDatId ?? "?"}");
            if (effectiveRank >= s.TargetLevel)
            {
                // A maxed tower cannot be re-specialized in-game; when it shows a different spec, log it and advance rather than risk a mis-click.
                int specIndex = service.GetSpecialization(s.TowerType);
                if (IsSpecializationStep(specIndex, s.TargetLevel)
                    && TowerShowsOtherSpecialization(service, s, specIndex))
                {
                    service.AddDebugStage($"Executor: VERIFY -> tower has the WRONG specialization for {s.TowerType} (spec index {specIndex}); cannot re-specialize a maxed tower - advancing");
                    return AdvanceStep(service, s, $"Step complete (wrong spec accepted - cannot re-specialize): {s.TowerType} lvl {s.TargetLevel}");
                }
                service.AddDebugStage("Executor: VERIFY -> step verified - advancing cursor");
                return AdvanceStep(service, s, $"Step complete: {s.TowerType} lvl {s.TargetLevel}");
            }

            if (elapsed > _verifyTimeoutMs)
            {
                _consecutiveFailures++;
                service.AddDebugStage($"Executor: VERIFY -> timeout - rank={currentRank} cached={cachedLevel} target={s.TargetLevel} failures={_consecutiveFailures}");

                // A retry click on an already-upgraded tower would over-upgrade it (e.g. Seismic 3 -> 4).
                if (effectiveRank >= s.TargetLevel)
                {
                    service.AddDebugStage($"Executor: VERIFY -> rank {effectiveRank} meets target - advancing without re-click");
                    return AdvanceStep(service, s, $"Step verified via cache: {s.TowerType} lvl {s.TargetLevel}");
                }

                if (ShouldSkipAfterVerifyFailures(s.Action, _consecutiveFailures))
                {
                    service.AddDebugStage("Executor: VERIFY -> 3 consecutive failures - skipping step");
                    AdvanceCursor();
                    return new BlightBuildAction(BlightBuildActionKind.Error,
                        DebugMessage: $"Skipped after 3 failures: {s.TowerType} at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
                }

                // An unconfirmed upgrade is an affordability/state pause — never skip it, or the plan advances past an upgrade that never happened.
                if (_consecutiveFailures >= 3)
                {
                    _consecutiveFailures = 0;
                    _phase = Phase.Walking;
                    service.AddDebugStage("Executor: VERIFY -> upgrade unconfirmed after retries - waiting for currency/state");
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: $"Upgrade unconfirmed ({s.TowerType} lvl {s.TargetLevel}) - waiting for currency/state");
                }

                service.AddDebugStage("Executor: VERIFY -> timeout, retrying from OpenMenu");
                _phase = Phase.OpenMenu;
                return new BlightBuildAction(BlightBuildActionKind.None,
                    DebugMessage: "Retrying step after verification timeout");
            }

            return new BlightBuildAction(BlightBuildActionKind.None,
                DebugMessage: $"Awaiting verification... ({elapsed:F0}ms)");
        }

        if (_phase == Phase.Walking)
        {
            // A plan refresh can rewind to Build after the click has already created the tower.
            // Check the rank before looking for the build opener, which no longer exists.
            if (tower.UpgradeLevel >= s.TargetLevel || GetTowerRank(service, s.FoundationPosition).Effective >= s.TargetLevel)
                return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");

            RectangleF? controlRect = gc == null ? null : GetStepControlRect(labels, s, service);
            bool regionClickable = controlRect is { } rect && service.TryResolveMenuControlPoint(rect, out _);
            if (!CanApproachTowerControl(GetStepControlVisibility(labels, s, service)))
                return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Tower control hidden - waiting");
            bool walkReady = regionClickable;

            if (!walkReady)
            {
                if (!service.CanPathfind)
                    return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Tower control blocked; pathfinding disabled");

                service.AddDebugStage($"Executor: WALK - walking to {s.Action} {s.TowerType} at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
                BlightBuildAction action = ResolveWalkAction(s, service, gc, controlRect);

                // Only Build steps skip after a bounded wait — a foundation with no entity has nothing to click. Upgrade steps keep waiting for the entity to reappear.
                if (action.Kind == BlightBuildActionKind.None)
                {
                    if (s.Action == BlightPlanAction.Build)
                    {
                        if (!BlightHelpers.SameGridPosition(_walkWaitStepPos, s.FoundationPosition))
                        {
                            _walkWaitStepPos = s.FoundationPosition;
                            _walkWaitTicks = 0;
                        }
                        if (++_walkWaitTicks >= MaxWalkWaitTicksBeforeSkip)
                        {
                            _walkWaitTicks = 0;
                            return SkipStep($"Foundation not scannable at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
                        }
                    }
                    else
                    {
                        _walkWaitTicks = 0;
                    }
                }
                else
                {
                    _walkWaitTicks = 0;
                }
                return action;
            }
            _walkWaitTicks = 0;
            service.AddDebugStage("Executor: WALK - step ready, stopping player");
            _stationaryTicks = 0;
            _stopPlayerSawMovement = false;
            _phase = Phase.StopPlayer;
        }

        if (_phase == Phase.StopPlayer && gc != null)
        {
            RectangleF? controlRect = GetStepControlRect(labels, s, service);
            if (!CanApproachTowerControl(GetStepControlVisibility(labels, s, service)))
                return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Tower control hidden - waiting");
            if (controlRect is not { } rect || !service.TryResolveMenuControlPoint(rect, out _))
                return WalkCloser(service, gc, s, "Executor: STOP - tower control no longer clickable", controlRect);

            if (IsPlayerMoving(gc))
            {
                _stationaryTicks = 0;
                _stopPlayerSawMovement = true;
                Vector2? stopPos = GetPlayerScreenPos(gc);
                if (stopPos == null)
                {
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: "Player moving, waiting for stop position...");
                }
                service.AddDebugStage("Executor: STOP - player moving, clicking at feet to stop");
                return new BlightBuildAction(BlightBuildActionKind.ClickPosition, stopPos.Value,
                    "Stop player movement");
            }

            // Settle only applies when the player just arrived from pathfinding; a standing player opens immediately.
            _stationaryTicks++;
            if (_stationaryTicks < MinStationaryTicksBeforeBuild && _stopPlayerSawMovement)
            {
                return new BlightBuildAction(BlightBuildActionKind.None,
                    DebugMessage: $"Waiting for player to settle ({_stationaryTicks}/{MinStationaryTicksBeforeBuild})...");
            }
            service.AddDebugStage("Executor: STOP - player stationary, opening menu");
            _phaseStartTimestamp = Stopwatch.GetTimestamp();
            _phase = Phase.OpenMenu;
        }

        Element? labelElement = labels == null ? null : FindLabelAt(labels, s.FoundationPosition, service);
        if (labelElement == null)
            return WalkCloser(service, gc, s, "Executor: OPEN - label unavailable - walking closer");
        service.AddDebugStage("Executor: OPEN - label found");

        if (_phase == Phase.OpenMenu)
        {
            if (s.Action == BlightPlanAction.Upgrade)
            {
                (_, _, int effectiveRank) = GetTowerRank(service, s.FoundationPosition);
                if (effectiveRank >= s.TargetLevel)
                    return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");

                // Upgrade is a single click on the upgrade icon (Child[3]); Fireball 3→4 opens a spec menu.
                if (BlightMenuInteractions.IsTowerMenuOpen(labelElement))
                {
                    service.AddDebugStage("Executor: OPEN - UPGRADE menu already open, going directly to specialization selection");
                    _phaseStartTimestamp = Stopwatch.GetTimestamp();
                    _phase = Phase.SelectSpecialization;
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: "Upgrade menu open, entering specialization selection");
                }
                else
                {
                    bool canUpgrade = BlightMenuInteractions.CanAffordUpgrade(labelElement);
                    service.AddDebugStage($"Executor: OPEN - UPGRADE canAfford={canUpgrade}");

                    if (!canUpgrade)
                    {
                        // Icon invisible — the tower may already meet the target; check the actual rank.
                        (int currentRank, _, _) = GetTowerRank(service, s.FoundationPosition);
                        service.AddDebugStage($"Executor: OPEN -> UPGRADE canAfford=false, actual rank={currentRank} target={s.TargetLevel}");

                        if (currentRank >= s.TargetLevel)
                        {
                            service.AddDebugStage($"Executor: OPEN -> upgrade already done (rank={currentRank} >= target={s.TargetLevel}) - advancing");
                            return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                        }

                        // Invisible icon = can't afford: pause and wait for currency rather than skip the step.
                        service.AddDebugStage($"Executor: OPEN -> UPGRADE cannot afford (rank={currentRank} < target={s.TargetLevel}) - waiting for currency");
                        return new BlightBuildAction(BlightBuildActionKind.None,
                            DebugMessage: $"Cannot afford upgrade ({s.TowerType} lvl {s.TargetLevel}) - waiting for currency");
                    }

                    Element? upgradeIcon = BlightMenuInteractions.GetMenuChildElement(labelElement, 3);
                    if (upgradeIcon == null)
                        return Fail("Upgrade icon not found");

                    return MenuClick(Phase.SelectSpecialization,
                        $"Upgrade {s.TowerType} - lvl {s.TargetLevel}", gc, service, s, labelElement,
                        upgradeIcon, opensUpgrade: true);
                }
            }

            // Check menu state FIRST — the affordability check becomes unreliable after the menu opens.
            bool menuPopulated = BlightMenuInteractions.IsTowerMenuOpen(labelElement);
            service.AddDebugStage($"Executor: OPEN -> menuPopulated={menuPopulated}");

            if (menuPopulated)
            {
                service.AddDebugStage("Executor: OPEN - menu populated, entering SelectTower");
                _lastBuildMenuClickTimestampMs = 0;
                _consecutiveFailures = 0;
                _phaseStartTimestamp = Stopwatch.GetTimestamp();
                _phase = Phase.SelectTower;
            }
            else
            {
                bool canBuild = BlightMenuInteractions.CanAffordBuild(labelElement);
                service.AddDebugStage($"Executor: OPEN - BUILD canAfford={canBuild}");
                if (!canBuild)
                {
                    // Build icon invisible — advance if the foundation already meets the target.
                    (_, _, int effectiveRank) = GetTowerRank(service, s.FoundationPosition);
                    if (effectiveRank >= s.TargetLevel)
                    {
                        service.AddDebugStage($"Executor: OPEN -> build already done (rank={effectiveRank} >= target={s.TargetLevel}) - advancing");
                        return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                    }

                    // Same affordability pause as upgrades: wait for currency.
                    service.AddDebugStage($"Executor: OPEN - BUILD cannot afford (rank={effectiveRank} < target={s.TargetLevel}) - waiting for currency");
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: $"Cannot afford build ({s.TowerType}) - waiting for currency");
                }

                // The build icon is a TOGGLE — re-clicking it while the sub-menu is still opening closes the menu again.  After a build-icon click, wait for the sub-menu to appear (or a retry timeout) instead of re-clicking on the next tick.
                long nowMs = Environment.TickCount64;
                if (ShouldWaitForBuildSubMenu(_lastBuildMenuClickTimestampMs, nowMs, MenuSubMenuWaitMs))
                {
                    service.AddDebugStage("Executor: OPEN - build icon clicked, waiting for sub-menu");
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: "Waiting for tower sub-menu...");
                }
                Element? buildIcon = BlightMenuInteractions.GetMenuChildElement(labelElement, 2);
                if (buildIcon == null)
                    return Fail("Build icon not found");

                BlightBuildAction open = MenuClick(Phase.OpenMenu, "Open tower menu (build icon)", gc, service, s,
                    labelElement, buildIcon);
                if (open.IsMenuClick)
                    _lastBuildMenuClickTimestampMs = nowMs;
                return open;
            }
        }

        double menuElapsed = (Stopwatch.GetTimestamp() - _phaseStartTimestamp) * 1000.0 / Stopwatch.Frequency;
        if (menuElapsed < 80 && _phase != Phase.WaitVerify && _phase != Phase.SelectSpecialization)
            return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Waiting for menu...");

        if (_phase == Phase.SelectTower)
        {
            if (service.TryEnableFreeTowerConstruction(labelElement))
                return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Free towers detected; replanned upgrades");

            int slotIndex = BlightMenuInteractions.GetStepSlotIndex(s, -1);
            Element? towerSlot = BlightMenuInteractions.GetMenuSlot(labelElement, slotIndex);
            if (towerSlot?.IsVisible != true)
            {
                long childCount = 0;
                try
                {
                    if (labelElement.ChildCount > 0)
                    {
                        Element c0 = labelElement.GetChildAtIndex(0);
                        if (c0 != null && c0.ChildCount > 3)
                            childCount = c0.GetChildAtIndex(3)?.ChildCount ?? 0;
                    }
                }
                catch { }
                service.AddDebugStage($"Executor: SELECT - {s.TowerType}(idx={(int)s.TowerType}) not found - menu has {childCount} children - FAIL #{_consecutiveFailures + 1}");
                return Fail($"Tower type {s.TowerType} not found in menu");
            }

            return MenuClick(Phase.WaitVerify, $"Build {s.TowerType} - lvl {s.TargetLevel}", gc, service, s, labelElement, towerSlot);
        }

        if (_phase == Phase.SelectSpecialization)
        {
            int specIndex = service.GetSpecialization(s.TowerType);
            bool isSpecializationStep = IsSpecializationStep(specIndex, s.TargetLevel);

            // Never re-click a tower that already meets the target — a stale menu or already-landed click would over-upgrade it (e.g. Seismic 3 -> 4) or pick the wrong spec.
            (int currentRank, _, _) = GetTowerRank(service, s.FoundationPosition);
            if (SystemMath.Max(currentRank, tower.UpgradeLevel) >= s.TargetLevel)
            {
                service.AddDebugStage($"Executor: SPEC - tower already at rank {SystemMath.Max(currentRank, tower.UpgradeLevel)} >= target {s.TargetLevel} - advancing without clicking");
                return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
            }

            bool menuOpen = BlightMenuInteractions.IsTowerMenuOpen(labelElement);
            service.AddDebugStage($"Executor: SPEC - menuOpen={menuOpen} ({menuElapsed:F0}ms)");

            if (!menuOpen)
            {
                // Plain upgrades are a single icon click — no sub-menu to wait for; only spec upgrades wait.
                if (!isSpecializationStep || menuElapsed > 300)
                {
                    service.AddDebugStage(isSpecializationStep
                        ? "Executor: SPEC - no specialization menu after 300ms, proceeding to Verify"
                        : "Executor: SPEC - plain upgrade (single click, no sub-menu), proceeding to Verify");
                    _phaseStartTimestamp = Stopwatch.GetTimestamp();
                    _phase = Phase.WaitVerify;
                }
                else
                {
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: $"Waiting for specialization menu... ({menuElapsed:F0}ms)");
                }
            }
            else
            {
                if (!isSpecializationStep)
                {
                    // Only the specialization menu has more than one visible button; a tower at max plain shows it even when the lagging rank read reports below max. A plain upgrade must never click a spec button (Seismic 3 -> 4).
                    int visibleUpgradeButtons = BlightMenuInteractions.CountVisibleUpgradeButtons(labelElement);
                    if (visibleUpgradeButtons > 1)
                    {
                        service.AddDebugStage($"Executor: SPEC - plain {s.TowerType} menu shows {visibleUpgradeButtons} buttons (max plain) - advancing without clicking");
                        return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                    }

                    // Plain upgrade — the at-target guard already ran, so click the next-tier button.
                    var plain = BlightMenuInteractions.GetPlainUpgradeButton(labelElement);
                    if (plain == null)
                    {
                        // No visible button — can't afford the tier: pause for currency.
                        service.AddDebugStage($"Executor: SPEC - no visible upgrade button for plain {s.TowerType} upgrade - waiting for currency");
                        return new BlightBuildAction(BlightBuildActionKind.None,
                            DebugMessage: $"No upgrade button visible for {s.TowerType} - waiting for currency");
                    }

                    // A plain upgrade must never click a spec button — the tower is at max plain, so the step is already complete.
                    if (ShouldSkipPlainUpgradeClick(plain.Value.UpgradeId))
                    {
                        service.AddDebugStage($"Executor: SPEC - plain upgrade button is a specialization ({plain.Value.UpgradeId}) - tower at max plain, advancing without clicking");
                        return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                    }

                    // The UpgradeResult dat read is unreliable in this build — a null id means we CANNOT verify the visible button is a real tier button rather than a specialization on a tower already at max plain. Never click blind: if the tower's live rank is already at max plain (3), the step is done and clicking would over-upgrade it.
                    if (plain.Value.UpgradeId == null)
                    {
                        (_, _, int effectiveRank) = GetTowerRank(service, s.FoundationPosition);
                        if (effectiveRank >= BlightTowerData.MaxUpgradeLevel - 1)
                        {
                            service.AddDebugStage($"Executor: SPEC - plain upgrade button unreadable, tower at rank {effectiveRank} (max plain) - advancing without clicking");
                            return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                        }
                    }

                    return MenuClick(Phase.WaitVerify,
                        $"Upgrade {s.TowerType} - lvl {s.TargetLevel}", gc, service, s, labelElement, plain.Value.Button);
                }

                // Prefer the catalog menu index for every type; the UpgradeResult dat read is unreliable in this build.
                TowerSpecialization spec = (TowerSpecialization)specIndex;
                int menuIndex = BlightMenuInteractions.GetStepSlotIndex(s, specIndex);
                string targetTowerId = BlightTowerData.GetSpecializationTowerId(s.TowerType, spec);

                Element? specSlot = BlightMenuInteractions.GetMenuSlot(labelElement, menuIndex);
                if (specSlot?.IsVisible != true)
                {
                    // Spec button not visible — can't afford the 3→4 upgrade: pause for currency.
                    service.AddDebugStage($"Executor: SPEC - '{targetTowerId}' (specIndex={specIndex}) not visible - waiting for currency");
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: $"Cannot afford specialization '{targetTowerId}' - waiting for currency");
                }

                return MenuClick(Phase.WaitVerify,
                    $"Select spec {targetTowerId} for {s.TowerType} - lvl {s.TargetLevel}", gc, service, s, labelElement,
                    specSlot);
            }
        }

        return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Idle");
    }

    private Element? FindLabelAt(
        IReadOnlyList<LabelOnGround> labels,
        NumVector2 pos,
        BlightService service)
    {
        if (_hasCachedLabel && ReferenceEquals(_cachedLabels, labels)
            && BlightHelpers.SameGridPosition(_cachedStepPos, pos))
            return _cachedLabelElement;

        _hasCachedLabel = false;
        _cachedLabels = labels;
        _cachedStepPos = pos;
        _cachedLabelElement = null;

        for (int i = 0; i < labels.Count; i++)
        {
            if (!service.IsBlightFoundationOrTowerLabel(labels[i]))
                continue;
            Entity? entity = BlightService.ResolveEntity(labels[i]);
            if (entity == null) continue;
            NumVector2 ePos = BlightHelpers.GetGridPosition(entity);
            if (BlightHelpers.SameGridPosition(ePos, pos))
            {
                _cachedLabelElement = BlightService.ResolveLabelElement(labels[i]);
                _hasCachedLabel = true;
                return _cachedLabelElement;
            }
        }
        return null;
    }

    // Un-throttled menu-state trail for the debug dump: exactly what the executor sees (label/menu child tree with indexes, visibility, rects, best-effort dat ids) right before it clicks, so a wrong button (e.g. Fireball 3->4 landing on Flamethrower) is diagnosable from the dump.
    private static void LogMenuState(BlightService service, Element labelElement, string phase)
        => service.AddExecutorEvent($"MENU {phase}: {BlightMenuInteractions.BuildMenuSnapshot(labelElement)}");

    private bool IsPlayerMoving(GameController gc)
    {
        try
        {
            if (gc?.Player == null) return false;
            NumVector2 current = new(gc.Player.GridPosNum.X, gc.Player.GridPosNum.Y);
            // Low threshold so slow drift (which still moves the camera and invalidates resolved screen positions) is caught before a menu click.
            bool moved = MathF.Abs(current.X - _lastPlayerGridPos.X) > 0.25f
                      || MathF.Abs(current.Y - _lastPlayerGridPos.Y) > 0.25f;
            _lastPlayerGridPos = current;
            return moved;
        }
        catch { return false; }
    }

    private static Vector2? GetPlayerScreenPos(GameController gc)
    {
        try
        {
            Camera? camera = gc.Game?.IngameState?.Camera;
            if (camera == null || gc.Player == null) return null;
            // WorldToScreen projects from WORLD (PosNum) — feeding GridPosNum puts the stop-click in the wrong place.
            NumVector2 screen = camera.WorldToScreen(gc.Player.PosNum);
            return new Vector2(screen.X, screen.Y);
        }
        catch { return null; }
    }

    // Resolves the executor's walk need: walk to the entity when cached, else to the persisted position when off-screen (the entity walk returns no target for a streamed-out foundation).
    private BlightBuildAction ResolveWalkAction(BlightPlanStep step, BlightService service, GameController? gc, RectangleF? blockedControl = null)
    {
        if (!service.CanPathfind)
            return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Blight tower pathfinding disabled");
        bool hasWalkEntity = service.GetBestEntityAtPosition(step.FoundationPosition) != null;
        bool positionOffScreen = gc != null && IsGridPositionOffScreen(gc, step.FoundationPosition);
        return ResolveWalkActionKind(hasWalkEntity, positionOffScreen) switch
        {
            BlightBuildActionKind.WalkToTarget => new BlightBuildAction(BlightBuildActionKind.WalkToTarget,
                DebugMessage: $"Walking to ({step.FoundationPosition.X:F0},{step.FoundationPosition.Y:F0})",
                GridPosition: step.FoundationPosition, WalkRegion: blockedControl),
            BlightBuildActionKind.WalkToPosition => new BlightBuildAction(BlightBuildActionKind.WalkToPosition,
                DebugMessage: $"Walking toward ({step.FoundationPosition.X:F0},{step.FoundationPosition.Y:F0})",
                GridPosition: step.FoundationPosition, WalkRegion: blockedControl),
            _ => new BlightBuildAction(BlightBuildActionKind.None,
                DebugMessage: "Foundation not yet scannable, waiting..."),
        };
    }

    internal static BlightBuildActionKind ResolveWalkActionKind(bool hasWalkEntity, bool positionOffScreen)
    {
        if (hasWalkEntity)
            return BlightBuildActionKind.WalkToTarget;
        return positionOffScreen
            ? BlightBuildActionKind.WalkToPosition
            : BlightBuildActionKind.None;
    }

    private static bool IsGridPositionOffScreen(GameController? gc, NumVector2 gridPos)
    {
        try
        {
            if (gc?.Game?.IngameState?.Camera is not { } camera)
                return false;
            return BlightHelpers.IsGridPosOffScreen(camera, GameWindowRectResolver.Resolve(gc).Size, gridPos);
        }
        catch { return false; }
    }


    internal bool IsStepWalkReady(
        IReadOnlyList<LabelOnGround>? labels,
        GameController? gc,
        BlightService service)
    {
        return gc != null && CurrentPlan?.CurrentStep is { } step
            && GetStepControlRect(labels, step, service) is { } region
            && service.TryResolveMenuControlPoint(region, out _);
    }

    private RectangleF? GetStepControlRect(IReadOnlyList<LabelOnGround>? labels, BlightPlanStep step, BlightService service)
    {
        if (labels == null || labels.Count == 0 || FindLabelAt(labels, step.FoundationPosition, service) is not { } label)
            return null;
        return BlightMenuInteractions.GetControlRect(ResolveApproachControl(label, step, service));
    }

    private bool? GetStepControlVisibility(IReadOnlyList<LabelOnGround>? labels, BlightPlanStep step, BlightService service)
    {
        try
        {
            if (labels == null || FindLabelAt(labels, step.FoundationPosition, service) is not { } label)
                return null;
            return ResolveApproachControl(label, step, service)?.IsVisible;
        }
        catch { return null; }
    }

    // A missing or streamed-out label cannot establish whether the control is hidden.
    internal static bool CanApproachTowerControl(bool? visible) => visible != false;

    private static Element? ResolveApproachControl(Element label, BlightPlanStep step, BlightService service)
    {
        if (!BlightMenuInteractions.IsTowerMenuOpen(label))
            return BlightMenuInteractions.GetMenuChildElement(label, BlightMenuInteractions.MenuChildIndexForStep(step.Action));

        int specialization = step.IsSpecializationStep ? service.GetSpecialization(step.TowerType) : -1;
        return BlightMenuInteractions.GetMenuSlot(label, BlightMenuInteractions.GetStepSlotIndex(step, specialization));
    }

    // The pipeline's pathfinding stop condition mirrors EVERY way the executor asks to walk closer.
    internal bool WantsWalkForCurrentStep(
        GameController? gc, BlightService service, IReadOnlyList<LabelOnGround>? labels)
    {
        if (gc == null || !service.CanPathfind || CurrentPlan == null || CurrentPlan.IsComplete)
            return false;
        BlightPlanStep? step = CurrentPlan.CurrentStep;
        if (step == null)
            return false;

        // The executor's phase is the source of truth: once it leaves Walking it needs the player stopped to click, so pathfinding must stand down.
        if (!WantsWalkForCurrentPhase(_phase))
            return false;

        return GetTowerRank(service, step.Value.FoundationPosition).Effective < step.Value.TargetLevel
            && CanApproachTowerControl(GetStepControlVisibility(labels, step.Value, service))
            && !IsStepWalkReady(labels, gc, service);
    }

    internal static bool WantsWalkForCurrentPhase(Phase phase) => phase == Phase.Walking;

    private BlightBuildAction Fail(string reason)
    {
        if (++_consecutiveFailures >= 3)
        {
            AdvanceCursor();
            return new BlightBuildAction(BlightBuildActionKind.Error,
                DebugMessage: $"Skipped after 3 failures: {reason}");
        }
        _phase = Phase.Walking;
        return new BlightBuildAction(BlightBuildActionKind.Error, DebugMessage: reason);
    }

    // Advances the cursor past a step that cannot be completed (best-effort skip, spec §4.7).
    private BlightBuildAction SkipStep(string reason)
    {
        AdvanceCursor();
        return new BlightBuildAction(BlightBuildActionKind.Error,
            DebugMessage: $"Skipped step: {reason}");
    }

    // Shared "step verified → advance" tail: updates the cached level, resets failures, advances the cursor and rewinds to Walking.
    private BlightBuildAction AdvanceStep(BlightService service, BlightPlanStep s, string message)
    {
        service.UpdateKnownTowerLevel(s.FoundationPosition, s.TowerType, s.TargetLevel);
        AdvanceCursor();
        return new BlightBuildAction(BlightBuildActionKind.Complete, DebugMessage: message);
    }

    // Shared cursor-advance tail used by every skip/advance path.
    private void AdvanceCursor()
    {
        _consecutiveFailures = 0;
        lock (_planLock)
        {
            CurrentCursor++;
            if (CurrentPlan != null)
                CurrentPlan = CurrentPlan.WithAdvancedCursor();
        }
        _phase = Phase.Walking;
        _lastBuildMenuClickTimestampMs = 0;
    }

    // Rank of the tower at a step position from the live entity path and the cached tower level. The path is read FRESH (not from the entity path cache): when a tower is upgraded in place the cached path can lag one rank behind reality, and that stale rank is exactly what makes the verify/retry loop re-click an already-upgraded tower (e.g. Seismic 3 -> 4, Fireball -> Flamethrower). The executor ticks at most a few times per second, so a fresh read is cheap.
    private static (int Rank, int Cached, int Effective) GetTowerRank(BlightService service, NumVector2 position)
    {
        // Prefer the freshly-scanned TOWER entity: the cached foundation entity's path stays a foundation path (rank 0) even after in-place upgrades, so reading it makes every upgrade verify fail and the retry loop over-click (Seismic 3 -> 4). The tower entity path carries the live rank and is read fresh.
        Entity? entity = service.GetTowerEntityAt(position) ?? service.GetBestEntityAtPosition(position);
        int rank = entity != null
            ? BlightHelpers.DetectUpgradeRankFromPath(BlightService.GetEntityPath(entity))
            : 0;
        int cached = BlightHelpers.FindTowerAt(service.KnownTowers, position)?.UpgradeLevel ?? 0;
        return (rank, cached, SystemMath.Max(rank, cached));
    }

    // The step's click target is off-screen or the label is missing — rewind to Walking so the player approaches closer before the next attempt.
    private BlightBuildAction WalkCloser(BlightService service, GameController? gc, BlightPlanStep s, string message, RectangleF? blockedControl = null)
    {
        service.AddDebugStage(message);
        _phase = Phase.Walking;
        return ResolveWalkAction(s, service, gc, blockedControl);
    }

    // Safety: never click a menu slot while the player is moving — a stale screen rect could build a different tower.
    private BlightBuildAction MenuClick(Phase nextPhase, string message, GameController? gc,
        BlightService service, BlightPlanStep step, Element label, Element? expected, bool opensUpgrade = false)
    {
        if (gc?.Player == null || expected == null || IsPlayerMoving(gc))
        {
            _phase = Phase.StopPlayer;
            return new BlightBuildAction(BlightBuildActionKind.None,
                DebugMessage: "Player still moving - waiting to stop before menu click");
        }
        if (!expected.IsVisible)
            return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Tower control hidden - waiting for currency");
        if (BlightMenuInteractions.GetControlRect(expected) is not { } controlRect)
            return Fail("Tower control has no readable rectangle");
        if (!service.TryResolveMenuControlPoint(controlRect, out Vector2 clickPos))
            return WalkCloser(service, gc, step, $"Executor: blocked control - {message}", controlRect);

        LogMenuState(service, label, message);
        _phaseStartTimestamp = Stopwatch.GetTimestamp();
        _phase = nextPhase;
        NumVector2 playerPosition = gc.Player.GridPosNum;
        BlightPlan? plan = CurrentPlan;
        return new BlightBuildAction(BlightBuildActionKind.ClickPosition, clickPos, message, IsMenuClick: true,
            ValidateHover: hover =>
            {
                if (!ReferenceEquals(plan, CurrentPlan)
                    || NumVector2.DistanceSquared(playerPosition, gc.Player.GridPosNum) > 0.01f
                    || !expected.IsVisible
                    || BlightMenuInteractions.GetControlRect(expected) is not { } region
                    || !region.Contains(clickPos)
                    || !BlightMenuInteractions.IsHoverInside(hover, expected)
                    || !service.IsMenuPointClickable(clickPos))
                    return false;

                if (step.Action != BlightPlanAction.Upgrade)
                    return true;

                (int rank, _, int effective) = GetTowerRank(service, step.FoundationPosition);
                if (rank != step.TargetLevel - 1 || effective >= step.TargetLevel)
                    return false;

                if (opensUpgrade)
                    return !BlightMenuInteractions.IsTowerMenuOpen(label);

                if (step.IsSpecializationStep)
                {
                    string? id = BlightMenuInteractions.ReadUpgradeResultTowerId(expected);
                    int specialization = service.GetSpecialization(step.TowerType);
                    return specialization >= 0 && (id == null || string.Equals(id,
                        BlightTowerData.GetSpecializationTowerId(step.TowerType, (TowerSpecialization)specialization),
                        StringComparison.OrdinalIgnoreCase));
                }

                // A hidden second specialization option can leave only one visible child.
                return BlightMenuInteractions.GetPlainUpgradeButton(label) is { } button
                    && button.Button.Address == expected.Address
                    && CanClickPlainUpgrade(BlightMenuInteractions.GetMenuChildElement(label, 3)?.ChildCount ?? 0,
                        button.UpgradeId, step.TowerType, step.TargetLevel);
            },
            OnRejected: () =>
            {
                if (ReferenceEquals(plan, CurrentPlan))
                {
                    _phase = Phase.Walking;
                    _lastBuildMenuClickTimestampMs = 0;
                }
            });
    }

    internal void Reset()
    {
        // Preserve the plan — only rewind to Walking so it can approach from a different angle.
        lock (_planLock)
            CurrentCursor = 0;
        _phase = Phase.Walking;
        _consecutiveFailures = 0;
        _stationaryTicks = 0;
        _phaseStartTimestamp = 0;
        _lastBuildMenuClickTimestampMs = 0;
    }

    internal void ClearPlan()
    {
        lock (_planLock)
        {
            CurrentPlan = null;
            CurrentCursor = 0;
        }
        _phase = Phase.Walking;
        _consecutiveFailures = 0;
        _stationaryTicks = 0;
        _phaseStartTimestamp = 0;
        _lastBuildMenuClickTimestampMs = 0;
    }

    // A step is a specialization step when it targets the spec tier (4) AND the strategy chose a specialization for that type. The tower's CURRENT level is deliberately NOT part of the gate: a cached level that lags reality (says 2 while the tower is really at 3) would otherwise degrade a Fireball 3->4 step to the plain-upgrade path, which clicks the first visible button — on a maxed tower that is a SPECIALIZATION button (Flamethrower for Fireball), so the tower gets the wrong spec. The at-target guard handles "already done" separately.
    internal static bool IsSpecializationStep(int specialization, int targetLevel)
        => specialization >= 0 && targetLevel > 3;

    // True when the tower at the step clearly has a DIFFERENT specialization of the same base type; fails open on unreadable state. The dat id is the authoritative signal — the entity path only carries the base type + rank (e.g. "BlightTowerFlameRank4"), so path-based detection alone can never distinguish Meteor from Flamethrower.
    private static bool TowerShowsOtherSpecialization(BlightService service, BlightPlanStep s, int specIndex)
    {
        if (specIndex < 0)
            return false;

        string? datId = service.GetTowerDatIdAt(s.FoundationPosition);
        if (!string.IsNullOrEmpty(datId))
            return DatIdShowsOtherSpecialization(datId, s.TowerType, (TowerSpecialization)specIndex);

        Entity? entity = service.GetBestEntityAtPosition(s.FoundationPosition);
        if (entity == null)
            return false;
        string? path = BlightService.GetEntityPath(entity);
        return PathShowsOtherSpecialization(path ?? string.Empty, s.TowerType, (TowerSpecialization)specIndex);
    }

    internal static bool DatIdShowsOtherSpecialization(string datId, BlightTowerType type, TowerSpecialization spec)
    {
        if (string.IsNullOrEmpty(datId))
            return false;
        string targetId = BlightTowerData.GetSpecializationTowerId(type, spec);
        if (datId.Equals(targetId, StringComparison.OrdinalIgnoreCase))
            return false; // the chosen spec — correct

        for (int i = 0; i < BlightTowerData.Catalog.Length; i++)
        {
            BlightTowerInfo info = BlightTowerData.Catalog[i];
            if (info.Type == type
                && info.Specialization != TowerSpecialization.None
                && info.Specialization != spec
                && datId.Equals(info.DatId, StringComparison.OrdinalIgnoreCase))
                return true; // dat id names a DIFFERENT spec tower — wrong click
        }
        return false;
    }

    internal static bool PathShowsOtherSpecialization(string path, BlightTowerType type, TowerSpecialization spec)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        string targetId = BlightTowerData.GetSpecializationTowerId(type, spec);
        if (targetId.Length > 0 && path.Contains(targetId, StringComparison.OrdinalIgnoreCase))
            return false; // the chosen spec — correct

        for (int i = 0; i < BlightTowerData.Catalog.Length; i++)
        {
            BlightTowerInfo info = BlightTowerData.Catalog[i];
            if (info.Type == type
                && info.Specialization != TowerSpecialization.None
                && info.Specialization != spec
                && path.Contains(info.DatId, StringComparison.OrdinalIgnoreCase))
                return true; // path names a DIFFERENT spec tower — wrong click
        }
        return false;
    }

    // A plain upgrade must never click a spec button — that button means the tower is already at max plain, so the step is complete and clicking would over-upgrade it.
    internal static bool ShouldSkipPlainUpgradeClick(string? upgradeButtonId)
        => upgradeButtonId != null && BlightTowerData.IsSpecializationTowerId(upgradeButtonId);

    internal static bool CanClickPlainUpgrade(long childCount, string? id, BlightTowerType type, int targetLevel)
        => childCount == 1 && targetLevel is 2 or 3
            && (id == null || (!ShouldSkipPlainUpgradeClick(id)
                && BlightTowerData.MapTowerIdToType(id) == type
                && id.EndsWith(targetLevel == 2 ? '2' : '3')));

    // Verify-failure skip is BUILD-only; an unconfirmed upgrade is a pause, never a skip.
    internal static bool ShouldSkipAfterVerifyFailures(BlightPlanAction action, int consecutiveFailures)
        => action != BlightPlanAction.Upgrade && consecutiveFailures >= 3;
}
