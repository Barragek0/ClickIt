namespace ClickIt.Features.Blight.Planning;

internal readonly record struct PumpBranch(int CoverageSegment, NumVector2 Anchor);

internal static class BlightBranches
{
    internal const float PumpBranchMaxDistanceSq = 30f * 30f;

    internal const float BranchMergeRadiusSq = 40f * 40f;

    internal static float Sq(float v) => v * v;

    internal static float SqDist(NumVector2 a, NumVector2 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    internal static List<PumpBranch> FindPumpBranches(
        LaneCoverageResult[] coverage,
        NumVector2? pumpPosition,
        IReadOnlyList<NumVector2>? pathwayPositions,
        IReadOnlyList<NumVector2>? cachedAnchors)
    {
        List<PumpBranch> branches = [];

        // One branch per pump component.  A component is rooted at an orphan point (the pump-near
        // lane start) and its children are the lane's first segments.  A lane that forks right at the
        // pump has several children of the SAME orphan root — those arms are ONE branch (the whole
        // component), so only the first child of each root starts a branch.  Splitting every child of
        // a root into its own branch counted the two arms of one lane as two lanes, produced phantom
        // branches, and let the coverage gate report "full coverage" while an arm was still
        // uncovered.
        bool[] rootHasBranch = new bool[coverage.Length];
        for (int s = 0; s < coverage.Length; s++)
        {
            if (coverage[s].ParentIndex == BlightLaneTopology.OrphanSentinel)
                continue;
            int parentSegment = coverage[s].ParentIndex;
            if (coverage[parentSegment].ParentIndex != BlightLaneTopology.OrphanSentinel)
                continue;
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

        // Persist branches from earlier rebuilds even when their pump-near entities streamed out; only
        // attach a CoverageSegment when a segment's midpoint is actually near the anchor.
        if (cachedAnchors != null)
        {
            for (int c = 0; c < cachedAnchors.Count; c++)
            {
                bool nearExisting = false;
                for (int b = 0; b < branches.Count; b++)
                {
                    if (SqDist(branches[b].Anchor, cachedAnchors[c]) <= BranchMergeRadiusSq)
                    { nearExisting = true; break; }
                }
                if (nearExisting)
                    continue;
                int segment = FindNearestConnectedSegment(coverage, cachedAnchors[c], BranchMergeRadiusSq);
                branches.Add(new PumpBranch(segment, cachedAnchors[c]));
            }
        }

        return branches;
    }

    // A branch is covered by a type only when EVERY segment in its subtree is covered — either now
    // (the coverage array, which already includes AND/OR propagation of built towers) or once an
    // in-progress (below-max) built tower reaches max.  The base-only check was the root cause of
    // false "full coverage" on forks: a tower covering just the branch base can leave fork arms
    // downstream uncovered, so the whole subtree must be verified.
    internal static bool BranchHasCoverage(
        PumpBranch branch, LaneCoverageResult[] coverage, bool seismic,
        IReadOnlyList<BlightCachedTower> knownTowers, int targetLevel)
    {
        if (branch.CoverageSegment < 0)
        {
            // A cached branch with no live segment is verified against its persisted ANCHOR: a built
            // tower whose radius reaches the anchor covers the branch base — coverage stays stable
            // when pump-near entities stream out.
            BlightTowerType type = seismic ? BlightTowerType.Seismic : BlightTowerType.Chilling;
            return BuiltTowerCovers(branch.Anchor, type, knownTowers, includeAtMax: true, targetLevel);
        }

        bool[] covered = ComputePlannedCoveredState(coverage, seismic, knownTowers, targetLevel);
        foreach (int s in BranchSegments(coverage, branch))
            if (!covered[s])
                return false;
        return true;
    }

    // The working coverage state the planner plans against: the current coverage array (which already
    // includes AND/OR propagation of built towers) plus the segments an in-progress (below-max) built
    // tower of the type will cover once upgraded to max, re-propagated through the same rules.
    internal static bool[] ComputePlannedCoveredState(
        LaneCoverageResult[] coverage, bool seismic, IReadOnlyList<BlightCachedTower> knownTowers,
        int targetLevel)
    {
        BlightTowerType type = seismic ? BlightTowerType.Seismic : BlightTowerType.Chilling;
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
        IReadOnlyList<BlightCachedTower> knownTowers, bool seismic, int targetLevel)
    {
        if (branch.CoverageSegment < 0)
        {
            BlightTowerType type = seismic ? BlightTowerType.Seismic : BlightTowerType.Chilling;
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
            _ => segment.HasFireball,
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

        // Direct children: any segment whose ParentIndex == segmentIdx.  Full scan — in the
        // pump-rooted tree a child can sit on either side of its parent in the array.
        for (int s = 0; s < coverage.Length; s++)
        {
            if (s != segmentIdx && coverage[s].ParentIndex == segmentIdx && !coverage[s].IsPumpStub)
                children.Add(s);
        }

        return children;
    }
}
