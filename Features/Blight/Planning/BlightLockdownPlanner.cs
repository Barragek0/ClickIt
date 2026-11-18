namespace ClickIt.Features.Blight.Planning;

[Flags]
internal enum BlightControl
{
    None = 0,
    Chilling = 1,
    Seismic = 2,
    Both = Chilling | Seismic
}

internal static class BlightLockdownPlanner
{
    private const float PortalMatchDistance = 4f;

    internal sealed record Lane(IReadOnlyList<NumVector2> Segments, bool IsCold, bool IsPhysical = false);

    internal static IReadOnlyList<Lane> FindLanes(
        IReadOnlyList<BlightPathwayIcon> icons, IReadOnlyDictionary<NumVector2, string> portals,
        IReadOnlyList<LaneCoverageResult> coverage, out HashSet<NumVector2> damageSegments,
        NumVector2 layoutOffset = default)
    {
        bool hasAlignedCoverage = coverage.Count == icons.Count;
        bool[] hasChild = FindNonTerminalBeams(icons, coverage);
        NumVector2?[] midpoints = new NumVector2?[icons.Count];
        damageSegments = [];
        List<Lane> lanes = [];
        for (int i = 0; i < icons.Count; i++)
        {
            BlightPathwayIcon icon = icons[i];
            if (icon.BeamStart == default || icon.BeamEnd == default)
                continue;
            NumVector2 midpoint = BlightHelpers.WorldToGrid((icon.BeamStart + icon.BeamEnd) / 2f) + layoutOffset;
            midpoints[i] = midpoint;
            // Include secondary incoming beams even when the control route takes another parent.
            damageSegments.Add(midpoint);
        }

        var portalLanes = MatchPortalEnds(icons, portals);

        for (int i = 0; i < icons.Count; i++)
        {
            if (hasChild[i] && !portalLanes.ContainsKey(i))
                continue;
            List<NumVector2> segments = [];
            // Raw beam direction identifies portal ends. The coverage tree reconnects
            // detached fragments to their shared pump route, so use it to trace coverage.
            for (int current = i, remaining = icons.Count; current >= 0 && remaining > 0; remaining--)
            {
                BlightPathwayIcon icon = icons[current];
                if (midpoints[current] is { } midpoint)
                    segments.Add(midpoint);
                current = hasAlignedCoverage
                    ? coverage[current].ParentIndex
                    : icon.Parents.Length > 0 ? icon.Parents[0] : -1;
            }
            if (segments.Count > 0)
            {
                var immunity = portalLanes.GetValueOrDefault(i);
                lanes.Add(new Lane(segments, immunity.Cold, immunity.Physical));
            }
        }
        return lanes;
    }

    internal static void MarkControlRoutes(
        IReadOnlyList<BlightPathwayIcon> icons, IReadOnlyDictionary<NumVector2, string> portals,
        LaneCoverageResult[] coverage)
    {
        if (icons.Count != coverage.Length || portals.Count == 0)
            return;

        bool[] hasChild = FindNonTerminalBeams(icons, coverage);

        var matches = MatchPortalEnds(icons, portals);
        for (int end = 0; end < icons.Count; end++)
        {
            if (!matches.TryGetValue(end, out var immunity) && hasChild[end])
                continue;
            BlightControl control = immunity.Cold == immunity.Physical ? BlightControl.Both
                : immunity.Cold ? BlightControl.Seismic : BlightControl.Chilling;
            for (int segment = end; (uint)segment < (uint)coverage.Length; segment = coverage[segment].ParentIndex)
            {
                BlightControl previous = coverage[segment].ControlRequired;
                if ((previous & control) == control)
                    break;
                coverage[segment] = coverage[segment] with { ControlRequired = previous | control };
            }
        }
    }

    private static bool[] FindNonTerminalBeams(IReadOnlyList<BlightPathwayIcon> icons, IReadOnlyList<LaneCoverageResult> coverage)
    {
        bool[] hasChild = new bool[icons.Count];
        foreach (BlightPathwayIcon icon in icons)
            foreach (int parent in icon.Parents)
                hasChild[parent] = true;
        if (coverage.Count == icons.Count)
            foreach (LaneCoverageResult segment in coverage)
                if (segment.ParentIndex >= 0)
                    hasChild[segment.ParentIndex] = true;
        return hasChild;
    }

    private static Dictionary<int, (bool Cold, bool Physical)> MatchPortalEnds(
        IReadOnlyList<BlightPathwayIcon> icons, IReadOnlyDictionary<NumVector2, string> portals)
    {
        // Allow the portal icon's visual offset without jumping to a nearby lane.
        Dictionary<int, (bool Cold, bool Physical)> portalLanes = [];
        foreach ((NumVector2 portal, string name) in portals)
        {
            int best = -1;
            float nearest = PortalMatchDistance * PortalMatchDistance;
            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i].BeamEnd == default)
                    continue;
                float distance = NumVector2.DistanceSquared(portal, BlightHelpers.WorldToGrid(icons[i].BeamEnd));
                if (distance >= nearest)
                    continue;
                nearest = distance;
                best = i;
            }
            if (best >= 0)
            {
                var immunity = portalLanes.GetValueOrDefault(best);
                portalLanes[best] = (immunity.Cold || name == "BlightPortalCold",
                    immunity.Physical || name == "BlightPortalPhysical");
            }
        }
        return portalLanes;
    }

    internal static BlightPlan Build(
        IReadOnlyList<BlightCachedTower> towers,
        IReadOnlyList<Lane> lanes,
        TowerBuildRule damage,
        int version,
        NumVector2 player,
        NumVector2? pump = null,
        IReadOnlySet<NumVector2>? damageSegments = null,
        bool freeConstruction = false)
    {
        if (lanes.Count == 0)
            return BlightPlan.Completed(version, "Waiting for lockdown lane geometry");

        List<BlightPlanStep> steps = [];
        HashSet<BlightCachedTower> reserved = [];
        BlightCachedTower[] ordered = [.. towers
            .OrderBy(tower => NumVector2.DistanceSquared(tower.WorldPosition, player))
            .ThenBy(tower => tower.WorldPosition.X)
            .ThenBy(tower => tower.WorldPosition.Y)];
        Lane[] needsChilling = [.. lanes.Where(lane => !lane.IsCold || lane.IsPhysical)];
        Lane[] needsSeismic = [.. lanes.Where(lane => !lane.IsPhysical || lane.IsCold)];
        Lane[] coldLanes = [.. lanes.Where(lane => lane.IsCold)];
        Lane[] physicalLanes = [.. lanes.Where(lane => lane.IsPhysical)];
        int seismicRadius = BlightService.GetCoverageRadiusForLevel(BlightTowerType.Seismic, 1);
        float seismicRadiusSq = seismicRadius * seismicRadius;
        HashSet<BlightCachedTower> uniqueSeismicSites = [];
        foreach (Lane lane in needsSeismic)
        {
            if (ordered.Any(tower => tower.UpgradeLevel > 0 && tower.TowerType == BlightTowerType.Seismic && Covers(tower, lane, seismicRadiusSq)))
                continue;
            BlightCachedTower[] sites = [.. ordered.Where(tower => tower.UpgradeLevel == 0
                && Covers(tower, lane, seismicRadiusSq)).Take(2)];
            if (sites.Length == 1)
                uniqueSeismicSites.Add(sites[0]);
        }
        var chilling = Assign(BlightTowerType.Chilling, needsChilling, defer: uniqueSeismicSites);
        var seismic = Assign(BlightTowerType.Seismic, needsSeismic);
        Lane[] supportTargets = [.. chilling.Concat(seismic).Select(tower => new Lane([tower.WorldPosition], false))];
        var empowering = Assign(BlightTowerType.Empowering, supportTargets);

        // Give every route its initial control towers before rushing the towers on
        // immune routes. A shared support site can serve both kinds of control.
        BuildTierOne(chilling, BlightTowerType.Chilling);
        BuildTierOne(seismic, BlightTowerType.Seismic);

        int chillingRadius = BlightService.GetCoverageRadiusForLevel(BlightTowerType.Chilling, 1);
        var urgentChilling = chilling.Where(tower => physicalLanes.Any(lane => Covers(tower, lane, chillingRadius * chillingRadius))).ToHashSet();
        var urgentSeismic = seismic.Where(tower => coldLanes.Any(lane => Covers(tower, lane, seismicRadiusSq))).ToHashSet();
        var urgentControl = urgentChilling.Concat(urgentSeismic).ToArray();
        var urgentEmpowering = empowering.Where(tower =>
        {
            int radius = BlightService.GetCoverageRadiusForLevel(BlightTowerType.Empowering, Math.Clamp(tower.UpgradeLevel, 1, 3));
            return urgentControl.Any(control => NumVector2.DistanceSquared(tower.WorldPosition, control.WorldPosition) <= radius * radius);
        }).ToHashSet();

        BuildTierOne(empowering.Where(urgentEmpowering.Contains), BlightTowerType.Empowering);
        Upgrade(chilling.Where(urgentChilling.Contains), BlightTowerType.Chilling, 3);
        Upgrade(seismic.Where(urgentSeismic.Contains), BlightTowerType.Seismic, 3);
        Upgrade(empowering.Where(urgentEmpowering.Contains), BlightTowerType.Empowering, 3);
        Upgrade(chilling.Where(tower => !urgentChilling.Contains(tower)), BlightTowerType.Chilling, 3);
        Upgrade(seismic.Where(tower => !urgentSeismic.Contains(tower)), BlightTowerType.Seismic, 3);
        BuildTierOne(empowering.Where(tower => !urgentEmpowering.Contains(tower)), BlightTowerType.Empowering);

        List<BlightPlanStep> damageSteps = [];
        (int coveredSegments, int totalSegments) = AssignDamage(damageSteps);
        int nextDamageStep = 0;
        if (urgentControl.Length > 0 && damageSteps.Count > 0
            && !ordered.Any(tower => tower.TowerType == damage.TowerType && tower.UpgradeLevel >= damage.MaxUpgradeLevel))
        {
            // Get damage going early while every route already has its planned control
            // and support placements. Keep the damage towers in pump-distance order.
            NumVector2 firstDamageTower = damageSteps[0].FoundationPosition;
            while (nextDamageStep < damageSteps.Count && damageSteps[nextDamageStep].FoundationPosition == firstDamageTower)
                steps.Add(damageSteps[nextDamageStep++]);
        }
        Upgrade(empowering.Where(tower => !urgentEmpowering.Contains(tower)), BlightTowerType.Empowering, 3);
        for (; nextDamageStep < damageSteps.Count; nextDamageStep++)
            steps.Add(damageSteps[nextDamageStep]);

        if (freeConstruction)
            steps = RushControlTowers(steps, damage.TowerType);

        string mode = freeConstruction ? "lockdown (free construction)" : "lockdown";
        return new BlightPlan(steps, version,
            $"v{version} {mode}: {lanes.Count} lanes, {coldLanes.Length} cold, {physicalLanes.Length} physical, {steps.Count} steps, damage segments={coveredSegments}/{totalSegments}");

        IReadOnlyList<BlightCachedTower> Assign(
            BlightTowerType type, IReadOnlyList<Lane> targets,
            IReadOnlySet<BlightCachedTower>? defer = null)
        {
            int radius = BlightService.GetCoverageRadiusForLevel(type, 1);
            float radiusSq = radius * radius;
            List<BlightCachedTower> selected = [];
            List<Lane> uncovered = [.. targets];
            foreach (BlightCachedTower tower in ordered)
            {
                if (tower.UpgradeLevel == 0 || tower.TowerType != type)
                    continue;
                int builtRadius = BlightService.GetCoverageRadiusForLevel(type, Math.Min(tower.UpgradeLevel, 3));
                if (uncovered.RemoveAll(lane => Covers(tower, lane, builtRadius * builtRadius)) > 0)
                    selected.Add(tower);
            }
            while (uncovered.Count > 0)
            {
                BlightCachedTower? best = null;
                int bestCount = 0;
                bool bestDeferred = true;
                foreach (BlightCachedTower tower in ordered)
                {
                    if (tower.UpgradeLevel != 0 || reserved.Contains(tower))
                        continue;
                    int count = uncovered.Count(lane => Covers(tower, lane, radiusSq));
                    bool deferred = defer?.Contains(tower) == true;
                    if (count == 0 || (best != null && (deferred == bestDeferred ? count <= bestCount : deferred)))
                        continue;
                    best = tower;
                    bestCount = count;
                    bestDeferred = deferred;
                }
                if (best == null)
                    break;
                reserved.Add(best);
                selected.Add(best);
                uncovered.RemoveAll(lane => Covers(best, lane, radiusSq));
            }
            return selected;
        }

        (int Covered, int Total) AssignDamage(List<BlightPlanStep> plannedDamage)
        {
            HashSet<NumVector2> uncovered = damageSegments != null
                ? [.. damageSegments]
                : [.. lanes.SelectMany(lane => lane.Segments)];
            int total = uncovered.Count;
            int finalRadius = damage.Specialization >= 0
                ? BlightService.GetCoverageRadius(BlightTowerData.FindRadius(
                    BlightTowerData.GetSpecializationTowerId(damage.TowerType, (TowerSpecialization)damage.Specialization)))
                : BlightService.GetCoverageRadiusForLevel(damage.TowerType, damage.MaxUpgradeLevel);
            NumVector2 origin = pump ?? player;
            BlightCachedTower[] byPumpDistance = [.. ordered
                .OrderBy(tower => NumVector2.DistanceSquared(tower.WorldPosition, origin))
                .ThenBy(tower => tower.WorldPosition.X)
                .ThenBy(tower => tower.WorldPosition.Y)];

            // Use existing damage towers first. Account for queued upgrades at their
            // final range so a foundation is only taken for a remaining coverage gap.
            foreach (BlightCachedTower tower in byPumpDistance)
            {
                if (tower.TowerType == damage.TowerType && tower.UpgradeLevel >= damage.MaxUpgradeLevel)
                    CoverSegments(tower, tower.Radius > 0 ? BlightService.GetCoverageRadius(tower.Radius) : finalRadius);
            }

            HashSet<BlightCachedTower> pendingUpgrades = [];
            foreach (BlightCachedTower tower in byPumpDistance)
            {
                if (tower.TowerType == damage.TowerType && tower.UpgradeLevel > 0
                    && tower.UpgradeLevel < damage.MaxUpgradeLevel && CoverSegments(tower, finalRadius))
                    pendingUpgrades.Add(tower);
            }

            foreach (BlightCachedTower tower in byPumpDistance)
            {
                if (pendingUpgrades.Contains(tower))
                {
                    Upgrade([tower], damage.TowerType, damage.MaxUpgradeLevel, plannedDamage);
                }
                else if (uncovered.Count != 0 && tower.UpgradeLevel == 0
                    && !reserved.Contains(tower) && CoverSegments(tower, finalRadius))
                {
                    plannedDamage.Add(new BlightPlanStep(BlightPlanAction.Build, tower.WorldPosition, damage.TowerType, 1));
                    Upgrade([tower], damage.TowerType, damage.MaxUpgradeLevel, plannedDamage);
                }
            }
            return (total - uncovered.Count, total);

            bool CoverSegments(BlightCachedTower tower, int radius)
                => uncovered.RemoveWhere(segment =>
                    NumVector2.DistanceSquared(tower.WorldPosition, segment) <= radius * radius) != 0;
        }

        void BuildTierOne(IEnumerable<BlightCachedTower> selected, BlightTowerType type)
        {
            foreach (BlightCachedTower tower in selected)
                if (tower.UpgradeLevel == 0)
                    steps.Add(new BlightPlanStep(BlightPlanAction.Build, tower.WorldPosition, type, 1));
        }

        void Upgrade(IEnumerable<BlightCachedTower> selected, BlightTowerType type, int maxLevel, List<BlightPlanStep>? destination = null)
        {
            foreach (BlightCachedTower tower in selected)
                for (int level = Math.Max(2, tower.UpgradeLevel + 1); level <= maxLevel; level++)
                    (destination ?? steps).Add(new BlightPlanStep(BlightPlanAction.Upgrade, tower.WorldPosition, type, level));
        }
    }

    private static List<BlightPlanStep> RushControlTowers(List<BlightPlanStep> steps, BlightTowerType damageType)
    {
        var towerSteps = steps.Where(step => step.TowerType != damageType)
            .ToLookup(step => step.FoundationPosition);
        HashSet<NumVector2> emitted = [];
        List<BlightPlanStep> ordered = new(steps.Count);

        // A cache refresh can replan between ranks. Finish selected towers
        // already under construction before placing new foundations.
        foreach (var group in towerSteps)
        {
            if (group.First().Action == BlightPlanAction.Upgrade)
            {
                emitted.Add(group.Key);
                ordered.AddRange(group);
            }
        }

        foreach (BlightPlanStep step in steps)
        {
            if (step.TowerType == damageType)
                ordered.Add(step);
            else if (emitted.Add(step.FoundationPosition))
                ordered.AddRange(towerSteps[step.FoundationPosition]);
        }

        return ordered;
    }

    private static bool Covers(BlightCachedTower tower, Lane lane, float radiusSq)
        => lane.Segments.Any(segment => NumVector2.DistanceSquared(tower.WorldPosition, segment) <= radiusSq);
}
