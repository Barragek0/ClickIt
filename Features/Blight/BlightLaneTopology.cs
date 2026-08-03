namespace ClickIt.Features.Blight;

internal static class BlightLaneTopology
{
    internal const float PhantomConnectDistance = 100f;

    // Chains whose pump-nearest point is inside this radius are separate ROOT branches (they start
    // at the pump), not underground lane continuations — phantom bridging must leave them separate.
    internal const float PumpRootRadius = 30f;
    internal const float PumpStubRadius = 30f;

    internal static LaneCoverageResult[] ComputeCoverage(
        IReadOnlyList<Entity> pathwayEntities,
        Func<NumVector2, (bool chilling, bool seismic, bool fireball)> getCoverage,
        float segmentConnectDistance = 45f,
        NumVector2? pumpGridPosition = null,
        Func<NumVector2, (bool empowering, bool shockNova, bool summoning)>? getSupportCoverage = null)
    {
        if (pathwayEntities.Count < 2)
            return [];

        NumVector2[] positions = new NumVector2[pathwayEntities.Count];
        for (int i = 0; i < positions.Length; i++)
            positions[i] = ToVec2(pathwayEntities[i]);

        return ComputeCoverage(positions, getCoverage, segmentConnectDistance, pumpGridPosition,
            getSupportCoverage: getSupportCoverage);
    }

    internal static LaneCoverageResult[] ComputeCoverage(
        IReadOnlyList<NumVector2> positions,
        Func<NumVector2, (bool chilling, bool seismic, bool fireball)> getCoverage,
        float segmentConnectDistance = 45f,
        NumVector2? pumpGridPosition = null,
        float phantomConnectDistance = PhantomConnectDistance,
        float pumpRootRadius = PumpRootRadius,
        Func<NumVector2, (bool empowering, bool shockNova, bool summoning)>? getSupportCoverage = null)
    {
        int n = positions.Count;
        if (n < 2)
            return [];

        LaneCoverageResult[] results = new LaneCoverageResult[n];

        for (int i = 0; i < n; i++)
            results[i] = new LaneCoverageResult(OrphanSentinel, false, NumVector2.Zero);

        int[] parent = pumpGridPosition.HasValue
            ? BuildPumpRootedParents(positions, pumpGridPosition.Value, segmentConnectDistance, out bool[] connected)
            : BuildSequentialParents(positions, segmentConnectDistance, out connected);

        bool[] isPhantom = pumpGridPosition.HasValue
            ? ConnectOrphanChains(positions, parent, connected, segmentConnectDistance,
                phantomConnectDistance, pumpGridPosition, pumpRootRadius)
            : new bool[n];
        bool[] isPumpStub = pumpGridPosition.HasValue
            ? MarkPumpStubs(positions, parent, pumpGridPosition.Value, PumpStubRadius)
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
                TowerBuilt: covered, IsPhantom: isPhantom[i], IsPumpStub: isPumpStub[i]);
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

    private static int[] BuildPumpRootedParents(
        IReadOnlyList<NumVector2> positions, NumVector2 pump, float segmentConnectDistance,
        out bool[] connected)
    {
        int n = positions.Count;
        int[] parent = new int[n];
        Array.Fill(parent, -1);
        bool[] visited = new bool[n];
        connected = visited;

        // Pump and pathway entities are both terrain objects, so the pump grid position is in
        // the same space as the pathway points here.
        int root = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            float d = Distance(positions[i], pump);
            if (d < bestDist)
            {
                bestDist = d;
                root = i;
            }
        }
        if (root < 0)
            return parent;

        visited[root] = true;
        int reached = 1;
        while (reached < n)
        {
            int bestJ = -1;
            int bestParent = -1;
            float bestD = float.MaxValue;
            for (int j = 0; j < n; j++)
            {
                if (visited[j])
                    continue;
                int nearestVisited = -1;
                float nearestD = float.MaxValue;
                for (int k = 0; k < n; k++)
                {
                    if (!visited[k])
                        continue;
                    float d = Distance(positions[j], positions[k]);
                    if (d <= segmentConnectDistance && d < nearestD)
                    {
                        nearestD = d;
                        nearestVisited = k;
                    }
                }
                if (nearestVisited >= 0 && nearestD < bestD)
                {
                    bestD = nearestD;
                    bestJ = j;
                    bestParent = nearestVisited;
                }
            }
            if (bestJ < 0)
                break;
            visited[bestJ] = true;
            parent[bestJ] = bestParent;
            reached++;
        }
        return parent;
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
        IReadOnlyList<NumVector2> positions, int[] parent, NumVector2 pump, float pumpStubRadius)
    {
        int n = positions.Count;
        bool[] stub = new bool[n];
        if (n == 0)
            return stub;

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
            if (nearPump[s])
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
                TowerBuilt: results[i].TowerBuilt,
                IsPhantom: results[i].IsPhantom,
                IsPumpStub: results[i].IsPumpStub);
        }

        return results;
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

        int nameIndex = 0;
        List<BlightLaneNode> forest = [];
        for (int t = 0; t < tops.Count; t++)
        {
            int top = tops[t];
            if (top != mainTop)
            {
                forest.Add(BuildLaneTree(coverage, children, top, $"{letter}-{++nameIndex}"));
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
                int size = SubtreeSize(children, arms[a]);
                if (size > best) { best = size; mainArm = arms[a]; }
            }
            List<int> ordered = [mainArm];
            for (int a = 0; a < arms.Count; a++)
                if (arms[a] != mainArm)
                    ordered.Add(arms[a]);
            for (int a = 0; a < ordered.Count; a++)
            {
                string name = a == 0 ? letter : $"{letter}-{++nameIndex}";
                forest.Add(BuildLaneTree(coverage, children, ordered[a], name));
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

    private static int SubtreeSize(List<List<int>> children, int start)
    {
        int count = 0;
        Stack<int> pending = new();
        pending.Push(start);
        while (pending.Count > 0)
        {
            int s = pending.Pop();
            count++;
            for (int i = 0; i < children[s].Count; i++)
                pending.Push(children[s][i]);
        }
        return count;
    }

    internal static BlightLaneNode BuildLaneTree(
        LaneCoverageResult[] coverage,
        List<List<int>> children,
        int laneStart,
        string name)
    {
        List<int> segments = [];
        int current = laneStart;
        while (true)
        {
            segments.Add(current);
            List<int> c = children[current];
            if (c.Count != 1)
                break;
            current = c[0];
        }

        List<BlightLaneNode> childLanes = [];
        List<int> forkChildren = children[current];
        for (int i = 0; i < forkChildren.Count; i++)
            childLanes.Add(BuildLaneTree(coverage, children, forkChildren[i], name + (char)('A' + i)));
        return new BlightLaneNode(name, segments, childLanes);
    }

    private static float Distance(NumVector2 a, NumVector2 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    internal static NumVector2 ToVec2(Entity e)
        => new(e.GridPosNum.X, e.GridPosNum.Y);

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
    bool TowerBuilt = false,
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
