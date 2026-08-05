namespace ClickIt.Features.Blight.Planning;

internal sealed class BlightPlanExecutor
{
    private long _phaseStartTimestamp;
    private int _consecutiveFailures;
    private NumVector2 _lastPlayerGridPos;
    private int _stationaryTicks;

    // True when the player actually moved during the current StopPlayer visit (e.g. just arrived
    // from pathfinding). The settle wait only applies then — a player who was already standing
    // still shouldn't hold up the next build click for extra settle ticks.
    private bool _stopPlayerSawMovement;

    // Bounds the wait for an on-screen foundation whose entity never becomes scannable (see the
    // Walking phase): after this many consecutive no-op waits on the same step, the step is skipped
    // so the plan keeps executing instead of stalling forever.
    private const int MaxWalkWaitTicksBeforeSkip = 25;
    private int _walkWaitTicks;
    private NumVector2 _walkWaitStepPos;

    // Foundation label/element resolution is the executor's most expensive per-tick read (a full
    // label scan with dynamic entity reads). It is queried several times per tick (Walking gate,
    // menu phases, and BlightService's walk-target resolution), so cache it per labels-list
    // reference — the cached labels refresh every ~50ms, which bounds the staleness.
    private IReadOnlyList<LabelOnGround>? _cachedLabels;
    private NumVector2 _cachedStepPos;
    private bool _hasCachedLabel;
    private Element? _cachedLabelElement;

    private enum Phase { Walking, StopPlayer, OpenMenu, SelectTower, SelectSpecialization, WaitVerify, Done }

    private Phase _phase;
    private int _verifyTimeoutMs = 2000;

    private const int MinStationaryTicksBeforeBuild = 3;

    internal BlightPlan? CurrentPlan { get; private set; }
    internal int CurrentCursor { get; private set; }

    internal void SetPlan(BlightPlan plan)
    {
        CurrentPlan = plan.WithCurrentStepIndex(0);
        CurrentCursor = 0;
        _phase = Phase.Walking;
        _consecutiveFailures = 0;
        _stationaryTicks = 0;
    }

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
            return new BlightBuildAction(BlightBuildActionKind.Error,
                DebugMessage: $"Foundation not found at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");

        // Verification reads only the entity cache (tower rank), never the tower label, so it runs
        // ahead of the label-resolution gate: a transient label gap right after a build/upgrade click
        // must not hold up confirming the step (it used to stall the verify until the label returned).
        if (_phase == Phase.WaitVerify)
        {
            int delayMs = s.Action == BlightPlanAction.Build
                ? service.BlightTowerBuildDelayMs
                : service.BlightTowerUpgradeDelayMs;

            double elapsed = (Stopwatch.GetTimestamp() - _phaseStartTimestamp) * 1000.0 / Stopwatch.Frequency;
            if (elapsed < delayMs)
                return new BlightBuildAction(BlightBuildActionKind.None,
                    DebugMessage: $"Waiting for {(s.Action == BlightPlanAction.Build ? "build" : "upgrade")}... ({elapsed:F0}ms)");

            int currentRank = BlightHelpers.DetectUpgradeRankFromEntityPath(
                service.GetBestEntityAtPosition(s.FoundationPosition));

            int cachedLevel = BlightHelpers.FindTowerAt(service.KnownTowers, s.FoundationPosition)?.UpgradeLevel ?? 0;

            int effectiveRank = SystemMath.Max(currentRank, cachedLevel);
            service.AddDebugStage($"Executor: VERIFY → rank={currentRank} cached={cachedLevel} effective={effectiveRank} target={s.TargetLevel}");
            if (effectiveRank >= s.TargetLevel)
            {
                service.AddDebugStage($"Executor: VERIFY → step verified — advancing cursor");
                service.UpdateKnownTowerLevel(s.FoundationPosition, s.TowerType, s.TargetLevel);
                _consecutiveFailures = 0;
                CurrentCursor++;
                _phase = Phase.Walking;
                if (CurrentPlan != null)
                    CurrentPlan = CurrentPlan.WithAdvancedCursor();
                return new BlightBuildAction(BlightBuildActionKind.Complete,
                    DebugMessage: $"Step complete: {s.TowerType} lvl {s.TargetLevel}");
            }

            if (elapsed > _verifyTimeoutMs)
            {
                _consecutiveFailures++;
                service.AddDebugStage($"Executor: VERIFY → timeout — rank={currentRank} cached={cachedLevel} target={s.TargetLevel} failures={_consecutiveFailures}");

                // Before retrying, check whether the tower already meets the target — the freshest
                // signal is the entity rank (the cache may lag by one scan). A retry click on an
                // already-upgraded tower would over-upgrade it (e.g. Seismic 3 -> 4 = Stone Gaze).
                if (effectiveRank >= s.TargetLevel)
                {
                    service.AddDebugStage($"Executor: VERIFY → rank {effectiveRank} meets target — advancing without re-click");
                    service.UpdateKnownTowerLevel(s.FoundationPosition, s.TowerType, s.TargetLevel);
                    _consecutiveFailures = 0;
                    CurrentCursor++;
                    _phase = Phase.Walking;
                    if (CurrentPlan != null)
                        CurrentPlan = CurrentPlan.WithAdvancedCursor();
                    return new BlightBuildAction(BlightBuildActionKind.Complete,
                        DebugMessage: $"Step verified via cache: {s.TowerType} lvl {s.TargetLevel}");
                }

                if (_consecutiveFailures >= 3)
                {
                    service.AddDebugStage($"Executor: VERIFY → 3 consecutive failures — skipping step");
                    _consecutiveFailures = 0;
                    CurrentCursor++;
                    _phase = Phase.Walking;
                    if (CurrentPlan != null)
                        CurrentPlan = CurrentPlan.WithAdvancedCursor();
                    return new BlightBuildAction(BlightBuildActionKind.Error,
                        DebugMessage: $"Skipped after 3 failures: {s.TowerType} at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
                }

                // Retry.
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
                service.AddDebugStage($"Executor: WALK → walking to {s.Action} {s.TowerType} at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
                BlightBuildAction action = ResolveWalkAction(s, service, gc);

                // ResolveWalkAction returns None when the foundation is on-screen but has no cached
                // entity (streamed out / label not resolving) — it waits for the scan with no walk
                // possible. Bound the wait per step position, then skip the step (best-effort, spec
                // §4.7) so the plan keeps executing; a later rebuild re-includes it if it becomes
                // scannable.
                if (action.Kind == BlightBuildActionKind.None)
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
                return action;
            }
            _walkWaitTicks = 0;
            service.AddDebugStage("Executor: WALK → step ready, stopping player");
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
                    // Player moving but screen position unresolvable — wait rather than open the menu on a moving target.
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: "Player moving, waiting for stop position...");
                }
                service.AddDebugStage("Executor: STOP → player moving, clicking at feet to stop");
                return new BlightBuildAction(BlightBuildActionKind.ClickPosition, stopPos.Value,
                    "Stop player movement");
            }

            // Several consecutive stationary samples confirm the player has truly stopped when they
            // were just moving (e.g. arrived from pathfinding). A player who was already standing
            // still opens the menu immediately — the extra settle ticks only delay the next click.
            _stationaryTicks++;
            if (_stationaryTicks < MinStationaryTicksBeforeBuild && _stopPlayerSawMovement)
            {
                return new BlightBuildAction(BlightBuildActionKind.None,
                    DebugMessage: $"Waiting for player to settle ({_stationaryTicks}/{MinStationaryTicksBeforeBuild})...");
            }
            service.AddDebugStage("Executor: STOP → player stationary, opening menu");
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
                service.AddDebugStage("Executor: OPEN → label not found, entity on screen — retrying");
                return new BlightBuildAction(BlightBuildActionKind.None,
                    DebugMessage: "Label not found, retrying...");
            }
            service.AddDebugStage("Executor: OPEN → label not found — walking closer");
            _phase = Phase.Walking;
            return ResolveWalkAction(s, service, gc);
        }
        service.AddDebugStage("Executor: OPEN → label found");

        if (_phase == Phase.OpenMenu)
        {
            if (s.Action == BlightPlanAction.Upgrade)
            {
                // Upgrade is a single click on the upgrade icon (Child[3]); some (Fireball 3→4) open a spec sub-menu.

                // If the specialization menu is already open, skip the icon click.
                if (BlightMenuInteractions.IsTowerMenuOpen(labelElement))
                {
                    service.AddDebugStage("Executor: OPEN → UPGRADE menu already open, going directly to specialization selection");
                    _phaseStartTimestamp = Stopwatch.GetTimestamp();
                    _phase = Phase.SelectSpecialization;
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: "Upgrade menu open, entering specialization selection");
                }
                else
                {
                    bool canUpgrade = BlightMenuInteractions.CanAffordUpgrade(labelElement);
                    service.AddDebugStage($"Executor: OPEN → UPGRADE canAfford={canUpgrade}");

                    if (!canUpgrade)
                    {
                        // Upgrade icon not visible — tower may already be at/above target (cache out of sync); check the actual rank.
                        int currentRank = BlightHelpers.DetectUpgradeRankFromEntityPath(
                            service.GetBestEntityAtPosition(s.FoundationPosition));
                        service.AddDebugStage($"Executor: OPEN → UPGRADE canAfford=false, actual rank={currentRank} target={s.TargetLevel}");

                        if (currentRank >= s.TargetLevel)
                        {
                            service.AddDebugStage($"Executor: OPEN → upgrade already done (rank={currentRank} >= target={s.TargetLevel}) — advancing");
                            service.UpdateKnownTowerLevel(s.FoundationPosition, s.TowerType, s.TargetLevel);
                            _consecutiveFailures = 0;
                            CurrentCursor++;
                            _phase = Phase.Walking;
                            if (CurrentPlan != null)
                                CurrentPlan = CurrentPlan.WithAdvancedCursor();
                            return new BlightBuildAction(BlightBuildActionKind.Complete,
                                DebugMessage: $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                        }

                        return new BlightBuildAction(BlightBuildActionKind.None,
                            DebugMessage: "Cannot afford to upgrade");
                    }

                    NumVector2? upgradeClickPos = BlightMenuInteractions.GetUpgradeIconClickPosition(labelElement);
                    if (upgradeClickPos == null)
                        return Fail("Upgrade icon not found");

                    if (!IsPositionInWindow(new Vector2(upgradeClickPos.Value.X, upgradeClickPos.Value.Y), gc))
                    {
                        service.AddDebugStage("Executor: OPEN → upgrade icon off-screen, walking closer");
                        _phase = Phase.Walking;
                        return ResolveWalkAction(s, service, gc);
                    }

                    service.AddDebugStage($"Executor: OPEN → clicking upgrade icon at ({upgradeClickPos.Value.X:F0},{upgradeClickPos.Value.Y:F0})");
                    _phaseStartTimestamp = Stopwatch.GetTimestamp();
                    _phase = Phase.SelectSpecialization;
                    return new BlightBuildAction(BlightBuildActionKind.ClickPosition,
                        new Vector2(upgradeClickPos.Value.X, upgradeClickPos.Value.Y),
                        $"Upgrade {s.TowerType} → lvl {s.TargetLevel}");
                }
            }

            // Check menu state FIRST — the affordability check becomes unreliable after the menu opens.
            bool menuPopulated = BlightMenuInteractions.IsTowerMenuOpen(labelElement);
            service.AddDebugStage($"Executor: OPEN → menuPopulated={menuPopulated}");

            if (menuPopulated)
            {
                service.AddDebugStage("Executor: OPEN → menu populated, entering SelectTower");
                _consecutiveFailures = 0;
                _phaseStartTimestamp = Stopwatch.GetTimestamp();
                _phase = Phase.SelectTower;
            }
            else
            {
                // Menu is not yet open — check affordability and click.
                bool canBuild = BlightMenuInteractions.CanAffordBuild(labelElement);
                service.AddDebugStage($"Executor: OPEN → BUILD canAfford={canBuild}");
                if (!canBuild)
                {
                    // Build icon not visible — the foundation may already carry a tower (cache/plan
                    // desync after a cursor reset). Advance when it already meets the target instead
                    // of stalling on an unbuildable step.
                    int currentRank = BlightHelpers.DetectUpgradeRankFromEntityPath(
                        service.GetBestEntityAtPosition(s.FoundationPosition));
                    int cachedLevel = BlightHelpers.FindTowerAt(service.KnownTowers, s.FoundationPosition)?.UpgradeLevel ?? 0;
                    int effectiveRank = SystemMath.Max(currentRank, cachedLevel);
                    if (effectiveRank >= s.TargetLevel)
                    {
                        service.AddDebugStage($"Executor: OPEN → build already done (rank={effectiveRank} >= target={s.TargetLevel}) — advancing");
                        service.UpdateKnownTowerLevel(s.FoundationPosition, s.TowerType, s.TargetLevel);
                        _consecutiveFailures = 0;
                        CurrentCursor++;
                        _phase = Phase.Walking;
                        if (CurrentPlan != null)
                            CurrentPlan = CurrentPlan.WithAdvancedCursor();
                        return new BlightBuildAction(BlightBuildActionKind.Complete,
                            DebugMessage: $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                    }

                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: "Cannot afford to build");
                }

                NumVector2? buildIconPos = BlightMenuInteractions.GetBuildIconClickPosition(labelElement);
                if (buildIconPos == null)
                    return Fail("Build icon not found");

                Vector2 clickPos = new(buildIconPos.Value.X, buildIconPos.Value.Y);
                if (!IsPositionInWindow(clickPos, gc))
                {
                    service.AddDebugStage("Executor: OPEN → build icon off-screen, walking closer");
                    _phase = Phase.Walking;
                    return ResolveWalkAction(s, service, gc);
                }

                service.AddDebugStage($"Executor: OPEN → clicking build icon at ({clickPos.X:F0},{clickPos.Y:F0})");
                _phaseStartTimestamp = Stopwatch.GetTimestamp();
                return new BlightBuildAction(BlightBuildActionKind.ClickPosition, clickPos,
                    "Open tower menu (build icon)");
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
                    Element c0 = labelElement.GetChildAtIndex(0);
                    Element? m = c0?.GetChildAtIndex(3);
                    if (m != null) childCount = m.ChildCount;
                }
                catch { }
                service.AddDebugStage($"Executor: SELECT → {s.TowerType}(idx={(int)s.TowerType}) not found — menu has {childCount} children — FAIL #{_consecutiveFailures + 1}");
                return Fail($"Tower type {s.TowerType} not found in menu");
            }

            if (!IsPositionInWindow(new Vector2(towerClickPos.Value.X, towerClickPos.Value.Y), gc))
            {
                service.AddDebugStage("Executor: SELECT → build menu button off-screen, walking closer");
                _phase = Phase.Walking;
                return ResolveWalkAction(s, service, gc);
            }

            service.AddDebugStage($"Executor: SELECT → clicking {s.TowerType} in build menu at ({towerClickPos.Value.X:F0},{towerClickPos.Value.Y:F0})");
            _phaseStartTimestamp = Stopwatch.GetTimestamp();
            _phase = Phase.WaitVerify;
            return new BlightBuildAction(BlightBuildActionKind.ClickPosition,
                new Vector2(towerClickPos.Value.X, towerClickPos.Value.Y),
                $"Build {s.TowerType} → lvl {s.TargetLevel}");
        }

        if (_phase == Phase.SelectSpecialization)
        {
            int specIndex = service.GetSpecialization(s.TowerType);
            bool isSpecializationStep = IsSpecializationStep(specIndex, s.TargetLevel, tower.UpgradeLevel);

            // Some upgrades (Fireball 3→4) open a specialization sub-menu — check if it is now populated.
            bool menuOpen = BlightMenuInteractions.IsTowerMenuOpen(labelElement);
            service.AddDebugStage($"Executor: SPEC → menuOpen={menuOpen} ({menuElapsed:F0}ms)");

            if (!menuOpen)
            {
                // Plain upgrades are a single click on the upgrade icon — no sub-menu ever opens, so
                // there is nothing to wait for; verify immediately. Only specialization upgrades need
                // their sub-menu to appear before the spec button can be clicked.
                if (!isSpecializationStep || menuElapsed > 300)
                {
                    service.AddDebugStage(isSpecializationStep
                        ? "Executor: SPEC → no specialization menu after 300ms, proceeding to Verify"
                        : "Executor: SPEC → plain upgrade (single click, no sub-menu), proceeding to Verify");
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
                // A specialization choice exists only when the tower is at max non-specialized level (3),
                // the step upgrades INTO the specialization tier (target > 3), and the rule chose one.

                if (!isSpecializationStep)
                {
                    // Plain upgrade — click the next-tier button. Never click when the tower already
                    // meets the target: a stale menu left open (or a previous click that already
                    // landed) turns an extra click into an over-upgrade (e.g. Seismic 3 -> 4 = Stone Gaze).
                    int currentRank = BlightHelpers.DetectUpgradeRankFromEntityPath(
                        service.GetBestEntityAtPosition(s.FoundationPosition));
                    if (SystemMath.Max(currentRank, tower.UpgradeLevel) >= s.TargetLevel)
                    {
                        service.AddDebugStage($"Executor: SPEC → tower already at rank {SystemMath.Max(currentRank, tower.UpgradeLevel)} >= target {s.TargetLevel} — advancing without clicking");
                        service.UpdateKnownTowerLevel(s.FoundationPosition, s.TowerType, s.TargetLevel);
                        _consecutiveFailures = 0;
                        CurrentCursor++;
                        _phase = Phase.Walking;
                        if (CurrentPlan != null)
                            CurrentPlan = CurrentPlan.WithAdvancedCursor();
                        return new BlightBuildAction(BlightBuildActionKind.Complete,
                            DebugMessage: $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                    }

                    (NumVector2 Position, string? UpgradeId)? plain = BlightMenuInteractions.GetFirstVisibleUpgradeButton(labelElement);
                    if (plain == null)
                    {
                        service.AddDebugStage($"Executor: SPEC → no visible upgrade button for plain {s.TowerType} upgrade — FAIL #{_consecutiveFailures + 1}");
                        return Fail($"No upgrade button in menu for {s.TowerType}");
                    }

                    if (!IsPositionInWindow(new Vector2(plain.Value.Position.X, plain.Value.Position.Y), gc))
                    {
                        service.AddDebugStage("Executor: SPEC → upgrade button off-screen, walking closer");
                        _phase = Phase.Walking;
                        return ResolveWalkAction(s, service, gc);
                    }

                    service.AddDebugStage($"Executor: SPEC → plain upgrade, clicking next-tier button ('{plain.Value.UpgradeId ?? "?"}') at ({plain.Value.Position.X:F0},{plain.Value.Position.Y:F0})");
                    _phaseStartTimestamp = Stopwatch.GetTimestamp();
                    _phase = Phase.WaitVerify;
                    return new BlightBuildAction(BlightBuildActionKind.ClickPosition,
                        new Vector2(plain.Value.Position.X, plain.Value.Position.Y),
                        $"Upgrade {s.TowerType} → lvl {s.TargetLevel}");
                }

                // Specialization upgrade — NO sub-menu; the two buttons are siblings in the open panel.
                // The strategy's enum value is NOT the menu index, so click the known child index or resolve by tower ID.
                TowerSpecialization spec = (TowerSpecialization)specIndex;
                string targetTowerId = BlightTowerData.GetSpecializationTowerId(s.TowerType, spec);
                NumVector2? specPos = BlightMenuInteractions.GetSpecializationChildClickPosition(
                    labelElement, BlightTowerData.GetSpecializationMenuChildIndex(s.TowerType, spec));
                if (specPos is null)
                    specPos = BlightMenuInteractions.GetSpecializationClickPosition(labelElement, targetTowerId);

                if (specPos is null)
                {
                    service.AddDebugStage($"Executor: SPEC → '{targetTowerId}' (specIndex={specIndex}) not found in menu — FAIL #{_consecutiveFailures + 1}");
                    return Fail($"Specialization '{targetTowerId}' not found in menu");
                }

                if (!IsPositionInWindow(new Vector2(specPos.Value.X, specPos.Value.Y), gc))
                {
                    service.AddDebugStage("Executor: SPEC → specialization button off-screen, walking closer");
                    _phase = Phase.Walking;
                    return ResolveWalkAction(s, service, gc);
                }

                service.AddDebugStage($"Executor: SPEC → clicking '{targetTowerId}' (specIndex={specIndex}) at ({specPos.Value.X:F0},{specPos.Value.Y:F0})");
                _phaseStartTimestamp = Stopwatch.GetTimestamp();
                _phase = Phase.WaitVerify;
                return new BlightBuildAction(BlightBuildActionKind.ClickPosition,
                    new Vector2(specPos.Value.X, specPos.Value.Y),
                    $"Select spec {targetTowerId} for {s.TowerType} → lvl {s.TargetLevel}");
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

    private bool IsPlayerMoving(GameController gc)
    {
        try
        {
            if (gc?.Player == null) return false;
            // Track grid position delta to detect movement (avoids the Pathfinding namespace).
            NumVector2 current = new(gc.Player.GridPosNum.X, gc.Player.GridPosNum.Y);
            bool moved = MathF.Abs(current.X - _lastPlayerGridPos.X) > 0.5f
                      || MathF.Abs(current.Y - _lastPlayerGridPos.Y) > 0.5f;
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

    // Resolves the walk the executor needs into a concrete action. When the foundation's entity is
    // cached, walk toward it (WalkToTarget). When the foundation is known but its entity has streamed
    // out (player too far for the scan), walk toward the persisted position instead — the entity walk
    // would return no target and the executor used to spin in an endless no-op walk loop.
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
            float scale = 1f / PoeMapExtension.WorldToGridConversion;
            NumVector2 screen = camera.WorldToScreen(new System.Numerics.Vector3(gridPos.X * scale, gridPos.Y * scale, 0f));
            Size2F win = gc.Window.GetWindowRectangleTimeCache.Size;
            return screen.X < 0f || screen.Y < 0f || screen.X > win.Width || screen.Y > win.Height;
        }
        catch { return false; }
    }


    // The build menu opens around the tower's menu region (Child[3]). Keep walking until that
    // region — enlarged 30% — is fully on-screen AND in a clickable place (not under the buff bar
    // etc.) before opening the menu.
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

    // Pure walk-ready decision shared by the Walking phase and BlightService's walk-target
    // resolution. Build steps click the build icon which opens the tower sub-menu, so the WHOLE
    // enlarged menu region must be on-screen and clickable before stopping. Upgrade steps are a
    // single click on the upgrade icon (no sub-menu opens), so the tower being fully on-screen is
    // enough — requiring the full region for upgrades made the executor keep walking into an
    // already-visible tower.
    internal static bool IsStepWalkReadyForAction(
        BlightPlanAction action, bool menuRegionReady, bool hasWalkEntity, bool entityFullyOnScreen)
        => action == BlightPlanAction.Upgrade
            ? hasWalkEntity && entityFullyOnScreen
            : menuRegionReady;

    // Single source of truth for "the current step is close enough to stop walking and open the
    // menu". Shared by the Walking phase and BlightService's walk-target resolution.
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
            // Upgrade steps are a single click on the upgrade icon — no sub-menu opens — so the
            // tower being fully on-screen is enough to stop pathfinding (labels aren't needed here;
            // the OpenMenu phase retries while a label is transiently missing).
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

    private BlightBuildAction Fail(string reason)
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= 3)
        {
            _consecutiveFailures = 0;
            CurrentCursor++;
            _phase = Phase.Walking;
            if (CurrentPlan != null)
                CurrentPlan = CurrentPlan.WithAdvancedCursor();
            return new BlightBuildAction(BlightBuildActionKind.Error,
                DebugMessage: $"Skipped after 3 failures: {reason}");
        }
        _phase = Phase.Walking;
        return new BlightBuildAction(BlightBuildActionKind.Error, DebugMessage: reason);
    }

    // Advances the cursor past a step that cannot be completed (best-effort skip, spec §4.7).
    private BlightBuildAction SkipStep(string reason)
    {
        _consecutiveFailures = 0;
        CurrentCursor++;
        _phase = Phase.Walking;
        if (CurrentPlan != null)
            CurrentPlan = CurrentPlan.WithAdvancedCursor();
        return new BlightBuildAction(BlightBuildActionKind.Error,
            DebugMessage: $"Skipped step: {reason}");
    }

    internal void Reset()
    {
        // Preserve the plan — only rewind to Walking so it can approach from a different angle.
        CurrentCursor = 0;
        _phase = Phase.Walking;
        _consecutiveFailures = 0;
        _stationaryTicks = 0;
        _phaseStartTimestamp = 0;
    }

    internal void ClearPlan()
    {
        CurrentPlan = null;
        CurrentCursor = 0;
        _phase = Phase.Walking;
        _consecutiveFailures = 0;
        _stationaryTicks = 0;
        _phaseStartTimestamp = 0;
    }

    internal static bool IsSpecializationStep(int specialization, int targetLevel, int currentTowerLevel)
        => specialization >= 0 && targetLevel > 3 && currentTowerLevel >= 3;
}
