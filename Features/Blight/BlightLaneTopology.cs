using static ClickIt.Features.Blight.Planning.BlightGeometry;

namespace ClickIt.Features.Blight;

internal static class BlightLaneTopology
{
    // Two segments are the same physical lane written twice when both their midpoints AND their parents' midpoints are within this distance.
    internal const float StackedLaneMergeDistance = 9f;

    // Pump-ward distance used by the branch-root debug dump to flag orphan segments near the pump.
    internal const float PumpRootRadius = 30f;

    internal static LaneCoverageResult[] ComputeCoverage(
        IReadOnlyList<NumVector2> positions,
        Func<NumVector2, (bool chilling, bool seismic, bool fireball)> getCoverage,
        IReadOnlyList<int> precomputedParents,
        Func<NumVector2, (bool empowering, bool shockNova, bool summoning)>? getSupportCoverage = null,
        IReadOnlyList<int[]>? allParents = null)
    {
        int n = positions.Count;
        if (n < 2)
            return [];

        LaneCoverageResult[] results = new LaneCoverageResult[n];
        for (int i = 0; i < n; i++)
            results[i] = new LaneCoverageResult(OrphanSentinel, false, NumVector2.Zero);

        // The parent tree comes straight from the game's beam chains (each segment's pump-ward neighbour), so there is no re-splitting, phantom bridging, or pump-stub removal.  At a convergence junction several beams can end at the SAME point that another starts from (two lanes joining), so an icon can have several pump-ward parents.  We probe EVERY incoming beam's midpoint and OR the flags — a tower on any real beam into the junction protects it — while the primary parent keeps the tree shape for propagation.
        for (int i = 0; i < n; i++)
        {
            int par = precomputedParents[i];
            if (par < 0)
                continue;

            int[] incoming = allParents != null && i < allParents.Count && allParents[i] is { Length: > 0 }
                ? allParents[i]
                : [par];

            NumVector2 midpoint = new(
                (positions[par].X + positions[i].X) / 2f,
                (positions[par].Y + positions[i].Y) / 2f);
            bool hc = false, hs = false, hf = false, he = false, hsn = false, hsu = false;
            for (int k = 0; k < incoming.Length; k++)
            {
                int p = incoming[k];
                if (p < 0)
                    continue;
                NumVector2 m = new(
                    (positions[p].X + positions[i].X) / 2f,
                    (positions[p].Y + positions[i].Y) / 2f);
                (bool c, bool s, bool f) = getCoverage(m);
                hc |= c; hs |= s; hf |= f;
                if (getSupportCoverage != null)
                {
                    (bool e, bool sn, bool su) = getSupportCoverage(m);
                    he |= e; hsn |= sn; hsu |= su;
                }
            }
            bool covered = hc || hs || hf;

            results[i] = new LaneCoverageResult(
                ParentIndex: par, IsFullyCovered: covered, Midpoint: midpoint,
                HasChilling: hc, HasSeismic: hs, HasFireball: hf,
                HasEmpowering: he, HasShockNova: hsn, HasSummoning: hsu);
        }

        return MergeAndPropagate(results, n);
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

        // Stacked parallel rows of one physical lane share coverage BEFORE propagation.  Without this, the AND-upward rule treats the stacked row as a fork arm with no coverage and blocks coverage from propagating up the lane.
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
        // Bucket segments by rounded midpoint so the pairwise stacked-row scan only compares segments in the same or adjacent buckets instead of every pair (O(n^2) on dense webs).
        int bucketSize = (int)StackedLaneMergeDistance;
        Dictionary<(int, int), List<int>> buckets = [];
        for (int i = 0; i < n; i++)
        {
            if (results[i].ParentIndex < 0 || results[i].IsPumpStub)
                continue;
            (int, int) key = (
                (int)MathF.Floor(results[i].Midpoint.X / bucketSize),
                (int)MathF.Floor(results[i].Midpoint.Y / bucketSize));
            if (!buckets.TryGetValue(key, out List<int>? list))
                buckets[key] = list = [];
            list.Add(i);
        }

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
                (int bx, int by) = (
                    (int)MathF.Floor(mi.X / bucketSize),
                    (int)MathF.Floor(mi.Y / bucketSize));
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (!buckets.TryGetValue((bx + dx, by + dy), out List<int>? list))
                            continue;
                        for (int k = 0; k < list.Count; k++)
                        {
                            int j = list[k];
                            if (j <= i)
                                continue;
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
        // Stacked rows of ONE lane run almost exactly parallel (cos ≈ 1).  Two arms of a genuine fork fan out at a wider angle, so 0.9 separates them while still accepting real stacked rows (the debug case is ~1-4 units apart and < 10° apart).
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

    // True when child's maximal fork-free run is a stacked duplicate of main's maximal run (every child segment sits within the merge distance of a main-run segment).
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

    // Whether a segment that is not part of the rendered forest is a stacked duplicate of a rendered lane (merged into it, so it must not be reported as an unmapped topology anomaly).
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

    // Phase 2 (AND upward) then Phase 3 (OR downward), each to fixed point — with a pump-rooted tree a child can sit on either side of its parent, so a single ascending/descending pass would be wrong.
    internal static bool[] PropagateType(LaneCoverageResult[] results, bool[] localHas)
    {
        int n = results.Length;
        bool[] has = (bool[])localHas.Clone();

        // Scratch buffers reused across fixed-point iterations (cleared each pass) instead of allocating a fresh pair per iteration — propagation depth is small but the arrays are per-iteration objects.
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

        // Every lane except the branch's main lane gets a unique short name ({letter}-{n}); the main lane keeps the bare {letter}. One counter shared across all tops and forks keeps names collision-free no matter how deep the divergence tree gets.
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

    // Precomputed subtree sizes let BuildLaneTree pick the largest arm as the lane continuation at every fork without walking the subtree per fork.
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

    // The shared visited set keeps the recursion finite even if the children graph is ever corrupted into a cycle.
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

            // At a fork the LARGEST arm continues THIS lane (same name) so a winding trunk stays ONE lane instead of fragmenting into a new lane per segment; only the smaller arms become short numbered side lanes.
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

                // A fork arm whose run is a stacked duplicate of the main continuation (parallel rows of the SAME physical lane — the game lays rows on top of one another) is merged into this lane instead of rendering as a separate divergence.
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
