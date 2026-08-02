namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightPlannerTests
{
    [TestMethod]
    public void CoverageRule_TowersPerBranch2_PlacesTwoTowersOnBranch()
    {
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = Foundations(
            (5, 0),  // F0 — closest to pump, covers segment 1 at base level
            (15, 0), // F1 — redundancy slot
            (25, 0)); // F2 — unused

        TowerBuildRule chilling = TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Chilling)
            .SetPriority(TowerBuildPriority.Critical)
            .SetMaxUpgradeLevel(3)
            .TreatAsCoverageTower()
            .TowersPerBranch(2)
            .Build();

        BlightPlan plan = BuildPlan(lane, foundations, [chilling], pump: new NumVector2(0, 0));

        var chillBuilds = plan.Steps
            .Where(s => s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling)
            .ToList();

        chillBuilds.Should().HaveCount(2, "TowersPerBranch(2) should place two Chilling towers");
        chillBuilds[0].FoundationPosition.X.Should().BeApproximately(5f, 0.01f, "first slot hugs the pump");
        chillBuilds[1].FoundationPosition.X.Should().BeApproximately(15f, 0.01f, "second slot is the redundancy tower");
    }

    [TestMethod]
    public void CoverageRule_MaxBuildCount1_CapsPerBranchSlots()
    {
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = Foundations((5, 0), (15, 0), (25, 0));

        TowerBuildRule chilling = TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Chilling)
            .SetPriority(TowerBuildPriority.Critical)
            .SetMaxUpgradeLevel(3)
            .TreatAsCoverageTower()
            .TowersPerBranch(3)
            .MaxBuildCount(1)
            .Build();

        BlightPlan plan = BuildPlan(lane, foundations, [chilling], pump: new NumVector2(0, 0));

        int chillBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling);
        chillBuilds.Should().Be(1, "MaxBuildCount(1) caps coverage towers");
    }

    [TestMethod]
    public void FillRule_MaxBuildCount2_OnlyAssignsTwoFoundations()
    {
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = Foundations(
            (5, 0),   // Chilling
            (5, 5),   // Seismic
            (20, 0),  // fill candidate 1
            (25, 5),  // fill candidate 2
            (30, 0),  // fill candidate 3
            (35, 5),  // fill candidate 4
            (40, 0)); // fill candidate 5

        var rules = CoverageRules();
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .MaxBuildCount(2)
            .Build());

        BlightPlan plan = BuildPlan(lane, foundations, rules, pump: new NumVector2(0, 0));

        int fireballBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Fireball);
        fireballBuilds.Should().Be(2, "MaxBuildCount(2) caps fill towers even with many foundations");
    }

    [TestMethod]
    public void FillRule_PreferCloseFoundationToPump_PlacesOnPumpFoundation()
    {
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = Foundations(
            (5, 0),   // Chilling
            (5, 5),   // Seismic
            (20, 0),  // nearest to pump
            (25, 5),  // mid
            (35, 5)); // far from pump

        var rules = CoverageRules();
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .PreferCloseFoundationToPump()
            .MaxBuildCount(1)
            .Build());

        BlightPlan plan = BuildPlan(lane, foundations, rules,
            pump: new NumVector2(0, 0), player: new NumVector2(35, 5));

        BlightPlanStep fireballBuild = plan.Steps.Single(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Fireball);
        fireballBuild.FoundationPosition.X.Should().BeApproximately(20f, 0.01f,
            "the fill tower goes on the foundation nearest the pump");
        fireballBuild.FoundationPosition.Y.Should().BeApproximately(0f, 0.01f);
    }

    [TestMethod]
    public void FillRule_PreferCloseFoundationToPump_BuildsNearestPumpFirst()
    {
        // Regression: AssignFill sorted fill candidates by the placement
        // preference, but BuildOrderedSteps re-iterated knownTowers in scan
        // order, so the preference was lost and far-away towers were built
        // before nearby ones.  Fill BUILD steps must follow the
        // placement-preferred order (nearest pump first).
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = new List<BlightCachedTower>
        {
            new(new NumVector2(5, 0), BlightTowerType.Chilling, 3),  // built — covers branch
            new(new NumVector2(5, 5), BlightTowerType.Seismic, 3),   // built — covers branch
            new(new NumVector2(20, 0), BlightTowerType.Chilling),    // nearest to pump
            new(new NumVector2(25, 5), BlightTowerType.Chilling),    // mid distance
            new(new NumVector2(35, 5), BlightTowerType.Chilling),    // far from pump
        };

        var rules = CoverageRules();
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .PreferCloseFoundationToPump()
            .AlwaysUpgradeBeforeBuildingNew()
            .Build());

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            lane, _ => (true, true, false));

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, rules, new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), new NumVector2(36, 5), lane);

        var fireballBuilds = plan.Steps
            .Where(s => s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Fireball)
            .ToList();

        fireballBuilds.Should().HaveCount(3);
        fireballBuilds[0].FoundationPosition.X.Should().BeApproximately(20f, 0.01f,
            "nearest foundation to the pump is built first");
        fireballBuilds[1].FoundationPosition.X.Should().BeApproximately(25f, 0.01f,
            "second-nearest foundation is built next");
        fireballBuilds[2].FoundationPosition.X.Should().BeApproximately(35f, 0.01f,
            "farthest from the pump is built last");
    }

    [TestMethod]
    public void CoverageTower_PreferCloseToPump_DoesNotReduceBranchCoverage()
    {
        var lane = new List<NumVector2>
        {
            new(0, 0), new(20, 0), new(0, 0), new(20, 40),
        };
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)),
            new(0, false, new NumVector2(10, 0), HasChilling: false, HasSeismic: false),  // branch A base
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)),
            new(2, false, new NumVector2(10, 40), HasChilling: false, HasSeismic: false), // branch B base
        ];

        var foundations = new List<BlightCachedTower>
        {
            new(new NumVector2(5, 0), BlightTowerType.Chilling),   // close to pump — covers only branch A
            new(new NumVector2(10, 20), BlightTowerType.Chilling), // farther from pump — covers both branches
        };

        TowerBuildRule chilling = TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Chilling)
            .SetPriority(TowerBuildPriority.Critical)
            .SetMaxUpgradeLevel(3)
            .TreatAsCoverageTower()
            .PreferCloseFoundationToPump()
            .Build();

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, [chilling], new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), null, lane);

        BlightPlanStep build = plan.Steps.Single(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling);
        build.FoundationPosition.X.Should().BeApproximately(10f, 0.01f,
            "the multi-branch foundation wins even though it is farther from the pump");
        build.FoundationPosition.Y.Should().BeApproximately(20f, 0.01f);
    }

    [TestMethod]
    public void FillRule_PlaceNearExistingTowers_ClustersNextToAssignedTower()
    {
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = Foundations(
            (5, 0),   // Chilling (assigned first)
            (5, 5),   // Seismic
            (10, 0),  // adjacent to the Chilling tower
            (30, 0)); // far away

        var rules = CoverageRules();
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .PlaceNearExistingTowers()
            .MaxBuildCount(1)
            .Build());

        BlightPlan plan = BuildPlan(lane, foundations, rules, pump: new NumVector2(0, 0));

        BlightPlanStep fireballBuild = plan.Steps.Single(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Fireball);
        fireballBuild.FoundationPosition.X.Should().BeApproximately(10f, 0.01f,
            "NearExistingTowers should pick the foundation next to the built Chilling tower");
        fireballBuild.FoundationPosition.Y.Should().BeApproximately(0f, 0.01f);
    }

    [TestMethod]
    public void FillRule_PlaceNearUncoveredLane_TargetsUncoveredChain()
    {
        var lane = CreateChain(
            (0, 0), (10, 0), (20, 0), (30, 0),
            (100, 100), (110, 100), (120, 100));

        var foundations = Foundations(
            (5, 0),    // near covered chain 1
            (5, 5),    // near covered chain 1
            (15, 0),   // near covered chain 1
            (25, 0),   // near covered chain 1
            (105, 100)); // on the uncovered chain 2

        var rules = CoverageRules();
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .PlaceNearUncoveredLane()
            .Build());

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            lane,
            midpoint => midpoint.X <= 25f ? (true, true, false) : (false, false, false));

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, rules, new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), null, lane);

        BlightPlanStep fireballBuild = plan.Steps.Single(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Fireball);
        fireballBuild.FoundationPosition.X.Should().BeApproximately(105f, 0.01f,
            "NearestUncoveredLane should place on the uncovered chain");
        fireballBuild.FoundationPosition.Y.Should().BeApproximately(100f, 0.01f);
    }

    [TestMethod]
    public void FillRule_AlwaysUpgradeBeforeBuildingNew_UpgradesExistingTowersFirst()
    {
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = new List<BlightCachedTower>
        {
            new(new NumVector2(5, 0), BlightTowerType.Chilling),            // coverage
            new(new NumVector2(5, 5), BlightTowerType.Seismic),             // coverage
            new(new NumVector2(10, 0), BlightTowerType.Fireball, 1),        // existing lvl 1
            new(new NumVector2(20, 0), BlightTowerType.Fireball, 1),        // existing lvl 1
            new(new NumVector2(30, 0), BlightTowerType.Chilling),           // new foundation
        };

        var rules = CoverageRules();
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .AlwaysUpgradeBeforeBuildingNew()
            .Build());

        BlightPlan plan = BuildPlan(lane, foundations, rules, pump: new NumVector2(0, 0));

        var fireballSteps = plan.Steps
            .Where(s => s.TowerType == BlightTowerType.Fireball)
            .ToList();

        fireballSteps.Take(6).Should().OnlyContain(s => s.Action == BlightPlanAction.Upgrade);

        BlightPlanStep build = fireballSteps.Single(s => s.Action == BlightPlanAction.Build);
        build.FoundationPosition.X.Should().BeApproximately(30f, 0.01f, "new tower is the last built");

        int firstBuildIdx = fireballSteps.FindIndex(s => s.Action == BlightPlanAction.Build);
        fireballSteps.Take(firstBuildIdx).Should().OnlyContain(s => s.Action == BlightPlanAction.Upgrade,
            "every existing tower is maxed before any new tower is built");
        fireballSteps.Skip(firstBuildIdx).Should().HaveCount(4, "build + upgrades 2,3,4 for the new tower");
    }

    [TestMethod]
    public void CoverageRule_DefaultUpgradeToMax_UpgradesCoverageTowerToMax()
    {
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = Foundations((5, 0), (15, 0), (25, 0));

        TowerBuildRule chilling = TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Chilling)
            .SetPriority(TowerBuildPriority.Critical)
            .SetMaxUpgradeLevel(3)
            .TreatAsCoverageTower()
            .Build();

        BlightPlan plan = BuildPlan(lane, foundations, [chilling], pump: new NumVector2(0, 0));

        var chillingUpgrades = plan.Steps
            .Where(s => s.TowerType == BlightTowerType.Chilling && s.Action == BlightPlanAction.Upgrade)
            .Select(s => s.TargetLevel)
            .ToList();

        chillingUpgrades.Should().ContainInOrder(2, 3);
    }

    [TestMethod]
    public void BuiltCoverageTowerDownstream_StillUpgradedToMaxBeforeFill()
    {
        // Regression (Residence 83): a built Seismic that covers the branch
        // from a downstream position (beyond its max range from the branch
        // ANCHOR) was never recorded as a coverage placement, so its upgrade
        // steps were silently dropped and the plan moved straight to Fireball
        // fill.  Coverage is measured against SEGMENTS (downstream reach), so
        // such a tower still contributes coverage and, with
        // UpgradeBeforeMovingOntoLowerPriority, must reach max level BEFORE
        // any fill step is emitted.
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = new List<BlightCachedTower>
        {
            new(new NumVector2(5, 0), BlightTowerType.Chilling, 3),  // built near base — already max
            new(new NumVector2(100, 0), BlightTowerType.Seismic, 1), // built downstream, beyond anchor range
            new(new NumVector2(15, 5), BlightTowerType.Chilling),    // unbuilt fill candidate
            new(new NumVector2(25, 5), BlightTowerType.Chilling),    // unbuilt fill candidate
        };

        var rules = new List<TowerBuildRule>
        {
            TowerStrategyBuilder.CreateRule()
                .SetTower(BlightTowerType.Chilling)
                .SetPriority(TowerBuildPriority.Critical)
                .SetMaxUpgradeLevel(3)
                .TreatAsCoverageTower()
                .Build(),
            TowerStrategyBuilder.CreateRule()
                .SetTower(BlightTowerType.Seismic)
                .SetPriority(TowerBuildPriority.Critical)
                .SetMaxUpgradeLevel(3)
                .TreatAsCoverageTower()
                .UpgradeOnlyWhenNeededForCoverage()
                .UpgradeBeforeMovingOntoLowerPriority()
                .Build(),
            TowerStrategyBuilder.CreateRule()
                .SetTower(BlightTowerType.Fireball)
                .SetPriority(TowerBuildPriority.High)
                .SetMaxUpgradeLevel(4)
                .Build(),
        };

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            lane, _ => (true, true, false));

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, rules, new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), null, lane);

        var seismicUpgrades = plan.Steps
            .Where(s => s.TowerType == BlightTowerType.Seismic && s.Action == BlightPlanAction.Upgrade)
            .Select(s => s.TargetLevel)
            .ToList();
        seismicUpgrades.Should().ContainInOrder(new[] { 2, 3 },
            "the downstream built Seismic must still be upgraded to max");

        int firstFireballBuild = plan.Steps.ToList().FindIndex(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Fireball);
        int lastSeismicUpgrade = plan.Steps.ToList().FindIndex(s =>
            s.TowerType == BlightTowerType.Seismic && s.Action == BlightPlanAction.Upgrade);
        lastSeismicUpgrade.Should().BeLessThan(firstFireballBuild,
            "Seismic upgrades must complete before Fireball building starts");
    }

    [TestMethod]
    public void BuiltCoverageTowerBelowCoverageLevel_NoDuplicateTowerPlaced()
    {
        // Regression (Residence 82 / Desert 82): a coverage tower that is
        // built but not yet upgraded to the level that actually covers its
        // branch used to be treated as "no coverage" on the next rebuild, so
        // the planner placed a SECOND tower for the same branch (observed:
        // two Chilling towers ~34 apart and two Seismic towers ~21 apart,
        // both pairs covering the same branch).  A built coverage tower whose
        // MAX-level range reaches the branch base counts as in-progress
        // coverage — the plan upgrades it instead of duplicating it.
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = new List<BlightCachedTower>
        {
            new(new NumVector2(60, 0), BlightTowerType.Chilling, 1), // built lvl1 — only covers the branch at max radius
            new(new NumVector2(60, 5), BlightTowerType.Seismic, 3),  // built max — seismic covered
            new(new NumVector2(15, 5), BlightTowerType.Chilling),    // unbuilt — would be a duplicate Chilling
            new(new NumVector2(25, 5), BlightTowerType.Chilling),    // unbuilt
        };

        var rules = CoverageRules();
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .Build());

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            lane, _ => (false, false, false));

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, rules, new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), null, lane);

        int chillBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling);
        chillBuilds.Should().Be(0,
            "the in-progress Chilling is upgraded, not duplicated");

        var chillUpgrades = plan.Steps
            .Where(s => s.TowerType == BlightTowerType.Chilling && s.Action == BlightPlanAction.Upgrade)
            .Select(s => s.TargetLevel)
            .ToList();
        chillUpgrades.Should().ContainInOrder(new[] { 2, 3 },
            "the built Chilling is still upgraded to max");
    }

    [TestMethod]
    public void NoCoverageStrategy_SingleFillRule_BuildsOnEveryFoundation()
    {
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = Foundations((5, 0), (15, 0), (25, 0));

        TowerBuildRule fireball = TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.Critical)
            .SetMaxUpgradeLevel(4)
            .SetSpecialization(TowerSpecialization.Meteor)
            .PreferCloseFoundationToPump()
            .Build();

        BlightPlan plan = BuildPlan(lane, foundations, [fireball], pump: new NumVector2(0, 0));

        int fireballBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Fireball);
        fireballBuilds.Should().Be(3, "a strategy with no coverage towers should build fill towers immediately");
    }

    [TestMethod]
    public void MixedTier_NonCoverageRuleSamePriority_AssignedAfterCoverage()
    {
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = Foundations((5, 0), (5, 5), (15, 0), (25, 0));

        var rules = new List<TowerBuildRule>
        {
            TowerStrategyBuilder.CreateRule()
                .SetTower(BlightTowerType.Chilling)
                .SetPriority(TowerBuildPriority.Critical)
                .SetMaxUpgradeLevel(3)
                .TreatAsCoverageTower()
                .Build(),
            TowerStrategyBuilder.CreateRule()
                .SetTower(BlightTowerType.Seismic)
                .SetPriority(TowerBuildPriority.Critical)
                .SetMaxUpgradeLevel(3)
                .TreatAsCoverageTower()
                .Build(),
            TowerStrategyBuilder.CreateRule()
                .SetTower(BlightTowerType.Fireball)
                .SetPriority(TowerBuildPriority.Critical)
                .SetMaxUpgradeLevel(4)
                .PreferCloseFoundationToPump()
                .Build(),
        };

        BlightPlan plan = BuildPlan(lane, foundations, rules, pump: new NumVector2(0, 0));

        int fireballBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Fireball);
        fireballBuilds.Should().Be(2, "same-tier non-coverage rules become fill after coverage completes");
    }

    [TestMethod]
    public void FillRuleHigherPriorityThanCoverage_WaitsForCoverageThenAssigns()
    {
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = Foundations((5, 0), (5, 5), (15, 0), (25, 0));

        var rules = new List<TowerBuildRule>
        {
            TowerStrategyBuilder.CreateRule()
                .SetTower(BlightTowerType.Fireball)
                .SetPriority(TowerBuildPriority.Critical)
                .SetMaxUpgradeLevel(4)
                .PreferCloseFoundationToPump()
                .Build(),
            TowerStrategyBuilder.CreateRule()
                .SetTower(BlightTowerType.Chilling)
                .SetPriority(TowerBuildPriority.High)
                .SetMaxUpgradeLevel(3)
                .TreatAsCoverageTower()
                .Build(),
            TowerStrategyBuilder.CreateRule()
                .SetTower(BlightTowerType.Seismic)
                .SetPriority(TowerBuildPriority.High)
                .SetMaxUpgradeLevel(3)
                .TreatAsCoverageTower()
                .Build(),
        };

        BlightPlan plan = BuildPlan(lane, foundations, rules, pump: new NumVector2(0, 0));

        int fireballBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Fireball);
        fireballBuilds.Should().Be(2, "a fill rule above the coverage tier is assigned once coverage completes");
    }

    [TestMethod]
    public void TwoRootChainsStartingNearPump_SingleTowerCoversBothAtBaseRadius()
    {
        var lane = new List<NumVector2>
        {
            new(30, 0), new(40, 0), new(50, 0),    // branch A — +X, starts 30 from pump
            new(-30, 0), new(-40, 0), new(-50, 0),  // branch B — -X, starts 30 from pump
        };
        var foundations = Foundations(
            (0, 0),    // Chilling — reaches both bases at Chilling's base radius (35)
            (0, -4),   // Seismic — reaches both bases at Seismic's base radius (45)
            (30, 0),   // covers branch A only
            (-30, 0),  // covers branch B only
            (40, 0),   // branch A only
            (-40, 0)); // branch B only

        BlightPlan plan = BuildPlan(lane, foundations, CoverageRules(), pump: new NumVector2(0, 0));

        int chillBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling);
        int seismicBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Seismic);

        chillBuilds.Should().Be(1, "one Chilling tower covers both root chains at base radius");
        seismicBuilds.Should().Be(1, "one Seismic tower covers both root chains at base radius");
        plan.DebugSummary.Should().Contain("full coverage", "both branches are covered by the single towers");
    }

    [TestMethod]
    public void SingleTowerAtSharedPump_CoversAllBranches()
    {
        var lane = new List<NumVector2>
        {
            new(5, 0), new(25, 0), new(45, 0),        // branch A — +X
            new(-5, 10), new(-25, 20), new(-45, 30),  // branch B — diagonal
            new(5, -10), new(25, -20), new(45, -30),  // branch C — diagonal
        };
        var foundations = Foundations(
            (0, 0),    // right on the pump — covers all three branch bases
            (20, 0),   // covers branches A + C
            (-20, 20), // covers branch B
            (20, -20));// covers branch C

        BlightPlan plan = BuildPlan(lane, foundations, CoverageRules(), pump: new NumVector2(0, 0));

        int chillBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling);
        int seismicBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Seismic);

        chillBuilds.Should().Be(1, "a single Chilling tower at the shared pump covers all branches");
        seismicBuilds.Should().Be(1, "a single Seismic tower at the shared pump covers all branches");

        plan.DebugSummary.Should().Contain("full coverage");
        plan.DebugSummary.Should().Contain("3 branches");
    }

    [TestMethod]
    public void BranchesBeyondSingleTowerRadius_EachBranchGetsOwnTower()
    {
        var lane = new List<NumVector2>
        {
            new(20, 0), new(52, 0), new(84, 0),    // branch A — base midpoint (36,0)
            new(-20, 0), new(-52, 0), new(-84, 0), // branch B — base midpoint (-36,0)
        };
        // (0,0) and (0,-4) reach both bases only with Seismic's base radius (45 ≥ 36); they are
        // outside Chilling's base radius (35 < 36), so (40,0)/(-40,0) cover each branch separately.
        var foundations = Foundations((0, 0), (0, -4), (40, 0), (-40, 0));

        BlightPlan plan = BuildPlan(lane, foundations, CoverageRules(), pump: new NumVector2(0, 0));

        int chillBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling);
        int seismicBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Seismic);

        chillBuilds.Should().Be(2, "Chilling's base radius cannot reach both branches — one per branch");
        seismicBuilds.Should().Be(1, "Seismic's base radius reaches both branches — one tower covers both");
    }

    [TestMethod]
    public void RootChainStartingBeyondRadius_NotCountedAsBranch()
    {
        // Chain A starts at (5,0) — within the pump radius, so it is a branch.
        // Chain B starts at (45,50) — 67 units from the pump, beyond the 30
        // radius, so it must NOT be counted as a main branch even though it is
        // a root chain.  Only root chains that START near the pump count.
        var lane = new List<NumVector2>
        {
            new(5, 0), new(15, 0), new(25, 0), new(35, 0),   // branch A — starts 5 from pump
            new(45, 50), new(55, 50), new(65, 50),           // starts 67 from pump — not a branch
        };
        var foundations = Foundations(
            (5, 0),   // branch A Chilling
            (15, 0),  // branch A Seismic
            (25, 0)); // unused

        BlightPlan plan = BuildPlan(lane, foundations, CoverageRules(), pump: new NumVector2(0, 0));

        int chillBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling);
        int seismicBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Seismic);

        chillBuilds.Should().Be(1, "only the root chain starting within the pump radius is a branch");
        seismicBuilds.Should().Be(1, "only the root chain starting within the pump radius is a branch");
    }

    [TestMethod]
    public void CachedBranchAnchors_KeepBranchesAliveWhenEntitiesStreamOut()
    {
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = Foundations((5, 0), (15, 0), (25, 0));

        var cache = new List<NumVector2>();

        // Initial build near the pump detects the branch and persists its anchor.
        BlightPlan first = BuildPlan(lane, foundations, CoverageRules(),
            pump: new NumVector2(0, 0), cachedAnchors: cache);
        first.Steps.Count(s => s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling)
            .Should().Be(1, "initial build detects the branch");
        cache.Should().NotBeEmpty("freshly detected branch anchors are persisted");

        // Simulate the player walking away: the pump-near pathway entities
        // stream out, so fresh chain-base detection finds nothing near the
        // pump.  The cached anchor must keep the branch alive.
        BlightPlan second = BuildPlan(lane, foundations, CoverageRules(),
            pump: new NumVector2(1000, 1000), cachedAnchors: cache);
        second.Steps.Count(s => s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling)
            .Should().Be(1, "cached branch anchors keep branches alive when entities stream out");
    }

    [TestMethod]
    public void CachedBranchWithoutNearbySegment_IsNotFalselyCoveredByAnotherBranch()
    {
        // A cached branch whose entities fully streamed out and whose anchor is
        // NOT near any connected segment must NOT be reported as covered.  The
        // old code mapped it to the globally-nearest connected segment (a
        // segment of ANOTHER branch near the shared pump node), which made it
        // inherit that branch's coverage — exactly the false "branch covered"
        // the user reported.  It must stay detected (cache keeps it alive) but
        // uncovered, so the planner cannot declare full coverage it doesn't have.
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = Foundations((5, 0), (15, 0), (25, 0));

        // Cached anchor far from branch A's chain — no connected segment is
        // within the merge radius, and no foundation can reach it either.
        var cache = new List<NumVector2> { new(200, 200) };

        BlightPlan plan = BuildPlan(lane, foundations, CoverageRules(),
            pump: new NumVector2(0, 0), cachedAnchors: cache);

        plan.DebugSummary.Should().Contain("2 branches", "the cached branch stays detected");
        plan.DebugSummary.Should().Contain("partial coverage",
            "an unverifiable cached branch must not inherit another branch's coverage");
    }

    [TestMethod]
    public void CachedBranch_BuiltCoverageNearAnchor_KeepsCoverageWhenEntitiesStreamOut()
    {
        // Regression: when the player walks away from the pump, pump-near
        // pathway entities stream out and the branch becomes cached (no live
        // segment to measure).  BranchHasCoverage used to report such a branch
        // as NOT covered, which flipped the coverage gate to incomplete and
        // collapsed the rebuilt plan to ZERO steps.  A branch whose built
        // coverage towers already reach its persisted ANCHOR must stay covered
        // — the anchor and the towers both survive streaming, so this is the
        // same physical guarantee the live segment check provides.
        //
        // Lane far from the pump: it is not a live branch.  The cached anchor
        var lane = CreateChain((200, 200), (210, 200));
        var foundations = new List<BlightCachedTower>
        {
            new(new NumVector2(4, 0), BlightTowerType.Chilling, 3),  // built — reaches cached anchor
            new(new NumVector2(6, 0), BlightTowerType.Seismic, 3),   // built — reaches cached anchor
            new(new NumVector2(200, 210), BlightTowerType.Chilling), // unbuilt fill candidate
            new(new NumVector2(210, 205), BlightTowerType.Chilling), // unbuilt fill candidate
        };

        var rules = CoverageRules();
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .Build());

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            lane, _ => (false, false, false));

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, rules, new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), null, lane, new List<NumVector2> { new(5, 0) });

        int fireballBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Fireball);
        fireballBuilds.Should().Be(2,
            "a cached branch with built coverage reaching its anchor keeps coverage, so the fill tier stays unlocked");
    }

    [TestMethod]
    public void NoPumpPosition_FallbackChainBasesStillPlanned()
    {
        var lane = CreateChain((0, 0), (10, 0), (20, 0), (30, 0));
        var foundations = Foundations((5, 0), (15, 0), (25, 0));

        TowerBuildRule chilling = TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Chilling)
            .SetPriority(TowerBuildPriority.Critical)
            .SetMaxUpgradeLevel(3)
            .TreatAsCoverageTower()
            .Build();

        BlightPlan plan = BuildPlan(lane, foundations, [chilling]);

        int chillBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling);
        chillBuilds.Should().Be(1, "the no-pump fallback still plans the single chain base");
    }

    [TestMethod]
    public void BranchWithOnlyDownstreamReachableFoundation_GetsSeismicThere_BeforeFill()
    {
        var lane = CreateChain((0, 0), (20, 0), (70, 0), (120, 0));
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)),
            new(0, true, new NumVector2(10, 0), HasChilling: true, HasSeismic: false),   // base segment — no foundation in range
            new(1, true, new NumVector2(60, 0), HasChilling: true, HasSeismic: false),   // mid segment
            new(2, true, new NumVector2(110, 0), HasChilling: true, HasSeismic: false),  // downstream segment
        ];

        var foundations = new List<BlightCachedTower>
        {
            new(new NumVector2(115, 0), BlightTowerType.Chilling), // only reachable from the downstream segment
            new(new NumVector2(200, 200), BlightTowerType.Chilling),
            new(new NumVector2(210, 205), BlightTowerType.Chilling),
        };

        var rules = CoverageRules();
        for (int r = 0; r < rules.Count; r++)
        {
            rules[r] = rules[r] with { UpgradePolicy = TowerUpgradePolicy.BuildThenUpgradeForCoverage };
        }
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .Build());

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, rules, new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), null, lane);

        BlightPlanStep seismicBuild = plan.Steps.Single(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Seismic);
        seismicBuild.FoundationPosition.X.Should().BeApproximately(115f, 0.01f,
            "the seismic goes on the only foundation that reaches the branch");

        plan.Steps.Where(s => s.TowerType == BlightTowerType.Seismic)
            .Select(s => s.TargetLevel).Should().OnlyContain(v => v == 1,
                "radius does not grow with upgrades, so no coverage upgrade is needed");

        int seismicIdx = plan.Steps.ToList().FindIndex(s => s.Equals(seismicBuild));
        int firstFireballIdx = plan.Steps.ToList().FindIndex(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Fireball);
        seismicIdx.Should().BeGreaterThanOrEqualTo(0, "the seismic build step exists in the plan");
        seismicIdx.Should().BeLessThan(firstFireballIdx, "coverage towers always precede fill towers");
        plan.DebugSummary.Should().Contain("full coverage", "the branch is covered once the seismic is placed");
    }

    [TestMethod]
    public void UnreachableBranch_BestEffort_AllowsFill()
    {
        var lane = CreateChain((5, 0), (15, 0), (25, 0), (5, 20), (15, 20));
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(5, 0)),
            new(0, true, new NumVector2(10, 0), HasChilling: true, HasSeismic: true),  // branch A base — fully covered
            new(1, true, new NumVector2(20, 0), HasChilling: true, HasSeismic: true),  // branch A extension
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(5, 20)),
            new(3, true, new NumVector2(10, 20), HasChilling: true, HasSeismic: false), // branch B base — seismic unreachable
        ];

        var foundations = new List<BlightCachedTower>
        {
            new(new NumVector2(8, 0), BlightTowerType.Chilling, 3),  // built — covers branch A
            new(new NumVector2(8, 2), BlightTowerType.Seismic, 3),   // built — covers branch A only
            new(new NumVector2(200, 200), BlightTowerType.Chilling), // unbuilt — too far from branch B
            new(new NumVector2(210, 205), BlightTowerType.Chilling), // unbuilt — too far from branch B
        };

        var rules = CoverageRules();
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .Build());

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, rules, new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), null, lane);

        plan.Steps.Should().Contain(s => s.TowerType == BlightTowerType.Fireball,
            "an unreachable branch is skipped best-effort and does not gate the fill tier");
        plan.DebugSummary.Should().Contain("full coverage", "the unreachable branch is treated as skipped");
    }

    [TestMethod]
    public void PhantomLane_UndergroundGap_ConnectsLaneAndOpensFill()
    {
        // User scenario: the game routed part of the lane underground, so the far chain is
        // disconnected from the main pump branch by a gap beyond the normal connect distance.  The
        // phantom edge bridges it, the whole lane reads as one fully-covered branch, and the fill
        // tier opens — the plan no longer sits at 0 steps with foundations left unused.
        var lane = new List<NumVector2>
        {
            new(0, 0),     // 0 root (pump)
            new(10, 10),   // 1
            new(20, 20),   // 2
            new(30, 30),   // 3 main end
            new(80, 80),   // 4 far chain start — 70.7 gap: beyond 35, within the phantom 100
            new(90, 90),   // 5
            new(100, 100), // 6
        };

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            lane,
            midpoint => (
                chilling: midpoint.X < 50f,  // Chilling + Seismic towers cover the main chain
                seismic: midpoint.X < 50f,
                fireball: false),
            pumpGridPosition: new NumVector2(0, 0));

        var foundations = new List<BlightCachedTower>
        {
            new(new NumVector2(85, 85), BlightTowerType.Chilling), // unbuilt foundation on the far chain
        };

        List<TowerBuildRule> rules = CoverageRules();
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .Build());

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, rules, new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), null, lane);

        // The far chain inherits coverage through the phantom edge, so coverage completes and the
        // fill tier opens (Fireball steps exist) instead of the plan stalling at 0 steps.
        plan.DebugSummary.Should().Contain("full coverage",
            "the phantom edge connects the far chain so the lane reads as fully covered");
        plan.Steps.Should().Contain(s => s.TowerType == BlightTowerType.Fireball,
            "fill runs once coverage is complete across the phantom lane");
    }

    [TestMethod]
    public void Fork_NewBranchWithoutSeismic_PlansSeismicOnNewBranch()
    {
        // User scenario: Branch A (trunk) is fully covered by Chilling + Seismic; a new branch forks
        // off between the Chilling tower (on the trunk) and the Seismic tower (on the main
        // continuation).  The Seismic tower does not cover the new fork, so the coverage must turn
        // blue there and the plan must add a Seismic tower for the new branch.
        var lane = new List<NumVector2>
        {
            new(0, 0),   // 0 root (pump)
            new(10, 10), // 1
            new(20, 20), // 2
            new(30, 30), // 3 fork
            new(40, 30), // 4 main branch
            new(50, 30), // 5 main branch
            new(30, 40), // 6 new branch
            new(30, 50), // 7 new branch
        };

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            lane,
            midpoint => (
                chilling: midpoint.Y < 30f,                       // Chilling tower on the trunk
                seismic: midpoint.Y == 30f && midpoint.X >= 35f,  // Seismic tower on the main branch only
                fireball: false),
            pumpGridPosition: new NumVector2(0, 0));

        var foundations = new List<BlightCachedTower>
        {
            new(new NumVector2(20, 20), BlightTowerType.Chilling, 3), // built — covers trunk + fork + both branches (Chilling complete)
            new(new NumVector2(45, 30), BlightTowerType.Seismic, 3),   // built — covers main branch only
            new(new NumVector2(30, 45), BlightTowerType.Chilling),     // unbuilt foundation on the new branch
        };

        List<TowerBuildRule> rules = CoverageRules();
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .Build());

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, rules, new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), null, lane);

        plan.Steps.Should().Contain(s =>
            s.Action == BlightPlanAction.Build &&
            s.TowerType == BlightTowerType.Seismic &&
            MathF.Abs(s.FoundationPosition.X - 30f) < 1f &&
            MathF.Abs(s.FoundationPosition.Y - 45f) < 1f,
            "the plan must build a Seismic tower on the new fork branch");
    }

    [TestMethod]
    public void Fork_NoFoundationReachesBase_DescendsOntoNewBranchForSeismic()
    {
        var lane = new List<NumVector2>
        {
            new(0, 0),    // 0 root (pump)
            new(10, 10),  // 1
            new(20, 20),  // 2
            new(30, 30),  // 3 fork
            new(40, 30),  // 4 main branch
            new(50, 30),  // 5 main branch
            new(30, 40),  // 6 new branch
            new(30, 70),  // 7 new branch
            new(30, 100), // 8 new branch
        };

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            lane,
            midpoint => (
                chilling: midpoint.Y < 30f || MathF.Abs(midpoint.X - 30f) < 1f, // Chilling tower on the trunk covers the fork + new branch
                seismic: midpoint.Y == 30f && midpoint.X >= 35f,                 // Seismic tower on the main branch only
                fireball: false),
            pumpGridPosition: new NumVector2(0, 0));

        var foundations = new List<BlightCachedTower>
        {
            new(new NumVector2(20, 20), BlightTowerType.Chilling, 3),  // built — trunk (Chilling complete)
            new(new NumVector2(45, 30), BlightTowerType.Seismic, 3),    // built — covers main branch only
            new(new NumVector2(30, 110), BlightTowerType.Chilling),     // unbuilt foundation deep on the new branch (> 75 from the base/fork)
        };

        List<TowerBuildRule> rules = CoverageRules();

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, rules, new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), null, lane);

        plan.Steps.Should().Contain(s =>
            s.Action == BlightPlanAction.Build &&
            s.TowerType == BlightTowerType.Seismic &&
            MathF.Abs(s.FoundationPosition.X - 30f) < 1f &&
            MathF.Abs(s.FoundationPosition.Y - 110f) < 1f,
            "the plan must descend into the fork and build Seismic on the new branch");
    }

    [TestMethod]
    public void WindingLane_BaseTowerCoversWholeLane_NoDuplicateTower()
    {
        // A winding (U-shaped) lane whose far end is beyond the base tower's CURRENT radius.  Under
        // the old "nearest strictly-closer-to-pump" parent tree the lane fragmented at the pump-
        // distance local minimum, so the coverage array showed the far end red and the planner
        // added a duplicate tower; the user also saw "full coverage" reported while segments stayed
        // uncovered.  With the intact lane tree, a tower covering ANY trunk segment covers the whole
        // winding lane through AND/OR propagation (Rule 1), so a single base tower is enough and no
        // second coverage tower is planned.
        var lane = new List<NumVector2>
        {
            new(10, 10), // 0 root
            new(40, 10), // 1
            new(70, 10), // 2
            new(70, 40), // 3
            new(70, 70), // 4
            new(40, 70), // 5
            new(10, 70), // 6 far end — beyond the tower's current radius (35)
        };

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            lane,
            midpoint =>
            {
                float dx = midpoint.X - 10f;
                float dy = midpoint.Y - 10f;
                return (chilling: (dx * dx) + (dy * dy) <= 35f * 35f, seismic: false, fireball: false);
            },
            pumpGridPosition: new NumVector2(0, 0));

        for (int i = 1; i <= 6; i++)
            coverage[i].HasChilling.Should().BeTrue($"segment {i} inherits coverage through the winding lane");

        var foundations = new List<BlightCachedTower>
        {
            new(new NumVector2(10, 10), BlightTowerType.Chilling, 3), // built — base
            new(new NumVector2(65, 40), BlightTowerType.Chilling),    // unbuilt — near the far end
            new(new NumVector2(40, 70), BlightTowerType.Chilling),    // unbuilt
        };

        List<TowerBuildRule> rules = CoverageRules();
        rules.Add(TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Fireball)
            .SetPriority(TowerBuildPriority.High)
            .SetMaxUpgradeLevel(4)
            .Build());

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, rules, new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), null, lane);

        int chillingBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling);
        chillingBuilds.Should().Be(0,
            "the built base tower covers the whole winding lane; no duplicate Chilling is planned");
        plan.DebugSummary.Should().Contain("full coverage",
            "the winding lane is fully covered once the built base tower is upgraded");
    }

    [TestMethod]
    public void ForkAtPumpRoot_IsOneBranch_NotOnePerArm()
    {
        // A lane that forks right at the pump-near root is ONE branch (one lane network from the
        // pump), not one branch per arm.  The old detection started a branch at every child of an
        // orphan root, so a fork at the pump produced phantom branches (the user saw "4 branches"
        // for two lanes) and let the coverage gate report "full coverage" while an arm was still
        // uncovered.
        var lane = new List<NumVector2>
        {
            new(5, 0),    // 0 root (pump-near)
            new(25, 0),   // 1 arm A
            new(45, 0),   // 2 arm A far
            new(5, -25),  // 3 arm B
            new(5, -45),  // 4 arm B far
        };

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            lane, _ => (false, false, false), pumpGridPosition: new NumVector2(0, 0));

        coverage[1].ParentIndex.Should().Be(0, "arm A starts at the root");
        coverage[3].ParentIndex.Should().Be(0, "arm B starts at the root");

        var foundations = Foundations((5, 0), (25, 5), (5, -30));

        BlightPlan plan = BlightPlanner.Build(
            foundations, coverage, CoverageRules(), new HashSet<NumVector2>(), 1,
            new NumVector2(0, 0), null, lane);

        plan.DebugSummary.Should().Contain(" 1 branches,",
            "a lane forking at the pump is ONE branch, not one branch per arm");

        int chillingBuilds = plan.Steps.Count(s =>
            s.Action == BlightPlanAction.Build && s.TowerType == BlightTowerType.Chilling);
        chillingBuilds.Should().BeGreaterThanOrEqualTo(1, "the forked lane needs Chilling coverage");
    }

    private static List<TowerBuildRule> CoverageRules()
    {
        return
        [
            TowerStrategyBuilder.CreateRule()
                .SetTower(BlightTowerType.Chilling)
                .SetPriority(TowerBuildPriority.Critical)
                .SetMaxUpgradeLevel(3)
                .TreatAsCoverageTower()
                .Build(),
            TowerStrategyBuilder.CreateRule()
                .SetTower(BlightTowerType.Seismic)
                .SetPriority(TowerBuildPriority.Critical)
                .SetMaxUpgradeLevel(3)
                .TreatAsCoverageTower()
                .Build(),
        ];
    }

    private static BlightPlan BuildPlan(
        List<NumVector2> lane,
        List<BlightCachedTower> foundations,
        List<TowerBuildRule> rules,
        NumVector2? pump = null,
        NumVector2? player = null,
        List<NumVector2>? cachedAnchors = null)
    {
        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            lane, _ => (false, false, false));
        return BlightPlanner.Build(
            foundations, coverage, rules, new HashSet<NumVector2>(), 1, pump, player, lane, cachedAnchors);
    }

    private static List<BlightCachedTower> Foundations(params (float X, float Y)[] positions)
        => positions
            .Select(p => new BlightCachedTower(new NumVector2(p.X, p.Y), BlightTowerType.Chilling))
            .ToList();

    private static List<NumVector2> CreateChain(params (float X, float Y)[] points)
        => points.Select(p => new NumVector2(p.X, p.Y)).ToList();
}
