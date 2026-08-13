namespace ClickIt.Features.Blight;

public sealed class BlightService
{
    private readonly ClickItSettings _settings;
    private readonly Func<Vector2, bool> _isPointInClickableArea;
    private readonly BreakdownRecorder? _recordBreakdown;
    private readonly Action<long, double>? _recordExecutorStage;
    private GameController? _gameController;

    private readonly BlightEntityCache _cache;
    private readonly BlightEncounter _encounter = new();

    internal IReadOnlyList<Entity> PathwayEntities => _cache.PathwayEntities;
    internal IReadOnlyList<(Entity Entity, string TowerId)> TowerEntities => _cache.TowerEntities;
    internal IReadOnlyList<BlightPathwayIcon> IconPathwaySnapshot => _cache.IconPathwaySnapshot;
    internal Entity? PumpEntity => _cache.PumpEntity;
    internal NumVector2? PumpGridPosition => _cache.PumpGridPosition;

    internal string DumpPumpStateMachine()
        => PumpEntity is { } pump ? BlightEncounter.DumpPumpStateMachine(pump) : "Pump: (none)";
    internal System.Numerics.Vector3? PumpWorldPosition => _cache.PumpWorldPosition;
    internal bool IsEncounterActive => _encounter.IsActive;
    internal IReadOnlyList<BlightCachedTower> KnownTowers => _cache.KnownTowers;
    internal IReadOnlyDictionary<NumVector2, System.Numerics.Vector3> PathwayWorldPositions => _cache.PathwayWorldPositions;

    internal BlightService(
        ClickItSettings settings,
        Func<Vector2, bool>? isPointInClickableArea = null,
        BreakdownRecorder? recordBreakdown = null,
        Action<long, double>? recordExecutorStage = null,
        Action<long, double>? recordEventStage = null)
    {
        _settings = settings;
        _isPointInClickableArea = isPointInClickableArea ?? (static _ => true);
        _recordBreakdown = recordBreakdown;
        _recordExecutorStage = recordExecutorStage;
        _cache = new BlightEntityCache(settings, _debugEvents, _encounter, recordEventStage);
        _cache.ClearRequested += Clear;
        _cache.EncounterEnded += ClearAfterEncounterEnd;
        _cache.DataChanged += HandleCacheDataChanged;
    }

    // Refresh sub-stage breakdown (entities / foundations / coverage), forwarded from the overlay.
    internal void RecordBreakdown(ReadOnlySpan<long> stageBytes, ReadOnlySpan<double> stageMs)
        => _recordBreakdown?.Invoke(stageBytes, stageMs);

    // Plugin disable/reload teardown: unhooks the live GameController entity-event subscriptions so the disposed cache's handler (a per-entity path read) stops running on every EntityAdded.
    internal void DisposeForShutdown()
        => _cache.DisposeForShutdown();

    // Executor stage (the click-pipeline building work): recorded separately from the refresh stages because it runs on the click thread at a different cadence.
    internal void RecordExecutorStage(long bytes, double ms)
        => _recordExecutorStage?.Invoke(bytes, ms);

    internal bool IsPointInClickableArea(Vector2 point) => _isPointInClickableArea(point);

    internal IBlightTowerStrategy CurrentStrategy => BlightStrategyResolver.Resolve(_settings);

    internal LaneCoverageResult[]? TryGetCachedCoverage() => _cache.TryGetCachedCoverage();
    internal (NumVector2[] Pathways, LaneCoverageResult[] Coverage)? TryGetRenderBundle() => _cache.TryGetRenderBundle();
    internal LaneCoverageResult[] ComputeLaneCoverage() => _cache.ComputeLaneCoverage();

    internal (List<NumVector2> Positions, List<(PumpBranch Branch, List<int> Segments)> Branches, List<int> UnassignedSegments, List<List<int>>? Children, List<List<BlightLaneNode>>? Forests) GetBranchDebug()
        => _cache.GetBranchDebugCached();

    internal void RefreshEntities(GameController? gameController)
    {
        _gameController = gameController;
        _cache.RefreshEntities(gameController);
    }

    internal void ScanFoundations(IReadOnlyList<LabelOnGround>? allLabels)
        => _cache.ScanFoundations(allLabels);
    internal int GetTowerRadiusCached(string towerId) => _cache.GetTowerRadiusCached(towerId);
    internal string DumpPathwayDebug()
    {
        IReadOnlyList<Entity> pathways = _cache.PathwayEntities;
        StringBuilder sb = new();
        NumVector2? pump = _cache.PumpGridPosition;

        static (NumVector2 Grid, float Dist)? TryResolve(Entity e, NumVector2? pump)
        {
            try
            {
                NumVector2 g = new(e.GridPosNum.X, e.GridPosNum.Y);
                float d = pump.HasValue
                    ? MathF.Sqrt(((g.X - pump.Value.X) * (g.X - pump.Value.X)) + ((g.Y - pump.Value.Y) * (g.Y - pump.Value.Y)))
                    : -1f;
                return (g, d);
            }
            catch { return null; }
        }

        int show = SystemMath.Min(pathways.Count, 16);
        for (int i = 0; i < show; i++)
        {
            (NumVector2 Grid, float Dist)? r = TryResolve(pathways[i], pump);
            if (r == null)
                continue;
            sb.Append($"  [{i}] id={pathways[i].Id} ({r.Value.Grid.X:F0},{r.Value.Grid.Y:F0})");
            if (r.Value.Dist >= 0f)
                sb.Append($" dPump={r.Value.Dist:F1}");
            sb.Append('\n');
        }
        if (pathways.Count > show)
            sb.Append($"  ... {pathways.Count - show} more\n");

        sb.AppendLine("Pathways near pump (d<=45, id desc):");
        int nearCount = 0;
        for (int i = 0; i < pathways.Count; i++)
        {
            (NumVector2 Grid, float Dist)? r = TryResolve(pathways[i], pump);
            if (r == null || r.Value.Dist > 45f)
                continue;
            sb.Append($"  id={pathways[i].Id} ({r.Value.Grid.X:F0},{r.Value.Grid.Y:F0}) dPump={r.Value.Dist:F1}\n");
            nearCount++;
        }
        sb.Append($"  near-pump pathways: {nearCount}\n");
        return sb.ToString();
    }

    internal string DumpPathwayIconDebug() => _cache.DumpPathwayIconDebug();

    internal string DumpBranchRootDebug()
    {
        LaneCoverageResult[]? coverage = _cache.TryGetCachedCoverage();
        if (coverage is not { Length: > 0 })
            return "(no coverage)";
        NumVector2[]? positions = _cache.CachedCoveragePathways;
        if (positions == null || positions.Length != coverage.Length)
            return "(no aligned pathway positions)";
        NumVector2? pump = _cache.PumpGridPosition;

        StringBuilder sb = new();
        sb.AppendLine($"Branch roots (orphan segments, {coverage.Length} total):");
        int nearCount = 0;
        for (int i = 0; i < coverage.Length; i++)
        {
            if (coverage[i].ParentIndex != BlightLaneTopology.OrphanSentinel)
                continue;
            NumVector2 p = positions[i];
            float d = pump.HasValue
                ? MathF.Sqrt(((p.X - pump.Value.X) * (p.X - pump.Value.X)) + ((p.Y - pump.Value.Y) * (p.Y - pump.Value.Y)))
                : -1f;
            bool near = pump.HasValue && d <= BlightLaneTopology.PumpRootRadius;
            if (near)
                nearCount++;
            sb.Append($"  [{i}] ({p.X:F0},{p.Y:F0})");
            if (d >= 0f)
                sb.Append($" dPump={d:F1} {(near ? "NEAR(<=30)" : "far")}");
            sb.Append('\n');
        }
        sb.Append($"  near-pump orphans: {nearCount}\n");
        return sb.ToString();
    }

    // Dumps the server's authoritative lane list header + raw element bytes for reverse-engineering.
    internal string DumpBlightServerLanes()
    {
        if (_gameController?.Game?.IngameState?.ServerData is not { } serverData)
            return "(no ServerData)";
        try
        {
            ServerDataOffsets offsets = serverData.ServerDataStruct;
            NativePtrArray lanes = offsets.BlightLanes;
            long span = lanes.Last - lanes.First;
            if (lanes.First == 0 || span <= 0)
                return $"(BlightLanes empty: First=0x{lanes.First:X} Last=0x{lanes.Last:X} End=0x{lanes.End:X})";

            StringBuilder sb = new();
            sb.AppendLine($"ServerData.BlightLanes: First=0x{lanes.First:X} Last=0x{lanes.Last:X} End=0x{lanes.End:X} span={span} bytes");
            foreach (int size in LaneElementSizeCandidates)
                if (span % size == 0)
                    sb.AppendLine($"  if {size} bytes/elem -> {span / size} lanes");
            sb.Append(DumpBlightLaneElements(lanes.First, (int)SystemMath.Min(span, 96)));
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"(BlightLanes read failed: {ex.GetType().Name}: {ex.Message})";
        }
    }

    private static readonly int[] LaneElementSizeCandidates = [8, 12, 16, 20, 24, 32, 40, 48, 64];

    private string DumpBlightLaneElements(long first, int count)
    {
        if (_gameController?.Memory is not { } mem)
            return "(no memory access)";
        byte[] raw;
        try { raw = mem.ReadBytes(first, count); }
        catch (Exception ex) { return $"(element read failed: {ex.GetType().Name})"; }

        StringBuilder sb = new();
        for (int i = 0; i < raw.Length; i += 32)
        {
            int len = SystemMath.Min(32, raw.Length - i);
            sb.Append($"  elem[{i / 32}] hex:");
            for (int b = 0; b < len; b++)
                sb.Append($" {raw[i + b]:X2}");
            sb.Append("\n            ints:");
            for (int b = 0; b + 4 <= len; b += 4)
                sb.Append($" {BitConverter.ToInt32(raw, i + b)}");
            sb.Append("\n            flts:");
            for (int b = 0; b + 4 <= len; b += 4)
                sb.Append($" {BitConverter.ToSingle(raw, i + b):F1}");
            sb.Append('\n');
        }
        return sb.ToString();
    }

    internal string DumpBlightTowerDat() => _cache.DumpBlightTowerDat();

    internal const int DefaultTowerRadius = 35;

    // Spec §2.2: every tower's radius is reduced by a fixed safety margin of 5 before ANY coverage decision (real radius still used for rendering).
    internal const int CoverageRadiusMargin = 5;

    internal static int GetCoverageRadiusForLevel(BlightTowerType type, int level)
        => SystemMath.Max(0, GetRadiusForLevel(type, level) - CoverageRadiusMargin);

    internal static int GetCoverageRadius(int realRadius)
        => realRadius > CoverageRadiusMargin ? realRadius - CoverageRadiusMargin : 0;

    internal static int GetRadiusForLevel(BlightTowerType type, int level)
    {
        // Real per-tier radii from BlightTowerDat; built towers use the live dat radius, this is the fallback for unbuilt ones.
        return BlightTowerData.RadiusForLevel(type, level);
    }

    private readonly BlightPlanExecutor _executor = new();
    private IReadOnlyList<LabelOnGround>? _lastLabels;
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

    // Un-throttled executor event trail (menu snapshots, phase transitions, click decisions) so the detailed "what did the executor actually see/click" trail is never dropped by the 10/sec Recent Stages throttle. Bounded + deduped like the stages buffer.
    private readonly BlightDebugEvents _executorEvents = new();
    internal IReadOnlyList<string> ExecutorEvents => _executorEvents.Stages;

    internal void AddExecutorEvent(string message) => _executorEvents.Add(message);

    // Throttle debug stages to ~10/sec; the overlay only needs a readable recent trail.
    private long _lastDebugStageTimestampMs;
    private const long DebugStageThrottleMs = 100;

    // Lets hot-path callers skip FORMATTING a debug message when the throttle would drop it.
    internal bool IsDebugStageDue
    {
        get
        {
            long now = Environment.TickCount64;
            return now - _lastDebugStageTimestampMs >= DebugStageThrottleMs;
        }
    }

    internal void AddDebugStage(string message)
    {
        long now = Environment.TickCount64;
        if (now - _lastDebugStageTimestampMs < DebugStageThrottleMs)
            return;
        _lastDebugStageTimestampMs = now;
        _debugEvents.Add(message);
    }

    internal ElementTreeInspector BlightChestDebug { get; } = new();

    internal Entity? GetPathfindingTargetEntity()
    {
        BlightPlan? plan = _executor.CurrentPlan;
        if (plan == null || plan.IsComplete) return null;
        BlightPlanStep? step = plan.CurrentStep;
        if (step == null) return null;

        // FoundationEntity is only valid for unbuilt towers — fall back to the tower entity list for built ones.
        Entity? entity = GetBestEntityAtPosition(step.Value.FoundationPosition);
        if (entity == null) return null;

        // Stop walking only when the executor genuinely doesn't need to approach any more — the executor and the pipeline share ONE walk-readiness decision (BlightPlanExecutor. WantsWalkForCurrentStep), so pathfinding can never refuse a walk the executor needs (e.g. an upgrade icon that sits off-window while the tower entity is already on-screen).
        if (!_executor.WantsWalkForCurrentStep(_gameController, this, _lastLabels))
            return null;
        return entity;
    }

    internal Entity? GetBestEntityAtPosition(NumVector2 pos) => _cache.GetBestEntityAtPosition(pos);

    // The tower's dat id at a position (e.g. "MeteorTower"/"FlamethrowerTower" for a specialized Fireball) — read from the cached component data. Unlike the entity path (which is the base type + rank, e.g. "BlightTowerFlameRank4"), the dat id reveals the actual specialization, so the executor can verify a spec click landed on the intended tower.
    internal string? GetTowerDatIdAt(NumVector2 pos) => _cache.GetTowerDatIdAt(pos);

    internal Entity? GetTowerEntityAt(NumVector2 pos) => _cache.GetTowerEntityAt(pos);

    // Cached entity-path read (entity-id validated) for the executor's per-tick rank/verify loops — avoids re-reading entity.Path (a process-memory read + string allocation) every tick.
    internal string? GetEntityPathCached(Entity entity) => _cache.GetEntityPathCached(entity);
    internal static string? GetEntityPath(Entity entity) => BlightEntityCache.GetEntityPathFresh(entity);

    internal bool IsEntityFullyOnScreen(Entity? entity)
    {
        if (entity == null || _gameController == null) return false;
        try
        {
            Camera? camera = _gameController.Game?.IngameState?.Camera;
            if (camera == null) return false;
            return BlightHelpers.IsWorldPosOnScreen(
                camera, _gameController.Window.GetWindowRectangleTimeCache.Size, entity.PosNum, allowance: 0f);
        }
        catch { return false; }
    }

    internal BlightBuildAction TryProgressBlightBuilding(IReadOnlyList<LabelOnGround>? allLabels)
    {
        _lastLabels = allLabels;
        if (!_settings.ClickBlightTowers.Value || !IsEncounterActive)
            return new BlightBuildAction(BlightBuildActionKind.None, DebugMessage: "Inactive");

        long executorStart = Stopwatch.GetTimestamp();
        long executorAllocStart = GC.GetAllocatedBytesForCurrentThread();
        BlightBuildAction action = _executor.Tick(_gameController, allLabels, this);
        _recordExecutorStage?.Invoke(
            GC.GetAllocatedBytesForCurrentThread() - executorAllocStart,
            (Stopwatch.GetTimestamp() - executorStart) * 1000.0 / Stopwatch.Frequency);
        return action;
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
        NumVector2? pumpPos = _cache.PumpGridPosition;
        NumVector2? playerPos = _gameController?.Player != null
            ? new NumVector2(_gameController.Player.GridPosNum.X, _gameController.Player.GridPosNum.Y)
            : null;

        List<NumVector2>? pathwayPositions = _cache.GetAlignedPathways(coverage);

        // Coordinate-space correction: terrain objects (pump/pathways) can report local-space positions offset from world space by thousands of units — apply the foundation-centroid offset only when it is large.
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

        BlightPlan plan = BlightPlanner.Build(KnownTowers, coverage, CurrentStrategy.Rules, _cache.FailedFoundationPositions, _planVersion, pumpPos, playerPos, pathwayPositions, _cache.CachedBranchAnchors, CurrentStrategy.GroupStepsByProximity);

        _cache.ApplyPlannedTowerTypes(plan);

        int prevCursor = _executor.CurrentCursor;
        _executor.SetPlan(plan);
        string cursorMsg = _executor.CurrentCursor == prevCursor
            ? $"cursor preserved at {_executor.CurrentCursor}"
            : $"cursor reset {prevCursor}->{_executor.CurrentCursor}";
        AddDebugStage($"{plan.DebugSummary} | {chains} chains {orphans} orphans segs={segCovered}+{segUncovered} {cursorMsg}");
    }

    private bool ShouldRebuildPlan()
    {
        // Rebuild when pathway, tower, built-count, or built-tower LEVEL data has changed.
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
        // Fail closed: no rule for this type means "no specialization", never a real spec index (0 would treat a stale lvl4 step as a spec step and click the first spec button — e.g. Meteor on a type whose strategy caps at 3).
        return (int)TowerSpecialization.None;
    }

    // Plan UI display: a spec-tier upgrade (Fireball lvl4) shows the specialization tower the strategy chose ("Meteor") instead of the raw base type + level ("Fireball lvl4").
    internal string GetStepTargetName(BlightPlanStep step)
    {
        if (step.Action == BlightPlanAction.Upgrade && step.TargetLevel > 3)
        {
            int specIndex = GetSpecialization(step.TowerType);
            if (specIndex >= 0)
            {
                string towerId = BlightTowerData.GetSpecializationTowerId(step.TowerType, (TowerSpecialization)specIndex);
                BlightTowerInfo? info = BlightTowerData.FindByDatId(towerId);
                if (info.HasValue)
                    return BlightTowerData.SpecDisplayName(info.Value);
            }
        }
        return BlightTowerData.DisplayName(step.TowerType);
    }

    internal void Clear()
    {
        _cache.ClearData();
        _encounter.Reset();
        ResetInteractionState();
        _executor.ClearPlan();

        // A cleared encounter must rebuild its plan on the next detection — without this reset a re-detected encounter with identical geometry would compare equal to the stale counters and ShouldRebuildPlan() would skip the rebuild.
        _lastPlannedTowerCount = 0;
        _lastPlannedPathwayCount = 0;
        _lastPlannedBuiltCount = 0;
        _lastPlannedBuiltSignature = string.Empty;
    }

    // An encounter END clears the data and stops the executor/plan, but must NOT call _encounter.Reset(): the ended latch must survive so the re-rendered blight web (the player walked far from the pump) can never re-activate a finished encounter in the same area. Only an area change / explicit Clear() resets the encounter for the next area.
    internal void ClearAfterEncounterEnd()
    {
        _cache.ClearData();
        ResetInteractionState();
        _executor.ClearPlan();
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
