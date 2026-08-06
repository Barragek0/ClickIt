namespace ClickIt.Features.Blight;

internal static class BlightLaneTopology
{
    internal const float PhantomConnectDistance = 100f;

    // Two segments are the same physical lane written twice (the game lays parallel pathway rows on
    // top of one another) when both their midpoints AND their parents' midpoints are within this
    // distance.  Towers covering one row cover both, so stacked rows must merge into ONE lane.
    // Kept "quite close" so genuinely separate parallel lanes never merge.
    internal const float StackedLaneMergeDistance = 9f;

    // Chains whose pump-nearest point is inside this radius are separate ROOT branches (they start
    // at the pump), not underground lane continuations — phantom bridging must leave them separate.
    internal const float PumpRootRadius = 30f;
    internal const float PumpStubRadius = 30f;

    internal static LaneCoverageResult[] ComputeCoverage(
        IReadOnlyList<NumVector2> positions,
        Func<NumVector2, (bool chilling, bool seismic, bool fireball)> getCoverage,
        float segmentConnectDistance = 45f,
        NumVector2? pumpGridPosition = null,
        float phantomConnectDistance = PhantomConnectDistance,
        float pumpRootRadius = PumpRootRadius,
        IReadOnlyList<long>? ids = null,
        Func<NumVector2, (bool empowering, bool shockNova, bool summoning)>? getSupportCoverage = null)
    {
        int n = positions.Count;
        if (n < 2)
            return [];

        LaneCoverageResult[] results = new LaneCoverageResult[n];

        for (int i = 0; i < n; i++)
            results[i] = new LaneCoverageResult(OrphanSentinel, false, NumVector2.Zero);

        // Build the lane-connectivity graph and root its pump-side tree by BFS.  With the game's
        // pathway ids the graph is built from id adjacency (consecutive ids within the connect
        // distance are the same lane — spec Rule 4's connectivity, plus shared fork/hub points),
        // which keeps real lane runs and never chains points from different lanes that merely pass
        // near each other.  Without ids (tests) the geometry-pruned graph is used.  The BFS parent
        // tree gives every node a parent on the same lane toward the pump and makes a fork node
        // the parent of every branch (Rules 2-3).
        LaneGraph graph = ids != null && ids.Count == n && pumpGridPosition.HasValue
            ? BlightLaneGraph.BuildIdBased(positions, ids, pumpGridPosition.Value, segmentConnectDistance)
            : BlightLaneGraph.Build(positions, segmentConnectDistance);
        int[] parent;
        bool[] connected;
        if (pumpGridPosition.HasValue)
        {
            parent = BlightLaneGraph.BuildPumpRootedParents(graph, pumpGridPosition.Value, out connected);
            // The game assigns each lane a contiguous id run — adjacent ids (id±1) within the
            // connect distance are the SAME lane.  On top of the graph tree this re-roots each
            // pump-near lane as its own branch and merges parallel rows of one game lane.
            if (ids != null && ids.Count == n)
                ApplyLaneBranchSplits(positions, pumpGridPosition.Value, segmentConnectDistance, pumpRootRadius, ids, parent);
        }
        else
        {
            parent = BuildSequentialParents(positions, segmentConnectDistance, out connected);
        }

        bool[] isPhantom = pumpGridPosition.HasValue
            ? ConnectOrphanChains(positions, parent, connected, segmentConnectDistance,
                phantomConnectDistance, pumpGridPosition, pumpRootRadius)
            : new bool[n];
        bool[] isPumpStub = pumpGridPosition.HasValue
            ? MarkPumpStubs(positions, parent, pumpGridPosition.Value, PumpStubRadius, segmentConnectDistance, ids)
            : new bool[n];

        for (int i = 0; i < n; i++)
        {
            int par = parent[i];
            if (par < 0)
                continue;

            NumVector2 midpoint = new(
                (positions[par].X + positions[i].X) / 2f,
                (positions[par].Y + positions[i].Y) / 2f);
            (bool hc, bool hs, bool hf) = getCoverage(midpoint);
            (bool he, bool hsn, bool hsu) = getSupportCoverage != null
                ? getSupportCoverage(midpoint)
                : (false, false, false);
            bool covered = hc || hs || hf;

            results[i] = new LaneCoverageResult(
                ParentIndex: par, IsFullyCovered: covered, Midpoint: midpoint,
                HasChilling: hc, HasSeismic: hs, HasFireball: hf,
                HasEmpowering: he, HasShockNova: hsn, HasSummoning: hsu,
                IsPhantom: isPhantom[i], IsPumpStub: isPumpStub[i]);
        }

        return MergeAndPropagate(results, n);
    }

    private static int[] BuildSequentialParents(
        IReadOnlyList<NumVector2> positions, float segmentConnectDistance, out bool[] connected)
    {
        int n = positions.Count;
        int[] parent = new int[n];
        Array.Fill(parent, -1);
        for (int i = 0; i < n - 1; i++)
        {
            if (Distance(positions[i], positions[i + 1]) > segmentConnectDistance)
                continue;
            parent[i + 1] = i;
        }
        connected = new bool[n];
        connected[0] = true;
        for (int i = 1; i < n; i++)
            connected[i] = parent[i] >= 0;
        return parent;
    }

    // Re-roots each pump-near lane as its own branch by id order (adjacent ids within the connect
    // distance are the same lane), cutting the lane's pump-closest segment; parallel rows of one
    // game lane are merged into a single branch.
    private static void ApplyLaneBranchSplits(
        IReadOnlyList<NumVector2> positions, NumVector2 pump, float segmentConnectDistance,
        float pumpRootRadius, IReadOnlyList<long> ids, int[] parent)
    {
        int n = positions.Count;
        Dictionary<long, int> indexById = [];
        for (int i = 0; i < n; i++)
            indexById.TryAdd(ids[i], i);

        // Lane groups = id-adjacency components.
        bool[] inLane = new bool[n];
        List<List<int>> groups = [];
        for (int start = 0; start < n; start++)
        {
            if (inLane[start])
                continue;
            List<int> lane = [];
            Queue<int> q = new();
            q.Enqueue(start);
            inLane[start] = true;
            while (q.Count > 0)
            {
                int cur = q.Dequeue();
                lane.Add(cur);
                for (int d = -1; d <= 1; d += 2)
                {
                    if (!indexById.TryGetValue(ids[cur] + d, out int t) || inLane[t])
                        continue;
                    if (Distance(positions[cur], positions[t]) <= segmentConnectDistance)
                    {
                        inLane[t] = true;
                        q.Enqueue(t);
                    }
                }
            }
            if (lane.Count >= 2)
                groups.Add(lane);
        }

        // Pump-near groups: pump-closest segment + dominant direction (root -> further id-neighbour).
        int[] ownRoot = new int[groups.Count];
        float[] ownRootDist = new float[groups.Count];
        float[] dirX = new float[groups.Count], dirY = new float[groups.Count];
        for (int g = 0; g < groups.Count; g++)
        {
            List<int> lane = groups[g];
            int root = lane[0];
            float bestD = Distance(positions[root], pump);
            for (int i = 1; i < lane.Count; i++)
            {
                float d = Distance(positions[lane[i]], pump);
                if (d < bestD)
                {
                    bestD = d;
                    root = lane[i];
                }
            }
            ownRoot[g] = root;
            ownRootDist[g] = bestD;
            float farD = bestD;
            for (int d = -1; d <= 1; d += 2)
            {
                if (!indexById.TryGetValue(ids[root] + d, out int t) || t == root)
                    continue;
                float tD = Distance(positions[t], pump);
                if (tD > farD)
                {
                    farD = tD;
                    dirX[g] = positions[t].X - positions[root].X;
                    dirY[g] = positions[t].Y - positions[root].Y;
                }
            }
        }

        // Merge parallel rows into one game lane (roots close + dominant directions parallel).
        const float LaneMergeDistance = 18f;
        const float LaneMergeCos = 0.82f; // ~35°
        int[] uf = new int[groups.Count];
        for (int i = 0; i < uf.Length; i++)
            uf[i] = i;
        static int Find(int[] uf, int x)
        {
            while (uf[x] != x)
            {
                uf[x] = uf[uf[x]];
                x = uf[x];
            }
            return x;
        }

        for (int i = 0; i < groups.Count; i++)
        {
            if (ownRootDist[i] > pumpRootRadius)
                continue;
            for (int j = i + 1; j < groups.Count; j++)
            {
                if (ownRootDist[j] > pumpRootRadius)
                    continue;
                float dx = positions[ownRoot[i]].X - positions[ownRoot[j]].X;
                float dy = positions[ownRoot[i]].Y - positions[ownRoot[j]].Y;
                if ((dx * dx) + (dy * dy) > LaneMergeDistance * LaneMergeDistance)
                    continue;
                float la = MathF.Sqrt((dirX[i] * dirX[i]) + (dirY[i] * dirY[i]));
                float lb = MathF.Sqrt((dirX[j] * dirX[j]) + (dirY[j] * dirY[j]));
                if (la < 1f || lb < 1f)
                    continue;
                float dot = (dirX[i] * dirX[j]) + (dirY[i] * dirY[j]);
                if ((dot / (la * lb)) < LaneMergeCos)
                    continue;
                int ri = Find(uf, i), rj = Find(uf, j);
                if (ri != rj)
                    uf[ri] = rj;
            }
        }

        // Branch root per merged cluster = the pump-closest root among its members.
        Dictionary<int, int> clusterBest = [];
        for (int g = 0; g < groups.Count; g++)
        {
            int r = Find(uf, g);
            if (!clusterBest.TryGetValue(r, out int best) || ownRootDist[g] < ownRootDist[best])
                clusterBest[r] = g;
        }

        // Re-chain each pump-near group toward its own pump-closest segment (id order); cut the
        // merged branch roots. Far lanes and isolated segments keep their greedy parents.
        for (int g = 0; g < groups.Count; g++)
        {
            if (ownRootDist[g] > pumpRootRadius)
                continue;
            int root = ownRoot[g];
            int branchRoot = ownRoot[clusterBest[Find(uf, g)]];
            List<int> lane = groups[g];
            for (int i = 0; i < lane.Count; i++)
            {
                int s = lane[i];
                if (s == root)
                {
                    if (s == branchRoot)
                        parent[s] = -1; // branch root — cut into its own branch
                    // else keep the greedy parent: ties this lane's row into the merged branch
                    continue;
                }
                long want = ids[s] > ids[root] ? ids[s] - 1 : ids[s] + 1;
                if (indexById.TryGetValue(want, out int t) && t != s
                    && Distance(positions[s], positions[t]) <= segmentConnectDistance)
                {
                    parent[s] = t;
                }
                // else keep greedy parent (id gap / run end)
            }
        }
    }

    private static bool[] ConnectOrphanChains(
        IReadOnlyList<NumVector2> positions, int[] parent, bool[] connected,
        float segmentConnectDistance, float phantomConnectDistance,
        NumVector2? pumpPosition, float pumpRootRadius)
    {
        int n = positions.Count;
        bool[] isPhantom = new bool[n];
        bool[] inChain = (bool[])connected.Clone();

        for (int i = 0; i < n; i++)
        {
            if (inChain[i])
                continue;

            List<int> chain = [];
            Queue<int> q = new();
            q.Enqueue(i);
            inChain[i] = true;
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                chain.Add(c);
                for (int j = 0; j < n; j++)
                {
                    if (inChain[j])
                        continue;
                    if (Distance(positions[c], positions[j]) <= segmentConnectDistance)
                    {
                        inChain[j] = true;
                        q.Enqueue(j);
                    }
                }
            }

            if (pumpPosition.HasValue)
            {
                float nearestPump = float.MaxValue;
                for (int a = 0; a < chain.Count; a++)
                {
                    float d = Distance(positions[chain[a]], pumpPosition.Value);
                    if (d < nearestPump)
                        nearestPump = d;
                }
                if (nearestPump <= pumpRootRadius)
                    continue;
            }

            int bridgeJ = -1, bridgeK = -1;
            float bridgeD = float.MaxValue;
            for (int a = 0; a < chain.Count; a++)
            {
                int j = chain[a];
                for (int k = 0; k < n; k++)
                {
                    if (!connected[k])
                        continue;
                    float d = Distance(positions[j], positions[k]);
                    if (d <= phantomConnectDistance && d < bridgeD)
                    { bridgeD = d; bridgeJ = j; bridgeK = k; }
                }
            }
            if (bridgeJ < 0)
                continue;

            bool[] linked = new bool[n];
            linked[bridgeJ] = true;
            int linkedCount = 1;
            while (linkedCount < chain.Count)
            {
                int bestJ = -1, bestP = -1;
                float bestD = float.MaxValue;
                for (int a = 0; a < chain.Count; a++)
                {
                    int j = chain[a];
                    if (linked[j])
                        continue;
                    for (int b = 0; b < chain.Count; b++)
                    {
                        int k = chain[b];
                        if (!linked[k])
                            continue;
                        float d = Distance(positions[j], positions[k]);
                        if (d <= segmentConnectDistance && d < bestD)
                        { bestD = d; bestJ = j; bestP = k; }
                    }
                }
                if (bestJ < 0)
                    break;
                linked[bestJ] = true;
                linkedCount++;
                parent[bestJ] = bestP;
            }

            parent[bridgeJ] = bridgeK;
            isPhantom[bridgeJ] = true;
        }
        return isPhantom;
    }

    private static bool[] MarkPumpStubs(
        IReadOnlyList<NumVector2> positions, int[] parent, NumVector2 pump, float pumpStubRadius,
        float segmentConnectDistance, IReadOnlyList<long>? ids = null)
    {
        int n = positions.Count;
        bool[] stub = new bool[n];
        if (n == 0)
            return stub;

        // Id-lane members (a segment with an id-adjacent neighbour within the connect distance) are
        // real lanes — never pump-object stubs, even when their pump-side chain is short.
        bool[] isLaneMember = new bool[n];
        if (ids != null && ids.Count == n)
        {
            Dictionary<long, int> indexById = [];
            for (int i = 0; i < n; i++)
                indexById.TryAdd(ids[i], i);
            for (int i = 0; i < n; i++)
            {
                for (int d = -1; d <= 1; d += 2)
                {
                    if (indexById.TryGetValue(ids[i] + d, out int t) && t != i
                        && Distance(positions[i], positions[t]) <= segmentConnectDistance)
                    {
                        isLaneMember[i] = true;
                        break;
                    }
                }
            }
        }

        List<List<int>> children = new(n);
        for (int i = 0; i < n; i++) children.Add([]);
        for (int i = 0; i < n; i++)
        {
            int par = parent[i];
            if (par >= 0 && par != i)
                children[par].Add(i);
        }

        bool[] nearPump = new bool[n];
        bool[] visited = new bool[n];
        for (int start = 0; start < n; start++)
        {
            if (visited[start])
                continue;
            List<int> order = [];
            Stack<int> pending = new();
            pending.Push(start);
            while (pending.Count > 0)
            {
                int v = pending.Pop();
                if (visited[v])
                    continue;
                visited[v] = true;
                order.Add(v);
                List<int> ch = children[v];
                for (int k = 0; k < ch.Count; k++)
                    if (!visited[ch[k]])
                        pending.Push(ch[k]);
            }
            for (int o = order.Count - 1; o >= 0; o--)
            {
                int v = order[o];
                bool near = Distance(positions[v], pump) <= pumpStubRadius;
                List<int> ch = children[v];
                for (int k = 0; k < ch.Count && near; k++)
                    near = nearPump[ch[k]];
                nearPump[v] = near;
            }
        }

        for (int s = 0; s < n; s++)
        {
            int par = parent[s];
            if (par < 0 || par == s)
                continue;
            if (nearPump[s] && !isLaneMember[s])
                stub[s] = true;
        }
        return stub;
    }

    private static LaneCoverageResult[] MergeAndPropagate(LaneCoverageResult[] results, int n)
    {
        bool[] localChilling = new bool[n];
        bool[] localSeismic = new bool[n];
        bool[] localFireball = new bool[n];
        bool[] localEmpowering = new bool[n];
        bool[] localShockNova = new bool[n];
        bool[] localSummoning = new bool[n];
        for (int i = 0; i < n; i++)
        {
            localChilling[i] = results[i].HasChilling;
            localSeismic[i] = results[i].HasSeismic;
            localFireball[i] = results[i].HasFireball;
            localEmpowering[i] = results[i].HasEmpowering;
            localShockNova[i] = results[i].HasShockNova;
            localSummoning[i] = results[i].HasSummoning;
        }

        // Stacked parallel rows of one physical lane share coverage BEFORE propagation.  Without
        // this, the AND-upward rule treats the stacked row as a fork arm with no coverage and blocks
        // coverage from propagating up the lane.
        MergeStackedLaneFlags(
            localChilling, localSeismic, localFireball,
            localEmpowering, localShockNova, localSummoning, results, n);

        bool[] chilling = PropagateType(results, localChilling);
        bool[] seismic = PropagateType(results, localSeismic);
        bool[] fireball = PropagateType(results, localFireball);
        bool[] empowering = PropagateType(results, localEmpowering);
        bool[] shockNova = PropagateType(results, localShockNova);
        bool[] summoning = PropagateType(results, localSummoning);

        for (int i = 0; i < n; i++)
        {
            results[i] = new LaneCoverageResult(
                ParentIndex: results[i].ParentIndex,
                IsFullyCovered: chilling[i] || seismic[i] || fireball[i],
                Midpoint: results[i].Midpoint,
                HasChilling: chilling[i],
                HasSeismic: seismic[i],
                HasFireball: fireball[i],
                HasEmpowering: empowering[i],
                HasShockNova: shockNova[i],
                HasSummoning: summoning[i],
                IsPhantom: results[i].IsPhantom,
                IsPumpStub: results[i].IsPumpStub);
        }

        return results;
    }

    private static void MergeStackedLaneFlags(
        bool[] chilling, bool[] seismic, bool[] fireball,
        bool[] empowering, bool[] shockNova, bool[] summoning,
        LaneCoverageResult[] results, int n)
    {
        bool changed;
        int guard = 0;
        do
        {
            changed = false;
            for (int i = 0; i < n; i++)
            {
                int pi = results[i].ParentIndex;
                if (pi < 0 || pi == i || results[i].IsPumpStub)
                    continue;
                NumVector2 mi = results[i].Midpoint;
                for (int j = i + 1; j < n; j++)
                {
                    int pj = results[j].ParentIndex;
                    if (pj < 0 || pj == j || results[j].IsPumpStub)
                        continue;
                    if (Distance(mi, results[j].Midpoint) > StackedLaneMergeDistance)
                        continue;
                    if (!RunsAreParallel(results, pi, i, pj, j))
                        continue;
                    changed |= UnionFlags(chilling, seismic, fireball, empowering, shockNova, summoning, i, j);
                    changed |= UnionFlags(chilling, seismic, fireball, empowering, shockNova, summoning, j, i);
                }
            }
            guard++;
        } while (changed && guard < 16);
    }

    private static bool RunsAreParallel(LaneCoverageResult[] results, int parentA, int segA, int parentB, int segB)
    {
        float ax = results[segA].Midpoint.X - results[parentA].Midpoint.X;
        float ay = results[segA].Midpoint.Y - results[parentA].Midpoint.Y;
        float bx = results[segB].Midpoint.X - results[parentB].Midpoint.X;
        float by = results[segB].Midpoint.Y - results[parentB].Midpoint.Y;
        float la = MathF.Sqrt((ax * ax) + (ay * ay));
        float lb = MathF.Sqrt((bx * bx) + (by * by));
        if (la < 0.5f || lb < 0.5f)
            return false; // unknown direction — don't merge on it
        // Stacked rows of ONE lane run almost exactly parallel (cos ≈ 1).  Two arms of a genuine
        // fork fan out at a wider angle, so 0.9 separates them while still accepting real stacked
        // rows (the debug case is ~1-4 units apart and < 10° apart).
        float cos = ((ax * bx) + (ay * by)) / (la * lb);
        return cos >= 0.9f;
    }

    private static bool UnionFlags(
        bool[] chilling, bool[] seismic, bool[] fireball,
        bool[] empowering, bool[] shockNova, bool[] summoning,
        int target, int source)
    {
        bool changed = false;
        if (chilling[source] && !chilling[target]) { chilling[target] = true; changed = true; }
        if (seismic[source] && !seismic[target]) { seismic[target] = true; changed = true; }
        if (fireball[source] && !fireball[target]) { fireball[target] = true; changed = true; }
        if (empowering[source] && !empowering[target]) { empowering[target] = true; changed = true; }
        if (shockNova[source] && !shockNova[target]) { shockNova[target] = true; changed = true; }
        if (summoning[source] && !summoning[target]) { summoning[target] = true; changed = true; }
        return changed;
    }

    // True when child's maximal fork-free run is a stacked duplicate of main's maximal run: every
    // child segment sits within the merge distance of a main-run segment (the rows run parallel and
    // almost on top of one another all the way down).  The "quite close" threshold keeps genuinely
    // separate parallel lanes from merging.
    internal static bool IsRunStackedOnRun(
        LaneCoverageResult[] coverage,
        List<List<int>> children,
        int childStart,
        int mainStart,
        float distance)
    {
        List<int> childRun = CollectMaximalRun(children, childStart);
        List<int> mainRun = CollectMaximalRun(children, mainStart);
        if (childRun.Count == 0 || mainRun.Count == 0)
            return false;

        for (int c = 0; c < childRun.Count; c++)
        {
            NumVector2 cm = coverage[childRun[c]].Midpoint;
            bool near = false;
            for (int m = 0; m < mainRun.Count; m++)
            {
                if (Distance(cm, coverage[mainRun[m]].Midpoint) <= distance)
                {
                    near = true;
                    break;
                }
            }
            if (!near)
                return false;
        }
        return true;
    }

    private static List<int> CollectMaximalRun(List<List<int>> children, int start)
    {
        List<int> run = [];
        int current = start;
        int guard = 0;
        while (current >= 0 && guard++ < children.Count)
        {
            run.Add(current);
            List<int> c = children[current];
            if (c.Count != 1)
                break;
            current = c[0];
        }
        return run;
    }

    private static void MarkSubtreeVisited(List<List<int>> children, bool[] visited, int node)
    {
        if (visited[node])
            return;
        visited[node] = true;
        List<int> c = children[node];
        for (int i = 0; i < c.Count; i++)
            MarkSubtreeVisited(children, visited, c[i]);
    }

    // Whether a segment that is not part of the rendered forest is a stacked duplicate of a rendered
    // lane (merged into it, so it must not be reported as an unmapped topology anomaly).
    internal static bool IsStackedOnRenderedLane(int segment, LaneCoverageResult[] coverage, IReadOnlySet<int> rendered)
    {
        int parent = coverage[segment].ParentIndex;
        NumVector2 mid = coverage[segment].Midpoint;
        foreach (int r in rendered)
        {
            if (r == segment)
                continue;
            if (Distance(mid, coverage[r].Midpoint) > StackedLaneMergeDistance)
                continue;
            int rp = coverage[r].ParentIndex;
            if (rp < 0 || rp == r)
                continue;
            if (!RunsAreParallel(coverage, parent, segment, rp, r))
                continue;
            return true;
        }
        return false;
    }

    internal static LaneCoverageResult AggregateLane(BlightLaneNode lane, LaneCoverageResult[] coverage)
    {
        LaneCoverageResult agg = default;
        for (int i = 0; i < lane.Segments.Count; i++)
        {
            LaneCoverageResult r = coverage[lane.Segments[i]];
            agg = agg with
            {
                IsFullyCovered = agg.IsFullyCovered || r.IsFullyCovered,
                HasChilling = agg.HasChilling || r.HasChilling,
                HasSeismic = agg.HasSeismic || r.HasSeismic,
                HasFireball = agg.HasFireball || r.HasFireball,
                HasEmpowering = agg.HasEmpowering || r.HasEmpowering,
                HasShockNova = agg.HasShockNova || r.HasShockNova,
                HasSummoning = agg.HasSummoning || r.HasSummoning,
                IsPhantom = agg.IsPhantom || r.IsPhantom,
            };
        }
        return agg;
    }

    // Phase 2 (AND upward) then Phase 3 (OR downward), each to fixed point — with a pump-rooted tree
    // a child can sit on either side of its parent, so a single ascending/descending pass would be wrong.
    internal static bool[] PropagateType(LaneCoverageResult[] results, bool[] localHas)
    {
        int n = results.Length;
        bool[] has = (bool[])localHas.Clone();

        // Scratch buffers reused across fixed-point iterations (cleared each pass) instead of allocating
        // a fresh pair per iteration — propagation depth is small but the arrays are per-iteration objects.
        bool[] ac = new bool[n];
        bool[] seen = new bool[n];

        while (true)
        {
            Array.Clear(ac, 0, n);
            Array.Clear(seen, 0, n);

            for (int i = n - 1; i >= 0; i--)
            {
                int par = results[i].ParentIndex;
                if (par < 0 || par == i) continue;

                if (!seen[par])
                { ac[par] = has[i]; seen[par] = true; }
                else { ac[par] = ac[par] && has[i]; }
            }

            bool anyChanged = false;
            for (int i = 0; i < n; i++)
            {
                bool m = localHas[i] || (seen[i] && ac[i]);
                if (m != has[i]) anyChanged = true;
                has[i] = m;
            }

            if (!anyChanged) break;
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < n; i++)
            {
                int par = results[i].ParentIndex;
                if (par < 0 || par == i) continue;
                if (!has[par]) continue;
                if (!has[i]) { has[i] = true; changed = true; }
            }
        }

        return has;
    }

    internal static List<List<int>> BuildCoverageChildren(LaneCoverageResult[] coverage)
    {
        int n = coverage.Length;
        List<List<int>> children = new(n);
        for (int i = 0; i < n; i++) children.Add([]);
        for (int i = 0; i < n; i++)
        {
            int par = coverage[i].ParentIndex;
            if (par >= 0 && par != i && !coverage[i].IsPumpStub)
                children[par].Add(i);
        }
        return children;
    }

    internal static bool IsRealLaneSegment(LaneCoverageResult r)
        => r.ParentIndex >= 0 && !r.IsPumpStub;

    internal static List<BlightLaneNode> BuildBranchLaneForest(
        LaneCoverageResult[] coverage,
        List<List<int>> children,
        IReadOnlyList<int> branchSegments,
        int mainStart,
        string letter)
    {
        static int TopOf(LaneCoverageResult[] cov, int s)
        {
            int guard = 0;
            while (cov[s].ParentIndex >= 0 && cov[s].ParentIndex != s && guard++ < cov.Length)
                s = cov[s].ParentIndex;
            return s;
        }

        List<int> tops = [];
        foreach (int s in branchSegments)
        {
            int top = TopOf(coverage, s);
            if (!tops.Contains(top))
                tops.Add(top);
        }
        int mainTop = TopOf(coverage, mainStart);
        if (tops.Remove(mainTop))
            tops.Insert(0, mainTop);

        // Every lane except the branch's main lane gets a unique short name ({letter}-{n}); the
        // main lane keeps the bare {letter}. One counter shared across all tops and forks keeps
        // names collision-free no matter how deep the divergence tree gets.
        int nameIndex = 0;
        bool[] visited = new bool[coverage.Length];
        int[] subtreeSize = ComputeSubtreeSizes(children);
        List<BlightLaneNode> forest = [];
        for (int t = 0; t < tops.Count; t++)
        {
            int top = tops[t];
            if (top != mainTop)
            {
                forest.Add(BuildLaneTree(coverage, children, top, $"{letter}-{++nameIndex}", letter, visited, ref nameIndex, subtreeSize));
                continue;
            }

            List<int> arms = children[top];
            if (arms.Count == 0)
            {
                forest.Add(new BlightLaneNode(letter, [top], []));
                continue;
            }

            int mainArm = mainStart;
            int best = -1;
            for (int a = 0; a < arms.Count; a++)
            {
                int size = subtreeSize[arms[a]];
                if (size > best) { best = size; mainArm = arms[a]; }
            }
            List<int> ordered = [mainArm];
            for (int a = 0; a < arms.Count; a++)
                if (arms[a] != mainArm)
                    ordered.Add(arms[a]);
            for (int a = 0; a < ordered.Count; a++)
            {
                string name = a == 0 ? letter : $"{letter}-{++nameIndex}";
                forest.Add(BuildLaneTree(coverage, children, ordered[a], name, letter, visited, ref nameIndex, subtreeSize));
            }
        }

        return forest;
    }

    internal static HashSet<int> CollectLaneSegments(List<BlightLaneNode> forest)
    {
        HashSet<int> set = [];
        void Walk(BlightLaneNode lane)
        {
            for (int i = 0; i < lane.Segments.Count; i++) set.Add(lane.Segments[i]);
            for (int i = 0; i < lane.Children.Count; i++) Walk(lane.Children[i]);
        }
        for (int i = 0; i < forest.Count; i++) Walk(forest[i]);
        return set;
    }

    // Precomputed subtree sizes (one O(n) pass) let BuildLaneTree pick the largest arm as the lane
    // continuation at every fork without walking the subtree per fork. Memoized so bridge-shared
    // nodes are counted once, and cycle-safe (a revisited node returns its partial size).
    private static int[] ComputeSubtreeSizes(List<List<int>> children)
    {
        int n = children.Count;
        int[] size = new int[n];
        bool[] done = new bool[n];
        for (int s = 0; s < n; s++)
            if (!done[s])
                ComputeSubtreeSize(children, size, done, s);
        return size;
    }

    private static int ComputeSubtreeSize(List<List<int>> children, int[] size, bool[] done, int node)
    {
        if (done[node])
            return size[node];
        done[node] = true;
        int total = 1;
        List<int> c = children[node];
        for (int i = 0; i < c.Count; i++)
            total += ComputeSubtreeSize(children, size, done, c[i]);
        size[node] = total;
        return total;
    }

    internal static BlightLaneNode BuildLaneTree(
        LaneCoverageResult[] coverage,
        List<List<int>> children,
        int laneStart,
        string name)
    {
        int nameIndex = 0;
        return BuildLaneTree(coverage, children, laneStart, name, name,
            new bool[coverage.Length], ref nameIndex, ComputeSubtreeSizes(children));
    }

    // A segment can only be claimed by ONE branch: the shared visited set makes the recursion finite
    // even if the children graph is ever corrupted into a cycle (a cycle would otherwise recurse
    // into a fresh child lane forever and stack-overflow the render/debug loop).
    private static BlightLaneNode BuildLaneTree(
        LaneCoverageResult[] coverage,
        List<List<int>> children,
        int laneStart,
        string name,
        string branchPrefix,
        bool[] visited,
        ref int nameIndex,
        int[] subtreeSize)
    {
        List<int> segments = [];
        List<BlightLaneNode> childLanes = [];
        int current = laneStart;

        while (current >= 0)
        {
            // Walk the maximal fork-free run, collecting segments into THIS lane.
            while (true)
            {
                if (visited[current])
                {
                    current = -1;
                    break;
                }
                visited[current] = true;
                segments.Add(current);
                List<int> c = children[current];
                if (c.Count != 1)
                    break;
                current = c[0];
            }
            if (current < 0)
                break;

            // At a fork the LARGEST arm continues THIS lane (same name) so a winding trunk stays
            // ONE lane instead of fragmenting into a new lane per segment; only the smaller arms
            // become short numbered side lanes.
            List<int> forkChildren = children[current];
            int mainChild = -1;
            int mainSize = -1;
            for (int i = 0; i < forkChildren.Count; i++)
            {
                int child = forkChildren[i];
                if (visited[child])
                    continue;
                int sz = subtreeSize[child];
                if (sz > mainSize)
                {
                    mainSize = sz;
                    mainChild = child;
                }
            }
            for (int i = 0; i < forkChildren.Count; i++)
            {
                int child = forkChildren[i];
                if (visited[child] || child == mainChild)
                    continue;

                // A fork arm whose run is a stacked duplicate of the main continuation (parallel
                // rows of the SAME physical lane — the game lays rows on top of one another) is
                // merged into this lane instead of rendering as a separate divergence.
                if (IsRunStackedOnRun(coverage, children, child, mainChild, StackedLaneMergeDistance))
                {
                    MarkSubtreeVisited(children, visited, child);
                    continue;
                }

                childLanes.Add(BuildLaneTree(coverage, children, child, $"{branchPrefix}-{++nameIndex}", branchPrefix, visited, ref nameIndex, subtreeSize));
            }
            current = mainChild;
        }

        return new BlightLaneNode(name, segments, childLanes);
    }

    private static float Distance(NumVector2 a, NumVector2 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    internal const int OrphanSentinel = -2;
}

internal readonly record struct BlightLaneNode(
    string Name,
    IReadOnlyList<int> Segments,
    IReadOnlyList<BlightLaneNode> Children);

internal readonly record struct LaneCoverageResult(
    int ParentIndex,
    bool IsFullyCovered,
    NumVector2 Midpoint,
    bool HasChilling = false,
    bool HasSeismic = false,
    bool HasFireball = false,
    bool HasEmpowering = false,
    bool HasShockNova = false,
    bool HasSummoning = false,
    bool IsPhantom = false,
    bool IsPumpStub = false);

internal static class BlightCoverageFlags
{
    private static readonly BlightTowerType[] s_displayOrder =
    [
        BlightTowerType.Chilling,
        BlightTowerType.Seismic,
        BlightTowerType.Fireball,
        BlightTowerType.Empowering,
        BlightTowerType.ShockNova,
        BlightTowerType.Summoning,
    ];

    internal static IReadOnlySet<BlightTowerType> ForStrategy(IBlightTowerStrategy strategy)
    {
        HashSet<BlightTowerType> coverage = [];
        IReadOnlyList<TowerBuildRule> rules = strategy.Rules;
        for (int i = 0; i < rules.Count; i++)
            if (rules[i].IsCoverageTower)
                coverage.Add(rules[i].TowerType);
        return coverage;
    }

    internal static string Format(LaneCoverageResult r, IReadOnlySet<BlightTowerType> coverageTypes)
    {
        StringBuilder sb = new(24);
        for (int i = 0; i < s_displayOrder.Length; i++)
        {
            BlightTowerType t = s_displayOrder[i];
            if (!coverageTypes.Contains(t))
                continue;
            sb.Append(HasType(r, t) ? Letter(t) : Dash(t)).Append(' ');
        }
        sb.Append(r.IsPhantom ? 'P' : '-');
        return sb.ToString();
    }

    internal static string Compact(LaneCoverageResult r, IReadOnlySet<BlightTowerType> coverageTypes)
    {
        StringBuilder sb = new(16);
        for (int i = 0; i < s_displayOrder.Length; i++)
        {
            BlightTowerType t = s_displayOrder[i];
            if (!coverageTypes.Contains(t) || !HasType(r, t))
                continue;
            sb.Append(Letter(t)).Append(' ');
        }
        if (r.IsPhantom)
            sb.Append('P').Append(' ');
        return sb.Length > 0 ? sb.ToString(0, sb.Length - 1) : "";
    }

    private static bool HasType(LaneCoverageResult r, BlightTowerType t) => t switch
    {
        BlightTowerType.Chilling => r.HasChilling,
        BlightTowerType.Seismic => r.HasSeismic,
        BlightTowerType.Fireball => r.HasFireball,
        BlightTowerType.Empowering => r.HasEmpowering,
        BlightTowerType.ShockNova => r.HasShockNova,
        BlightTowerType.Summoning => r.HasSummoning,
        _ => false,
    };

    private static string Letter(BlightTowerType t) => t switch
    {
        BlightTowerType.Chilling => "C",
        BlightTowerType.Seismic => "S",
        BlightTowerType.Fireball => "F",
        BlightTowerType.Empowering => "E",
        BlightTowerType.ShockNova => "SH",
        BlightTowerType.Summoning => "SU",
        _ => "?",
    };

    private static string Dash(BlightTowerType t)
        => t is BlightTowerType.ShockNova or BlightTowerType.Summoning ? "--" : "-";
}
