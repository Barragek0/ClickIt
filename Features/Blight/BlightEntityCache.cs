namespace ClickIt.Features.Blight;

// One BlightPathway IngameIcon = one real lane segment. BeamStart is the segment's own world position; BeamEnd is the world position one step OUTWARD (away from the pump). Lanes form a WEB: every icon whose BeamEnd matches this icon's BeamStart is a pump-ward parent. Parents[0] is the primary/tree parent (used for coverage propagation and branch naming); Parents[1..] are the extra real beam connections at convergence junctions (two lanes joining into one). Computed per scan.
internal readonly record struct BlightPathwayIcon(
    int Id,
    NumVector2 GridPos,
    int VisualState,
    System.Numerics.Vector3 BeamStart,
    System.Numerics.Vector3 BeamEnd,
    int[] Parents)
{
    // visual == 1 (spawned, not yet sending enemies) and visual == 2 (actively sending) show the lane; visual == 3 (all enemies sent, inactive) hides it. 0 = unknown, hidden.
    internal bool IsActive => VisualState is 1 or 2;
}

internal sealed class BlightEntityCache
{
    private readonly ClickItSettings _settings;
    private readonly BlightDebugEvents _debug;
    private readonly BlightEncounter _encounter;

    internal event Action? ClearRequested;
    internal event Action? EncounterEnded;
    internal event Action? DataChanged;

    // Plugin disable/reload teardown: clears the cache's blight state (and the shared hub's blight category); the hub's EntityAdded/EntityRemoved subscription is unhooked in ClickIt.OnClose.
    internal void DisposeForShutdown()
    {
        ClearData();
    }

    private readonly Lock _blightDataLock = new();
    private readonly List<BlightCachedTower> _knownTowers = [];
    private IReadOnlyList<LabelOnGround>? _lastProcessedLabels;
    private int _lastProcessedCount;
    private GameController? _gameController;

    private readonly List<Entity> _pathwayEntities = [];
    private readonly List<(Entity Entity, string TowerId)> _towerEntities = [];
    private Entity? _pumpEntity;
    private NumVector2? _persistedPumpGridPosition;
    private bool _hasDetectedAnyBlightContent;
    private bool _hasCompletedInitialScan;
    // When no blight content has been found, the entity scan pauses between full scans; this bounds how long it stays paused so an encounter starting in the current area is still picked up.
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

    // Shared by the scan coroutine, the entity-event thread (EntityEventHub.Reseed/EntityAdded) and the render thread (DrawTowerRanges) without a common lock, so both caches must be concurrent.
    private readonly ConcurrentDictionary<string, int> _towerRadiusCache = new(StringComparer.OrdinalIgnoreCase);

    // entity.Path reads are the heaviest per-entity memory/alloc cost in the scan; cache the string by entity address so the 200ms sweep only reads it once per entity (path is immutable while the entity lives). Cleared on encounter end / area change.
    private readonly ConcurrentDictionary<long, (long Id, string Path)> _entityPathCache = [];

    private LaneCoverageResult[]? _cachedCoverage;
    // Dirty flag — keeps last-good coverage available during recomputation so lanes never flash red.
    private bool _coverageDirty;
    // Signature of the last scanned coverage-relevant data; when unchanged, the scan skips invalidating the coverage cache so the steady state never re-allocates the coverage computation.
    private int _lastScanCoverageSignature;
    // Pathway snapshot aligned with _cachedCoverage so the render thread always draws a consistent bundle.
    private NumVector2[]? _cachedCoveragePathways;

    // Branch-debug data cached on the scan thread so the render thread never re-runs the branch search or the coverage-tree building (children + lane forests) that the debug tree and lane labels render.
    private LaneCoverageResult[]? _cachedBranchDebugCoverage;
    private List<NumVector2>? _cachedBranchDebugPositions;
    private List<(PumpBranch Branch, List<int> Segments)>? _cachedBranchDebugBranches;
    private List<int>? _cachedBranchDebugUnassigned;
    private List<List<int>>? _cachedBranchDebugChildren;
    private List<List<BlightLaneNode>>? _cachedBranchDebugForests;

    private Dictionary<NumVector2, System.Numerics.Vector3> _pathwayWorldPositions = [];

    // Cached array snapshots for the render thread — repopulated lazily, avoids per-read ToArray().
    private Entity[]? _cachedPathwayEntities;
    private (Entity Entity, string TowerId)[]? _cachedTowerEntities;
    private BlightCachedTower[]? _cachedKnownTowers;

    // Reused per-scan scratch buffers (the scan runs on a single coroutine thread), so a steady-state scan does not re-allocate the saved-state map and local result lists every 200ms.
    private readonly Dictionary<NumVector2, (BlightTowerType Type, int Level, BlightTowerType Planned)> _scanSavedState = [];
    private readonly List<Entity> _scanLocalPathways = [];
    private readonly List<(Entity Entity, string TowerId)> _scanLocalTowers = [];
    private readonly List<BlightCachedTower> _scanLocalKnown = [];

    // Game-visible pathway icons (BlightPathway IngameIcons) with active/inactive + beam endpoints; the coverage tree and the lane overlay both derive from this snapshot.
    private readonly List<BlightPathwayIcon> _iconPathwaySnapshot = [];
    // Icons keyed by entity Id so streamed-out segments (OnlyValidEntities drops entities as the player moves) survive the next scan — same persistence policy as the old geometry positions.
    private readonly Dictionary<long, BlightPathwayIcon> _persistedIcons = [];
    // Live count of currently-flowing lanes (StateMachine pending > 0 among valid retained entities); the encounter uses it as the liveness signal when the pump has streamed out and is unreadable.
    private int _activePathwayCount;
    private BlightPathwayIcon[]? _cachedIconPathwaySnapshot;

    // The shared EntityEventHub retains the blight set (pathways + pump + towers + foundations) with ONE EntityAdded/EntityRemoved subscription and ONE path read per event (the walking burst must never do a read per consumer per entity — the freeze source). The refresh re-reads each retained entity's CURRENT path on the small retained set to classify pathway vs structure, and tower level/type from the component because it changes in place and does not fire events. Reports the accumulated entity-event cost (bytes, ms) once per refresh; wired to the Blight breakdown's "Events" stage so the walking-triggered entity-event burst is visible.
    private readonly Action<long, double>? _recordEventCost;

    internal BlightEntityCache(ClickItSettings settings, BlightDebugEvents debug, BlightEncounter encounter, Action<long, double>? recordEventCost = null)
    {
        _settings = settings;
        _debug = debug;
        _encounter = encounter;
        _recordEventCost = recordEventCost;
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
    internal ConcurrentDictionary<NumVector2, byte> CachedBranchAnchors { get; } = new();
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

    internal IReadOnlyList<BlightPathwayIcon> IconPathwaySnapshot
    {
        get { lock (_blightDataLock) return _cachedIconPathwaySnapshot ??= [.. _iconPathwaySnapshot]; }
    }

    private void InvalidateCachedSnapshots()
    {
        _cachedPathwayEntities = null;
        _cachedTowerEntities = null;
        _cachedKnownTowers = null;
        _cachedIconPathwaySnapshot = null;
    }

    private static BlightPathwayIcon ReadPathwayIcon(Entity entity)
    {
        NumVector2 grid = BlightHelpers.GetGridPosition(entity);

        int visualState = ReadPathwayVisualState(entity);

        System.Numerics.Vector3 beamStart = default;
        System.Numerics.Vector3 beamEnd = default;
        if (DynamicAccess.TryGetComponent(entity, out Beam? beam) && beam != null)
        {
            beamStart = ReadVector3(beam, static b => b.BeamStartNum);
            beamEnd = ReadVector3(beam, static b => b.BeamEndNum);
        }

        return new BlightPathwayIcon((int)entity.Id, grid, visualState, beamStart, beamEnd, []);
    }

    // The lane's own StateMachine "visual" state is the authoritative render signal: visual == 1 (spawned, not yet sending enemies) and visual == 2 (actively sending) both show the lane, visual == 3 (all enemies sent, inactive) hides it. Returns the visual value when readable, else 0 (unknown — treated as hidden).
    private static int ReadPathwayVisualState(Entity entity)
    {
        long? visual = BlightEncounter.TryReadPumpState(entity, "visual");
        return visual is 1 or 2 or 3 ? (int)visual.Value : 0;
    }

    // Only currently-valid entities have trustworthy component reads; streamed-out (retained but invalid) entities must keep their last-good persisted icon data instead.
    private static bool IsEntityCurrentlyValid(Entity entity)
    {
        try { return entity.IsValid; }
        catch { return false; }
    }

    // Path read for a retained-but-invalid (streamed-out/far-away) structure entity: GetEntityPathCached does Address+Id DLR reads that can throw on an invalid entity, so the retained path is read directly and fail-closed (null) so the caller keeps last-known state.
    private static string? ReadRetainedPath(Entity entity)
    {
        try { return entity.Path; }
        catch { return null; }
    }

    // Fail-closed world-position read for a retained structure entity (PosNum can throw on an invalid/streamed-out entity, e.g. a far-away foundation's in-world dot).
    private static System.Numerics.Vector3 SafeReadPosNum(Entity entity)
    {
        try { return entity.PosNum; }
        catch { return default; }
    }

    // Subscribe to the retained-entity events once per GameController; reseed when the set was cleared (encounter end, area change, blight toggle) so entities already present refill it. Cooldown the reseed: with no blight pathways in the area the set stays empty, and a per-refresh reseed would scan every retained entity on every 200ms refresh.
    private const long EventReseedIntervalMs = 2000;
    private long _lastEventReseedMs;

    private void EnsureEntityEventSubscription(GameController gameController)
    {
        EntityEventHub.Instance.EnsureSubscribed(gameController);
        long now = Environment.TickCount64;
        if (EntityEventHub.Instance.Blight.Count == 0 && now - _lastEventReseedMs >= EventReseedIntervalMs)
        {
            _lastEventReseedMs = now;
            EntityEventHub.Instance.Reseed();
        }
    }

    private static System.Numerics.Vector3 ReadVector3(object? source, Func<dynamic, object?> accessor)
    {
        if (DynamicAccess.TryGetDynamicValue(source, accessor, out object? raw) && raw is System.Numerics.Vector3 v3)
            return v3;
        return default;
    }

    // Lanes chain by beam endpoints: this segment's BeamEnd == the next segment's BeamStart (BeamEnd points one step OUTWARD, away from the pump). Lanes form a WEB, not a strict tree: at a convergence junction two beams can end at the SAME point that another beam starts from (two lanes joining into one), so an icon can have several pump-ward parents. We connect EVERY beam first (multimap over beam ends), then apply the genuine filters: gap-close only fills icons with no exact parent, and the co-located twin merge folds duplicate node entities. Parents[0] is the primary (tree) parent used by coverage propagation and branch naming; Parents[1..] are the extra real beam connections the overlay draws.
    internal static int[][] ComputePathwayParents(IReadOnlyList<BlightPathwayIcon> icons)
    {
        int n = icons.Count;
        int[][] parents = new int[n][];
        for (int i = 0; i < n; i++)
            parents[i] = [];

        Dictionary<long, List<int>> byEnd = new(n);
        for (int i = 0; i < n; i++)
        {
            long key = RoundBeamKey(icons[i].BeamEnd);
            if (!byEnd.TryGetValue(key, out List<int>? list))
            {
                list = [];
                byEnd[key] = list;
            }
            list.Add(i);
        }

        for (int i = 0; i < n; i++)
        {
            if (byEnd.TryGetValue(RoundBeamKey(icons[i].BeamStart), out List<int>? list))
            {
                List<int> ps = [];
                for (int k = 0; k < list.Count; k++)
                    if (list[k] != i)
                        ps.Add(list[k]);
                parents[i] = ps.ToArray();
            }
        }

        // Gap-close fallback: an icon whose beam start matches no beam end exactly (the game's rounded endpoints can drift by a hair) chains to the nearest icon whose beam end is within GapCloseLinkMaxDistance of its beam start, so coverage keeps propagating past a single nearly-matching hop instead of severing the lane at an orphan.
        for (int i = 0; i < n; i++)
        {
            if (parents[i].Length > 0)
                continue;
            System.Numerics.Vector3 start = icons[i].BeamStart;
            int best = -1;
            float bestDistSq = GapCloseLinkMaxDistance * GapCloseLinkMaxDistance;
            for (int j = 0; j < n; j++)
            {
                if (j == i)
                    continue;
                float dx = icons[j].BeamEnd.X - start.X;
                float dy = icons[j].BeamEnd.Y - start.Y;
                float d = (dx * dx) + (dy * dy);
                if (d < bestDistSq) { bestDistSq = d; best = j; }
            }
            if (best >= 0)
                parents[i] = [best];
        }

        // Co-located twin merge: the game can emit several pathway entities for the SAME lane node (one per outgoing arm at a fork/hub). When co-located icons share the same valid parent (a real fork node), keep the first as the node and make the later ones zero-length children so the shared incoming edge is drawn once instead of stacked on top of itself.
        for (int i = 0; i < n; i++)
        {
            if (parents[i].Length == 0)
                continue;
            long key = RoundKeyXY(icons[i].GridPos.X, icons[i].GridPos.Y);
            for (int j = i + 1; j < n; j++)
            {
                if (parents[j].Length == 0)
                    continue;
                if (!SameParents(parents[i], parents[j]))
                    continue;
                if (RoundKeyXY(icons[j].GridPos.X, icons[j].GridPos.Y) != key)
                    continue;
                parents[j] = [i];
            }
        }

        // Two overlapping routes between the same lane junctions (the game lays one lane twice) wrap the beam parent chain into a cycle. A cycle has no orphan head, so the loop component is never claimed by any branch and renders as unassigned — break it so every chain reaches a root, preferring to connect the loop to a rooted tree via a secondary parent.
        BreakParentCycles(parents);
        return parents;
    }

    // Breaks primary-parent cycles so the lane tree stays acyclic and every pump-ward chain reaches an orphan root. When a cycle node has a secondary parent that leads toward a rooted tree its primary is re-pointed there (connecting the whole loop to the main tree); otherwise the node whose parent closes the loop is orphaned so the loop becomes a proper tree.
    private static void BreakParentCycles(int[][] parents)
    {
        int n = parents.Length;
        if (n < 2)
            return;

        int[] prim = new int[n];
        for (int i = 0; i < n; i++)
            prim[i] = parents[i].Length > 0 ? parents[i][0] : -1;

        // Flood "rooted" from orphan heads (no primary parent) along primary-parent edges, so a node is rooted exactly when its chain reaches an orphan; the un-rooted nodes are the cycle parts.
        List<int>[] children = new List<int>[n];
        for (int i = 0; i < n; i++)
            children[i] = [];
        for (int i = 0; i < n; i++)
            if (prim[i] >= 0)
                children[prim[i]].Add(i);

        bool[] rooted = new bool[n];
        Queue<int> queue = new();
        for (int i = 0; i < n; i++)
            if (prim[i] < 0)
            {
                rooted[i] = true;
                queue.Enqueue(i);
            }
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (int child in children[cur])
                if (!rooted[child])
                {
                    rooted[child] = true;
                    queue.Enqueue(child);
                }
        }

        // Resolve each un-rooted component (a cycle plus any chain feeding it): walk the primary chain to locate the cycle, then connect it to a rooted tree via a cycle node's secondary parent, or orphan the closing node so the loop becomes a proper tree.
        int[] state = new int[n]; // 0 unvisited, 1 on the current walk, 2 resolved
        for (int start = 0; start < n; start++)
        {
            if (rooted[start] || state[start] != 0)
                continue;

            List<int> path = [];
            int cur = start;
            while (cur >= 0 && !rooted[cur] && state[cur] == 0)
            {
                state[cur] = 1;
                path.Add(cur);
                cur = prim[cur];
            }

            // The walk ended at a root or an already-resolved node: the whole chain resolves.
            if (cur < 0 || rooted[cur] || state[cur] == 2)
            {
                foreach (int p in path)
                    state[p] = 2;
                continue;
            }

            // state[cur] == 1: cur is already on the path, so path[cycleStart..] is the cycle.
            int cycleStart = path.IndexOf(cur);
            bool connected = false;
            for (int i = cycleStart; i < path.Count; i++)
            {
                int node = path[i];
                for (int k = 1; k < parents[node].Length; k++)
                {
                    int alt = parents[node][k];
                    if (alt >= 0 && alt < n && rooted[alt])
                    {
                        // Move the rooted secondary to the primary slot, keeping every other real beam edge (deduped) so the loop connects to the rooted tree without dropping the overlapping route from the web.
                        int[] existing = parents[node];
                        int[] updated = new int[existing.Length];
                        updated[0] = alt;
                        int w = 1;
                        for (int m = 0; m < existing.Length; m++)
                            if (existing[m] != alt)
                                updated[w++] = existing[m];
                        if (w < updated.Length)
                            Array.Resize(ref updated, w);
                        parents[node] = updated;
                        prim[node] = alt;
                        connected = true;
                        break;
                    }
                }
                if (connected)
                    break;
            }

            if (!connected)
            {
                int node = path[^1]; // its primary parent closes the loop
                parents[node] = [];
                prim[node] = -1;     // orphan it so the loop becomes a tree rooted here
            }

            foreach (int p in path)
                state[p] = 2;
        }
    }

    private static bool SameParents(int[] a, int[] b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i])
                return false;
        return true;
    }

    // Primary (tree) parent per icon — Parents[0], or -1 at the pump end of a lane.
    internal static int[] ComputePathwayLinks(IReadOnlyList<BlightPathwayIcon> icons, ref int[]? scratch)
    {
        int[][] parents = ComputePathwayParents(icons);
        int[] links = scratch != null && scratch.Length >= icons.Count ? scratch : new int[icons.Count];
        scratch = links;
        for (int i = 0; i < icons.Count; i++)
            links[i] = parents[i].Length > 0 ? parents[i][0] : -1;
        return links;
    }

    // The game can spawn duplicate pathway entities (same spot and same beam, separate entity ids); keep one icon per (spot, beam) triple so duplicates never split or tangle the lane chain. Shared pump stubs keep their own beams and stay separate.
    internal static List<BlightPathwayIcon> DedupePathwayIcons(IEnumerable<BlightPathwayIcon> icons)
    {
        List<BlightPathwayIcon> result = [];
        Dictionary<(long Grid, long Start, long End), int> byKey = [];
        foreach (BlightPathwayIcon icon in icons)
        {
            (long, long, long) key = (
                RoundKeyXY(icon.GridPos.X, icon.GridPos.Y),
                RoundKeyXY(icon.BeamStart.X, icon.BeamStart.Y),
                RoundKeyXY(icon.BeamEnd.X, icon.BeamEnd.Y));
            if (!byKey.TryGetValue(key, out int index))
            {
                byKey[key] = result.Count;
                result.Add(icon);
                continue;
            }

            // A dead duplicate at the same spot must never hide the live lane, so the active icon wins when two overlapping lane entities collide.
            if (!result[index].IsActive && icon.IsActive)
                result[index] = icon;
        }
        return result;
    }

    private static long RoundBeamKey(System.Numerics.Vector3 v)
        => RoundKeyXY(v.X, v.Y);

    private static long RoundKeyXY(float x, float y)
        => ((long)MathF.Round(x * 10f) << 32) | (uint)MathF.Round(y * 10f);

    private const float GapCloseLinkMaxDistance = 2.5f;

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

    private void RefreshBranchDebugCache(LaneCoverageResult[] coverage, NumVector2[] positions)
    {
        NumVector2? pump = PumpGridPosition;
        List<(PumpBranch, List<int>)> branchData = [];
        List<int> unassigned = [];
        List<List<int>>? children = null;
        List<List<BlightLaneNode>>? forests = null;
        if (pump.HasValue)
        {
            List<PumpBranch> branches = BlightBranches.FindPumpBranches(coverage, pump, positions, CachedBranchAnchors);
            bool[] assigned = new bool[coverage.Length];
            for (int b = 0; b < branches.Count; b++)
            {
                List<int> segments = BlightBranches.BranchSegments(coverage, branches[b]);
                branchData.Add((branches[b], segments));
                for (int i = 0; i < segments.Count; i++)
                    assigned[segments[i]] = true;
            }
            for (int s = 0; s < coverage.Length; s++)
            {
                if (coverage[s].IsPumpStub || assigned[s])
                    continue;
                unassigned.Add(s);
            }
            if (branchData.Count > 0)
            {
                children = BlightLaneTopology.BuildCoverageChildren(coverage);
                forests = [];
                for (int b = 0; b < branchData.Count; b++)
                {
                    (PumpBranch branch, List<int> segments) = branchData[b];
                    if (branch.CoverageSegment < 0)
                    {
                        forests.Add([]);
                        continue;
                    }
                    forests.Add(BlightLaneTopology.BuildBranchLaneForest(
                        coverage, children, segments, branch.CoverageSegment, ((char)('A' + (b % 26))).ToString()));
                }
            }
        }
        lock (_blightDataLock)
        {
            _cachedBranchDebugCoverage = coverage;
            _cachedBranchDebugPositions = [.. positions];
            _cachedBranchDebugBranches = branchData;
            _cachedBranchDebugUnassigned = unassigned;
            _cachedBranchDebugChildren = children;
            _cachedBranchDebugForests = forests;
        }
    }

    internal (List<NumVector2> Positions, List<(PumpBranch Branch, List<int> Segments)> Branches, List<int> UnassignedSegments, List<List<int>>? Children, List<List<BlightLaneNode>>? Forests) GetBranchDebugCached()
    {
        lock (_blightDataLock)
        {
            if (_cachedBranchDebugCoverage != null
                && ReferenceEquals(_cachedBranchDebugCoverage, _cachedCoverage)
                && _cachedBranchDebugBranches != null)
                return (_cachedBranchDebugPositions!, _cachedBranchDebugBranches, _cachedBranchDebugUnassigned!, _cachedBranchDebugChildren, _cachedBranchDebugForests);
        }
        LaneCoverageResult[]? coverage = TryGetCachedCoverage();
        if (coverage is not { Length: > 0 } || _cachedCoveragePathways is not { } pathways)
            return (new List<NumVector2>(), new List<(PumpBranch, List<int>)>(), new List<int>(), null, null);
        RefreshBranchDebugCache(coverage, pathways);
        lock (_blightDataLock)
            return (_cachedBranchDebugPositions!, _cachedBranchDebugBranches!, _cachedBranchDebugUnassigned!, _cachedBranchDebugChildren, _cachedBranchDebugForests);
    }

    // Debug: raw tracked pathway entities vs. the persisted icon snapshot, with beam endpoints, so a lane connector that never made it into the snapshot (zero GridPos, invalid entity, dedupe) is visible instead of silently missing from the coverage tree.
    internal string DumpPathwayIconDebug()
    {
        StringBuilder sb = new();
        lock (_blightDataLock)
        {
            sb.AppendLine($"Tracked pathway entities: {_pathwayEntities.Count}, persisted icons: {_persistedIcons.Count}, snapshot icons: {_iconPathwaySnapshot.Count}");
            for (int p = 0; p < _pathwayEntities.Count; p++)
            {
                Entity e = _pathwayEntities[p];
                if (e == null)
                    continue;
                if (!IsEntityCurrentlyValid(e))
                {
                    sb.AppendLine($"  [raw {p}] id={SafeId(e)} INVALID");
                    continue;
                }
                BlightPathwayIcon icon = ReadPathwayIcon(e);
                NumVector2 start = BlightHelpers.BeamAnchorToGrid(icon.GridPos, icon.BeamStart);
                NumVector2 end = BlightHelpers.WorldToGrid(icon.BeamEnd);
                bool inSnapshot = _persistedIcons.ContainsKey(icon.Id);
                sb.Append($"  [raw {p}] id={icon.Id} grid=({icon.GridPos.X:F0},{icon.GridPos.Y:F0})");
                sb.Append($" act={(icon.IsActive ? 1 : 0)}");
                sb.Append($" beamStart=({start.X:F1},{start.Y:F1}) beamEnd=({end.X:F1},{end.Y:F1})");
                sb.Append($" kept={(inSnapshot ? 1 : 0)}");
                sb.Append('\n');
            }
            sb.AppendLine("Icon snapshot (post-filter):");
            for (int p = 0; p < _iconPathwaySnapshot.Count; p++)
            {
                BlightPathwayIcon icon = _iconPathwaySnapshot[p];
                NumVector2 start = BlightHelpers.BeamAnchorToGrid(icon.GridPos, icon.BeamStart);
                NumVector2 end = BlightHelpers.WorldToGrid(icon.BeamEnd);
                sb.Append($"  [{p}] id={icon.Id} grid=({icon.GridPos.X:F0},{icon.GridPos.Y:F0})");
                sb.Append($" act={(icon.IsActive ? 1 : 0)}");
                sb.Append($" beamStart=({start.X:F1},{start.Y:F1}) beamEnd=({end.X:F1},{end.Y:F1})");
                sb.Append($" parents=[{string.Join(",", icon.Parents)}]");
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    private static long SafeId(Entity entity)
    {
        try { return entity.Id; }
        catch { return -1; }
    }

    internal LaneCoverageResult[] ComputeLaneCoverage()
    {
        lock (_blightDataLock)
        {
            // The scan already flags coverage dirty only when the icon/tower data it depends on changed, so an unchanged scan returns the cached tree instead of recomputing it (the O(n^2)-ish coverage pass on a 400+ segment web is the single biggest Blight cost).
            if (!_coverageDirty && _cachedCoverage != null)
                return _cachedCoverage;
        }
        return ComputeIconLaneCoverage();
    }

    // Icon-lane mode: the coverage tree is built from the game's BlightPathway icon beam chains instead of the geometry/id-run graph, so every consumer (render bundle, branches debug, planner) sees the true lane structure.  The pump-ward head of each chain is the branch root.
    private LaneCoverageResult[] ComputeIconLaneCoverage()
    {
        BlightPathwayIcon[] iconsSnapshot;
        lock (_blightDataLock)
        {
            if (_iconPathwaySnapshot.Count < 2)
                return [];
            iconsSnapshot = [.. _iconPathwaySnapshot];
        }

        Func<NumVector2, (bool chilling, bool seismic, bool fireball, bool empowering, bool shockNova, bool summoning)> probe
            = BuildCoverageProbe(out _, out _);

        NumVector2[] positions = new NumVector2[iconsSnapshot.Length];
        int[] parents = new int[iconsSnapshot.Length];
        int[][]? allParents = null;
        for (int i = 0; i < iconsSnapshot.Length; i++)
        {
            // The beam is the true lane geometry: anchor every segment at its beam start (world -> grid) instead of the icon's dot position, so the lane lines, coverage midpoints, and arrows all follow the beams the monsters actually walk.
            positions[i] = BlightHelpers.BeamAnchorToGrid(iconsSnapshot[i].GridPos, iconsSnapshot[i].BeamStart);
            parents[i] = iconsSnapshot[i].Parents.Length > 0 ? iconsSnapshot[i].Parents[0] : -1;
            if (iconsSnapshot[i].Parents.Length > 1)
            {
                // A convergence junction feeds the tree's single primary parent, but every incoming beam is a real walkable segment: probe all of them for coverage (OR), not just the one the tree keeps as the continuation.
                allParents ??= new int[iconsSnapshot.Length][];
                allParents[i] = iconsSnapshot[i].Parents;
            }
        }

        NumVector2? pumpPos = PumpGridPosition;
        (bool, bool, bool) CoverProbe(NumVector2 m)
        {
            (bool c, bool s, bool f, _, _, _) = probe(m);
            return (c, s, f);
        }
        (bool, bool, bool) SupportProbe(NumVector2 m)
        {
            (_, _, _, bool e, bool sn, bool su) = probe(m);
            return (e, sn, su);
        }
        LaneCoverageResult[] result = BlightLaneTopology.ComputeCoverage(positions, CoverProbe, parents, SupportProbe, allParents);

        // The game can split one visual lane into several beam chains. After the normal branch pass, any chain that still has no branch is attached to the most appropriate branch — its head sits at a branch lane's end, so it is re-parented under that end and becomes the lane's continuation.
        if (pumpPos.HasValue)
        {
            List<PumpBranch> branches = BlightBranches.FindPumpBranches(result, pumpPos, positions, CachedBranchAnchors);
            bool attachedAny = BlightBranches.AttachUnassignedLanes(result, positions, branches);
            attachedAny |= BlightBranches.AttachParallelLanes(result, positions, branches);
            if (attachedAny)
            {
                // Rebuild with the attached parent tree so the re-parented chain gets a real midpoint (an orphan attached to a lane end otherwise keeps the (0,0) default), freshly probed coverage flags, and propagation through the joined tree.
                for (int i = 0; i < result.Length; i++)
                    parents[i] = result[i].ParentIndex;
                result = BlightLaneTopology.ComputeCoverage(positions, CoverProbe, parents, SupportProbe, allParents);
            }
        }

        lock (_blightDataLock)
        {
            // The icon snapshot keeps the RAW beam web (Parents) — the overlay draws the primary edge from the coverage tree (coverage[i].ParentIndex, gap-closing attached links included) and every extra parent edge from Parents[1..], so no post-attach sync of the snapshot's parents is needed.
            _cachedIconPathwaySnapshot = null;
            _cachedCoverage = result;
            _cachedCoveragePathways = positions;
            _coverageDirty = false;
        }
        RefreshBranchDebugCache(result, positions);
        return result;
    }

    // Snapshot the tower state and build the per-midpoint coverage probe under the data lock; the returned closure is safe to call without the lock because it only touches the copied arrays.
    private Func<NumVector2, (bool chilling, bool seismic, bool fireball, bool empowering, bool shockNova, bool summoning)> BuildCoverageProbe(
        out (Entity Entity, string TowerId, int RadiusSq)[] towersSnapshot,
        out (NumVector2 Pos, BlightTowerType Type, int RadiusSq)[] cachedBuilt)
    {
        // Local copies so the returned closure never captures the out parameters (forbidden in C#).
        (Entity Entity, string TowerId, int RadiusSq)[] towers;
        (NumVector2 Pos, BlightTowerType Type, int RadiusSq)[] built;
        lock (_blightDataLock)
        {
            (Entity Entity, string TowerId)[] rawTowers = [.. _towerEntities];
            towersSnapshot = new (Entity, string, int)[rawTowers.Length];
            for (int t = 0; t < rawTowers.Length; t++)
            {
                int actualRadius = GetTowerRadiusCached(rawTowers[t].TowerId);
                int coverageRadius = BlightService.GetCoverageRadius(actualRadius);
                towersSnapshot[t] = (rawTowers[t].Entity, rawTowers[t].TowerId, coverageRadius * coverageRadius);
            }

            // Use the tower's ACTUAL dat radius when captured from a loaded entity; the linear estimate can exceed the real radius and would falsely mark segments covered.
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
            towers = towersSnapshot;
            built = cachedBuilt;

            // Loaded towers' entity-based radius is authoritative — skip their cached fallback to avoid inflated coverage.
            HashSet<NumVector2> loadedTowerPositions = [];
            for (int t = 0; t < towers.Length; t++)
            {
                Entity te = towers[t].Entity;
                loadedTowerPositions.Add(BlightHelpers.GetGridPosition(te));
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

                for (int t = 0; t < towers.Length; t++)
                {
                    (Entity te, string tid, int rSq) = towers[t];
                    NumVector2 grid = BlightHelpers.GetGridPosition(te);
                    MarkAt(grid.X, grid.Y, rSq, BlightHelpers.MapTowerIdToType(tid));
                }
                // Also check cached built towers so streamed-out towers keep their coverage; skip loaded ones already checked.
                for (int c = 0; c < built.Length; c++)
                {
                    (NumVector2 pos, BlightTowerType type, int rSq) = built[c];
                    if (loadedTowerPositions.Contains(pos))
                        continue;
                    MarkAt(pos.X, pos.Y, rSq, type);
                }
                return (hasChilling, hasSeismic, hasFireball, hasEmpowering, hasShockNova, hasSummoning);
            }

            return Probe;
        }
    }

    internal static int ComputeCoverageDataSignature(
        IReadOnlyList<BlightPathwayIcon> pathwayIcons,
        IReadOnlyList<(Entity Entity, string TowerId)> towerEntities,
        IReadOnlyList<BlightCachedTower> knownTowers)
    {
        HashCode hash = new();
        hash.Add(pathwayIcons.Count);
        for (int i = 0; i < pathwayIcons.Count; i++)
        {
            hash.Add(pathwayIcons[i].GridPos.X);
            hash.Add(pathwayIcons[i].GridPos.Y);
            hash.Add(pathwayIcons[i].Id);
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

        // Report the entity-event cost accumulated since the last refresh as ONE sample, so a walking burst (many EntityAdded path reads on the main thread) shows as a single large Events-stage value instead of being hidden behind per-event averages.
        if (_recordEventCost != null)
        {
            (long eventBytes, double eventMs) = EntityEventHub.Instance.TakePendingCost();
            if (eventBytes > 0 || eventMs > 0)
                _recordEventCost(eventBytes, eventMs);
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

        EnsureEntityEventSubscription(gameController);

        bool skipScan;
        long now = Environment.TickCount64;
        lock (_blightDataLock)
        {
            // When no blight content has been found, re-scan occasionally so an encounter starting in the current area is still picked up.
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

        // Blight structures (pump/towers/foundations) come from the EVENT-MAINTAINED retained set — the complete structure set with no per-refresh full-entity walk. Re-read each retained entity's CURRENT state because tower level/type changes in place (build/upgrade/specialize) and does not fire EntityAdded/Removed; this is what the old VisitValidEntities pass did but only for the handful of retained structure entities instead of every entity in the area. A valid-entity walk is only the fallback when nothing is retained yet (first scan or a stale/unseeded retained cache), so the common per-refresh path stays cheap.
        List<Entity> blightEntities = EntityEventHub.Instance.Blight.Snapshot();

        void ClassifyStructure(Entity entity, string? path)
        {
            if (path != null && path.Contains(BlightPumpMetadata, StringComparison.OrdinalIgnoreCase))
            {
                localPump ??= entity;
                _hasDetectedAnyBlightContent = true;
                pumpFound = true;
            }

            if (IsEntityCurrentlyValid(entity))
            {
                if (DynamicAccess.TryGetComponent(entity, out BlightTower? blightComp)
                    && blightComp != null)
                {
                    string towerId = BlightHelpers.GetBlightTowerId(blightComp);
                    if (!string.IsNullOrEmpty(towerId))
                        _scanLocalTowers.Add((entity, towerId));
                }
            }
            else if (path != null && path.Contains(BlightTowerPathMarker, StringComparison.OrdinalIgnoreCase))
            {
                // Streamed-out/far-away tower: the component is unreadable, so derive type + rank from the retained path so coverage and the plan still account for it. The tower import loop prefers the authoritative component/known state when it becomes valid.
                BlightTowerType? fType = BlightHelpers.DetectTowerTypeFromPath(path);
                if (fType.HasValue)
                {
                    NumVector2 wp = BlightHelpers.GetGridPosition(entity);
                    if (wp != NumVector2.Zero && BlightHelpers.FindTowerIndexAt(_scanLocalKnown, wp) < 0)
                    {
                        int rank = BlightHelpers.DetectUpgradeRankFromPath(path);
                        _scanLocalKnown.Add(new BlightCachedTower(wp, fType.Value, SystemMath.Max(1, rank)));
                        entityScannedFoundations++;
                    }
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

                // In-world helpers project from PosNum, not GridPosNum.
                _scanLocalKnown[idx].FoundationEntity = entity;
                _scanLocalKnown[idx].WorldPos3 = SafeReadPosNum(entity);
            }
        }

        if (blightEntities.Count > 0)
        {
            for (int b = 0; b < blightEntities.Count; b++)
            {
                Entity entity = blightEntities[b];
                string? path = IsEntityCurrentlyValid(entity) ? GetEntityPathCached(entity) : ReadRetainedPath(entity);
                if (path != null && path.Contains(BlightPathwayMetadata, StringComparison.OrdinalIgnoreCase))
                {
                    // Pathway: part of the lane set.
                    _scanLocalPathways.Add(entity);
                    _hasDetectedAnyBlightContent = true;
                    pathwayCount++;
                }
                else
                {
                    ClassifyStructure(entity, path);
                }
            }
        }
        else
        {
            // Discovery fallback (only when nothing is retained yet): a stale/unseeded retained cache or an encounter that started after the last reseed. All entities here are valid, so the component-based tower path applies; no path-derived far-away fallback is needed. This fallback also discovers pathways by the same path marker.
            EntityQueryService.VisitValidEntities(gameController, entity =>
            {
                string? path = GetEntityPathCached(entity);
                if (path != null && path.Contains(BlightPathwayMetadata, StringComparison.OrdinalIgnoreCase))
                {
                    _scanLocalPathways.Add(entity);
                    _hasDetectedAnyBlightContent = true;
                    pathwayCount++;
                }
                else
                {
                    ClassifyStructure(entity, path);
                }
                return false;
            });
        }

        // Sort the collected pathways by entity Id DESC (the game's lane order) in one O(n log n) pass instead of O(n²) insertion-order inserts during the scan.
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
            _pathwayEntities.Clear();
            _pathwayEntities.AddRange(_scanLocalPathways);
            _towerEntities.Clear();
            _towerEntities.AddRange(_scanLocalTowers);
            _pumpEntity = localPump;
            if (localPump != null)
            {
                _persistedPumpGridPosition = new NumVector2(localPump.GridPosNum.X, localPump.GridPosNum.Y);
            }
            _knownTowers.Clear();
            _knownTowers.AddRange(_scanLocalKnown);

            // Game-visible pathway icons feed the icon-lane coverage and overlay. Persisted by entity Id so icons that stream out of the active list (player moved away) keep rendering on the map; only currently-valid entities refresh their active/beam state — a streamed-out entity's component reads can be stale and must not overwrite the last good persisted value.
            _iconPathwaySnapshot.Clear();
            _activePathwayCount = 0;
            for (int p = 0; p < _scanLocalPathways.Count; p++)
            {
                Entity e = _scanLocalPathways[p];
                if (!IsEntityCurrentlyValid(e))
                    continue;
                BlightPathwayIcon icon = ReadPathwayIcon(e);
                if (icon.IsActive)
                    _activePathwayCount++;
                // A junction connector can carry a real Beam but no readable icon dot (GridPos (0,0)); keep it when the beam is valid — the beam anchor becomes its position (lanes are beam geometry now), so a lane joining another at a corner is not lost.
                bool hasBeam = icon.BeamStart != default || icon.BeamEnd != default;
                if (icon.GridPos.X == 0f && icon.GridPos.Y == 0f && !hasBeam)
                    continue;
                _persistedIcons[icon.Id] = icon;
            }
            _iconPathwaySnapshot.AddRange(DedupePathwayIcons(_persistedIcons.Values));
            int[][] parents = ComputePathwayParents(_iconPathwaySnapshot);
            for (int p = 0; p < _iconPathwaySnapshot.Count; p++)
                _iconPathwaySnapshot[p] = _iconPathwaySnapshot[p] with { Parents = parents[p] };

            // Snapshot the pathway world positions (with terrain Z) for lane-label/arrow projection, keyed by BOTH the dot grid position and the beam-derived anchor so labels and arrows resolve the terrain height whether they project dot positions or beam anchors.
            Dictionary<NumVector2, System.Numerics.Vector3> newWorldPositions = [];
            for (int p = 0; p < _iconPathwaySnapshot.Count; p++)
            {
                BlightPathwayIcon icon = _iconPathwaySnapshot[p];
                System.Numerics.Vector3 world = BlightHelpers.BeamWorld(icon.GridPos, icon.BeamStart);
                NumVector2 beamGrid = BlightHelpers.BeamAnchorToGrid(icon.GridPos, icon.BeamStart);
                if (icon.GridPos.X != 0f || icon.GridPos.Y != 0f)
                    newWorldPositions.TryAdd(icon.GridPos, world);
                if (beamGrid != icon.GridPos)
                    newWorldPositions.TryAdd(beamGrid, world);
            }
            _pathwayWorldPositions = newWorldPositions;

            // Invalidate coverage only when the scan changed data the coverage computation depends on.
            int newSignature = ComputeCoverageDataSignature(_iconPathwaySnapshot, _towerEntities, _knownTowers);
            if (newSignature != _lastScanCoverageSignature)
            {
                _lastScanCoverageSignature = newSignature;
                _coverageDirty = true;
            }

            // Snapshot cache is stale after list changes; next read reallocates.
            InvalidateCachedSnapshots();

            // A scan that finds no live blight content re-arms the no-blight pause so later refreshes skip the per-200ms scan — otherwise one transient detection (previous area, a stray entity) keeps the expensive scan running forever in a blight-free area.
            if (_towerEntities.Count == 0 && _pathwayEntities.Count == 0 && _pumpEntity == null)
                _hasDetectedAnyBlightContent = false;
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
        int activePathwayCount;
        lock (_blightDataLock)
        {
            pump = _pumpEntity;
            pathwayCount = _pathwayEntities.Count;
            activePathwayCount = _activePathwayCount;
        }

        // The pump's completion StateMachine is only readable while the pump is in range; compute it here so Update can latch it for the streamed-out case.
        bool pumpValid = BlightEncounter.IsPumpCurrentlyValid(pump);
        bool pumpCompleted = pumpValid && pump != null && BlightEncounter.IsPumpCompleted(pump);

        bool wasActive = _encounter.IsActive;
        bool ended = _encounter.Update(pump, pathwayCount, activePathwayCount, pumpCompleted);

        // An encounter (re)start must not wait for the next label change to trigger the plan rebuild — the entity scan already knows the encounter is back (e.g. after respawn), so signal it immediately.  Clears (ended) deliberately do not fire DataChanged.
        if (!wasActive && _encounter.IsActive)
            DataChanged?.Invoke();

        if (!ended)
            return;

        // Encounter ended: clear the cached data WITHOUT the full ClearRequested path - a full clear calls Reset() on the encounter, which would clear the ended latch and let the re-rendered lanes (player walked far from the pump) re-activate a finished encounter.
        _debug.Add("Blight encounter ended - clearing cached data");
        EncounterEnded?.Invoke();
    }

    private static string? GetEntityPath(Entity entity)
    {
        try { return entity.Path; }
        catch { return null; }
    }

    // Fresh (uncached) entity-path read for the executor's verify/rank checks — the cached path can lag one rank behind when a tower is upgraded in place, and that stale rank makes the verify loop re-click an already-upgraded tower. The executor ticks a few times per second, so the process-memory read here is cheap.
    internal static string? GetEntityPathFresh(Entity entity) => GetEntityPath(entity);

    // Cached entity-path read (validated by entity id) — avoids a process-memory path read + string allocation per call in the executor's hot per-tick rank/verify loops.
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

            // The foundation type comes from the label's metadata path (the same BlightFoundation<Tower> pattern the entity scan uses in RefreshEntities) — never from a hardcoded default.
            BlightTowerType currentType = BlightHelpers.DetectFoundationTypeFromPath(path);

            lock (_blightDataLock)
            {
                int existingIndex = BlightHelpers.FindTowerIndexAt(_knownTowers, worldPos);
                if (existingIndex >= 0)
                {
                    BlightCachedTower existing = _knownTowers[existingIndex];
                    // Never let a foundation label clobber a built tower's type — built types come from the entity import / executor, and a lingering foundation label must not turn a built Seismic/Fireball into a Chilling tower.
                    if (existing.UpgradeLevel == 0)
                        existing.TowerType = currentType;
                    existing.FoundationEntity = entity;
                }
                else
                {
                    _knownTowers.Add(new BlightCachedTower(worldPos, currentType)
                    {
                        FoundationEntity = entity
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

    // The freshly-scanned tower entity at a position (null when only a foundation exists). Prefer this over the cached foundation entity for rank reads: the foundation path stays a foundation path (rank 0) even after in-place upgrades, while the tower entity path carries the live rank.
    internal Entity? GetTowerEntityAt(NumVector2 pos)
    {
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

    // The tower dat id at a position from the cached component data (e.g. "MeteorTower" for a specialized Fireball). The entity path only carries the base type + rank, so the dat id is the ONLY cached signal that reveals which specialization a tower actually is.
    internal string? GetTowerDatIdAt(NumVector2 pos)
    {
        IReadOnlyList<(Entity Entity, string TowerId)> towerList = TowerEntities;
        for (int t = 0; t < towerList.Count; t++)
        {
            NumVector2 tePos;
            try { tePos = new NumVector2(towerList[t].Entity.GridPosNum.X, towerList[t].Entity.GridPosNum.Y); }
            catch { continue; }
            if (BlightHelpers.SameGridPosition(tePos, pos))
                return towerList[t].TowerId;
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
        // A streamed-out / dead entity must never be used as a walk/verify target — it would make the executor chase a tower that isn't there ("target REJECTED valid=False" loop).
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
            if (DynamicAccess.TryGetLabelItemOnGround(label, out Entity? entity))
                return entity;
        }
        catch { }
        return null;
    }

    private static string? TryGetMetadataPath(LabelOnGround label)
    {
        try
        {
            if (DynamicAccess.TryGetLabelItemOnGround(label, out Entity? item)
                && DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string resolvedPath))
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
            _persistedIcons.Clear();
            _iconPathwaySnapshot.Clear();
            _activePathwayCount = 0;
            EntityEventHub.Instance.Blight.Clear();
            _towerEntities.Clear();
            _pumpEntity = null;
            _persistedPumpGridPosition = null;
            _entityPathCache.Clear();
            _pathwayWorldPositions = [];
        }
        CachedBranchAnchors.Clear();
        _lastProcessedLabels = null;
        _hasDetectedAnyBlightContent = false;
        _hasCompletedInitialScan = false;
        lock (_blightDataLock)
        {
            _cachedCoverage = null;
            _cachedCoveragePathways = null;
            _coverageDirty = false;
            _lastScanCoverageSignature = 0;
        }
        // A clear invalidates the retained set, so the next refresh reseeds promptly to refill.
        _lastEventReseedMs = 0;
    }
}
