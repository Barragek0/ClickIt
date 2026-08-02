namespace ClickIt.Features.Blight;

public sealed class BlightService
{
    private readonly ClickItSettings _settings;
    private GameController? _gameController;

    private readonly BlightEntityCache _cache;
    private readonly BlightEncounter _encounter = new();

    internal void InvalidateCoverageCache() => _cache.InvalidateCoverageCache();
    internal IReadOnlyList<Entity> PathwayEntities => _cache.PathwayEntities;
    internal IReadOnlyList<(Entity Entity, string TowerId)> TowerEntities => _cache.TowerEntities;
    internal Entity? PumpEntity => _cache.PumpEntity;
    internal bool IsEncounterActive => _encounter.IsActive;
    internal IReadOnlyList<BlightCachedTower> KnownTowers => _cache.KnownTowers;

    internal BlightService(ClickItSettings settings)
    {
        _settings = settings;
        _cache = new BlightEntityCache(settings, _debugEvents, _encounter);
        _cache.ClearRequested += Clear;
        _cache.DataChanged += HandleCacheDataChanged;
    }

    internal IBlightTowerStrategy CurrentStrategy => BlightStrategyResolver.Resolve(_settings);

    internal LaneCoverageResult[]? TryGetCachedCoverage() => _cache.TryGetCachedCoverage();
    internal (NumVector2[] Pathways, LaneCoverageResult[] Coverage)? TryGetRenderBundle() => _cache.TryGetRenderBundle();
    internal LaneCoverageResult[] ComputeLaneCoverage() => _cache.ComputeLaneCoverage();

    internal (List<NumVector2> Positions, List<(PumpBranch Branch, List<int> Segments)> Branches) GetBranchDebug()
    {
        LaneCoverageResult[]? coverage = _cache.TryGetCachedCoverage();
        if (coverage is not { Length: > 0 })
            return (new List<NumVector2>(), new List<(PumpBranch, List<int>)>());

        List<NumVector2> positions = [];
        NumVector2[]? aligned = _cache.CachedCoveragePathways;
        if (aligned != null && aligned.Length == coverage.Length)
            positions = [.. aligned];

        NumVector2? pump = _cache.PumpEntity is { } pe
            ? new NumVector2(pe.GridPosNum.X, pe.GridPosNum.Y)
            : null;

        List<PumpBranch> branches = BlightBranches.FindPumpBranches(
            coverage, pump, positions.Count == coverage.Length ? positions : null, _cache.CachedBranchAnchors);
        List<(PumpBranch, List<int>)> result = [];
        for (int b = 0; b < branches.Count; b++)
            result.Add((branches[b], BlightBranches.BranchSegments(coverage, branches[b])));
        return (positions, result);
    }

    internal void RefreshEntities(GameController? gameController)
    {
        _gameController = gameController;
        _cache.RefreshEntities(gameController);
    }

    internal void ScanFoundations(IReadOnlyList<LabelOnGround>? allLabels)
        => _cache.ScanFoundations(allLabels);
    internal int GetTowerRadiusCached(string towerId) => _cache.GetTowerRadiusCached(towerId);
    internal string DumpBlightTowerDat() => _cache.DumpBlightTowerDat();

    internal const int DefaultTowerRadius = 35;

    internal static int GetRadiusForLevel(BlightTowerType type, int level)
    {
        // Real per-tier radii from the game's BlightTowerDat (captured 2026-08-02).  Radius is
        // constant across ranks for Chilling/Seismic/Summoning (the v4 root-cause finding), but it
        // DOES grow with rank for Fireball/ShockNova/Empowering — the old constant-base model
        // under-estimated Fireball at rank 3/4 (75/100) and over-estimated it at rank 1 (45).
        // The actual radius for built towers still comes from the game dat (BlightCachedTower.Radius);
        // this is the fallback for unbuilt and streamed-out towers.
        return BlightTowerData.RadiusForLevel(type, level);
    }

    private readonly BlightPlanExecutor _executor = new();
    private int _planVersion;
    private int _lastPlannedTowerCount;
    private int _lastPlannedPathwayCount;
    private int _lastPlannedBuiltCount;
    private string _lastPlannedBuiltSignature = string.Empty;

    internal int BlightTowerBuildDelayMs => _settings.BlightTowerBuildDelayMs.Value;
    internal int BlightTowerUpgradeDelayMs => _settings.BlightTowerUpgradeDelayMs.Value;
    internal BlightPlan? CurrentPlan => _executor.CurrentPlan;

    internal int CurrentPlanCursor => _executor.CurrentCursor;

    private readonly BlightDebugEvents _debugEvents = new();
    internal IReadOnlyList<string> DebugStages => _debugEvents.Stages;
    internal void AddDebugStage(string message) => _debugEvents.Add(message);

    internal BlightCachedTower? CurrentTarget
    {
        get
        {
            BlightPlanStep? step = _executor.CurrentPlan?.CurrentStep;
            if (step == null) return null;
            return BlightHelpers.FindTowerAt(KnownTowers, step.Value.FoundationPosition);
        }
    }

    internal Entity? GetPathfindingTargetEntity()
    {
        BlightPlan? plan = _executor.CurrentPlan;
        if (plan == null || plan.IsComplete) return null;
        BlightPlanStep? step = plan.CurrentStep;
        if (step == null) return null;

        // FoundationEntity is only valid for unbuilt towers — fall back to the tower entity list for built ones.
        Entity? entity = GetBestEntityAtPosition(step.Value.FoundationPosition);
        if (entity != null && !IsEntityFullyOnScreen(entity))
            return entity;
        return null;
    }

    internal Entity? GetBestEntityAtPosition(NumVector2 pos) => _cache.GetBestEntityAtPosition(pos);

    internal bool IsEntityFullyOnScreen(Entity? entity)
    {
        if (entity == null || _gameController == null) return false;
        try
        {
            Camera? camera = _gameController.Game?.IngameState?.Camera;
            if (camera == null) return false;
            NumVector2 screenPos = camera.WorldToScreen(entity.PosNum);
            Size2F w = _gameController.Window.GetWindowRectangleTimeCache.Size;
            const float m = 200f;
            return screenPos.X >= m && screenPos.Y >= m
                && screenPos.X <= w.Width - m && screenPos.Y <= w.Height - m;
        }
        catch { return false; }
    }

    internal BlightBuildAction TryProgressBlightBuilding(IReadOnlyList<LabelOnGround>? allLabels)
    {
        if (!_settings.ClickBlightTowers.Value || !IsEncounterActive)
            return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Inactive");

        return _executor.Tick(_gameController, allLabels, this);
    }

    internal void UpdateKnownTowerLevel(NumVector2 position, BlightTowerType type, int upgradeLevel)
        => _cache.UpdateKnownTowerLevel(position, type, upgradeLevel);

    private void HandleCacheDataChanged()
    {
        if (IsEncounterActive && _cache.KnownTowerCount > 0 && ShouldRebuildPlan())
            RebuildPlan();
    }

    internal void RebuildPlan()
    {
        if (!IsEncounterActive || _cache.KnownTowerCount == 0) return;
        LaneCoverageResult[] coverage = ComputeLaneCoverage();

        int builtCount = _cache.BuiltTowerCount;

        _lastPlannedTowerCount = _cache.KnownTowerCount;
        _lastPlannedPathwayCount = _cache.PathwayCount;
        _lastPlannedBuiltCount = builtCount;
        _lastPlannedBuiltSignature = BuiltTowerSignature(_cache.KnownTowers);

        int chains = 0, orphans = 0, segCovered = 0, segUncovered = 0;
        for (int i = 0; i < coverage.Length; i++)
        {
            if (coverage[i].ParentIndex == BlightLaneTopology.OrphanSentinel)
                orphans++;
            else if (!coverage[i].IsPumpStub && (i == 0 || coverage[i - 1].ParentIndex == BlightLaneTopology.OrphanSentinel))
                chains++;
            if (BlightLaneTopology.IsRealLaneSegment(coverage[i]))
            {
                if (coverage[i].IsFullyCovered) segCovered++;
                else segUncovered++;
            }
        }

        _planVersion++;
        NumVector2? pumpPos = _cache.PumpEntity is { } pump
            ? new NumVector2(pump.GridPosNum.X, pump.GridPosNum.Y)
            : null;
        NumVector2? playerPos = _gameController?.Player != null
            ? new NumVector2(_gameController.Player.GridPosNum.X, _gameController.Player.GridPosNum.Y)
            : null;

        List<NumVector2>? pathwayPositions = null;
        NumVector2[]? aligned = _cache.CachedCoveragePathways;
        if (aligned != null && aligned.Length == coverage.Length)
            pathwayPositions = [.. aligned];

        // Coordinate-space correction: terrain objects (pump/pathways) can report local-space positions offset
        // from world space by thousands of units — apply the foundation-centroid offset only when it is large.
        IReadOnlyList<BlightCachedTower> towerSnapshot = KnownTowers;
        if (pumpPos.HasValue && pathwayPositions != null && pathwayPositions.Count > 0 && towerSnapshot.Count > 0)
        {
            float cx = 0f, cy = 0f;
            for (int i = 0; i < towerSnapshot.Count; i++)
            {
                cx += towerSnapshot[i].WorldPosition.X;
                cy += towerSnapshot[i].WorldPosition.Y;
            }
            cx /= towerSnapshot.Count;
            cy /= towerSnapshot.Count;

            float ox = cx - pumpPos.Value.X;
            float oy = cy - pumpPos.Value.Y;
            if ((ox * ox) + (oy * oy) > 400f * 400f)
            {
                for (int i = 0; i < pathwayPositions.Count; i++)
                    pathwayPositions[i] = new NumVector2(pathwayPositions[i].X + ox, pathwayPositions[i].Y + oy);
                pumpPos = new NumVector2(pumpPos.Value.X + ox, pumpPos.Value.Y + oy);
            }
        }

        BlightPlan plan = BlightPlanner.Build(KnownTowers, coverage, CurrentStrategy.Rules, _cache.FailedFoundationPositions, _planVersion, pumpPos, playerPos, pathwayPositions, _cache.CachedBranchAnchors);

        _cache.ApplyPlannedTowerTypes(plan);

        int prevCursor = _executor.CurrentCursor;
        _executor.SetPlan(plan);
        string cursorMsg = _executor.CurrentCursor == prevCursor
            ? $"cursor preserved at {_executor.CurrentCursor}"
            : $"cursor reset {prevCursor}->{_executor.CurrentCursor}";
        AddDebugStage($"{plan.DebugSummary} | {chains} chains {orphans} orphans segs={segCovered}+{segUncovered} {cursorMsg}");
        if (!string.IsNullOrEmpty(plan.Details))
            AddDebugStage($"Plan details: {plan.Details}");
    }

    private bool ShouldRebuildPlan()
    {
        // Rebuild when pathway, tower, built-count, or built-tower LEVEL data has changed.  The level
        // signature catches manual upgrades by the player — a rank change alters coverage but not any
        // count, so without it the stale plan would keep executing against the old geometry.
        return _lastPlannedPathwayCount != _cache.PathwayCount
            || _lastPlannedTowerCount != _cache.KnownTowerCount
            || _lastPlannedBuiltCount != _cache.BuiltTowerCount
            || !string.Equals(_lastPlannedBuiltSignature, BuiltTowerSignature(_cache.KnownTowers), StringComparison.Ordinal);
    }

    private static string BuiltTowerSignature(IReadOnlyList<BlightCachedTower> towers)
    {
        List<string> parts = [];
        for (int i = 0; i < towers.Count; i++)
        {
            if (towers[i].UpgradeLevel <= 0)
                continue;
            parts.Add($"{towers[i].WorldPosition.X:F0},{towers[i].WorldPosition.Y:F0}:{towers[i].UpgradeLevel}");
        }
        parts.Sort(StringComparer.Ordinal);
        return string.Join(";", parts);
    }

    internal void ResetInteractionState()
    {
        _executor.Reset();
        _cache.FailedFoundationPositions.Clear();
    }

    internal int GetSpecialization(BlightTowerType type)
    {
        IReadOnlyList<TowerBuildRule> rules = CurrentStrategy.Rules;
        for (int r = 0; r < rules.Count; r++)
            if (rules[r].TowerType == type) return rules[r].Specialization;
        return 0;
    }

    internal IReadOnlyList<BlightCachedTower> GetFoundationsInPriorityOrder()
        => KnownTowers;

    internal void Clear()
    {
        _cache.ClearData();
        _encounter.Reset();
        ResetInteractionState();
        _executor.ClearPlan();

        // A cleared encounter must rebuild its plan on the next detection.  Without this reset, a
        // re-detected encounter with identical geometry (e.g. respawn at checkpoint in the same
        // map) compares equal to the stale plan counters, ShouldRebuildPlan() skips the rebuild,
        // and the blight stays active with no plan — exactly the stuck-plan scenario after dying.
        _lastPlannedTowerCount = 0;
        _lastPlannedPathwayCount = 0;
        _lastPlannedBuiltCount = 0;
        _lastPlannedBuiltSignature = string.Empty;
    }

    internal bool IsBlightFoundationOrTowerLabel(LabelOnGround label)
        => BlightEntityCache.IsBlightFoundationOrTowerLabel(label);
    internal static Element? ResolveLabelElement(LabelOnGround label)
        => BlightEntityCache.ResolveLabelElement(label);
    internal static Entity? ResolveEntity(LabelOnGround label)
        => BlightEntityCache.ResolveEntity(label);
    internal static BlightTowerType? MapTowerIdToType(string towerId)
        => BlightHelpers.MapTowerIdToType(towerId);
}
