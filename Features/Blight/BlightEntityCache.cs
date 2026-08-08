namespace ClickIt.Features.Blight;

internal sealed class BlightEntityCache
{
    private readonly ClickItSettings _settings;
    private readonly BlightDebugEvents _debug;
    private readonly BlightEncounter _encounter;

    internal event Action? ClearRequested;
    internal event Action? DataChanged;

    private readonly Lock _blightDataLock = new();
    private readonly List<BlightCachedTower> _knownTowers = [];
    private IReadOnlyList<LabelOnGround>? _lastProcessedLabels;
    private int _lastProcessedCount;
    private GameController? _gameController;

    private readonly List<Entity> _pathwayEntities = [];
    private readonly List<NumVector2> _persistedPathwayPositions = [];
    private readonly List<long> _persistedPathwayIds = [];
    private readonly List<(Entity Entity, string TowerId)> _towerEntities = [];
    private Entity? _pumpEntity;
    private NumVector2? _persistedPumpGridPosition;
    private System.Numerics.Vector3? _persistedPumpWorldPosition;
    private bool _hasDetectedAnyBlightContent;
    private bool _hasCompletedInitialScan;
    // When no blight content has been found, the entity scan pauses between full scans; this bounds
    // how long it stays paused so an encounter starting in the current area is still picked up.
    private const long NoBlightContentRescanIntervalMs = 2000;
    private long _lastFullRefreshEntityScanMs;
    private long _lastRefreshDebugTimestampMs;
    private long _lastAreaHash = long.MinValue;

    private const string BlightFoundationPathMarker = "BlightFoundation";
    private const string BlightTowerPathMarker = "BlightTower";
    private const string BlightPathwayMetadata = "Metadata/Terrain/Leagues/Blight/Objects/BlightPathway";
    private const string BlightPumpMetadata = "Metadata/Terrain/Leagues/Blight/Objects/BlightPump";
    private const string BlightFoundationEntityMetadata = "Monsters/LeagueBlight/BlightFoundation";
    private const int MaxEntityPathCacheEntries = 2048;

    private readonly Dictionary<string, int> _towerRadiusCache = new(StringComparer.OrdinalIgnoreCase);

    // entity.Path reads are the heaviest per-entity memory/alloc cost in the scan; cache the string
    // by entity address so the 200ms sweep only reads it once per entity (path is immutable while
    // the entity lives). Cleared on encounter end / area change; touched only by the scan thread.
    private readonly Dictionary<long, (long Id, string Path)> _entityPathCache = [];

    private LaneCoverageResult[]? _cachedCoverage;
    private int _cachedCoveragePathwayCount;
    private int _cachedCoverageTowerCount;
    // Dirty flag — keeps last-good coverage available during recomputation so lanes never flash red.
    private bool _coverageDirty;
    // Signature of the last scanned coverage-relevant data; when unchanged, the scan skips invalidating
    // the coverage cache so the steady state never re-allocates the coverage computation.
    private int _lastScanCoverageSignature;
    // Pathway snapshot aligned with _cachedCoverage so the render thread always draws a consistent bundle.
    private NumVector2[]? _cachedCoveragePathways;

    private Dictionary<NumVector2, System.Numerics.Vector3> _pathwayWorldPositions = [];

    // Cached array snapshots for the render thread — repopulated lazily, avoids per-read ToArray().
    private Entity[]? _cachedPathwayEntities;
    private (Entity Entity, string TowerId)[]? _cachedTowerEntities;
    private BlightCachedTower[]? _cachedKnownTowers;

    // Reused per-scan scratch buffers (the scan runs on a single coroutine thread), so a steady-state
    // scan does not re-allocate the saved-state map and local result lists every 200ms.
    private readonly Dictionary<NumVector2, (BlightTowerType Type, int Level, BlightTowerType Planned)> _scanSavedState = [];
    private readonly List<Entity> _scanLocalPathways = [];
    private readonly List<(Entity Entity, string TowerId)> _scanLocalTowers = [];
    private readonly List<BlightCachedTower> _scanLocalKnown = [];

    internal BlightEntityCache(ClickItSettings settings, BlightDebugEvents debug, BlightEncounter encounter)
    {
        _settings = settings;
        _debug = debug;
        _encounter = encounter;
    }

    internal IReadOnlyList<Entity> PathwayEntities
    {
        get
        {
            lock (_blightDataLock)
                return _cachedPathwayEntities ??= [.. _pathwayEntities];
        }
    }
    internal int PathwayCount
    {
        get { lock (_blightDataLock) return _pathwayEntities.Count; }
    }
    internal IReadOnlyList<(Entity Entity, string TowerId)> TowerEntities
    {
        get
        {
            lock (_blightDataLock)
                return _cachedTowerEntities ??= [.. _towerEntities];
        }
    }
    internal Entity? PumpEntity
    {
        get { lock (_blightDataLock) return _pumpEntity; }
    }

    internal NumVector2? PumpGridPosition
    {
        get { lock (_blightDataLock) return _persistedPumpGridPosition; }
    }

    internal System.Numerics.Vector3? PumpWorldPosition
    {
        get { lock (_blightDataLock) return _persistedPumpWorldPosition; }
    }
    internal IReadOnlyList<BlightCachedTower> KnownTowers
    {
        get
        {
            lock (_blightDataLock)
                return _cachedKnownTowers ??= [.. _knownTowers];
        }
    }
    internal int KnownTowerCount
    {
        get { lock (_blightDataLock) return _knownTowers.Count; }
    }
    internal int BuiltTowerCount
    {
        get
        {
            lock (_blightDataLock)
            {
                int count = 0;
                for (int i = 0; i < _knownTowers.Count; i++)
                    if (_knownTowers[i].UpgradeLevel > 0)
                        count++;
                return count;
            }
        }
    }
    internal HashSet<NumVector2> FailedFoundationPositions { get; } = [];
    internal List<NumVector2> CachedBranchAnchors { get; } = [];
    internal NumVector2[]? CachedCoveragePathways
    {
        get { lock (_blightDataLock) return _cachedCoveragePathways; }
    }
    // The pathway positions aligned 1:1 with a coverage result (both derive from the same scan).
    internal List<NumVector2>? GetAlignedPathways(LaneCoverageResult[] coverage)
    {
        NumVector2[]? aligned = CachedCoveragePathways;
        return aligned != null && aligned.Length == coverage.Length ? [.. aligned] : null;
    }
    internal IReadOnlyDictionary<NumVector2, System.Numerics.Vector3> PathwayWorldPositions
    {
        get { lock (_blightDataLock) return _pathwayWorldPositions; }
    }

    private void InvalidateCachedSnapshots()
    {
        _cachedPathwayEntities = null;
        _cachedTowerEntities = null;
        _cachedKnownTowers = null;
    }

    internal LaneCoverageResult[]? TryGetCachedCoverage()
    {
        lock (_blightDataLock)
        {
            return _cachedCoverage;
        }
    }

    internal (NumVector2[] Pathways, LaneCoverageResult[] Coverage)? TryGetRenderBundle()
    {
        lock (_blightDataLock)
        {
            if (_cachedCoveragePathways == null || _cachedCoverage == null)
                return null;
            return (_cachedCoveragePathways, _cachedCoverage);
        }
    }

    internal LaneCoverageResult[] ComputeLaneCoverage()
    {
        NumVector2[] pathwaysSnapshot;
        long[] idsSnapshot;
        (Entity Entity, string TowerId, int RadiusSq)[] towersSnapshot;
        (NumVector2 Pos, BlightTowerType Type, int RadiusSq)[] cachedBuilt;
        // Allocated only on the recompute path; the cache-hit/idle paths below never touch it.
        HashSet<NumVector2> loadedTowerPositions = null!;

        lock (_blightDataLock)
        {
            if (_persistedPathwayPositions.Count < 2)
                return [];

            // Cache hit check — same lock guards the write, so no race.
            if (!_coverageDirty
                && _cachedCoverage != null
                && _cachedCoveragePathwayCount == _persistedPathwayPositions.Count
                && _cachedCoverageTowerCount == _towerEntities.Count)
                return _cachedCoverage;

            _coverageDirty = false;
            pathwaysSnapshot = [.. _persistedPathwayPositions];
            idsSnapshot = [.. _persistedPathwayIds];
            (Entity Entity, string TowerId)[] rawTowers = [.. _towerEntities];
            towersSnapshot = new (Entity, string, int)[rawTowers.Length];
            for (int t = 0; t < rawTowers.Length; t++)
            {
                int actualRadius = GetTowerRadiusCached(rawTowers[t].TowerId);
                int coverageRadius = BlightService.GetCoverageRadius(actualRadius);
                towersSnapshot[t] = (rawTowers[t].Entity, rawTowers[t].TowerId, coverageRadius * coverageRadius);
            }

            _cachedCoveragePathwayCount = _persistedPathwayPositions.Count;
            _cachedCoverageTowerCount = _towerEntities.Count;

            // Use the tower's ACTUAL dat radius when captured from a loaded entity; the linear estimate can
            // exceed the real radius and would falsely mark segments covered.
            List<(NumVector2, BlightTowerType, int)> builtList = [];
            for (int i = 0; i < _knownTowers.Count; i++)
            {
                if (_knownTowers[i].UpgradeLevel > 0)
                {
                    int r = _knownTowers[i].Radius > 0
                        ? _knownTowers[i].Radius
                        : BlightService.GetRadiusForLevel(_knownTowers[i].TowerType, _knownTowers[i].UpgradeLevel);
                    int coverageRadius = BlightService.GetCoverageRadius(r);
                    builtList.Add((_knownTowers[i].WorldPosition, _knownTowers[i].TowerType, coverageRadius * coverageRadius));
                }
            }
            cachedBuilt = [.. builtList];

            // Loaded towers' entity-based radius is authoritative — skip their cached fallback to avoid inflated coverage.
            loadedTowerPositions = [];
            for (int t = 0; t < towersSnapshot.Length; t++)
            {
                Entity te = towersSnapshot[t].Entity;
                loadedTowerPositions.Add(new NumVector2(te.GridPosNum.X, te.GridPosNum.Y));
            }
        }

        (bool chilling, bool seismic, bool fireball, bool empowering, bool shockNova, bool summoning) Probe(NumVector2 midpoint)
        {
            bool hasChilling = false, hasSeismic = false, hasFireball = false;
            bool hasEmpowering = false, hasShockNova = false, hasSummoning = false;

            void Mark(BlightTowerType? type)
            {
                if (type == BlightTowerType.Chilling) hasChilling = true;
                else if (type == BlightTowerType.Seismic) hasSeismic = true;
                else if (type == BlightTowerType.Fireball) hasFireball = true;
                else if (type == BlightTowerType.Empowering) hasEmpowering = true;
                else if (type == BlightTowerType.ShockNova) hasShockNova = true;
                else if (type == BlightTowerType.Summoning) hasSummoning = true;
            }

            void MarkAt(float x, float y, int radiusSq, BlightTowerType? type)
            {
                float dx = x - midpoint.X;
                float dy = y - midpoint.Y;
                if ((dx * dx) + (dy * dy) <= radiusSq)
                    Mark(type);
            }

            for (int t = 0; t < towersSnapshot.Length; t++)
            {
                (Entity te, string tid, int rSq) = towersSnapshot[t];
                MarkAt(te.GridPosNum.X, te.GridPosNum.Y, rSq, BlightHelpers.MapTowerIdToType(tid));
            }
            // Also check cached built towers so streamed-out towers keep their coverage; skip loaded ones already checked.
            for (int c = 0; c < cachedBuilt.Length; c++)
            {
                (NumVector2 pos, BlightTowerType type, int rSq) = cachedBuilt[c];
                if (loadedTowerPositions.Contains(pos))
                    continue;
                MarkAt(pos.X, pos.Y, rSq, type);
            }
            return (hasChilling, hasSeismic, hasFireball, hasEmpowering, hasShockNova, hasSummoning);
        }

        NumVector2? pumpPos = PumpGridPosition;

        LaneCoverageResult[] result = BlightLaneTopology.ComputeCoverage(
            pathwaysSnapshot,
            m => { (bool c, bool s, bool f, _, _, _) = Probe(m); return (c, s, f); },
            // Root the lane tree at the pump so a mid-lane fork becomes a real multi-child fork
            // (each branch attaches to the fork point) and the AND-at-fork coverage rule applies.
            pumpGridPosition: pumpPos,
            // The game's per-lane id order lets the pump hub split into one branch per lane start.
            ids: idsSnapshot,
            getSupportCoverage: m => { (_, _, _, bool e, bool sn, bool su) = Probe(m); return (e, sn, su); });

        lock (_blightDataLock)
        {
            _cachedCoverage = result;
            _cachedCoveragePathways = pathwaysSnapshot;
            _coverageDirty = false;
        }
        return result;
    }

    internal static int ComputeCoverageDataSignature(
        IReadOnlyList<NumVector2> pathwayPositions,
        IReadOnlyList<(Entity Entity, string TowerId)> towerEntities,
        IReadOnlyList<BlightCachedTower> knownTowers)
    {
        HashCode hash = new();
        hash.Add(pathwayPositions.Count);
        for (int i = 0; i < pathwayPositions.Count; i++)
        {
            hash.Add(pathwayPositions[i].X);
            hash.Add(pathwayPositions[i].Y);
        }

        hash.Add(towerEntities.Count);
        for (int t = 0; t < towerEntities.Count; t++)
        {
            (Entity entity, string towerId) = towerEntities[t];
            NumVector2 grid;
            try { grid = new NumVector2(entity.GridPosNum.X, entity.GridPosNum.Y); }
            catch { grid = default; }
            hash.Add(grid.X);
            hash.Add(grid.Y);
            hash.Add(towerId, StringComparer.Ordinal);
        }

        hash.Add(knownTowers.Count);
        for (int k = 0; k < knownTowers.Count; k++)
        {
            BlightCachedTower tower = knownTowers[k];
            hash.Add(tower.WorldPosition.X);
            hash.Add(tower.WorldPosition.Y);
            hash.Add(tower.TowerType);
            hash.Add(tower.UpgradeLevel);
            hash.Add(tower.Radius);
        }

        return hash.ToHashCode();
    }

    internal void RefreshEntities(GameController? gameController)
    {
        _gameController = gameController;

        if (!_settings.ClickBlightTowers.Value || gameController == null)
        {
            ClearRequested?.Invoke();
            return;
        }

        // Detect area transitions directly so towers from a previous encounter can't leak into the next area's plan.
        long currentAreaHash = AreaUiSnapshotReader.TryReadCurrentAreaHash(gameController, out long areaHash)
            ? areaHash
            : long.MinValue;
        if (AreaChangeRules.HasAreaHashChanged(currentAreaHash, _lastAreaHash))
        {
            _lastAreaHash = currentAreaHash;
            ClearRequested?.Invoke();
        }

        bool skipScan;
        long now = Environment.TickCount64;
        lock (_blightDataLock)
        {
            // When no blight content has ever been found, re-scan occasionally instead of skipping
            // forever: the encounter can start in the current area (pump click) with no zone change,
            // and a plugin load before that must still pick it up. The 2s bound keeps the entity
            // re-scan cheap in non-blight areas.
            skipScan = _hasCompletedInitialScan && !_hasDetectedAnyBlightContent
                && _knownTowers.Count == 0 && _pumpEntity == null
                && now - _lastFullRefreshEntityScanMs < NoBlightContentRescanIntervalMs;
        }
        if (skipScan)
            return;

        const int refreshIntervalMs = 200;
        if (_hasCompletedInitialScan && now - _lastFullRefreshEntityScanMs < refreshIntervalMs)
        {
            // Keep the debug line current even when the scan is skipped.
            PublishRefreshDebugLine(now);
            return;
        }
        _lastFullRefreshEntityScanMs = now;

        // Preserve every known tower position before the clear so streamed-out foundations survive.
        int towersBeforeClear = _knownTowers.Count;
        _scanSavedState.Clear();
        for (int i = 0; i < _knownTowers.Count; i++)
        {
            BlightCachedTower t = _knownTowers[i];
            _scanSavedState[t.WorldPosition] = (t.TowerType, t.UpgradeLevel, t.PlannedTowerType);
        }
        int savedCount = _scanSavedState.Count;

        int entityScannedFoundations = 0;
        int pathwayCount = 0;
        bool pumpFound = false;

        _scanLocalPathways.Clear();
        _scanLocalTowers.Clear();
        _scanLocalKnown.Clear();
        Entity? localPump = null;

        EntityQueryService.VisitValidEntities(gameController, entity =>
        {
            string? path = GetEntityPathCached(entity);

            if (path != null)
            {
                if (path.Contains(BlightPathwayMetadata, StringComparison.OrdinalIgnoreCase))
                {
                    _scanLocalPathways.Add(entity);
                    _hasDetectedAnyBlightContent = true;
                    pathwayCount++;
                }
                else if (path.Contains(BlightPumpMetadata, StringComparison.OrdinalIgnoreCase))
                {
                    localPump = entity;
                    _hasDetectedAnyBlightContent = true;
                    pumpFound = true;
                }
            }

            // Check for BlightTower component via reflection-safe wrapper
            if (DynamicAccess.TryGetComponent(entity, out BlightTower? blightComp)
                && blightComp != null)
            {
                string towerId = BlightHelpers.GetBlightTowerId(blightComp);
                if (!string.IsNullOrEmpty(towerId))
                {
                    _scanLocalTowers.Add((entity, towerId));
                }
            }

            // Also detect foundation entities directly (bypasses label dependency)
            if (path != null && path.Contains(BlightFoundationEntityMetadata, StringComparison.OrdinalIgnoreCase))
            {
                _hasDetectedAnyBlightContent = true;
                NumVector2 wp = BlightHelpers.GetGridPosition(entity);
                int idx = BlightHelpers.FindTowerIndexAt(_scanLocalKnown, wp);
                if (idx < 0)
                {
                    BlightTowerType fType = BlightHelpers.DetectFoundationTypeFromPath(path);
                    idx = _scanLocalKnown.Count;
                    _scanLocalKnown.Add(new BlightCachedTower(wp, fType));
                    entityScannedFoundations++;
                }

                // Store entity reference + world position (in-world helpers project from PosNum, not GridPosNum).
                _scanLocalKnown[idx].FoundationEntity = entity;
                _scanLocalKnown[idx].WorldPos3 = entity.PosNum;
            }

            return false;
        });

        // Sort the collected pathways by entity Id DESC (the game's lane order) in one O(n log n)
        // pass instead of O(n²) insertion-order inserts during the scan.
        _scanLocalPathways.Sort(static (a, b) => b.Id.CompareTo(a.Id));

        _hasCompletedInitialScan = true;

        int restoredCount = RestoreSavedState(_scanLocalKnown, _scanSavedState);

        int importedTowers = 0;
        for (int t = 0; t < _scanLocalTowers.Count; t++)
        {
            (Entity te, string tid) = _scanLocalTowers[t];
            NumVector2 tePos = BlightHelpers.GetGridPosition(te);

            int upgradeLevel = BlightHelpers.DetectUpgradeRankFromEntityPath(te);
            if (upgradeLevel == 0)
                upgradeLevel = BlightHelpers.ParseTowerIdLevel(tid);
            if (upgradeLevel == 0)
                upgradeLevel = 1;
            if (BlightHelpers.IsSpecializationTowerId(tid))
                upgradeLevel = BlightTowerData.MaxUpgradeLevel;

            BlightTowerType? mappedType = BlightHelpers.MapTowerIdToType(tid);
            if (mappedType == null)
                continue;

            // If already in localKnown, update its upgrade level — component data is more recent than saved state.
            int existingIdx = BlightHelpers.FindTowerIndexAt(_scanLocalKnown, tePos);
            if (existingIdx >= 0)
            {
                BlightCachedTower existing = _scanLocalKnown[existingIdx];
                if (existing.UpgradeLevel < upgradeLevel || existing.TowerType != mappedType.Value)
                {
                    if (existing.UpgradeLevel < upgradeLevel)
                        existing.UpgradeLevel = upgradeLevel;
                    existing.TowerType = mappedType.Value;
                    existing.PlannedTowerType = mappedType.Value;
                    importedTowers++;
                }

                // Capture the tower's ACTUAL radius from game dat — never fall back to the estimate for a measurable tower.
                _scanLocalKnown[existingIdx].Radius = GetTowerRadiusCached(tid);

                // Restore the live world position so the in-world dot keeps rendering under a built tower.
                _scanLocalKnown[existingIdx].WorldPos3 = te.PosNum;
                continue;
            }

            _scanLocalKnown.Add(new BlightCachedTower(tePos, mappedType.Value, upgradeLevel)
            {
                PlannedTowerType = mappedType.Value,
                WorldPos3 = te.PosNum,
                Radius = GetTowerRadiusCached(tid)
            });
            importedTowers++;
        }

        lock (_blightDataLock)
        {
            // Merge detected pathway positions so streamed-out segments survive the next scan.
            for (int p = 0; p < _scanLocalPathways.Count; p++)
            {
                NumVector2 pos = BlightHelpers.GetGridPosition(_scanLocalPathways[p]);
                if (pos.X == 0f && pos.Y == 0f)
                    continue;
                bool exists = false;
                for (int k = 0; k < _persistedPathwayPositions.Count; k++)
                {
                    if (BlightHelpers.SameGridPosition(_persistedPathwayPositions[k], pos))
                    { exists = true; break; }
                }
                if (!exists)
                {
                    _persistedPathwayPositions.Add(pos);
                    _persistedPathwayIds.Add(_scanLocalPathways[p].Id);
                }
            }

            _pathwayEntities.Clear();
            _pathwayEntities.AddRange(_scanLocalPathways);
            _towerEntities.Clear();
            _towerEntities.AddRange(_scanLocalTowers);
            _pumpEntity = localPump;
            if (localPump != null)
            {
                _persistedPumpGridPosition = new NumVector2(localPump.GridPosNum.X, localPump.GridPosNum.Y);
                _persistedPumpWorldPosition = localPump.PosNum;
            }
            _knownTowers.Clear();
            _knownTowers.AddRange(_scanLocalKnown);

            // Snapshot the visible pathways' world positions (with terrain Z) for lane-label projection.
            Dictionary<NumVector2, System.Numerics.Vector3> newWorldPositions = [];
            for (int p = 0; p < _scanLocalPathways.Count; p++)
            {
                Entity e = _scanLocalPathways[p];
                NumVector2 g = BlightHelpers.GetGridPosition(e);
                if (g.X == 0f && g.Y == 0f)
                    continue;
                newWorldPositions[g] = e.PosNum;
            }
            _pathwayWorldPositions = newWorldPositions;

            // Invalidate coverage only when the scan changed data the coverage computation depends on.
            // Steady-state scans (no new/changed towers, pathways, or built levels) keep the cached
            // coverage, so ComputeLaneCoverage returns it without re-allocating; the last-good bundle
            // stays visible to the render thread either way.
            int newSignature = ComputeCoverageDataSignature(_persistedPathwayPositions, _towerEntities, _knownTowers);
            if (newSignature != _lastScanCoverageSignature)
            {
                _lastScanCoverageSignature = newSignature;
                _coverageDirty = true;
            }

            // Snapshot cache is stale after list changes; next read reallocates.
            InvalidateCachedSnapshots();
        }

        UpdateEncounterState();

        int totalKnown = _knownTowers.Count;

        PublishRefreshDebugLine(now, towersBeforeClear, savedCount,
            entityScannedFoundations, restoredCount, importedTowers,
            pathwayCount, pumpFound, totalKnown);
    }

    internal static int RestoreSavedState(
        List<BlightCachedTower> scannedFoundations,
        Dictionary<NumVector2, (BlightTowerType Type, int Level, BlightTowerType Planned)> savedState)
    {
        for (int i = 0; i < scannedFoundations.Count; i++)
        {
            NumVector2 pos = scannedFoundations[i].WorldPosition;
            NumVector2? matchKey = null;
            foreach (KeyValuePair<NumVector2, (BlightTowerType Type, int Level, BlightTowerType Planned)> kvp in savedState)
            {
                if (BlightHelpers.SameGridPosition(kvp.Key, pos))
                {
                    matchKey = kvp.Key;
                    break;
                }
            }
            if (!matchKey.HasValue)
                continue;
            NumVector2 key = matchKey.Value;
            (BlightTowerType Type, int Level, BlightTowerType Planned) state = savedState[key];
            scannedFoundations[i].TowerType = state.Type;
            scannedFoundations[i].UpgradeLevel = state.Level;
            scannedFoundations[i].PlannedTowerType = state.Planned;
            savedState.Remove(key);
        }

        int restoredCount = 0;
        foreach (KeyValuePair<NumVector2, (BlightTowerType Type, int Level, BlightTowerType Planned)> kvp in savedState)
        {
            scannedFoundations.Add(new BlightCachedTower(kvp.Key, kvp.Value.Type, kvp.Value.Level)
            {
                PlannedTowerType = kvp.Value.Planned
            });
            restoredCount++;
        }
        return restoredCount;
    }

    private void PublishRefreshDebugLine(long now, int towersBefore = -1, int saved = -1,
        int scannedF = -1, int restored = -1, int imported = -1,
        int pathways = -1, bool pumpFound = false, int totalKnown = -1)
    {
        if (now - _lastRefreshDebugTimestampMs < 1000)
            return;

        _lastRefreshDebugTimestampMs = now;

        int towers = totalKnown >= 0 ? totalKnown : _knownTowers.Count;
        string active = _encounter.IsActive ? "yes" : "no";
        string pump = pumpFound ? "yes" : (_pumpEntity != null || _persistedPumpGridPosition.HasValue ? "cached" : "none");

        // Omit skipped-scan stats (-1) from the summary.
        StringBuilder sb = new("Refresh:");

        sb.Append($" towers={towers}");

        if (towersBefore >= 0)
            sb.Append($" before={towersBefore}");
        if (saved >= 0)
            sb.Append($" saved={saved}");
        if (scannedF >= 0)
            sb.Append($" scannedF={scannedF}");
        if (restored >= 0)
            sb.Append($" restored={restored}");
        if (imported >= 0)
            sb.Append($" imported={imported}");

        if (pathways >= 0)
            sb.Append($" pathways={pathways}");
        sb.Append($" pump={pump} active={active}");

        _debug.Add(sb.ToString());
    }

    private void UpdateEncounterState()
    {
        Entity? pump;
        int pathwayCount;
        bool hasPersistedPump;
        lock (_blightDataLock)
        {
            pump = _pumpEntity;
            pathwayCount = _pathwayEntities.Count;
            hasPersistedPump = _persistedPumpGridPosition.HasValue;
        }

        bool wasActive = _encounter.IsActive;
        bool ended = _encounter.Update(pump, pathwayCount, hasPersistedPump);

        // An encounter (re)start must not wait for the next label change to trigger the plan
        // rebuild — the entity scan already knows the encounter is back (e.g. after respawn), so
        // signal it immediately.  Clears (ended) deliberately do not fire DataChanged.
        if (!wasActive && _encounter.IsActive)
            DataChanged?.Invoke();

        if (!ended)
            return;

        if (pump == null)
        {
            ClearRequested?.Invoke();
        }
        else
        {
            _debug.Add("Blight encounter ended - clearing cached data");
            ClearRequested?.Invoke();
        }
    }

    private static string? GetEntityPath(Entity entity)
    {
        try { return entity.Path; }
        catch { return null; }
    }

    // Cached entity-path read (validated by entity id) — avoids a process-memory path read + string
    // allocation per call in the executor's hot per-tick rank/verify loops.
    internal string? GetEntityPathCached(Entity entity)
    {
        long address = DynamicAccess.TryGetDynamicValue(entity, DynamicAccessProfiles.Address, out object? rawAddress)
            && rawAddress != null
            ? Convert.ToInt64(rawAddress)
            : 0;
        long entityId = ReadEntityId(entity);
        if (address != 0 && entityId != 0
            && _entityPathCache.TryGetValue(address, out (long Id, string Path) cached)
            && cached.Id == entityId)
            return cached.Path;

        string? path = GetEntityPath(entity);
        if (path != null && address != 0 && entityId != 0)
        {
            if (_entityPathCache.Count >= MaxEntityPathCacheEntries)
                _entityPathCache.Clear();
            _entityPathCache[address] = (entityId, path);
        }

        return path;
    }

    private static long ReadEntityId(Entity entity)
    {
        try { return entity.Id; }
        catch { return 0; }
    }

    internal void ScanFoundations(IReadOnlyList<LabelOnGround>? allLabels)
    {
        if (!_settings.ClickBlightTowers.Value)
        {
            ClearRequested?.Invoke();
            return;
        }

        if (ReferenceEquals(_lastProcessedLabels, allLabels) && _lastProcessedCount == (allLabels?.Count ?? 0))
            return;

        _lastProcessedLabels = allLabels;
        _lastProcessedCount = allLabels?.Count ?? 0;

        if (allLabels == null || allLabels.Count == 0)
            return;

        int labelCount = 0;
        foreach (LabelOnGround label in allLabels)
        {
            if (TryGetMetadataPath(label)?.Contains(BlightFoundationPathMarker, StringComparison.OrdinalIgnoreCase) == true)
                labelCount++;
        }
        _debug.Add($"ScanFoundations: {labelCount} blight labels among {allLabels.Count} total");

        foreach (LabelOnGround label in allLabels)
        {
            string? path = TryGetMetadataPath(label);
            if (path == null || !path.Contains(BlightFoundationPathMarker, StringComparison.OrdinalIgnoreCase))
                continue;

            Element? labelElement = ResolveLabelElement(label);
            if (labelElement == null)
                continue;

            Entity? entity = ResolveEntity(label);
            if (entity == null)
                continue;

            NumVector2 worldPos = BlightHelpers.GetGridPosition(entity);

            // The foundation type comes from the label's metadata path (the same BlightFoundation<Tower>
            // pattern the entity scan uses in RefreshEntities) — never from a hardcoded default.
            BlightTowerType currentType = BlightHelpers.DetectFoundationTypeFromPath(path);

            lock (_blightDataLock)
            {
                int existingIndex = BlightHelpers.FindTowerIndexAt(_knownTowers, worldPos);
                if (existingIndex >= 0)
                {
                    BlightCachedTower existing = _knownTowers[existingIndex];
                    // Never let a foundation label clobber a built tower's type — built types come
                    // from the entity import / executor, and a lingering foundation label must not
                    // turn a built Seismic/Fireball into a Chilling tower.
                    if (existing.UpgradeLevel == 0)
                        existing.TowerType = currentType;
                    existing.FoundationEntity = entity;
                    existing.LabelElement = labelElement;
                }
                else
                {
                    _knownTowers.Add(new BlightCachedTower(worldPos, currentType)
                    {
                        FoundationEntity = entity,
                        LabelElement = labelElement
                    });
                }
            }
        }

        DataChanged?.Invoke();
    }

    internal int GetTowerRadiusCached(string towerId)
    {
        if (_towerRadiusCache.TryGetValue(towerId, out int cached))
            return cached;

        try
        {
            if (_gameController?.Game?.Files?.BlightTowers?.EntriesList != null)
            {
                foreach (BlightTowerDat? entry in _gameController.Game.Files.BlightTowers.EntriesList)
                {
                    string? entryId = null;
                    try { entryId = entry.Id; } catch { }
                    if (string.Equals(entryId, towerId, StringComparison.OrdinalIgnoreCase))
                    {
                        int radius = BlightService.DefaultTowerRadius;
                        try { radius = entry.Radius; } catch { }
                        _towerRadiusCache[towerId] = radius;
                        return radius;
                    }
                }
            }
        }
        catch
        {
        }

        int fallback = BlightTowerData.FindRadius(towerId);
        if (fallback <= 0)
            fallback = BlightService.DefaultTowerRadius;
        _towerRadiusCache[towerId] = fallback;
        return fallback;
    }

    internal string DumpBlightTowerDat()
    {
        StringBuilder sb = new();
        try
        {
            List<BlightTowerDat>? entries = _gameController?.Game?.Files?.BlightTowers?.EntriesList;
            if (entries == null)
            {
                sb.AppendLine("Blight Tower Dat: (BlightTowers file not loaded)");
                return sb.ToString();
            }

            sb.AppendLine($"Blight Tower Dat ({entries.Count} entries):");
            foreach (BlightTowerDat? entry in entries)
            {
                if (entry == null) continue;
                string id = "?";
                string name = "?";
                int radius = 0;
                try { id = (entry.Id as string) ?? "?"; } catch { }
                try { name = (entry.Name as string) ?? "?"; } catch { }
                try { radius = entry.Radius; } catch { }
                sb.AppendLine($"  {id} | {radius} | {name}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Blight Tower Dat: (error: {ex.Message})");
        }
        return sb.ToString();
    }

    internal void UpdateKnownTowerLevel(NumVector2 position, BlightTowerType type, int upgradeLevel)
    {
        lock (_blightDataLock)
        {
            int index = BlightHelpers.FindTowerIndexAt(_knownTowers, position);
            if (index < 0)
                return;
            if (upgradeLevel > _knownTowers[index].UpgradeLevel)
            {
                _knownTowers[index].UpgradeLevel = upgradeLevel;
                _knownTowers[index].TowerType = type;
                _knownTowers[index].PlannedTowerType = type;
            }
        }
    }

    internal void ApplyPlannedTowerTypes(BlightPlan plan)
    {
        lock (_blightDataLock)
        {
            for (int s = 0; s < plan.Steps.Count; s++)
            {
                BlightPlanStep step = plan.Steps[s];
                int index = BlightHelpers.FindTowerIndexAt(_knownTowers, step.FoundationPosition);
                if (index >= 0)
                    _knownTowers[index].PlannedTowerType = step.TowerType;
            }
        }
    }

    internal Entity? GetBestEntityAtPosition(NumVector2 pos)
    {
        IReadOnlyList<BlightCachedTower> towers = KnownTowers;
        int foundationIndex = BlightHelpers.FindTowerIndexAt(towers, pos);
        if (foundationIndex >= 0 && towers[foundationIndex].FoundationEntity != null
            && IsBlightFoundationOrTowerEntity(towers[foundationIndex].FoundationEntity))
            return towers[foundationIndex].FoundationEntity;

        IReadOnlyList<(Entity Entity, string TowerId)> towerList = TowerEntities;
        for (int t = 0; t < towerList.Count; t++)
        {
            NumVector2 tePos;
            try { tePos = new NumVector2(towerList[t].Entity.GridPosNum.X, towerList[t].Entity.GridPosNum.Y); }
            catch { continue; }
            if (BlightHelpers.SameGridPosition(tePos, pos))
                return towerList[t].Entity;
        }

        return null;
    }

    private static bool IsBlightFoundationOrTowerEntity(Entity? entity)
    {
        if (entity == null)
            return false;
        string? path = GetEntityPath(entity);
        if (path == null
            || (!path.Contains(BlightFoundationPathMarker, StringComparison.OrdinalIgnoreCase)
                && !path.Contains(BlightTowerPathMarker, StringComparison.OrdinalIgnoreCase)))
            return false;
        // A streamed-out / dead entity must never be used as a walk/verify target — it would make the
        // executor chase a tower that isn't there ("target REJECTED valid=False" loop).
        return DynamicAccess.TryReadBool(entity, DynamicAccessProfiles.IsValid, out bool valid) && valid;
    }

    internal static bool IsBlightFoundationOrTowerLabel(LabelOnGround label)
    {
        string? path = TryGetMetadataPath(label);
        return path != null
            && (path.Contains(BlightFoundationPathMarker, StringComparison.OrdinalIgnoreCase)
                || path.Contains(BlightTowerPathMarker, StringComparison.OrdinalIgnoreCase));
    }

    internal static Element? ResolveLabelElement(LabelOnGround label)
    {
        try
        {
            if (DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.Label, out object? rawLabel)
                && rawLabel is Element labelElement)
                return labelElement;
        }
        catch { }
        return null;
    }

    internal static Entity? ResolveEntity(LabelOnGround label)
    {
        try
        {
            if (DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                && rawItem is Entity entity)
                return entity;
        }
        catch { }
        return null;
    }

    private static string? TryGetMetadataPath(LabelOnGround label)
    {
        try
        {
            if (DynamicAccess.TryGetDynamicValue(label, DynamicAccessProfiles.ItemOnGround, out object? rawItem)
                && rawItem != null
                && DynamicAccess.TryReadString(rawItem, DynamicAccessProfiles.Path, out string resolvedPath))
                return resolvedPath;
        }
        catch { }
        return null;
    }

    internal void ClearData()
    {
        lock (_blightDataLock)
        {
            _knownTowers.Clear();
            _pathwayEntities.Clear();
            _persistedPathwayPositions.Clear();
            _persistedPathwayIds.Clear();
            _towerEntities.Clear();
            _pumpEntity = null;
            _persistedPumpGridPosition = null;
            _persistedPumpWorldPosition = null;
            _entityPathCache.Clear();
            _pathwayWorldPositions = [];
        }
        CachedBranchAnchors.Clear();
        _lastProcessedLabels = null;
        _hasDetectedAnyBlightContent = false;
        _hasCompletedInitialScan = false;
        FailedFoundationPositions.Clear();
        lock (_blightDataLock)
        {
            _cachedCoverage = null;
            _cachedCoveragePathways = null;
            _coverageDirty = false;
            _lastScanCoverageSignature = 0;
        }
    }
}
