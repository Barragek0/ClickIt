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

        bool[] branchHasChilling = new bool[branchCount];
        bool[] branchHasSeismic = new bool[branchCount];
        int chillingTarget = BlightFillPlanner.FindRule(rules, BlightTowerType.Chilling)?.MaxUpgradeLevel ?? BlightTowerData.MaxUpgradeLevel;
        int seismicTarget = BlightFillPlanner.FindRule(rules, BlightTowerType.Seismic)?.MaxUpgradeLevel ?? BlightTowerData.MaxUpgradeLevel;
        for (int b = 0; b < branchCount; b++)
        {
            branchHasChilling[b] = BranchHasCoverage(pumpBranches[b], coverage, seismic: false, knownTowers, chillingTarget);
            branchHasSeismic[b] = BranchHasCoverage(pumpBranches[b], coverage, seismic: true, knownTowers, seismicTarget);
        }

        bool[] plannedChilling = new bool[branchCount];
        bool[] plannedSeismic = new bool[branchCount];
        Array.Copy(branchHasChilling, plannedChilling, branchCount);
        Array.Copy(branchHasSeismic, plannedSeismic, branchCount);

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
                            pumpBranches, Sq(BlightService.GetRadiusForLevel(knownTowers[i].TowerType, covRule.MaxUpgradeLevel)))
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

        // A zero branch count does NOT mean coverage is complete — fill only activates after all branches have both types.
        bool hasCoverageRules = rules.Any(r => r.IsCoverageTower);
        bool coverageComplete = !hasCoverageRules;
        int nowCoveredC = 0, nowCoveredS = 0;

        foreach (IGrouping<TowerBuildPriority, TowerBuildRule> group in priorityGroups)
        {
            List<TowerBuildRule> tierRules = [.. group];
            if (!tierRules.Any(r => r.IsCoverageTower))
                continue;

            AssignCoverage(
                tierRules, knownTowers, coverage, pumpBranches,
                plannedChilling, plannedSeismic,
                failedPositions, assignedIndices, assignments,
                pumpPosition, playerPosition, coveragePlacements);

            // Coverage is complete ONLY when every branch has BOTH coverage types — the fill tier must
            // never run while any branch still lacks a coverage type.
            if (branchCount > 0 && AllBranchesCovered(plannedChilling, plannedSeismic))
                coverageComplete = true;
        }

        // Best-effort escape (spec §4.7): a branch whose subtree has no reachable foundation can never
        // be covered, so mark it skipped instead of blocking the fill tier forever.  Only branches with
        // a live coverage segment can be assessed this way.
        if (hasCoverageRules && branchCount > 0 && !coverageComplete)
        {
            bool[] skippedChilling = new bool[branchCount];
            bool[] skippedSeismic = new bool[branchCount];
            for (int b = 0; b < branchCount; b++)
            {
                if (pumpBranches[b].CoverageSegment < 0)
                    continue;
                if (!plannedChilling[b])
                    skippedChilling[b] = !SubtreeHasReachableFoundation(
                        coverage, pumpBranches[b],
                        CoverageMaxRadiusSq(rules, BlightTowerType.Chilling),
                        knownTowers, failedPositions, assignedIndices);
                if (!plannedSeismic[b])
                    skippedSeismic[b] = !SubtreeHasReachableFoundation(
                        coverage, pumpBranches[b],
                        CoverageMaxRadiusSq(rules, BlightTowerType.Seismic),
                        knownTowers, failedPositions, assignedIndices);
            }

            coverageComplete = true;
            for (int b = 0; b < branchCount; b++)
            {
                if (!(plannedChilling[b] || skippedChilling[b]) || !(plannedSeismic[b] || skippedSeismic[b]))
                {
                    coverageComplete = false;
                    break;
                }
            }
        }

        for (int b = 0; b < branchCount; b++)
        {
            if (plannedChilling[b]) nowCoveredC++;
            if (plannedSeismic[b]) nowCoveredS++;
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

        int cBefore = 0, cAfter = 0;
        for (int b = 0; b < branchCount; b++)
        {
            if (plannedChilling[b]) cAfter++;
            if (branchHasChilling[b]) cBefore++;
        }

        System.Text.StringBuilder branchDbg = new();
        for (int b = 0; b < branchCount; b++)
        {
            if (b > 0) branchDbg.Append(' ');
            branchDbg.Append((char)('A' + b));
            branchDbg.Append('(');
            branchDbg.Append(branchHasChilling[b] ? 'C' : 'c');
            branchDbg.Append(branchHasSeismic[b] ? 'S' : 's');
            branchDbg.Append(')');
        }

        System.Text.StringBuilder anchorDbg = new();
        for (int b = 0; b < branchCount; b++)
        {
            if (b > 0) anchorDbg.Append(' ');
            anchorDbg.Append('(');
            anchorDbg.Append(pumpBranches[b].Anchor.X.ToString("F0"));
            anchorDbg.Append(',');
            anchorDbg.Append(pumpBranches[b].Anchor.Y.ToString("F0"));
            anchorDbg.Append(')');
        }

        System.Text.StringBuilder branchSegDbg = new();
        for (int b = 0; b < branchCount; b++)
        {
            if (b > 0) branchSegDbg.Append(' ');
            PumpBranch pb = pumpBranches[b];
            branchSegDbg.Append((char)('A' + b));
            branchSegDbg.Append("(seg=");
            branchSegDbg.Append(pb.CoverageSegment);
            if (pb.CoverageSegment >= 0)
            {
                branchSegDbg.Append(" mid=(");
                branchSegDbg.Append(coverage[pb.CoverageSegment].Midpoint.X.ToString("F0"));
                branchSegDbg.Append(',');
                branchSegDbg.Append(coverage[pb.CoverageSegment].Midpoint.Y.ToString("F0"));
                branchSegDbg.Append(") c=");
                branchSegDbg.Append(coverage[pb.CoverageSegment].HasChilling ? '1' : '0');
                branchSegDbg.Append(" s=");
                branchSegDbg.Append(coverage[pb.CoverageSegment].HasSeismic ? '1' : '0');
            }
            branchSegDbg.Append(')');
        }

        System.Text.StringBuilder towerRadiusDbg = new();
        for (int i = 0; i < knownTowers.Count; i++)
        {
            BlightCachedTower t = knownTowers[i];
            if (t.UpgradeLevel <= 0) continue;
            if (towerRadiusDbg.Length > 0) towerRadiusDbg.Append(' ');
            int actual = t.Radius;
            int estimate = BlightService.GetRadiusForLevel(t.TowerType, t.UpgradeLevel);
            towerRadiusDbg.Append(t.TowerType.ToString()[..3]);
            towerRadiusDbg.Append('@');
            towerRadiusDbg.Append(t.UpgradeLevel);
            towerRadiusDbg.Append(" r=");
            towerRadiusDbg.Append(actual > 0 ? actual.ToString() : "?");
            towerRadiusDbg.Append("(est");
            towerRadiusDbg.Append(estimate);
            towerRadiusDbg.Append(')');
        }

        List<BlightPlanStep> steps = BlightFillPlanner.BuildOrderedSteps(
            knownTowers, assignments, rules, coveragePlacements, orderedFillPositions);

        if (groupStepsByProximity)
            steps = ReorderStepsByProximity(steps, rules);

        System.Text.StringBuilder assignDbg = new();
        for (int i = 0; i < knownTowers.Count; i++)
        {
            if (i > 0) assignDbg.Append(' ');
            BlightCachedTower t = knownTowers[i];
            bool isAssigned = assignments.ContainsKey(t.WorldPosition);
            char marker = isAssigned ? '+' : '-';
            string typeStr = isAssigned ? assignments[t.WorldPosition].Type.ToString()[..3] : "---";
            assignDbg.Append($"{marker}{t.UpgradeLevel}{typeStr}");
        }

        System.Text.StringBuilder foundDbg = new();
        for (int i = 0; i < knownTowers.Count; i++)
        {
            if (i > 0) foundDbg.Append(' ');
            foundDbg.Append('(');
            foundDbg.Append(knownTowers[i].WorldPosition.X.ToString("F0"));
            foundDbg.Append(',');
            foundDbg.Append(knownTowers[i].WorldPosition.Y.ToString("F0"));
            foundDbg.Append(')');
        }

        string summary = $"v{version} ({steps.Count} steps, {branchCount} branches, {(coverageComplete ? "full" : "partial")} coverage) [{branchDbg}]"
            + $" anchors={anchorDbg}"
            + $" pump={(pumpPosition.HasValue ? $"({pumpPosition.Value.X:F0},{pumpPosition.Value.Y:F0})" : "none")}"
            + $" preB={preAssignedBuilt}"
            + $" C={cBefore}->{cAfter}/{branchCount} S={nowCoveredS}/{branchCount}"
            + $" |{assignDbg}";
        string details = $"[{branchDbg}]"
            + $" anchors={anchorDbg}"
            + $" branchSeg={branchSegDbg}"
            + $" towers={towerRadiusDbg}"
            + $" pump={(pumpPosition.HasValue ? $"({pumpPosition.Value.X:F0},{pumpPosition.Value.Y:F0})" : "none")}"
            + $" found={foundDbg}"
            + $" preBuilt={preAssignedBuilt}"
            + $" tierCcov={nowCoveredC}/{branchCount} tierScov={nowCoveredS}/{branchCount}"
            + $" assigned={assignments.Count}"
            + $" | {assignDbg}";

        return new BlightPlan(steps, version, summary, details);
    }

    internal static List<BlightPlanStep> ReorderStepsByProximity(
        List<BlightPlanStep> steps,
        IReadOnlyList<TowerBuildRule> rules)
    {
        if (steps.Count <= 1)
            return steps;

        Dictionary<BlightTowerType, TowerBuildRule> ruleByType = [];
        for (int r = 0; r < rules.Count; r++)
            ruleByType[rules[r].TowerType] = rules[r];

        List<BlightPlanStep> coverageSteps = [];
        List<BlightPlanStep> fillSteps = [];
        for (int i = 0; i < steps.Count; i++)
        {
            BlightPlanStep step = steps[i];
            if (ruleByType.TryGetValue(step.TowerType, out TowerBuildRule rule) && rule.IsCoverageTower)
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

    private static bool AllBranchesCovered(bool[] chilling, bool[] seismic)
    {
        for (int b = 0; b < chilling.Length; b++)
            if (!chilling[b] || !seismic[b])
                return false;
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
        foreach (int s in BranchSegments(coverage, branch))
            pending.Push(s);
        while (pending.Count > 0)
        {
            int s = pending.Pop();
            for (int i = 0; i < knownTowers.Count; i++)
            {
                if (assignedIndices.Contains(i)) continue;
                if (failedPositions.Contains(knownTowers[i].WorldPosition)) continue;
                if (SqDist(knownTowers[i].WorldPosition, coverage[s].Midpoint) <= maxRadiusSq)
                    return true;
            }
            foreach (int child in FindSubBranches(coverage, s))
                pending.Push(child);
        }
        return false;
    }

    private static float CoverageMaxRadiusSq(IReadOnlyList<TowerBuildRule> rules, BlightTowerType type)
    {
        TowerBuildRule? rule = BlightFillPlanner.FindRule(rules, type);
        int maxLevel = rule?.MaxUpgradeLevel ?? BlightTowerData.MaxUpgradeLevel;
        return Sq(BlightService.GetRadiusForLevel(type, maxLevel));
    }

    private static void AssignCoverage(
        List<TowerBuildRule> tierRules,
        IReadOnlyList<BlightCachedTower> knownTowers,
        LaneCoverageResult[] coverage,
        List<PumpBranch> branches,
        bool[] plannedChilling,
        bool[] plannedSeismic,
        HashSet<NumVector2> failedPositions,
        HashSet<int> assignedIndices,
        Dictionary<NumVector2, (BlightTowerType Type, int MaxLevel)> assignments,
        NumVector2? pumpPosition,
        NumVector2? playerPosition,
        List<CoveragePlacement> coveragePlacements)
    {
        TowerBuildRule? chillingRule = BlightFillPlanner.FindRule(tierRules, BlightTowerType.Chilling);
        TowerBuildRule? seismicRule = BlightFillPlanner.FindRule(tierRules, BlightTowerType.Seismic);
        if (chillingRule == null && seismicRule == null)
            return;

        if (chillingRule != null)
            AssignCoverageType(chillingRule.Value, BlightTowerType.Chilling,
                knownTowers, coverage, branches, plannedChilling,
                failedPositions, assignedIndices, assignments,
                pumpPosition, playerPosition, coveragePlacements);

        if (seismicRule != null)
            AssignCoverageType(seismicRule.Value, BlightTowerType.Seismic,
                knownTowers, coverage, branches, plannedSeismic,
                failedPositions, assignedIndices, assignments,
                pumpPosition, playerPosition, coveragePlacements);
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
        float maxRadiusSq = Sq(BlightService.GetRadiusForLevel(type, rule.MaxUpgradeLevel));
        bool seismic = type == BlightTowerType.Seismic;

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
        bool[] covered = ComputePlannedCoveredState(coverage, seismic, knownTowers, rule.MaxUpgradeLevel);

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

                float metric = PlacementMetric(knownTowers, assignedIndices, i, maxDistSq, rule.Placement, pumpPosition, playerPosition);
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
            if (SubtreeFullyCovered(branches[b], coverage, covered, knownTowers, seismic, rule.MaxUpgradeLevel))
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
        float maxRadiusSq = Sq(BlightService.GetRadiusForLevel(type, rule.MaxUpgradeLevel));

        (int idx, float distSq) = FindBestFoundationForSegment(
            knownTowers, branch.Anchor, maxRadiusSq,
            failedPositions, assignedIndices, pumpPosition, playerPosition, rule.Placement);

        if (idx < 0 && branch.CoverageSegment >= 0)
        {
            foreach (int sb in BranchSegments(coverage, branch))
            {
                if (sb == branch.CoverageSegment)
                    continue;
                (idx, distSq) = FindBestFoundationForSegment(
                    knownTowers, coverage[sb].Midpoint, maxRadiusSq,
                    failedPositions, assignedIndices, pumpPosition, playerPosition, rule.Placement);
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

    private static float PlacementMetric(
        IReadOnlyList<BlightCachedTower> knownTowers,
        HashSet<int> assignedIndices,
        int candidateIdx,
        float fallbackMetric,
        BlightPlacementPreference placement,
        NumVector2? pumpPosition,
        NumVector2? playerPosition)
    {
        NumVector2 p = knownTowers[candidateIdx].WorldPosition;
        return placement switch
        {
            BlightPlacementPreference.NearestPump when pumpPosition.HasValue
                => (p - pumpPosition.Value).LengthSquared(),
            BlightPlacementPreference.NearestPlayer when playerPosition.HasValue
                => (p - playerPosition.Value).LengthSquared(),
            BlightPlacementPreference.NearExistingTowers
                => BlightFillPlanner.DistanceToNearestAssignedTowerSq(knownTowers, assignedIndices, candidateIdx),
            _ => fallbackMetric,
        };
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
        NumVector2? playerPosition,
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

            float metric = placement switch
            {
                BlightPlacementPreference.NearestPump when pumpPosition.HasValue
                    => (knownTowers[i].WorldPosition - pumpPosition.Value).LengthSquared(),
                BlightPlacementPreference.NearestPlayer when playerPosition.HasValue
                    => (knownTowers[i].WorldPosition - playerPosition.Value).LengthSquared(),
                BlightPlacementPreference.NearExistingTowers
                    => BlightFillPlanner.DistanceToNearestAssignedTowerSq(knownTowers, assignedIndices, i),
                _ => distSq,
            };

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
