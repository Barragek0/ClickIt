using static ClickIt.Features.Blight.Planning.BlightGeometry;

namespace ClickIt.Features.Blight.Planning;

internal readonly record struct PumpBranch(int CoverageSegment, NumVector2 Anchor);

internal static class BlightBranches
{
    internal const float PumpBranchMaxDistanceSq = 30f * 30f;

    internal const float BranchMergeRadiusSq = 40f * 40f;

    // Shared placement metric for coverage and fill tiers: how desirable a foundation is for a rule.  NearestPump prefers pump proximity, NearExistingTowers prefers clustering beside an already-assigned tower, and everything else falls back to the caller's metric.
    internal static float PlacementMetric(
        IReadOnlyList<BlightCachedTower> knownTowers,
        HashSet<int> assignedIndices,
        int candidateIdx,
        float fallbackMetric,
        BlightPlacementPreference placement,
        NumVector2? pumpPosition)
    {
        NumVector2 p = knownTowers[candidateIdx].WorldPosition;
        return placement switch
        {
            BlightPlacementPreference.NearestPump when pumpPosition.HasValue
                => SqDist(p, pumpPosition.Value),
            BlightPlacementPreference.NearExistingTowers
                => DistanceToNearestAssignedTowerSq(knownTowers, assignedIndices, candidateIdx),
            _ => fallbackMetric,
        };
    }

    internal static float DistanceToNearestAssignedTowerSq(
        IReadOnlyList<BlightCachedTower> knownTowers,
        HashSet<int> assignedIndices,
        int candidateIdx)
    {
        float best = float.MaxValue;
        NumVector2 p = knownTowers[candidateIdx].WorldPosition;
        foreach (int i in assignedIndices)
        {
            if (i == candidateIdx) continue;
            float d = SqDist(knownTowers[i].WorldPosition, p);
            if (d < best) best = d;
        }
        return best;
    }

    internal static List<PumpBranch> FindPumpBranches(
        LaneCoverageResult[] coverage,
        NumVector2? pumpPosition,
        IReadOnlyList<NumVector2>? pathwayPositions,
        ConcurrentDictionary<NumVector2, byte>? cachedAnchors)
    {
        List<PumpBranch> branches = [];

        // One branch per pump component; arms forking off the SAME orphan root are one branch, so only the first child of each root starts a branch.
        bool[] rootHasBranch = new bool[coverage.Length];
        for (int s = 0; s < coverage.Length; s++)
        {
            if (coverage[s].ParentIndex == BlightLaneTopology.OrphanSentinel)
                continue;
            int parentSegment = coverage[s].ParentIndex;
            if (coverage[parentSegment].ParentIndex != BlightLaneTopology.OrphanSentinel)
                continue;
            if (coverage[s].IsPumpStub)
                continue; // a pump-connector stub never starts a branch — it is not a monster lane
            if (rootHasBranch[parentSegment])
                continue;
            rootHasBranch[parentSegment] = true;

            NumVector2 anchor = pathwayPositions != null && pathwayPositions.Count == coverage.Length
                ? pathwayPositions[parentSegment]
                : coverage[s].Midpoint;
            if (pumpPosition.HasValue && SqDist(anchor, pumpPosition.Value) > PumpBranchMaxDistanceSq)
                continue;
            branches.Add(new PumpBranch(s, anchor));
        }

        // Persist branches from earlier rebuilds even when their pump-near entities streamed out; only attach a CoverageSegment when a segment's midpoint is actually near the anchor.
        if (cachedAnchors != null)
        {
            foreach (NumVector2 anchor in cachedAnchors.Keys)
            {
                bool nearExisting = false;
                for (int b = 0; b < branches.Count; b++)
                {
                    if (SqDist(branches[b].Anchor, anchor) <= BranchMergeRadiusSq)
                    { nearExisting = true; break; }
                }
                if (nearExisting)
                    continue;
                int segment = FindNearestConnectedSegment(coverage, anchor, BranchMergeRadiusSq);
                branches.Add(new PumpBranch(segment, anchor));
            }
        }

        return branches;
    }

    // A branch is covered by a type only when EVERY segment in its subtree is covered, now or once an in-progress tower reaches max.
    internal static bool BranchHasCoverage(
        PumpBranch branch, LaneCoverageResult[] coverage, BlightTowerType type,
        IReadOnlyList<BlightCachedTower> knownTowers, int targetLevel)
    {
        bool[] covered = ComputePlannedCoveredState(coverage, type, knownTowers, targetLevel);
        return SubtreeFullyCovered(branch, coverage, covered, knownTowers, type, targetLevel);
    }

    // The working coverage state the planner plans against: the current coverage array (which already includes AND/OR propagation of built towers) plus the segments an in-progress (below-max) built tower of the type will cover once upgraded to max, re-propagated through the same rules.
    internal static bool[] ComputePlannedCoveredState(
        LaneCoverageResult[] coverage, BlightTowerType type, IReadOnlyList<BlightCachedTower> knownTowers,
        int targetLevel)
    {
        bool[] local = new bool[coverage.Length];
        for (int s = 0; s < coverage.Length; s++)
        {
            if (coverage[s].ParentIndex < 0)
                continue;
            local[s] = SegmentHasType(coverage[s], type)
                || BuiltTowerCovers(coverage[s].Midpoint, type, knownTowers, includeAtMax: false, targetLevel);
        }
        return BlightLaneTopology.PropagateType(coverage, local);
    }

    internal static List<int> BranchSegments(LaneCoverageResult[] coverage, PumpBranch branch)
    {
        List<int> result = [];
        if (branch.CoverageSegment < 0)
            return result;

        int root = branch.CoverageSegment;
        int guard = 0;
        while (coverage[root].ParentIndex >= 0 && coverage[root].ParentIndex != root && guard++ < coverage.Length)
            root = coverage[root].ParentIndex;

        Stack<int> pending = new();
        bool[] visited = new bool[coverage.Length];
        foreach (int c in FindSubBranches(coverage, root))
            pending.Push(c);
        while (pending.Count > 0)
        {
            int s = pending.Pop();
            if (visited[s])
                continue;
            visited[s] = true;
            result.Add(s);
            foreach (int c in FindSubBranches(coverage, s))
                if (!visited[c])
                    pending.Push(c);
        }
        return result;
    }

    internal static bool SubtreeFullyCovered(
        PumpBranch branch, LaneCoverageResult[] coverage, bool[] covered,
        IReadOnlyList<BlightCachedTower> knownTowers, BlightTowerType type, int targetLevel)
    {
        if (branch.CoverageSegment < 0)
        {
            return BuiltTowerCovers(branch.Anchor, type, knownTowers, includeAtMax: true, targetLevel);
        }
        foreach (int s in BranchSegments(coverage, branch))
            if (!covered[s])
                return false;
        return true;
    }

    internal static bool SegmentHasType(LaneCoverageResult segment, BlightTowerType type)
        => type switch
        {
            BlightTowerType.Chilling => segment.HasChilling,
            BlightTowerType.Seismic => segment.HasSeismic,
            BlightTowerType.Fireball => segment.HasFireball,
            BlightTowerType.Empowering => segment.HasEmpowering,
            BlightTowerType.ShockNova => segment.HasShockNova,
            BlightTowerType.Summoning => segment.HasSummoning,
            _ => false,
        };

    internal static bool BuiltTowerCovers(
        NumVector2 point, BlightTowerType type, IReadOnlyList<BlightCachedTower> knownTowers,
        bool includeAtMax, int targetLevel)
    {
        for (int i = 0; i < knownTowers.Count; i++)
        {
            BlightCachedTower t = knownTowers[i];
            if (t.TowerType != type || t.UpgradeLevel <= 0)
                continue;
            if (t.UpgradeLevel >= targetLevel)
            {
                if (!includeAtMax)
                    continue; // already reflected in the live measurement
                int radius = t.Radius > 0
                    ? t.Radius
                    : BlightService.GetRadiusForLevel(type, t.UpgradeLevel);
                if (SqDist(t.WorldPosition, point) <= Sq(BlightService.GetCoverageRadius(radius)))
                    return true;
            }
            else if (SqDist(t.WorldPosition, point) <= Sq(BlightService.GetCoverageRadiusForLevel(type, targetLevel)))
            {
                return true; // in-progress — will cover once upgraded to target
            }
        }
        return false;
    }

    internal static int FindNearestConnectedSegment(
        LaneCoverageResult[] coverage, NumVector2 point, float maxDistSq)
    {
        int best = -1;
        float bestD = float.MaxValue;
        for (int s = 0; s < coverage.Length; s++)
        {
            if (coverage[s].ParentIndex < 0) continue;
            float d = SqDist(coverage[s].Midpoint, point);
            if (d > maxDistSq) continue;
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    internal static int FindNearestBranch(
        BlightCachedTower tower,
        List<PumpBranch> branches)
    {
        int bestBranch = -1;
        float bestDist = float.MaxValue;
        for (int b = 0; b < branches.Count; b++)
        {
            float distSq = SqDist(tower.WorldPosition, branches[b].Anchor);
            if (distSq < bestDist)
            {
                bestDist = distSq;
                bestBranch = b;
            }
        }
        return bestBranch;
    }

    internal static List<int> FindSubBranches(LaneCoverageResult[] coverage, int segmentIdx)
    {
        List<int> children = [];

        // Direct children: any segment whose ParentIndex == segmentIdx.  Full scan — in the pump-rooted tree a child can sit on either side of its parent in the array.
        for (int s = 0; s < coverage.Length; s++)
        {
            if (s != segmentIdx && coverage[s].ParentIndex == segmentIdx && !coverage[s].IsPumpStub)
                children.Add(s);
        }

        return children;
    }

    // Post-pass over the computed coverage: chains that the normal branch pass left with no branch are attached to the most appropriate branch. The game can split one visual lane into several beam chains, so a split-off chain's head (pump-ward end) sits right at the end of a branch lane; re-parenting it under that lane end makes it the lane's continuation (e.g. A-1.4...). If the head is out of range, walk the chain from its head (furthest from the portal) toward the portal end and attach the first in-range segment. A very close gap (<= closeGapDistance) joins on a looser angle — real continuations can leave the lane end at a noticeable angle, but a lane that far-off the end that far away is separate. Unattachable chains are left untouched so the debug UI can surface them as unassigned.
    internal static bool AttachUnassignedLanes(
        LaneCoverageResult[] coverage,
        IReadOnlyList<NumVector2> positions,
        List<PumpBranch> branches,
        float attachDistance = 35f,
        float alignmentCos = 0.85f,
        float closeGapDistance = 5f,
        float closeAlignmentCos = 0.4f)
    {
        int n = coverage.Length;
        if (n < 2 || positions == null || positions.Count != n)
            return false;

        bool attachedAny = false;
        bool[] assigned = BuildAssignedMask(coverage, branches);
        List<List<int>> children = BuildAttachChildren(coverage);
        List<int> unassignedRoots = FindUnassignedRoots(coverage, assigned);

        foreach (int root in unassignedRoots)
        {
            // Walk the chain from its pump-ward head (furthest from the portal) toward the portal end, trying each segment as the attach point: a short stub poking back toward the pump can put the head out of range while the real junction sits one segment deeper.
            int seg = root;
            while (true)
            {
                int best = -1;
                float bestScore = float.MaxValue;
                for (int e = 0; e < n; e++)
                {
                    if (!assigned[e] || children[e].Count != 0)
                        continue;
                    float d = SqDist(positions[seg], positions[e]);
                    if (d > attachDistance * attachDistance || d < 0.0001f)
                        continue;
                    float minCos = d <= closeGapDistance * closeGapDistance ? closeAlignmentCos : alignmentCos;
                    if (!AlignedAt(coverage, positions, e, seg, minCos))
                        continue;
                    if (d < bestScore) { bestScore = d; best = e; }
                }
                if (best >= 0)
                {
                    ReparentUnder(coverage, children, assigned, seg, best);
                    attachedAny = true;
                    break;
                }
                if (children[seg].Count == 0)
                    break;
                seg = children[seg][0];
            }
        }
        return attachedAny;
    }

    // Post-pass over the computed coverage: chains that run PARALLEL to an assigned lane within a small distance are merged into that lane. The game lays one visual lane twice as stacked parallel rows a few units apart (MergeStackedLaneFlags already merges their coverage flags), but a stacked chain's head does not sit at a lane END, so the lane-end attach pass above never attaches it and it stays an unassigned branch. When a chain segment travels parallel to an assigned segment (|cos| >= alignmentCos — the row can be laid either way up) within parallelDistance, re-parent the chain onto that segment so both rows are treated as one lane (e.g. the orange row beside B-1.2/1.3/1.4 and the red row after B-1.21).
    internal static bool AttachParallelLanes(
        LaneCoverageResult[] coverage,
        IReadOnlyList<NumVector2> positions,
        List<PumpBranch> branches,
        float parallelDistance = 9f,
        float alignmentCos = 0.9f)
    {
        int n = coverage.Length;
        if (n < 2 || positions == null || positions.Count != n)
            return false;

        bool attachedAny = false;
        bool[] assigned = BuildAssignedMask(coverage, branches);
        List<List<int>> children = BuildAttachChildren(coverage);
        List<int> unassignedRoots = FindUnassignedRoots(coverage, assigned);

        foreach (int root in unassignedRoots)
        {
            // Walk the chain from its pump-ward head toward the portal end, trying each segment as the attach point — the row's head can drift out of the parallel window while a deeper segment hugs the lane.
            int seg = root;
            while (true)
            {
                int best = -1;
                float bestScore = float.MaxValue;
                NumVector2 chainDir = ChainDirectionAt(coverage, positions, children, seg);
                float chainLen = chainDir.Length();
                if (chainLen >= 0.001f)
                {
                    chainDir /= chainLen;
                    for (int a = 0; a < n; a++)
                    {
                        if (!assigned[a])
                            continue;
                        int pa = coverage[a].ParentIndex;
                        if (pa < 0 || pa >= n || pa == a)
                            continue;
                        NumVector2 laneDir = positions[a] - positions[pa];
                        float laneLen = laneDir.Length();
                        if (laneLen < 0.001f)
                            continue;
                        float d = SqDist(positions[seg], positions[a]);
                        if (d > parallelDistance * parallelDistance || d < 0.0001f)
                            continue;
                        laneDir /= laneLen;
                        if (Math.Abs(NumVector2.Dot(chainDir, laneDir)) < alignmentCos)
                            continue;
                        if (d < bestScore) { bestScore = d; best = a; }
                    }
                }
                if (best >= 0)
                {
                    ReparentUnder(coverage, children, assigned, seg, best);
                    attachedAny = true;
                    break;
                }
                if (children[seg].Count == 0)
                    break;
                seg = children[seg][0];
            }
        }
        return attachedAny;
    }

    // Segments already claimed by a branch (the union of every branch's subtree). BranchSegments walks a branch's subtree but excludes the branch's own orphan root, so those roots are marked assigned here too — they anchor a branch and are never unassigned.
    private static bool[] BuildAssignedMask(LaneCoverageResult[] coverage, List<PumpBranch> branches)
    {
        int n = coverage.Length;
        bool[] assigned = new bool[n];
        for (int b = 0; b < branches.Count; b++)
        {
            if (branches[b].CoverageSegment < 0)
                continue;
            int root = branches[b].CoverageSegment;
            int guard = 0;
            while (coverage[root].ParentIndex >= 0 && coverage[root].ParentIndex != root && guard++ < n)
                root = coverage[root].ParentIndex;
            assigned[root] = true;
            List<int> segs = BranchSegments(coverage, branches[b]);
            for (int i = 0; i < segs.Count; i++)
                assigned[segs[i]] = true;
        }
        return assigned;
    }

    // Children map, kept in sync as chains attach — drives lane-end detection + subtree walks.
    private static List<List<int>> BuildAttachChildren(LaneCoverageResult[] coverage)
    {
        int n = coverage.Length;
        List<List<int>> children = new(n);
        for (int i = 0; i < n; i++) children.Add([]);
        for (int s = 0; s < n; s++)
        {
            int par = coverage[s].ParentIndex;
            if (par >= 0 && par < n && par != s)
                children[par].Add(s);
        }
        return children;
    }

    // Chain heads (ParentIndex < 0) that no branch claims.
    private static List<int> FindUnassignedRoots(LaneCoverageResult[] coverage, bool[] assigned)
    {
        List<int> unassignedRoots = [];
        for (int s = 0; s < coverage.Length; s++)
        {
            if (coverage[s].ParentIndex >= 0 || assigned[s])
                continue;
            unassignedRoots.Add(s);
        }
        return unassignedRoots;
    }

    // The chain's travel direction at seg, pointing from the pump-ward head toward the portal end.
    private static NumVector2 ChainDirectionAt(
        LaneCoverageResult[] coverage, IReadOnlyList<NumVector2> positions, List<List<int>> children, int seg)
    {
        int parent = coverage[seg].ParentIndex;
        if (parent >= 0 && parent < coverage.Length && parent != seg)
            return positions[seg] - positions[parent];
        // chain head: use the direction toward its first child (the chain's continuation)
        List<int> c = children[seg];
        if (c.Count > 0)
            return positions[c[0]] - positions[seg];
        return NumVector2.Zero;
    }

    // Re-parent seg under an assigned lane segment and reconnect the head-side (the segments between the chain head and seg) onto seg: the whole chain is one physical lane, so it joins the branch as a single connected run instead of leaving an unassigned stub that shows as a gap.
    private static void ReparentUnder(
        LaneCoverageResult[] coverage, List<List<int>> children, bool[] assigned, int seg, int newParent)
    {
        int attached = seg;
        int oldParent = coverage[seg].ParentIndex;
        if (oldParent >= 0 && oldParent < coverage.Length)
            children[oldParent].Remove(seg);
        coverage[seg] = coverage[seg] with { ParentIndex = newParent };
        children[newParent].Add(seg);
        assigned[seg] = true;

        int headSide = oldParent;
        while (headSide >= 0 && headSide < coverage.Length && !assigned[headSide])
        {
            int nextHead = coverage[headSide].ParentIndex;
            if (nextHead >= 0 && nextHead < coverage.Length)
                children[nextHead].Remove(headSide);
            coverage[headSide] = coverage[headSide] with { ParentIndex = seg };
            children[seg].Add(headSide);
            assigned[headSide] = true;
            seg = headSide;
            headSide = nextHead;
        }

        foreach (int s in CollectSubtree(children, attached))
            assigned[s] = true;
    }

    // Whether the direction from a branch lane end toward an unattached chain head aligns with the lane's own heading at that end (the fragment continues the lane, not a crossing line).
    private static bool AlignedAt(
        LaneCoverageResult[] coverage, IReadOnlyList<NumVector2> positions, int laneEnd, int fragmentHead, float cos)
    {
        int parent = coverage[laneEnd].ParentIndex;
        if (parent < 0 || parent >= coverage.Length)
            return true; // single-segment branch lane: accept by distance alone
        NumVector2 laneDir = positions[laneEnd] - positions[parent];
        float laneLen = laneDir.Length();
        if (laneLen < 0.001f)
            return true;
        laneDir /= laneLen;
        NumVector2 fragDir = positions[fragmentHead] - positions[laneEnd];
        float fragLen = fragDir.Length();
        if (fragLen < 0.001f)
            return false;
        fragDir /= fragLen;
        return NumVector2.Dot(laneDir, fragDir) >= cos;
    }

    private static List<int> CollectSubtree(List<List<int>> children, int root)
    {
        List<int> result = [];
        Stack<int> pending = new();
        pending.Push(root);
        while (pending.Count > 0)
        {
            int s = pending.Pop();
            result.Add(s);
            List<int> c = children[s];
            for (int i = 0; i < c.Count; i++)
                pending.Push(c[i]);
        }
        return result;
    }
}
