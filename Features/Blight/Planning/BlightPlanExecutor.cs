namespace ClickIt.Features.Blight.Planning;

internal sealed class BlightPlanExecutor
{
    private long _phaseStartTimestamp;
    private int _consecutiveFailures;
    private NumVector2 _lastPlayerGridPos;
    private int _stationaryTicks;

    private enum Phase { Walking, StopPlayer, OpenMenu, SelectTower, SelectSpecialization, WaitVerify, Done }

    private Phase _phase;
    private int _verifyTimeoutMs = 2000;

    private const int MinStationaryTicksBeforeBuild = 3;

    internal BlightPlan? CurrentPlan { get; private set; }
    internal int CurrentCursor { get; private set; }

    internal void SetPlan(BlightPlan plan)
    {
        if (CurrentPlan != null && CurrentCursor < CurrentPlan.Steps.Count)
        {
            BlightPlanStep current = CurrentPlan.Steps[CurrentCursor];
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                if (plan.Steps[i].FoundationPosition == current.FoundationPosition
                    && plan.Steps[i].TowerType == current.TowerType
                    && plan.Steps[i].TargetLevel == current.TargetLevel)
                {
                    CurrentCursor = i;
                    CurrentPlan = plan;
                    return;
                }
            }
        }
        CurrentPlan = plan;
        CurrentCursor = plan.CurrentStepIndex;
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

        if (_phase == Phase.Walking)
        {
            bool menuReady = IsMenuRegionReadyForStep(labels, gc, service);

            if (!menuReady)
            {
                service.AddDebugStage($"Executor: WALK → walking to {s.Action} {s.TowerType} at ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
                return new BlightBuildAction(BlightBuildActionKind.WalkToTarget,
                    DebugMessage: $"Walking to ({s.FoundationPosition.X:F0},{s.FoundationPosition.Y:F0})");
            }
            service.AddDebugStage("Executor: WALK → menu region on-screen and clickable, stopping player");
            _stationaryTicks = 0;
            _phase = Phase.StopPlayer;
        }

        if (_phase == Phase.StopPlayer && gc != null)
        {
            if (IsPlayerMoving(gc))
            {
                _stationaryTicks = 0;
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

            // Require several consecutive stationary samples so movement settles before the menu opens.
            _stationaryTicks++;
            if (_stationaryTicks < MinStationaryTicksBeforeBuild)
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
            if (walkEntity == null || !service.IsEntityFullyOnScreen(walkEntity))
            {
                service.AddDebugStage($"Executor: OPEN → label not found, entity off-screen — walking closer");
                _phase = Phase.Walking;
                return new BlightBuildAction(BlightBuildActionKind.WalkToTarget,
                    DebugMessage: "Label not found — walking closer");
            }
            service.AddDebugStage("Executor: OPEN → label not found, entity on screen — retrying");
            return new BlightBuildAction(BlightBuildActionKind.None,
                DebugMessage: "Label not found, retrying...");
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
                        return new BlightBuildAction(BlightBuildActionKind.WalkToTarget,
                            DebugMessage: "Upgrade icon off-screen");
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
                    return new BlightBuildAction(BlightBuildActionKind.None,
                        DebugMessage: "Cannot afford to build");

                NumVector2? buildIconPos = BlightMenuInteractions.GetBuildIconClickPosition(labelElement);
                if (buildIconPos == null)
                    return Fail("Build icon not found");

                Vector2 clickPos = new(buildIconPos.Value.X, buildIconPos.Value.Y);
                if (!IsPositionInWindow(clickPos, gc))
                {
                    service.AddDebugStage("Executor: OPEN → build icon off-screen, walking closer");
                    _phase = Phase.Walking;
                    return new BlightBuildAction(BlightBuildActionKind.WalkToTarget,
                        DebugMessage: "Build icon off-screen");
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

            service.AddDebugStage($"Executor: SELECT → clicking {s.TowerType} in build menu at ({towerClickPos.Value.X:F0},{towerClickPos.Value.Y:F0})");
            _phaseStartTimestamp = Stopwatch.GetTimestamp();
            _phase = Phase.WaitVerify;
            return new BlightBuildAction(BlightBuildActionKind.ClickPosition,
                new Vector2(towerClickPos.Value.X, towerClickPos.Value.Y),
                $"Build {s.TowerType} → lvl {s.TargetLevel}");
        }

        if (_phase == Phase.SelectSpecialization)
        {
            // Some upgrades (Fireball 3→4) open a specialization sub-menu — check if it is now populated.
            bool menuOpen = BlightMenuInteractions.IsTowerMenuOpen(labelElement);
            service.AddDebugStage($"Executor: SPEC → menuOpen={menuOpen} ({menuElapsed:F0}ms)");

            if (!menuOpen)
            {
                if (menuElapsed > 300)
                {
                    // No specialization menu appeared — direct upgrade, proceed to verification.
                    service.AddDebugStage("Executor: SPEC → no specialization menu after 300ms, proceeding to Verify");
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
                int specIndex = service.GetSpecialization(s.TowerType);
                bool isSpecializationStep = IsSpecializationStep(specIndex, s.TargetLevel, tower.UpgradeLevel);

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

                service.AddDebugStage($"Executor: SPEC → clicking '{targetTowerId}' (specIndex={specIndex}) at ({specPos.Value.X:F0},{specPos.Value.Y:F0})");
                _phaseStartTimestamp = Stopwatch.GetTimestamp();
                _phase = Phase.WaitVerify;
                return new BlightBuildAction(BlightBuildActionKind.ClickPosition,
                    new Vector2(specPos.Value.X, specPos.Value.Y),
                    $"Select spec {targetTowerId} for {s.TowerType} → lvl {s.TargetLevel}");
            }
        }

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

        return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Idle");
    }

    private static Element? FindLabelAt(
        IReadOnlyList<LabelOnGround> labels,
        NumVector2 pos,
        BlightService service)
    {
        for (int i = 0; i < labels.Count; i++)
        {
            if (!service.IsBlightFoundationOrTowerLabel(labels[i]))
                continue;
            Entity? entity = BlightService.ResolveEntity(labels[i]);
            if (entity == null) continue;
            NumVector2 ePos = BlightHelpers.GetGridPosition(entity);
            if (BlightHelpers.SameGridPosition(ePos, pos))
                return BlightService.ResolveLabelElement(labels[i]);
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

    // Single source of truth for "the current step's tower menu region is fully on-screen and
    // clickable". Shared by the Walking phase and BlightService's walk-target resolution so
    // pathfinding keeps moving until the WHOLE phantom rectangle is usable — not just the entity.
    internal bool IsMenuRegionReadyForStep(
        IReadOnlyList<LabelOnGround>? labels,
        GameController? gc,
        BlightService service)
    {
        if (gc == null || labels == null || labels.Count == 0) return false;
        BlightPlanStep? step = CurrentPlan?.CurrentStep;
        if (step == null) return false;
        Element? labelEl = FindLabelAt(labels, step.Value.FoundationPosition, service);
        if (labelEl == null) return false;
        RectangleF? menuRect = BlightMenuInteractions.GetMenuRegionRect(labelEl);
        return menuRect != null && IsMenuRegionUsable(menuRect.Value, gc, service);
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
