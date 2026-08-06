namespace ClickIt.Features.Blight.Planning;

internal sealed class BlightPlanExecutor
{
    private long _phaseStartTimestamp;
    private int _consecutiveFailures;
    private NumVector2 _lastPlayerGridPos;
    private int _stationaryTicks;

    private bool _stopPlayerSawMovement;

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
            return Fail($"Foundation not found at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");

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
            if (service.IsDebugStageDue)
                service.AddDebugStage($"Executor: VERIFY → rank={currentRank} cached={cachedLevel} effective={effectiveRank} target={s.TargetLevel}");
            if (effectiveRank >= s.TargetLevel)
            {
                // Spec steps (3→4) must land on the strategy's chosen specialization — a wrong-slot
                // click (e.g. Flamethrower instead of Meteor) passes the rank check. When the tower's
                // path clearly shows a DIFFERENT spec of the same base type, the click went wrong:
                // re-enter the spec selection instead of advancing.
                int specIndex = service.GetSpecialization(s.TowerType);
                if (IsSpecializationStep(specIndex, s.TargetLevel, tower.UpgradeLevel)
                    && TowerShowsOtherSpecialization(service, s, specIndex))
                {
                    service.AddDebugStage($"Executor: VERIFY → tower has the WRONG specialization for {s.TowerType} — re-selecting");
                    _phase = Phase.SelectSpecialization;
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: $"Tower has wrong specialization — re-selecting");
                }
                service.AddDebugStage($"Executor: VERIFY → step verified — advancing cursor");
                return AdvanceStep(service, s, $"Step complete: {s.TowerType} lvl {s.TargetLevel}");
            }

            if (elapsed > _verifyTimeoutMs)
            {
                _consecutiveFailures++;
                service.AddDebugStage($"Executor: VERIFY → timeout — rank={currentRank} cached={cachedLevel} target={s.TargetLevel} failures={_consecutiveFailures}");

                // A retry click on an already-upgraded tower would over-upgrade it (e.g. Seismic 3 -> 4).
                if (effectiveRank >= s.TargetLevel)
                {
                    service.AddDebugStage($"Executor: VERIFY → rank {effectiveRank} meets target — advancing without re-click");
                    return AdvanceStep(service, s, $"Step verified via cache: {s.TowerType} lvl {s.TargetLevel}");
                }

                if (ShouldSkipAfterVerifyFailures(s.Action, _consecutiveFailures))
                {
                    service.AddDebugStage("Executor: VERIFY → 3 consecutive failures — skipping step");
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
                    service.AddDebugStage($"Executor: VERIFY → upgrade unconfirmed after retries — waiting for currency/state");
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: $"Upgrade unconfirmed ({s.TowerType} lvl {s.TargetLevel}) — waiting for currency/state");
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
                service.AddDebugStage($"Executor: WALK → walking to {s.Action} {s.TowerType} at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
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
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: "Player moving, waiting for stop position...");
                }
                service.AddDebugStage("Executor: STOP → player moving, clicking at feet to stop");
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
            return WalkCloser(service, gc, s, "Executor: OPEN → label not found — walking closer");
        }
        service.AddDebugStage("Executor: OPEN → label found");

        if (_phase == Phase.OpenMenu)
        {
            if (s.Action == BlightPlanAction.Upgrade)
            {
                // Upgrade is a single click on the upgrade icon (Child[3]); Fireball 3→4 opens a spec menu.
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
                        return WalkCloser(service, gc, s, "Executor: OPEN → upgrade icon off-screen, walking closer");

                    service.AddDebugStage($"Executor: OPEN → clicking upgrade icon at ({upgradeClickPos.Value.X:F0},{upgradeClickPos.Value.Y:F0})");
                    return MenuClick(Phase.SelectSpecialization, new Vector2(upgradeClickPos.Value.X, upgradeClickPos.Value.Y),
                        $"Upgrade {s.TowerType} → lvl {s.TargetLevel}", gc);
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
                bool canBuild = BlightMenuInteractions.CanAffordBuild(labelElement);
                service.AddDebugStage($"Executor: OPEN → BUILD canAfford={canBuild}");
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
                    service.AddDebugStage($"Executor: OPEN → BUILD cannot afford (rank={effectiveRank} < target={s.TargetLevel}) — waiting for currency");
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: $"Cannot afford build ({s.TowerType}) — waiting for currency");
                }

                NumVector2? buildIconPos = BlightMenuInteractions.GetBuildIconClickPosition(labelElement);
                if (buildIconPos == null)
                    return Fail("Build icon not found");

                Vector2 clickPos = new(buildIconPos.Value.X, buildIconPos.Value.Y);
                if (!IsPositionInWindow(clickPos, gc))
                    return WalkCloser(service, gc, s, "Executor: OPEN → build icon off-screen, walking closer");

                service.AddDebugStage($"Executor: OPEN → clicking build icon at ({clickPos.X:F0},{clickPos.Y:F0})");
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
                    Element c0 = labelElement.GetChildAtIndex(0);
                    Element? m = c0?.GetChildAtIndex(3);
                    if (m != null) childCount = m.ChildCount;
                }
                catch { }
                service.AddDebugStage($"Executor: SELECT → {s.TowerType}(idx={(int)s.TowerType}) not found — menu has {childCount} children — FAIL #{_consecutiveFailures + 1}");
                return Fail($"Tower type {s.TowerType} not found in menu");
            }

            if (!IsPositionInWindow(new Vector2(towerClickPos.Value.X, towerClickPos.Value.Y), gc))
                return WalkCloser(service, gc, s, "Executor: SELECT → build menu button off-screen, walking closer");

            service.AddDebugStage($"Executor: SELECT → clicking {s.TowerType} in build menu at ({towerClickPos.Value.X:F0},{towerClickPos.Value.Y:F0})");
            return MenuClick(Phase.WaitVerify, new Vector2(towerClickPos.Value.X, towerClickPos.Value.Y),
                $"Build {s.TowerType} → lvl {s.TargetLevel}", gc);
        }

        if (_phase == Phase.SelectSpecialization)
        {
            int specIndex = service.GetSpecialization(s.TowerType);
            bool isSpecializationStep = IsSpecializationStep(specIndex, s.TargetLevel, tower.UpgradeLevel);

            // Never re-click a tower that already meets the target — a stale menu or already-landed
            // click would over-upgrade it (e.g. Seismic 3 -> 4) or pick the wrong spec.
            (int currentRank, _, _) = GetTowerRank(service, s.FoundationPosition);
            if (SystemMath.Max(currentRank, tower.UpgradeLevel) >= s.TargetLevel)
            {
                service.AddDebugStage($"Executor: SPEC → tower already at rank {SystemMath.Max(currentRank, tower.UpgradeLevel)} >= target {s.TargetLevel} — advancing without clicking");
                return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
            }

            bool menuOpen = BlightMenuInteractions.IsTowerMenuOpen(labelElement);
            service.AddDebugStage($"Executor: SPEC → menuOpen={menuOpen} ({menuElapsed:F0}ms)");

            if (!menuOpen)
            {
                // Plain upgrades are a single icon click — no sub-menu to wait for; only spec upgrades wait.
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
                if (!isSpecializationStep)
                {
                    // Plain upgrade — the at-target guard already ran, so click the next-tier button.
                    (NumVector2 Position, string? UpgradeId)? plain = BlightMenuInteractions.GetFirstVisibleUpgradeButton(labelElement);
                    if (plain == null)
                    {
                        // No visible button — can't afford the tier: pause for currency.
                        service.AddDebugStage($"Executor: SPEC → no visible upgrade button for plain {s.TowerType} upgrade — waiting for currency");
                        return new BlightBuildAction(BlightBuildActionKind.None,
                            DebugMessage: $"No upgrade button visible for {s.TowerType} — waiting for currency");
                    }

                    // A plain upgrade must never click a spec button — the tower is at max plain, so the
                    // step is already complete.
                    if (ShouldSkipPlainUpgradeClick(plain.Value.UpgradeId))
                    {
                        service.AddDebugStage($"Executor: SPEC → plain upgrade button is a specialization ({plain.Value.UpgradeId}) — tower at max plain, advancing without clicking");
                        return AdvanceStep(service, s, $"Step already complete: {s.TowerType} lvl {s.TargetLevel}");
                    }

                    if (!IsPositionInWindow(new Vector2(plain.Value.Position.X, plain.Value.Position.Y), gc))
                        return WalkCloser(service, gc, s, "Executor: SPEC → upgrade button off-screen, walking closer");

                    service.AddDebugStage($"Executor: SPEC → plain upgrade, clicking next-tier button ('{plain.Value.UpgradeId ?? "?"}') at ({plain.Value.Position.X:F0},{plain.Value.Position.Y:F0})");
                    return MenuClick(Phase.WaitVerify, new Vector2(plain.Value.Position.X, plain.Value.Position.Y),
                        $"Upgrade {s.TowerType} → lvl {s.TargetLevel}", gc);
                }

                // Fireball's spec slots are verified in-game (Flamethrower=0, Meteor=1) and the
                // UpgradeResult dat read is unreliable — so Fireball uses the verified index first;
                // non-Fireball types have no verified slot and resolve by tower ID.
                TowerSpecialization spec = (TowerSpecialization)specIndex;
                int menuIndex = BlightTowerData.GetSpecializationMenuChildIndex(s.TowerType, spec);
                string targetTowerId = BlightTowerData.GetSpecializationTowerId(s.TowerType, spec);
                bool verifiedIndex = BlightTowerData.HasVerifiedSpecializationMenuIndex(s.TowerType);

                NumVector2? specPos = verifiedIndex
                    ? BlightMenuInteractions.GetSpecializationChildClickPosition(labelElement, menuIndex)
                    : BlightMenuInteractions.GetSpecializationClickPosition(labelElement, targetTowerId);
                if (specPos is null)
                    specPos = verifiedIndex
                        ? BlightMenuInteractions.GetSpecializationClickPosition(labelElement, targetTowerId)
                        : BlightMenuInteractions.GetSpecializationChildClickPosition(labelElement, menuIndex);

                if (specPos is null)
                {
                    // Spec button not visible — can't afford the 3→4 upgrade: pause for currency.
                    service.AddDebugStage($"Executor: SPEC → '{targetTowerId}' (specIndex={specIndex}) not visible — waiting for currency");
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: $"Cannot afford specialization '{targetTowerId}' — waiting for currency");
                }

                if (!IsPositionInWindow(new Vector2(specPos.Value.X, specPos.Value.Y), gc))
                    return WalkCloser(service, gc, s, "Executor: SPEC → specialization button off-screen, walking closer");

                service.AddDebugStage($"Executor: SPEC → clicking '{targetTowerId}' (specIndex={specIndex}) at ({specPos.Value.X:F0},{specPos.Value.Y:F0})");
                return MenuClick(Phase.WaitVerify, new Vector2(specPos.Value.X, specPos.Value.Y),
                    $"Select spec {targetTowerId} for {s.TowerType} → lvl {s.TargetLevel}", gc);
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
            float scale = 1f / PoeMapExtension.WorldToGridConversion;
            NumVector2 screen = camera.WorldToScreen(new System.Numerics.Vector3(gridPos.X * scale, gridPos.Y * scale, 0f));
            Size2F win = gc.Window.GetWindowRectangleTimeCache.Size;
            return screen.X < 0f || screen.Y < 0f || screen.X > win.Width || screen.Y > win.Height;
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

    // The pipeline's pathfinding stop condition — mirrors EVERY way the executor asks to walk
    // closer, so pathfinding never refuses a walk the executor needs (e.g. an upgrade icon that
    // sits off-window while the tower entity is already on-screen). Returns true while the current
    // step still needs the player to approach.
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

    // Pure walk decision for the pathfinding stop condition.  The walk-ready gate is the Walking
    // phase's own condition; for upgrades the upgrade icon must also be on-screen (a single click),
    // and a missing label only keeps walking while the entity is off-screen (the executor retries
    // otherwise).
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
        if (CurrentPlan != null)
            CurrentPlan = CurrentPlan.WithAdvancedCursor();
    }

    // Rank of the tower at a step position from the live entity path and the cached tower level.
    // The path read is routed through the entity path cache so the per-tick verify loop does not
    // allocate a fresh path string on every tick.
    private static (int Rank, int Cached, int Effective) GetTowerRank(BlightService service, NumVector2 position)
    {
        Entity? entity = service.GetBestEntityAtPosition(position);
        int rank = entity != null
            ? BlightHelpers.DetectUpgradeRankFromPath(service.GetEntityPathCached(entity))
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

    // Safety: a menu slot position is resolved from a screen rect that goes stale the moment the
    // player moves (the camera moves with the player), so clicking while moving can land on a
    // different tower slot — e.g. building an Empowering tower when clicking the Fireball slot.
    // Never click a menu slot while the player is moving; return to StopPlayer and wait instead.
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

    // True when the tower's entity path clearly names a DIFFERENT specialization of the same base
    // type than the one the strategy chose — i.e. the spec click landed on the wrong button.  The
    // path is the only reliable signal (the UpgradeResult dat read is garbage in this ExileCore
    // build); when the path is unreadable or shows neither spec, this returns false (no false trip).
    private static bool TowerShowsOtherSpecialization(BlightService service, BlightPlanStep s, int specIndex)
    {
        if (specIndex < 0)
            return false;
        Entity? entity = service.GetBestEntityAtPosition(s.FoundationPosition);
        if (entity == null)
            return false;
        string? path = service.GetEntityPathCached(entity);
        return PathShowsOtherSpecialization(path ?? string.Empty, s.TowerType, (TowerSpecialization)specIndex);
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
