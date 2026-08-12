namespace ClickIt.Features.Blight.Planning;

internal sealed class BlightPlanExecutor
{
    private long _phaseStartTimestamp;
    private int _consecutiveFailures;
    private NumVector2 _lastPlayerGridPos;
    private int _stationaryTicks;

    private bool _stopPlayerSawMovement;

    // Timestamp of the last build-icon click (OpenMenu BUILD).  The build icon is a TOGGLE — a
    // re-click while the sub-menu is still opening closes it again — so the executor waits for the
    // sub-menu to appear instead of re-clicking every tick.
    private long _lastBuildMenuClickTimestampMs;
    private const long MenuSubMenuWaitMs = 500;

    private const int MaxWalkWaitTicksBeforeSkip = 25;
    private int _walkWaitTicks;
    private NumVector2 _walkWaitStepPos;

    // Foundation label resolution is the most expensive per-tick read; cache per labels-list reference
    // (the labels refresh ~50ms, bounding staleness).
    private IReadOnlyList<LabelOnGround>? _cachedLabels;
    private NumVector2 _cachedStepPos;
    private bool _hasCachedLabel;
    private Element? _cachedLabelElement;

    private enum Phase { Walking, StopPlayer, OpenMenu, SelectTower, SelectSpecialization, WaitVerify, Done }

    private Phase _phase;
    private Phase _lastLoggedPhase = (Phase)(-1);
    private int _verifyTimeoutMs = 2000;

    private const int MinStationaryTicksBeforeBuild = 3;

    // Kind.None placeholder when the debug-stage throttle is not due — avoids a per-tick string alloc.
    private const string WaitingDebugMessage = "Waiting for build/upgrade...";

    internal BlightPlan? CurrentPlan { get; private set; }
    internal int CurrentCursor { get; private set; }

    internal void SetPlan(BlightPlan plan)
    {
        CurrentPlan = plan.WithCurrentStepIndex(0);
        CurrentCursor = 0;
        _phase = Phase.Walking;
        _consecutiveFailures = 0;
        _stationaryTicks = 0;
        _lastBuildMenuClickTimestampMs = 0;
    }

    // True while a just-clicked build icon still needs time for the sub-menu to appear — re-clicking
    // the toggle would close the menu.  Pure so the toggle-race guard is unit-testable.
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

        // Un-throttled executor skeleton: log every phase transition with the step, so the dump shows
        // the exact executor flow (walk -> stop -> open -> select/spec -> verify) without the
        // 10/sec Recent Stages throttle dropping entries.
        if (_phase != _lastLoggedPhase)
        {
            _lastLoggedPhase = _phase;
            service.AddExecutorEvent($"PHASE {_phase} step={s.ActionLabel} {s.TowerType} lvl{s.TargetLevel} at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0}) towerLvl={tower.UpgradeLevel}");
        }

        // Verify reads the entity cache (tower rank), not the label, so a transient label gap after
        // a click can't stall confirmation.
        if (_phase == Phase.WaitVerify)
        {
            int delayMs = s.Action == BlightPlanAction.Build
                ? service.BlightTowerBuildDelayMs
                : service.BlightTowerUpgradeDelayMs;

            double elapsed = (Stopwatch.GetTimestamp() - _phaseStartTimestamp) * 1000.0 / Stopwatch.Frequency;
            if (elapsed < delayMs)
            {
                // Kind.None — the message is never published; only build it when the debug stage
                // throttle is due so the per-tick waiting loop does not allocate a string each tick.
                string waitMsg = service.IsDebugStageDue
                    ? $"Waiting for {(s.Action == BlightPlanAction.Build ? "build" : "upgrade")}... ({elapsed:F0}ms)"
                    : WaitingDebugMessage;
                return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: waitMsg);
            }

            (int currentRank, int cachedLevel, int effectiveRank) = GetTowerRank(service, s.FoundationPosition);
            string? verifyDatId = service.GetTowerDatIdAt(s.FoundationPosition);
            if (service.IsDebugStageDue)
                service.AddDebugStage($"Executor: VERIFY → rank={currentRank} cached={cachedLevel} effective={effectiveRank} target={s.TargetLevel} datId={verifyDatId ?? "?"}");
            if (effectiveRank >= s.TargetLevel)
            {
                // A spec step must land on the strategy's chosen specialization. A level-4 tower
                // cannot be re-specialized in-game, so when the tower shows a DIFFERENT spec there is
                // no safe re-click that can fix it (the spec menu index is the only mechanism, and
                // clicking a maxed tower's spec slot risks a mis-click). Log it clearly and advance —
                // the prior "re-select" path was dead code because SelectSpecialization's at-target
                // guard advanced without clicking on the very next tick.
                int specIndex = service.GetSpecialization(s.TowerType);
                if (IsSpecializationStep(specIndex, s.TargetLevel)
                    && TowerShowsOtherSpecialization(service, s, specIndex))
                {
                    service.AddDebugStage($"Executor: VERIFY → tower has the WRONG specialization for {s.TowerType} (spec index {specIndex}); cannot re-specialize a maxed tower - advancing");
                    return AdvanceStep(service, s, $"Step complete (wrong spec accepted - cannot re-specialize): {s.TowerType} lvl {s.TargetLevel}");
                }
                service.AddDebugStage($"Executor: VERIFY → step verified - advancing cursor");
                return AdvanceStep(service, s, $"Step complete: {s.TowerType} lvl {s.TargetLevel}");
            }

            if (elapsed > _verifyTimeoutMs)
            {
                _consecutiveFailures++;
                service.AddDebugStage($"Executor: VERIFY → timeout - rank={currentRank} cached={cachedLevel} target={s.TargetLevel} failures={_consecutiveFailures}");

                // A retry click on an already-upgraded tower would over-upgrade it (e.g. Seismic 3 -> 4).
                if (effectiveRank >= s.TargetLevel)
                {
                    service.AddDebugStage($"Executor: VERIFY → rank {effectiveRank} meets target — advancing without re-click");
                    return AdvanceStep(service, s, $"Step verified via cache: {s.TowerType} lvl {s.TargetLevel}");
                }

                if (ShouldSkipAfterVerifyFailures(s.Action, _consecutiveFailures))
                {
                    service.AddDebugStage("Executor: VERIFY → 3 consecutive failures - skipping step");
                    AdvanceCursor();
                    return new BlightBuildAction(BlightBuildActionKind.Error,
                        DebugMessage: $"Skipped after 3 failures: {s.TowerType} at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
                }

                // An unconfirmed upgrade is an affordability/state pause — never skip it, or the plan
                // advances past an upgrade that never happened.
                if (_consecutiveFailures >= 3)
                {
                    _consecutiveFailures = 0;
                    _phase = Phase.Walking;
                    service.AddDebugStage($"Executor: VERIFY → upgrade unconfirmed after retries - waiting for currency/state");
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: $"Upgrade unconfirmed ({s.TowerType} lvl {s.TargetLevel}) - waiting for currency/state");
                }

                service.AddDebugStage("Executor: VERIFY → timeout, retrying from OpenMenu");
                _phase = Phase.OpenMenu;
                return new BlightBuildAction(BlightBuildActionKind.None,
                    DebugMessage: "Retrying step after verification timeout");
            }

            return new BlightBuildAction(BlightBuildActionKind.None,
                DebugMessage: $"Awaiting verification... ({elapsed:F0}ms)");
        }

        if (_phase == Phase.Walking)
        {
            bool walkReady = IsStepWalkReady(labels, gc, service);

            if (!walkReady)
            {
                service.AddDebugStage($"Executor: WALK - walking to {s.Action} {s.TowerType} at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
                BlightBuildAction action = ResolveWalkAction(s, service, gc);

                // Only Build steps skip after a bounded wait — a foundation with no entity has nothing
                // to click. Upgrade steps keep waiting for the entity to reappear.
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

            // Settle only applies when the player just arrived from pathfinding; a standing player
            // opens immediately.
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

        if (labels == null || labels.Count == 0)
            return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "No labels available");

        Element? labelElement = FindLabelAt(labels, s.FoundationPosition, service);
        if (labelElement == null)
        {
            Entity? walkEntity = service.GetBestEntityAtPosition(s.FoundationPosition);
            if (walkEntity != null && service.IsEntityFullyOnScreen(walkEntity))
            {
                service.AddDebugStage("Executor: OPEN - label not found, entity on screen - retrying");
                return new BlightBuildAction(BlightBuildActionKind.None,
                    DebugMessage: "Label not found, retrying...");
            }
            return WalkCloser(service, gc, s, "Executor: OPEN - label not found - walking closer");
        }
        service.AddDebugStage("Executor: OPEN - label found");

        if (_phase == Phase.OpenMenu)
        {
            if (s.Action == BlightPlanAction.Upgrade)
            {
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
                        service.AddDebugStage($"Executor: OPEN → UPGRADE canAfford=false, actual rank={currentRank} target={s.TargetLevel}");

                        if (currentRank >= s.TargetLevel)
                        {
                            service.AddDebugStage($"Executor: OPEN → upgrade already done (rank={currentRank} >= target={s.TargetLevel}) — advancing");
                            return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                        }

                        // Invisible icon = can't afford: pause and wait for currency rather than skip the step.
                        service.AddDebugStage($"Executor: OPEN → UPGRADE cannot afford (rank={currentRank} < target={s.TargetLevel}) — waiting for currency");
                        return new BlightBuildAction(BlightBuildActionKind.None,
                            DebugMessage: $"Cannot afford upgrade ({s.TowerType} lvl {s.TargetLevel}) — waiting for currency");
                    }

                    NumVector2? upgradeClickPos = BlightMenuInteractions.GetUpgradeIconClickPosition(labelElement);
                    if (upgradeClickPos == null)
                        return Fail("Upgrade icon not found");

                    if (!IsPositionInWindow(new Vector2(upgradeClickPos.Value.X, upgradeClickPos.Value.Y), gc))
                        return WalkCloser(service, gc, s, "Executor: OPEN - upgrade icon off-screen, walking closer");

                    service.AddDebugStage($"Executor: OPEN - clicking upgrade icon at ({upgradeClickPos.Value.X:F0},{upgradeClickPos.Value.Y:F0})");
                    LogMenuState(service, labelElement, $"open-upgrade {s.TowerType}->lvl{s.TargetLevel}");
                    return MenuClick(Phase.SelectSpecialization, new Vector2(upgradeClickPos.Value.X, upgradeClickPos.Value.Y),
                        $"Upgrade {s.TowerType} - lvl {s.TargetLevel}", gc);
                }
            }

            // Check menu state FIRST — the affordability check becomes unreliable after the menu opens.
            bool menuPopulated = BlightMenuInteractions.IsTowerMenuOpen(labelElement);
            service.AddDebugStage($"Executor: OPEN → menuPopulated={menuPopulated}");

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
                        service.AddDebugStage($"Executor: OPEN → build already done (rank={effectiveRank} >= target={s.TargetLevel}) — advancing");
                        return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                    }

                    // Same affordability pause as upgrades: wait for currency.
                    service.AddDebugStage($"Executor: OPEN - BUILD cannot afford (rank={effectiveRank} < target={s.TargetLevel}) - waiting for currency");
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: $"Cannot afford build ({s.TowerType}) - waiting for currency");
                }

                // The build icon is a TOGGLE — re-clicking it while the sub-menu is still opening
                // closes the menu again.  After a build-icon click, wait for the sub-menu to appear
                // (or a retry timeout) instead of re-clicking on the next tick.
                long nowMs = Environment.TickCount64;
                if (ShouldWaitForBuildSubMenu(_lastBuildMenuClickTimestampMs, nowMs, MenuSubMenuWaitMs))
                {
                    service.AddDebugStage("Executor: OPEN - build icon clicked, waiting for sub-menu");
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: "Waiting for tower sub-menu...");
                }
                _lastBuildMenuClickTimestampMs = nowMs;

                NumVector2? buildIconPos = BlightMenuInteractions.GetBuildIconClickPosition(labelElement);
                if (buildIconPos == null)
                    return Fail("Build icon not found");

                Vector2 clickPos = new(buildIconPos.Value.X, buildIconPos.Value.Y);
                if (!IsPositionInWindow(clickPos, gc))
                    return WalkCloser(service, gc, s, "Executor: OPEN - build icon off-screen, walking closer");

                service.AddDebugStage($"Executor: OPEN - clicking build icon at ({clickPos.X:F0},{clickPos.Y:F0})");
                LogMenuState(service, labelElement, $"open-build {s.TowerType}");
                return MenuClick(Phase.OpenMenu, clickPos, "Open tower menu (build icon)", gc);
            }
        }

        double menuElapsed = (Stopwatch.GetTimestamp() - _phaseStartTimestamp) * 1000.0 / Stopwatch.Frequency;
        if (menuElapsed < 80 && _phase != Phase.WaitVerify && _phase != Phase.SelectSpecialization)
            return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Waiting for menu...");

        if (_phase == Phase.SelectTower)
        {
            NumVector2? towerClickPos = BlightMenuInteractions.GetTowerMenuChildClickPosition(labelElement, s.TowerType);
            if (towerClickPos == null)
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
                service.AddDebugStage($"Executor: SELECT - {s.TowerType}(idx={(int)s.TowerType}) not found — menu has {childCount} children - FAIL #{_consecutiveFailures + 1}");
                return Fail($"Tower type {s.TowerType} not found in menu");
            }

            if (!IsPositionInWindow(new Vector2(towerClickPos.Value.X, towerClickPos.Value.Y), gc))
                return WalkCloser(service, gc, s, "Executor: SELECT - build menu button off-screen, walking closer");

            service.AddDebugStage($"Executor: SELECT - clicking {s.TowerType} in build menu at ({towerClickPos.Value.X:F0},{towerClickPos.Value.Y:F0})");
            LogMenuState(service, labelElement, $"select-tower {s.TowerType}->lvl{s.TargetLevel}");
            return MenuClick(Phase.WaitVerify, new Vector2(towerClickPos.Value.X, towerClickPos.Value.Y),
                $"Build {s.TowerType} - lvl {s.TargetLevel}", gc);
        }

        if (_phase == Phase.SelectSpecialization)
        {
            int specIndex = service.GetSpecialization(s.TowerType);
            bool isSpecializationStep = IsSpecializationStep(specIndex, s.TargetLevel);

            // Never re-click a tower that already meets the target — a stale menu or already-landed
            // click would over-upgrade it (e.g. Seismic 3 -> 4) or pick the wrong spec.
            (int currentRank, _, _) = GetTowerRank(service, s.FoundationPosition);
            if (SystemMath.Max(currentRank, tower.UpgradeLevel) >= s.TargetLevel)
            {
                service.AddDebugStage($"Executor: SPEC - tower already at rank {SystemMath.Max(currentRank, tower.UpgradeLevel)} >= target {s.TargetLevel} — advancing without clicking");
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
                    // Only the specialization menu has more than one visible button; a tower at max
                    // plain shows it even when the lagging rank read reports below max. A plain
                    // upgrade must never click a spec button (Seismic 3 -> 4).
                    int visibleUpgradeButtons = BlightMenuInteractions.CountVisibleUpgradeButtons(labelElement);
                    if (visibleUpgradeButtons > 1)
                    {
                        service.AddDebugStage($"Executor: SPEC - plain {s.TowerType} menu shows {visibleUpgradeButtons} buttons (max plain) - advancing without clicking");
                        return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                    }

                    // Plain upgrade — the at-target guard already ran, so click the next-tier button.
                    (NumVector2 Position, string? UpgradeId)? plain = BlightMenuInteractions.GetFirstVisibleUpgradeButton(labelElement);
                    if (plain == null)
                    {
                        // No visible button — can't afford the tier: pause for currency.
                        service.AddDebugStage($"Executor: SPEC - no visible upgrade button for plain {s.TowerType} upgrade - waiting for currency");
                        return new BlightBuildAction(BlightBuildActionKind.None,
                            DebugMessage: $"No upgrade button visible for {s.TowerType} - waiting for currency");
                    }

                    // A plain upgrade must never click a spec button — the tower is at max plain, so the
                    // step is already complete.
                    if (ShouldSkipPlainUpgradeClick(plain.Value.UpgradeId))
                    {
                        service.AddDebugStage($"Executor: SPEC - plain upgrade button is a specialization ({plain.Value.UpgradeId}) - tower at max plain, advancing without clicking");
                        return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                    }

                    // The UpgradeResult dat read is unreliable in this build — a null id means we CANNOT
                    // verify the visible button is a real tier button rather than a specialization on a
                    // tower already at max plain. Never click blind: if the tower's live rank is already
                    // at max plain (3), the step is done and clicking would over-upgrade it.
                    if (plain.Value.UpgradeId == null)
                    {
                        (_, _, int effectiveRank) = GetTowerRank(service, s.FoundationPosition);
                        if (effectiveRank >= BlightTowerData.MaxUpgradeLevel - 1)
                        {
                            service.AddDebugStage($"Executor: SPEC - plain upgrade button unreadable, tower at rank {effectiveRank} (max plain) - advancing without clicking");
                            return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                        }
                    }

                    if (!IsPositionInWindow(new Vector2(plain.Value.Position.X, plain.Value.Position.Y), gc))
                        return WalkCloser(service, gc, s, "Executor: SPEC - upgrade button off-screen, walking closer");

                    service.AddDebugStage($"Executor: SPEC - plain upgrade, clicking next-tier button ('{plain.Value.UpgradeId ?? "?"}') at ({plain.Value.Position.X:F0},{plain.Value.Position.Y:F0})");
                    LogMenuState(service, labelElement, $"plain-upgrade {s.TowerType}->lvl{s.TargetLevel}");
                    return MenuClick(Phase.WaitVerify, new Vector2(plain.Value.Position.X, plain.Value.Position.Y),
                        $"Upgrade {s.TowerType} - lvl {s.TargetLevel}", gc);
                }

                // Prefer the catalog menu index for every type; the UpgradeResult dat read is unreliable in this build.
                TowerSpecialization spec = (TowerSpecialization)specIndex;
                int menuIndex = BlightTowerData.GetSpecializationMenuChildIndex(s.TowerType, spec);
                string targetTowerId = BlightTowerData.GetSpecializationTowerId(s.TowerType, spec);

                NumVector2? specPos = menuIndex >= 0
                    ? BlightMenuInteractions.GetSpecializationChildClickPosition(labelElement, menuIndex)
                    : null;

                if (specPos is null)
                {
                    // Spec button not visible — can't afford the 3→4 upgrade: pause for currency.
                    service.AddDebugStage($"Executor: SPEC - '{targetTowerId}' (specIndex={specIndex}) not visible - waiting for currency");
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: $"Cannot afford specialization '{targetTowerId}' - waiting for currency");
                }

                if (!IsPositionInWindow(new Vector2(specPos.Value.X, specPos.Value.Y), gc))
                    return WalkCloser(service, gc, s, "Executor: SPEC - specialization button off-screen, walking closer");

                service.AddDebugStage($"Executor: SPEC - clicking '{targetTowerId}' (specIndex={specIndex}) at ({specPos.Value.X:F0},{specPos.Value.Y:F0})");
                LogMenuState(service, labelElement, $"spec {targetTowerId} for {s.TowerType}->lvl{s.TargetLevel}");
                return MenuClick(Phase.WaitVerify, new Vector2(specPos.Value.X, specPos.Value.Y),
                    $"Select spec {targetTowerId} for {s.TowerType} - lvl {s.TargetLevel}", gc);
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

    // Un-throttled menu-state trail for the debug dump: exactly what the executor sees (label/menu
    // child tree with indexes, visibility, rects, best-effort dat ids) right before it clicks, so a
    // wrong button (e.g. Fireball 3->4 landing on Flamethrower) is diagnosable from the dump.
    private static void LogMenuState(BlightService service, Element labelElement, string phase)
        => service.AddExecutorEvent($"MENU {phase}: {BlightMenuInteractions.BuildMenuSnapshot(labelElement)}");

    private bool IsPlayerMoving(GameController gc)
    {
        try
        {
            if (gc?.Player == null) return false;
            NumVector2 current = new(gc.Player.GridPosNum.X, gc.Player.GridPosNum.Y);
            // Low threshold so slow drift (which still moves the camera and invalidates resolved
            // screen positions) is caught before a menu click.
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

    private static bool IsPositionInWindow(Vector2 screenPos, GameController? gc)
    {
        if (gc == null) return false;
        try
        {
            Size2F win = gc.Window.GetWindowRectangleTimeCache.Size;
            return screenPos.X >= 0 && screenPos.Y >= 0
                && screenPos.X <= win.Width && screenPos.Y <= win.Height;
        }
        catch { return false; }
    }

    // Resolves the executor's walk need: walk to the entity when cached, else to the persisted
    // position when off-screen (the entity walk returns no target for a streamed-out foundation).
    private static BlightBuildAction ResolveWalkAction(BlightPlanStep step, BlightService service, GameController? gc)
    {
        bool hasWalkEntity = service.GetBestEntityAtPosition(step.FoundationPosition) != null;
        bool positionOffScreen = gc != null && IsGridPositionOffScreen(gc, step.FoundationPosition);
        return ResolveWalkActionKind(hasWalkEntity, positionOffScreen) switch
        {
            BlightBuildActionKind.WalkToTarget => new BlightBuildAction(BlightBuildActionKind.WalkToTarget,
                DebugMessage: $"Walking to ({step.FoundationPosition.X:F0},{step.FoundationPosition.Y:F0})"),
            BlightBuildActionKind.WalkToPosition => new BlightBuildAction(BlightBuildActionKind.WalkToPosition,
                DebugMessage: $"Walking toward ({step.FoundationPosition.X:F0},{step.FoundationPosition.Y:F0})",
                GridPosition: step.FoundationPosition),
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
            return BlightHelpers.IsGridPosOffScreen(camera, gc.Window.GetWindowRectangleTimeCache.Size, gridPos);
        }
        catch { return false; }
    }


    // The build menu opens around Child[3]; keep walking until that region — enlarged 30% — is fully
    // on-screen and clickable before opening the menu.
    private static bool IsMenuRegionUsable(RectangleF rect, GameController? gc, BlightService service)
    {
        if (gc == null) return false;
        try
        {
            Size2F win = gc.Window.GetWindowRectangleTimeCache.Size;
            return IsMenuRegionUsable(rect, win.Width, win.Height, service.IsPointInClickableArea);
        }
        catch { return false; }
    }

    internal static bool IsMenuRegionUsable(RectangleF rect, float windowWidth, float windowHeight, Func<Vector2, bool> isPointInClickableArea)
    {
        if (windowWidth <= 0f || windowHeight <= 0f || rect.Width <= 0f || rect.Height <= 0f)
            return false;

        if (rect.X < 0f || rect.Y < 0f || rect.Right > windowWidth || rect.Bottom > windowHeight)
            return false;

        return isPointInClickableArea(new Vector2(rect.X, rect.Y))
            && isPointInClickableArea(new Vector2(rect.Right, rect.Y))
            && isPointInClickableArea(new Vector2(rect.X, rect.Bottom))
            && isPointInClickableArea(new Vector2(rect.Right, rect.Bottom));
    }

    // Build steps click the build icon (opens the tower sub-menu) so the whole enlarged region must be
    // on-screen; upgrade steps are a single icon click, so the tower on-screen is enough.
    internal static bool IsStepWalkReadyForAction(
        BlightPlanAction action, bool menuRegionReady, bool hasWalkEntity, bool entityFullyOnScreen)
        => action == BlightPlanAction.Upgrade
            ? hasWalkEntity && entityFullyOnScreen
            : menuRegionReady;

    internal bool IsStepWalkReady(
        IReadOnlyList<LabelOnGround>? labels,
        GameController? gc,
        BlightService service)
    {
        if (gc == null) return false;
        BlightPlanStep? step = CurrentPlan?.CurrentStep;
        if (step == null) return false;

        if (step.Value.Action == BlightPlanAction.Upgrade)
        {
            // Upgrade is a single icon click (no sub-menu), so the tower on-screen is enough; labels
            // aren't needed here.
            Entity? entity = service.GetBestEntityAtPosition(step.Value.FoundationPosition);
            return IsStepWalkReadyForAction(step.Value.Action,
                menuRegionReady: false,
                hasWalkEntity: entity != null,
                entityFullyOnScreen: entity != null && service.IsEntityFullyOnScreen(entity));
        }

        if (labels == null || labels.Count == 0) return false;
        Element? labelEl = FindLabelAt(labels, step.Value.FoundationPosition, service);
        if (labelEl == null) return false;
        int childIndex = BlightMenuInteractions.MenuChildIndexForStep(step.Value.Action);
        RectangleF? menuRect = BlightMenuInteractions.GetMenuRegionRect(labelEl, childIndex);
        return IsStepWalkReadyForAction(step.Value.Action,
            menuRegionReady: menuRect != null && IsMenuRegionUsable(menuRect.Value, gc, service),
            hasWalkEntity: true,
            entityFullyOnScreen: false);
    }

    // The pipeline's pathfinding stop condition mirrors EVERY way the executor asks to walk closer.
    internal bool WantsWalkForCurrentStep(
        GameController? gc, BlightService service, IReadOnlyList<LabelOnGround>? labels)
    {
        if (gc == null || CurrentPlan == null || CurrentPlan.IsComplete)
            return false;
        BlightPlanStep? step = CurrentPlan.CurrentStep;
        if (step == null)
            return false;

        bool walkReadyGate = IsStepWalkReady(labels, gc, service);
        if (step.Value.Action != BlightPlanAction.Upgrade)
            return WantsWalkForAction(step.Value.Action, walkReadyGate, upgradeLabelFound: false, upgradeIconInWindow: false, upgradeEntityOnScreen: false);

        Element? labelEl = FindLabelAt(labels ?? [], step.Value.FoundationPosition, service);
        Entity? entity = service.GetBestEntityAtPosition(step.Value.FoundationPosition);
        bool iconInWindow = labelEl != null
            && BlightMenuInteractions.GetUpgradeIconClickPosition(labelEl) is { } iconPos
            && IsPositionInWindow(new Vector2(iconPos.X, iconPos.Y), gc);
        return WantsWalkForAction(step.Value.Action, walkReadyGate,
            upgradeLabelFound: labelEl != null,
            upgradeIconInWindow: iconInWindow,
            upgradeEntityOnScreen: entity != null && service.IsEntityFullyOnScreen(entity));
    }

    // Pure walk decision for the pathfinding stop condition; upgrades also need the upgrade icon on-screen.
    internal static bool WantsWalkForAction(
        BlightPlanAction action,
        bool walkReadyGate,
        bool upgradeLabelFound,
        bool upgradeIconInWindow,
        bool upgradeEntityOnScreen)
    {
        if (!walkReadyGate)
            return true;
        if (action != BlightPlanAction.Upgrade)
            return false;
        if (!upgradeLabelFound)
            return !upgradeEntityOnScreen;
        return !upgradeIconInWindow;
    }

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

    // Shared "step verified → advance" tail: updates the cached level, resets failures, advances
    // the cursor and rewinds to Walking.
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
        CurrentCursor++;
        _phase = Phase.Walking;
        _lastBuildMenuClickTimestampMs = 0;
        if (CurrentPlan != null)
            CurrentPlan = CurrentPlan.WithAdvancedCursor();
    }

    // Rank of the tower at a step position from the live entity path and the cached tower level.
    // The path is read FRESH (not from the entity path cache): when a tower is upgraded in place the
    // cached path can lag one rank behind reality, and that stale rank is exactly what makes the
    // verify/retry loop re-click an already-upgraded tower (e.g. Seismic 3 -> 4, Fireball ->
    // Flamethrower). The executor ticks at most a few times per second, so a fresh read is cheap.
    private static (int Rank, int Cached, int Effective) GetTowerRank(BlightService service, NumVector2 position)
    {
        // Prefer the freshly-scanned TOWER entity: the cached foundation entity's path stays a
        // foundation path (rank 0) even after in-place upgrades, so reading it makes every upgrade
        // verify fail and the retry loop over-click (Seismic 3 -> 4). The tower entity path carries
        // the live rank and is read fresh.
        Entity? entity = service.GetTowerEntityAt(position) ?? service.GetBestEntityAtPosition(position);
        int rank = entity != null
            ? BlightHelpers.DetectUpgradeRankFromPath(BlightService.GetEntityPath(entity))
            : 0;
        int cached = BlightHelpers.FindTowerAt(service.KnownTowers, position)?.UpgradeLevel ?? 0;
        return (rank, cached, SystemMath.Max(rank, cached));
    }

    // The step's click target is off-screen or the label is missing — rewind to Walking so the
    // player approaches closer before the next attempt.
    private BlightBuildAction WalkCloser(BlightService service, GameController? gc, BlightPlanStep s, string message)
    {
        service.AddDebugStage(message);
        _phase = Phase.Walking;
        return ResolveWalkAction(s, service, gc);
    }

    // Safety: never click a menu slot while the player is moving — a stale screen rect could build a different tower.
    private BlightBuildAction MenuClick(Phase nextPhase, Vector2 clickPos, string message, GameController? gc)
    {
        if (gc != null && IsPlayerMoving(gc))
        {
            _phase = Phase.StopPlayer;
            return new BlightBuildAction(BlightBuildActionKind.None,
                DebugMessage: "Player still moving — waiting to stop before menu click");
        }
        _phaseStartTimestamp = Stopwatch.GetTimestamp();
        _phase = nextPhase;
        return new BlightBuildAction(BlightBuildActionKind.ClickPosition, clickPos, message, IsMenuClick: true);
    }

    internal void Reset()
    {
        // Preserve the plan — only rewind to Walking so it can approach from a different angle.
        CurrentCursor = 0;
        _phase = Phase.Walking;
        _consecutiveFailures = 0;
        _stationaryTicks = 0;
        _phaseStartTimestamp = 0;
        _lastBuildMenuClickTimestampMs = 0;
    }

    internal void ClearPlan()
    {
        CurrentPlan = null;
        CurrentCursor = 0;
        _phase = Phase.Walking;
        _consecutiveFailures = 0;
        _stationaryTicks = 0;
        _phaseStartTimestamp = 0;
        _lastBuildMenuClickTimestampMs = 0;
    }

    // A step is a specialization step when it targets the spec tier (4) AND the strategy chose a
    // specialization for that type. The tower's CURRENT level is deliberately NOT part of the gate:
    // a cached level that lags reality (says 2 while the tower is really at 3) would otherwise
    // degrade a Fireball 3->4 step to the plain-upgrade path, which clicks the first visible button
    // — on a maxed tower that is a SPECIALIZATION button (Flamethrower for Fireball), so the tower
    // gets the wrong spec. The at-target guard handles "already done" separately.
    internal static bool IsSpecializationStep(int specialization, int targetLevel)
        => specialization >= 0 && targetLevel > 3;

    // True when the tower at the step clearly has a DIFFERENT specialization of the same base type;
    // fails open on unreadable state. The dat id is the authoritative signal — the entity path only
    // carries the base type + rank (e.g. "BlightTowerFlameRank4"), so path-based detection alone can
    // never distinguish Meteor from Flamethrower.
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

    // A plain upgrade must never click a spec button — that button means the tower is already at max
    // plain, so the step is complete and clicking would over-upgrade it.
    internal static bool ShouldSkipPlainUpgradeClick(string? upgradeButtonId)
        => upgradeButtonId != null && BlightTowerData.IsSpecializationTowerId(upgradeButtonId);

    // Verify-failure skip is BUILD-only; an unconfirmed upgrade is a pause, never a skip.
    internal static bool ShouldSkipAfterVerifyFailures(BlightPlanAction action, int consecutiveFailures)
        => action != BlightPlanAction.Upgrade && consecutiveFailures >= 3;
}
