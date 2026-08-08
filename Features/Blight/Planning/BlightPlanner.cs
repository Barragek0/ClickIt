using static ClickIt.Features.Blight.Planning.BlightBranches;

namespace ClickIt.Features.Blight.Planning;

internal static class BlightPlanner
{
    internal static BlightPlan Build(
        IReadOnlyList<BlightCachedTower> knownTowers,
        LaneCoverageResult[] coverage,
        IReadOnlyList<TowerBuildRule> rules,
        HashSet<NumVector2> failedPositions,
        int version,
        NumVector2? pumpPosition = null,
        NumVector2? playerPosition = null,
        IReadOnlyList<NumVector2>? pathwayPositions = null,
        List<NumVector2>? cachedBranchAnchors = null,
        bool groupStepsByProximity = true)
    {
        if (rules.Count == 0 || knownTowers.Count == 0)
            return BlightPlan.Completed(version, "No rules or no foundations");

        List<PumpBranch> pumpBranches = FindPumpBranches(coverage, pumpPosition, pathwayPositions, cachedBranchAnchors);

        if (cachedBranchAnchors != null)
        {
            for (int b = 0; b < pumpBranches.Count; b++)
            {
                bool present = false;
                for (int c = 0; c < cachedBranchAnchors.Count; c++)
                {
                    if (SqDist(cachedBranchAnchors[c], pumpBranches[b].Anchor) <= BranchMergeRadiusSq)
                    { present = true; break; }
                }
                if (!present)
                    cachedBranchAnchors.Add(pumpBranches[b].Anchor);
            }
        }

        int branchCount = pumpBranches.Count;

        List<BlightTowerType> coverageTypes = [];
        for (int r = 0; r < rules.Count; r++)
            if (rules[r].IsCoverageTower && !coverageTypes.Contains(rules[r].TowerType))
                coverageTypes.Add(rules[r].TowerType);

        Dictionary<BlightTowerType, bool[]> branchHasType = [];
        Dictionary<BlightTowerType, bool[]> plannedType = [];
        for (int t = 0; t < coverageTypes.Count; t++)
        {
            BlightTowerType type = coverageTypes[t];
            int target = BlightFillPlanner.FindRule(rules, type)?.MaxUpgradeLevel ?? BlightTowerData.MaxUpgradeLevel;
            bool[] has = new bool[branchCount];
            for (int b = 0; b < branchCount; b++)
                has[b] = BranchHasCoverage(pumpBranches[b], coverage, type, knownTowers, target);
            branchHasType[type] = has;
            plannedType[type] = (bool[])has.Clone();
        }

        Dictionary<NumVector2, (BlightTowerType Type, int MaxLevel)> assignments = [];
        HashSet<int> assignedIndices = [];
        int preAssignedBuilt = 0;
        List<CoveragePlacement> coveragePlacements = [];
        List<NumVector2> orderedFillPositions = [];

        for (int i = 0; i < knownTowers.Count; i++)
        {
            if (knownTowers[i].UpgradeLevel > 0)
            {
                preAssignedBuilt++;
                assignedIndices.Add(i);
                assignments[knownTowers[i].WorldPosition]
                    = (knownTowers[i].TowerType, BlightTowerData.MaxUpgradeLevel);

                TowerBuildRule? rule = BlightFillPlanner.FindRule(rules, knownTowers[i].TowerType);
                if (rule is TowerBuildRule covRule && covRule.IsCoverageTower)
                {
                    // Coverage is measured against segments, not branch anchors, so a built tower is
                    // upgraded as far as the farthest branch-subtree segment its max radius reaches.
                    int branchIdx = FindNearestBranch(knownTowers[i], pumpBranches);
                    float reqRadiusSq = branchIdx >= 0
                        ? MaxCoveredBranchSegmentDistSq(knownTowers[i], knownTowers[i].TowerType, coverage,
                            pumpBranches, Sq(BlightService.GetCoverageRadiusForLevel(knownTowers[i].TowerType, covRule.MaxUpgradeLevel)))
                        : 0f;
                    if (branchIdx >= 0)
                    {
                        float distToPlayerSq = playerPosition.HasValue
                            ? (knownTowers[i].WorldPosition - playerPosition.Value).LengthSquared()
                            : 0f;
                        coveragePlacements.Add(new CoveragePlacement(
                            branchIdx, knownTowers[i].TowerType, i, distToPlayerSq, reqRadiusSq));
                    }
                }
                else
                {
                    // Built fill-tier tower assigned here, not by AssignFill — record it so BuildOrderedSteps plans its upgrades.
                    orderedFillPositions.Add(knownTowers[i].WorldPosition);
                }
            }
        }

        List<IGrouping<TowerBuildPriority, TowerBuildRule>> priorityGroups = [.. rules
            .GroupBy(r => r.Priority)
            .OrderBy(g => g.Key)];

        // A zero branch count does NOT mean coverage is complete — fill only activates after all
        // branches have full coverage of every coverage type the strategy declares.
        bool hasCoverageRules = coverageTypes.Count > 0;
        bool coverageComplete = !hasCoverageRules;

        foreach (IGrouping<TowerBuildPriority, TowerBuildRule> group in priorityGroups)
        {
            List<TowerBuildRule> tierRules = [.. group];
            if (!tierRules.Any(r => r.IsCoverageTower))
                continue;

            AssignCoverage(
                tierRules, knownTowers, coverage, pumpBranches,
                plannedType,
                failedPositions, assignedIndices, assignments,
                pumpPosition, playerPosition, coveragePlacements);

            // Coverage is complete ONLY when every branch has full coverage of every coverage type
            // — the fill tier must never run while any branch still lacks a coverage type.
            if (branchCount > 0 && AllBranchesCovered(plannedType))
                coverageComplete = true;
        }

        // Best-effort escape (spec §4.7): a branch whose subtree has no reachable foundation can never
        // be covered, so mark it skipped instead of blocking the fill tier forever.  Only branches with
        // a live coverage segment can be assessed this way.
        if (hasCoverageRules && branchCount > 0 && !coverageComplete)
        {
            coverageComplete = true;
            for (int t = 0; t < coverageTypes.Count; t++)
            {
                BlightTowerType type = coverageTypes[t];
                bool[] planned = plannedType[type];
                bool[] skipped = new bool[branchCount];
                for (int b = 0; b < branchCount; b++)
                {
                    if (pumpBranches[b].CoverageSegment < 0)
                        continue;
                    if (!planned[b])
                        skipped[b] = !SubtreeHasReachableFoundation(
                            coverage, pumpBranches[b],
                            CoverageMaxRadiusSq(rules, type),
                            knownTowers, failedPositions, assignedIndices);
                }
                for (int b = 0; b < branchCount; b++)
                {
                    if (!(planned[b] || skipped[b]))
                    {
                        coverageComplete = false;
                        break;
                    }
                }
                if (!coverageComplete)
                    break;
            }
        }

        // Pass 2 — fill tiers in priority order.  Fill always runs after the coverage passes, even
        // when coverage is incomplete: the plan carries the full build (coverage + fill), so unused
        // foundations are never left behind while coverage placements are still being planned.
        foreach (IGrouping<TowerBuildPriority, TowerBuildRule> group in priorityGroups)
        {
            List<TowerBuildRule> tierRules = [.. group];
            List<TowerBuildRule> fillRules = [];
            for (int r = 0; r < tierRules.Count; r++)
                if (!tierRules[r].IsCoverageTower)
                    fillRules.Add(tierRules[r]);
            if (fillRules.Count == 0)
                continue;

            BlightFillPlanner.AssignFill(fillRules, knownTowers, coverage, failedPositions,
                assignedIndices, assignments, orderedFillPositions,
                pumpPosition, playerPosition);
        }

        // Per-type coverage counts (branchHas -> planned) for the debug summaries.
        System.Text.StringBuilder covStat = new();
        for (int t = 0; t < coverageTypes.Count; t++)
        {
            BlightTowerType type = coverageTypes[t];
            int before = 0, after = 0;
            bool[] has = branchHasType[type];
            bool[] planned = plannedType[type];
            for (int b = 0; b < branchCount; b++)
            {
                if (has[b]) before++;
                if (planned[b]) after++;
            }
            if (covStat.Length > 0) covStat.Append(' ');
            covStat.Append(type.ToString()[..3]).Append('=').Append(before).Append("->").Append(after).Append('/').Append(branchCount);
        }

        // Debug summaries — one pass over branches, one over towers, producing all formats.
        System.Text.StringBuilder branchDbg = new(), anchorDbg = new(), branchSegDbg = new();
        for (int b = 0; b < branchCount; b++)
        {
            PumpBranch pb = pumpBranches[b];
            char letter = (char)('A' + b);
            if (b > 0) { branchDbg.Append(' '); anchorDbg.Append(' '); branchSegDbg.Append(' '); }
            branchDbg.Append(letter).Append('(');
            for (int t = 0; t < coverageTypes.Count; t++)
            {
                BlightTowerType type = coverageTypes[t];
                char ch = char.ToUpperInvariant(type.ToString()[0]);
                branchDbg.Append(branchHasType[type][b] ? ch : char.ToLowerInvariant(ch));
            }
            branchDbg.Append(')');
            anchorDbg.Append('(').Append(pb.Anchor.X.ToString("F0")).Append(',').Append(pb.Anchor.Y.ToString("F0")).Append(')');
            branchSegDbg.Append(letter).Append("(seg=").Append(pb.CoverageSegment);
            if (pb.CoverageSegment >= 0)
                branchSegDbg.Append(" mid=(").Append(coverage[pb.CoverageSegment].Midpoint.X.ToString("F0"))
                    .Append(',').Append(coverage[pb.CoverageSegment].Midpoint.Y.ToString("F0"))
                    .Append(") c=").Append(coverage[pb.CoverageSegment].HasChilling ? '1' : '0')
                    .Append(" s=").Append(coverage[pb.CoverageSegment].HasSeismic ? '1' : '0');
            branchSegDbg.Append(')');
        }

        System.Text.StringBuilder towerRadiusDbg = new(), assignDbg = new(), foundDbg = new();
        for (int i = 0; i < knownTowers.Count; i++)
        {
            BlightCachedTower t = knownTowers[i];
            if (i > 0) { assignDbg.Append(' '); foundDbg.Append(' '); }
            if (t.UpgradeLevel > 0)
            {
                if (towerRadiusDbg.Length > 0) towerRadiusDbg.Append(' ');
                int estimate = BlightService.GetRadiusForLevel(t.TowerType, t.UpgradeLevel);
                towerRadiusDbg.Append(t.TowerType.ToString()[..3]).Append('@').Append(t.UpgradeLevel)
                    .Append(" r=").Append(t.Radius > 0 ? t.Radius.ToString() : "?").Append("(est").Append(estimate).Append(')');
            }
            bool isAssigned = assignments.ContainsKey(t.WorldPosition);
            assignDbg.Append(isAssigned ? '+' : '-').Append(t.UpgradeLevel)
                .Append(isAssigned ? assignments[t.WorldPosition].Type.ToString()[..3] : "---");
            foundDbg.Append('(').Append(t.WorldPosition.X.ToString("F0")).Append(',').Append(t.WorldPosition.Y.ToString("F0")).Append(')');
        }

        List<BlightPlanStep> steps = BlightFillPlanner.BuildOrderedSteps(
            knownTowers, assignments, rules, coveragePlacements, orderedFillPositions);

        if (groupStepsByProximity)
        {
            // A step is a coverage step when its foundation is a coverage placement (rules can have
            // both coverage and fill rules for the same tower type, so type alone is ambiguous).
            HashSet<NumVector2> coveragePositions = [];
            for (int i = 0; i < coveragePlacements.Count; i++)
                coveragePositions.Add(knownTowers[coveragePlacements[i].KnownTowerIndex].WorldPosition);
            steps = ReorderStepsByProximity(steps, coveragePositions);
        }

        string summary = $"v{version} ({steps.Count} steps, {branchCount} branches, {(coverageComplete ? "full" : "partial")} coverage) [{branchDbg}]"
            + $" anchors={anchorDbg}"
            + $" pump={(pumpPosition.HasValue ? $"({pumpPosition.Value.X:F0},{pumpPosition.Value.Y:F0})" : "none")}"
            + $" preB={preAssignedBuilt}"
            + $" {covStat}"
            + $" |{assignDbg}";
        string details = $"[{branchDbg}]"
            + $" anchors={anchorDbg}"
            + $" branchSeg={branchSegDbg}"
            + $" towers={towerRadiusDbg}"
            + $" pump={(pumpPosition.HasValue ? $"({pumpPosition.Value.X:F0},{pumpPosition.Value.Y:F0})" : "none")}"
            + $" found={foundDbg}"
            + $" preBuilt={preAssignedBuilt}"
            + $" tierCov={covStat}"
            + $" assigned={assignments.Count}"
            + $" | {assignDbg}";

        return new BlightPlan(steps, version, summary, details);
    }

    internal static List<BlightPlanStep> ReorderStepsByProximity(
        List<BlightPlanStep> steps,
        HashSet<NumVector2> coveragePositions)
    {
        if (steps.Count <= 1)
            return steps;

        List<BlightPlanStep> coverageSteps = [];
        List<BlightPlanStep> fillSteps = [];
        for (int i = 0; i < steps.Count; i++)
        {
            BlightPlanStep step = steps[i];
            if (coveragePositions.Contains(step.FoundationPosition))
                coverageSteps.Add(step);
            else
                fillSteps.Add(step);
        }

        if (fillSteps.Count == 0)
            return GroupStepsByProximity(coverageSteps);
        if (coverageSteps.Count == 0)
            return GroupStepsByProximity(fillSteps);

        List<BlightPlanStep> reordered = GroupStepsByProximity(coverageSteps);
        reordered.AddRange(GroupStepsByProximity(fillSteps));
        return reordered;
    }

    private static List<BlightPlanStep> GroupStepsByProximity(List<BlightPlanStep> steps)
    {
        if (steps.Count <= 1)
            return steps;

        List<List<BlightPlanStep>> clusters = [];
        List<NumVector2> clusterPositions = [];
        for (int i = 0; i < steps.Count; i++)
        {
            BlightPlanStep step = steps[i];
            int clusterIdx = -1;
            for (int c = 0; c < clusters.Count; c++)
            {
                if (SqDist(clusterPositions[c], step.FoundationPosition) < 1f)
                {
                    clusterIdx = c;
                    break;
                }
            }

            if (clusterIdx < 0)
            {
                clusters.Add([step]);
                clusterPositions.Add(step.FoundationPosition);
            }
            else
            {
                clusters[clusterIdx].Add(step);
            }
        }

        if (clusters.Count <= 1)
            return steps;

        List<BlightPlanStep> result = [];
        bool[] visited = new bool[clusters.Count];
        int current = 0;
        int placed = 0;
        while (placed < clusters.Count)
        {
            visited[current] = true;
            result.AddRange(clusters[current]);
            placed++;

            int next = -1;
            float bestSq = float.MaxValue;
            for (int c = 0; c < clusters.Count; c++)
            {
                if (visited[c]) continue;
                float d = (clusterPositions[c] - clusterPositions[current]).LengthSquared();
                if (d < bestSq)
                {
                    bestSq = d;
                    next = c;
                }
            }
            if (next < 0) break;
            current = next;
        }

        return result;
    }

    internal readonly record struct CoveragePlacement(
        int BranchIdx,
        BlightTowerType Type,
        int KnownTowerIndex,
        float DistanceToPlayerSq,
        float RequiredRadiusSq);

    private static bool AllBranchesCovered(Dictionary<BlightTowerType, bool[]> planned)
    {
        foreach (KeyValuePair<BlightTowerType, bool[]> entry in planned)
        {
            bool[] arr = entry.Value;
            for (int b = 0; b < arr.Length; b++)
                if (!arr[b])
                    return false;
        }
        return true;
    }

    private static bool SubtreeHasReachableFoundation(
        LaneCoverageResult[] coverage,
        PumpBranch branch,
        float maxRadiusSq,
        IReadOnlyList<BlightCachedTower> knownTowers,
        HashSet<NumVector2> failedPositions,
        HashSet<int> assignedIndices)
    {
        Stack<int> pending = new();
        bool[] visited = new bool[coverage.Length];
        foreach (int s in BranchSegments(coverage, branch))
            pending.Push(s);
        while (pending.Count > 0)
        {
            int s = pending.Pop();
            if (visited[s])
                continue;
            visited[s] = true;
            for (int i = 0; i < knownTowers.Count; i++)
            {
                if (assignedIndices.Contains(i)) continue;
                if (failedPositions.Contains(knownTowers[i].WorldPosition)) continue;
                if (SqDist(knownTowers[i].WorldPosition, coverage[s].Midpoint) <= maxRadiusSq)
                    return true;
            }
            foreach (int child in FindSubBranches(coverage, s))
                if (!visited[child])
                    pending.Push(child);
        }
        return false;
    }

    private static float CoverageMaxRadiusSq(IReadOnlyList<TowerBuildRule> rules, BlightTowerType type)
    {
        TowerBuildRule? rule = BlightFillPlanner.FindRule(rules, type);
        int maxLevel = rule?.MaxUpgradeLevel ?? BlightTowerData.MaxUpgradeLevel;
        return Sq(BlightService.GetCoverageRadiusForLevel(type, maxLevel));
    }

    private static void AssignCoverage(
        List<TowerBuildRule> tierRules,
        IReadOnlyList<BlightCachedTower> knownTowers,
        LaneCoverageResult[] coverage,
        List<PumpBranch> branches,
        Dictionary<BlightTowerType, bool[]> plannedByType,
        HashSet<NumVector2> failedPositions,
        HashSet<int> assignedIndices,
        Dictionary<NumVector2, (BlightTowerType Type, int MaxLevel)> assignments,
        NumVector2? pumpPosition,
        NumVector2? playerPosition,
        List<CoveragePlacement> coveragePlacements)
    {
        for (int r = 0; r < tierRules.Count; r++)
        {
            TowerBuildRule rule = tierRules[r];
            if (!rule.IsCoverageTower)
                continue;
            if (!plannedByType.TryGetValue(rule.TowerType, out bool[] planned))
            {
                planned = new bool[branches.Count];
                plannedByType[rule.TowerType] = planned;
            }

            AssignCoverageType(rule, rule.TowerType,
                knownTowers, coverage, branches, planned,
                failedPositions, assignedIndices, assignments,
                pumpPosition, playerPosition, coveragePlacements);
        }
    }

    private static void AssignCoverageType(
        TowerBuildRule rule,
        BlightTowerType type,
        IReadOnlyList<BlightCachedTower> knownTowers,
        LaneCoverageResult[] coverage,
        List<PumpBranch> branches,
        bool[] planned,
        HashSet<NumVector2> failedPositions,
        HashSet<int> assignedIndices,
        Dictionary<NumVector2, (BlightTowerType Type, int MaxLevel)> assignments,
        NumVector2? pumpPosition,
        NumVector2? playerPosition,
        List<CoveragePlacement> coveragePlacements)
    {
        int maxBuild = rule.MaxBuildCount;
        int placed = maxBuild > 0 ? BlightFillPlanner.CountBuilt(knownTowers, type) : 0;
        float maxRadiusSq = Sq(BlightService.GetCoverageRadiusForLevel(type, rule.MaxUpgradeLevel));

        // Only segments that belong to a pump branch are coverable — isolated fragments far from the
        // pump are ignored (spec §4.7), and covering them would steal foundations from the fill tier.
        bool[] isBranchSegment = new bool[coverage.Length];
        for (int b = 0; b < branches.Count; b++)
        {
            if (branches[b].CoverageSegment < 0)
                continue;
            foreach (int s in BranchSegments(coverage, branches[b]))
                isBranchSegment[s] = true;
        }

        // Working covered state: everything the built towers cover today (the coverage array, which
        // already includes AND/OR propagation) plus what in-progress (below-max) towers will cover once
        // upgraded.  New towers extend this state through the SAME propagation rules, so a tower on one
        // fork arm that completes the fork correctly covers the trunk (Rule 2), and a trunk tower covers
        // both arms (Rule 3) — the greedy below is propagation-aware, not per-midpoint.
        bool[] covered = ComputePlannedCoveredState(coverage, type, knownTowers, rule.MaxUpgradeLevel);

        while (maxBuild <= 0 || placed < maxBuild)
        {
            int bestIdx = -1;
            int bestNew = 0;
            float bestReqSq = 0f;
            float bestMetric = float.MaxValue;

            for (int i = 0; i < knownTowers.Count; i++)
            {
                if (assignedIndices.Contains(i)) continue;
                if (failedPositions.Contains(knownTowers[i].WorldPosition)) continue;

                NumVector2 pos = knownTowers[i].WorldPosition;
                // Count directly-covered segments first; only clone+propagate when a candidate actually
                // covers something, so candidates too far away or already-fully-covered cost nothing.
                bool[]? local2 = null;
                int direct = 0;
                float maxDistSq = 0f;
                for (int s = 0; s < coverage.Length; s++)
                {
                    if (!isBranchSegment[s] || covered[s]) continue;
                    float d = SqDist(pos, coverage[s].Midpoint);
                    if (d > maxRadiusSq) continue;
                    direct++;
                    if (d > maxDistSq) maxDistSq = d;
                    (local2 ??= (bool[])covered.Clone())[s] = true;
                }
                if (direct == 0) continue;

                bool[] covered2 = BlightLaneTopology.PropagateType(coverage, local2!);
                int newly = 0;
                for (int s = 0; s < coverage.Length; s++)
                    if (isBranchSegment[s] && covered2[s] && !covered[s]) newly++;
                if (newly == 0) continue;

                float metric = PlacementMetric(knownTowers, assignedIndices, i, maxDistSq, rule.Placement, pumpPosition);
                if (bestIdx < 0 || newly > bestNew || (newly == bestNew && metric < bestMetric))
                {
                    bestIdx = i;
                    bestNew = newly;
                    bestReqSq = maxDistSq;
                    bestMetric = metric;
                }
            }

            if (bestIdx < 0)
                break;

            BlightCachedTower tower = knownTowers[bestIdx];
            assignments[tower.WorldPosition] = (type, BlightTowerData.MaxUpgradeLevel);
            assignedIndices.Add(bestIdx);

            bool[] localNext = (bool[])covered.Clone();
            for (int s = 0; s < coverage.Length; s++)
            {
                if (!isBranchSegment[s]) continue;
                if (SqDist(tower.WorldPosition, coverage[s].Midpoint) <= maxRadiusSq)
                    localNext[s] = true;
            }
            covered = BlightLaneTopology.PropagateType(coverage, localNext);

            int branchIdx = FindNearestBranch(tower, branches);
            AddCoveragePlacement(knownTowers, branchIdx, type, bestIdx, bestReqSq, playerPosition, coveragePlacements);
            placed++;
        }

        // A branch is planned only when its WHOLE subtree is covered — a tower that reaches the branch
        // base but stops short of a fork arm no longer counts as covering the branch.
        for (int b = 0; b < branches.Count; b++)
        {
            if (planned[b]) continue;
            if (SubtreeFullyCovered(branches[b], coverage, covered, knownTowers, type, rule.MaxUpgradeLevel))
                planned[b] = true;
        }

        // Redundancy slots (TowersPerBranch > 1) — extra towers near each branch without changing coverage state.
        for (int slot = 1; slot < rule.TowersPerBranch; slot++)
        {
            for (int b = 0; b < branches.Count; b++)
            {
                if (maxBuild > 0 && placed >= maxBuild) break;

                int before = coveragePlacements.Count;
                TryPlaceExtraTower(knownTowers, coverage, branches, b, type, rule,
                    failedPositions, assignedIndices, assignments,
                    pumpPosition, playerPosition, coveragePlacements);
                if (coveragePlacements.Count - before > 0)
                    placed++;
            }
        }
    }

    private static void TryPlaceExtraTower(
        IReadOnlyList<BlightCachedTower> knownTowers,
        LaneCoverageResult[] coverage,
        List<PumpBranch> branches,
        int branchIdx,
        BlightTowerType type,
        TowerBuildRule rule,
        HashSet<NumVector2> failedPositions,
        HashSet<int> assignedIndices,
        Dictionary<NumVector2, (BlightTowerType Type, int MaxLevel)> assignments,
        NumVector2? pumpPosition,
        NumVector2? playerPosition,
        List<CoveragePlacement> coveragePlacements)
    {
        PumpBranch branch = branches[branchIdx];
        float maxRadiusSq = Sq(BlightService.GetCoverageRadiusForLevel(type, rule.MaxUpgradeLevel));

        (int idx, float distSq) = FindBestFoundationForSegment(
            knownTowers, branch.Anchor, maxRadiusSq,
            failedPositions, assignedIndices, pumpPosition, rule.Placement);

        if (idx < 0 && branch.CoverageSegment >= 0)
        {
            foreach (int sb in BranchSegments(coverage, branch))
            {
                if (sb == branch.CoverageSegment)
                    continue;
                (idx, distSq) = FindBestFoundationForSegment(
                    knownTowers, coverage[sb].Midpoint, maxRadiusSq,
                    failedPositions, assignedIndices, pumpPosition, rule.Placement);
                if (idx >= 0)
                    break;
            }
        }

        if (idx < 0)
            return;

        BlightCachedTower tower = knownTowers[idx];
        assignments[tower.WorldPosition] = (type, BlightTowerData.MaxUpgradeLevel);
        assignedIndices.Add(idx);
        AddCoveragePlacement(knownTowers, branchIdx, type, idx, distSq, playerPosition, coveragePlacements);
    }

    private static void AddCoveragePlacement(
        IReadOnlyList<BlightCachedTower> knownTowers,
        int branchIdx,
        BlightTowerType type,
        int towerIdx,
        float requiredRadiusSq,
        NumVector2? playerPosition,
        List<CoveragePlacement> coveragePlacements)
    {
        float distToPlayerSq = playerPosition.HasValue
            ? (knownTowers[towerIdx].WorldPosition - playerPosition.Value).LengthSquared()
            : 0f;
        coveragePlacements.Add(new CoveragePlacement(branchIdx, type, towerIdx, distToPlayerSq, requiredRadiusSq));
    }

    private static (int Index, float DistSq) FindBestFoundationForSegment(
        IReadOnlyList<BlightCachedTower> knownTowers,
        NumVector2 target,
        float radiusSq,
        HashSet<NumVector2> failedPositions,
        HashSet<int> assignedIndices,
        NumVector2? pumpPosition,
        BlightPlacementPreference placement = BlightPlacementPreference.Default)
    {
        int bestIdx = -1;
        float bestMetric = float.MaxValue;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < knownTowers.Count; i++)
        {
            if (assignedIndices.Contains(i)) continue;
            if (failedPositions.Contains(knownTowers[i].WorldPosition)) continue;

            float distSq = SqDist(knownTowers[i].WorldPosition, target);
            if (distSq > radiusSq)
                continue;

            float metric = PlacementMetric(knownTowers, assignedIndices, i, distSq, placement, pumpPosition);

            if (metric < bestMetric)
            {
                bestMetric = metric;
                bestDistSq = distSq;
                bestIdx = i;
            }
        }

        return (bestIdx, bestDistSq);
    }

    // The farthest branch-subtree segment midpoint a built tower's max radius reaches.  Drives the
    // upgrade level of a built coverage tower so it covers the segments it serves (segments, not the
    // branch anchor — a fork-arm tower serves its arm even when it cannot reach the anchor).
    private static float MaxCoveredBranchSegmentDistSq(
        BlightCachedTower tower,
        BlightTowerType type,
        LaneCoverageResult[] coverage,
        List<PumpBranch> branches,
        float maxRadiusSq)
    {
        float best = 0f;
        for (int b = 0; b < branches.Count; b++)
        {
            if (branches[b].CoverageSegment < 0)
                continue;
            foreach (int s in BranchSegments(coverage, branches[b]))
            {
                float d = SqDist(tower.WorldPosition, coverage[s].Midpoint);
                if (d <= maxRadiusSq && d > best)
                    best = d;
            }
        }
        return best;
    }
}
