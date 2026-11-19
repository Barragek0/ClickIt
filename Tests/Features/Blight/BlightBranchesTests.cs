namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightBranchesTests
{
    [TestMethod]
    public void IsolatedOverlappingBeamFromDump_ReusesConnectedControlRoute()
    {
        NumVector2[] starts = [new(402.5f, 977.5f), new(398.2f, 973.3f), new(393.7f, 968.7f), new(391.5f, 966.5f)];
        NumVector2[] ends = [starts[1], starts[2], new(384.5f, 959.5f), new(381.5f, 966.5f)];
        BlightPathwayIcon[] icons = [.. starts.Select((start, i) => new BlightPathwayIcon(i, start, 2,
            BlightHelpers.GridToWorld(start), BlightHelpers.GridToWorld(ends[i]), i is 1 or 2 ? [i - 1] : []))];
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -2);
        List<PumpBranch> branches = [new(1, starts[0])];
        NumVector2 pump = new(612, 961);

        BlightPathwayIcon[] crossing = [.. icons];
        crossing[3] = crossing[3] with { BeamEnd = BlightHelpers.GridToWorld(new(384.5f, 973.5f)) };
        BlightBranches.AttachBeamEndpointGaps((LaneCoverageResult[])coverage.Clone(), crossing, starts, branches, pump)
            .Should().BeFalse("a crossing beam is not evidence of a shared lane");

        BlightPathwayIcon[] alreadyLinked = [.. icons];
        alreadyLinked[2] = alreadyLinked[2] with { Parents = [1, 3] };
        BlightBranches.AttachBeamEndpointGaps((LaneCoverageResult[])coverage.Clone(), alreadyLinked, starts, branches, pump)
            .Should().BeFalse("a raw link to another beam must not be replaced by a guessed connection");

        BlightBranches.AttachBeamEndpointGaps(coverage, icons, starts, branches, pump).Should().BeTrue();
        coverage[3].ParentIndex.Should().Be(2);
        var lanes = BlightLockdownPlanner.FindLanes(icons, new Dictionary<NumVector2, string>(), coverage, out _);
        BlightCachedTower[] towers =
        [new(starts[0], BlightTowerType.Chilling, 3), new(starts[0], BlightTowerType.Seismic, 3),
            new(new(385, 979), BlightTowerType.Fireball)];
        TowerBuildRule meteor = new ChillingSeismicMeteorStrategy().GetRule(BlightTowerType.Fireball)!.Value;
        BlightLockdownPlanner.Build(towers, lanes, meteor, 1, pump).Steps
            .Should().NotContain(step => step.Action == BlightPlanAction.Build &&
                (step.TowerType == BlightTowerType.Chilling || step.TowerType == BlightTowerType.Seismic));
    }

    [TestMethod]
    public void AngledDuplicateBeamGap_JoinsConnectedJunctionWithoutFollowingBackwardsEndpoint()
    {
        NumVector2[] starts = [new(265.2f, 640.9f), new(262.5f, 630.5f),
            new(264.5f, 621.5f), new(264.5f, 621.5f), new(269.5f, 621.5f), new(268.5f, 610f)];
        NumVector2[] ends = [new(262.5f, 630.5f), new(252, 621.8f),
            new(269.5f, 621.5f), new(269.4f, 621.5f), new(280, 621.5f), new(268.5f, 620f)];
        BlightPathwayIcon[] icons = [.. starts.Select((start, i) => new BlightPathwayIcon(i, start, 2,
            BlightHelpers.GridToWorld(start), BlightHelpers.GridToWorld(ends[i]), []))];
        LaneCoverageResult[] coverage = Coverage(-2, 0, -2, -2, 3, 0);
        List<PumpBranch> branches = [new(0, new NumVector2(273, 712))];

        LaneCoverageResult[] noDuplicate = (LaneCoverageResult[])coverage.Clone();
        noDuplicate[3] = noDuplicate[3] with { ParentIndex = 4 };
        BlightBranches.AttachBeamEndpointGaps(noDuplicate, icons, starts, branches, new(273, 712))
            .Should().BeFalse("a single nearby fragment does not establish a junction");

        LaneCoverageResult[] noJunction = (LaneCoverageResult[])coverage.Clone();
        noJunction[1] = noJunction[1] with { ParentIndex = -2 };
        BlightBranches.AttachBeamEndpointGaps(noJunction, icons, starts, branches, new(273, 712))
            .Should().BeFalse("a nearby endpoint without a connected continuation is ambiguous");

        BlightBranches.AttachBeamEndpointGaps(coverage, icons, starts, branches, new(273, 712))
            .Should().BeTrue();
        coverage[2].ParentIndex.Should().Be(0);
        coverage[3].ParentIndex.Should().Be(0);
        coverage[4].ParentIndex.Should().Be(3);
    }

    [TestMethod]
    public void EndpointGapFromDump_ReconnectsPortalRouteWithoutRequiringAnotherSeismic()
    {
        List<BlightPathwayIcon> icons = [];
        AddPath([(630.5f, 634.5f), (636.5f, 629.5f), (647.6f, 628.2f), (654.9f, 626.8f),
            (665.5f, 623.5f), (675.4f, 622.2f), (682.1f, 621.8f), (692.5f, 621.5f),
            (703.0f, 621.5f), (710.0f, 621.5f), (720.5f, 621.5f), (730.9f, 621.5f),
            (737.6f, 621.5f), (747.5f, 621.5f)]);
        AddPath([(644.5f, 701.5f), (650.9f, 709.5f), (652.6f, 713.5f), (651.5f, 717.5f)]);
        AddPath([(655.5f, 713.5f), (652.1f, 717.8f), (649.4f, 722.2f), (644.5f, 731.5f),
            (650.4f, 742.0f), (653.1f, 749.0f), (655.5f, 759.5f)]);
        AddPath([(630.5f, 634.5f), (625.5f, 633.5f), (616.5f, 640.8f), (612.5f, 646.2f),
            (609.5f, 655.5f), (617.0f, 666.0f), (620.0f, 673.0f), (621.5f, 683.5f),
            (627.6f, 692.8f), (632.9f, 697.2f), (642.5f, 701.5f), (650.9f, 710.6f),
            (654.6f, 716.9f), (657.5f, 726.5f), (668.0f, 732.8f), (675.0f, 735.2f),
            (685.5f, 736.5f), (696.0f, 737.9f), (703.0f, 740.6f), (713.5f, 747.5f)]);
        NumVector2[] positions = [.. icons.Select(icon => icon.GridPos)];
        int[] parents = [.. icons.Select(icon => icon.Parents.Length == 0 ? -1 : icon.Parents[0])];
        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(positions, static _ => default, parents);
        NumVector2 pump = new(637, 651);
        List<PumpBranch> branches = BlightBranches.FindPumpBranches(coverage, pump, positions, null);
        BlightBranches.AttachUnassignedLanes(coverage, positions, branches);
        BlightBranches.AttachParallelLanes(coverage, positions, branches);
        coverage[16].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel);

        Dictionary<NumVector2, string> portals = new() { [new(655, 759)] = "BlightPortalMinion" };
        BlightCachedTower[] towers =
        [
            new(new(629, 669), BlightTowerType.Seismic, 3),
            new(new(643, 691), BlightTowerType.Chilling, 3),
            new(new(690, 611), BlightTowerType.Chilling, 3),
            new(new(640, 745), BlightTowerType.Seismic)
        ];
        TowerBuildRule meteor = new ChillingSeismicMeteorStrategy().GetRule(BlightTowerType.Fireball)!.Value;
        var before = BlightLockdownPlanner.FindLanes(icons, portals, coverage, out _);
        BlightLockdownPlanner.Build(towers, before, meteor, 1, pump).Steps
            .Should().Contain(step => step.Action == BlightPlanAction.Build && step.TowerType == BlightTowerType.Seismic);

        BlightPathwayIcon[] crossing = [.. icons];
        crossing[16] = crossing[16] with { BeamEnd = BlightHelpers.GridToWorld(new(661, 713.5f)) };
        BlightBranches.AttachBeamEndpointGaps((LaneCoverageResult[])coverage.Clone(), crossing, positions, branches, pump)
            .Should().BeFalse("a nearby crossing does not provide evidence of a continuation");

        BlightBranches.AttachBeamEndpointGaps(coverage, icons, positions, branches, pump).Should().BeTrue();
        coverage[16].ParentIndex.Should().Be(15);
        var repaired = BlightLockdownPlanner.FindLanes(icons, portals, coverage, out _);
        repaired.Should().HaveCount(3);
        BlightLockdownPlanner.Build(towers, repaired, meteor, 2, pump).Steps
            .Should().NotContain(step => step.Action == BlightPlanAction.Build && step.TowerType == BlightTowerType.Seismic);

        void AddPath((float X, float Y)[] points)
        {
            for (int i = 0; i + 1 < points.Length; i++)
            {
                NumVector2 start = new(points[i].X, points[i].Y);
                NumVector2 end = new(points[i + 1].X, points[i + 1].Y);
                int index = icons.Count;
                icons.Add(new BlightPathwayIcon(index, start, 2, BlightHelpers.GridToWorld(start),
                    BlightHelpers.GridToWorld(end), i == 0 ? [] : [index - 1]));
            }
        }
    }

    [TestMethod]
    public void AttachUnassignedLanes_ReparentsOrphanChain_AndRebuildRestoresMidpointAndCoverage()
    {
        // A lane (0->1->2) plus a chain head (3) whose beam link is broken (orphan): the attach pass re-parents it under the lane end (2). Re-running ComputeCoverage with the attached parents gives the segment a real midpoint instead of the orphan (0,0) default so the debug tree, lane labels, and coverage propagation all reflect the joined lane.
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -1);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (30, 0));
        var branches = new List<PumpBranch> { new(0, new NumVector2(5, 5)) };

        bool attached = BlightBranches.AttachUnassignedLanes(coverage, positions, branches);

        attached.Should().BeTrue();
        coverage[3].ParentIndex.Should().Be(2, "the chain head joins the lane end");
        coverage[3].Midpoint.Should().Be(NumVector2.Zero, "the raw attach only re-parents - it does not fix the stale orphan midpoint");

        int[] parents = new int[coverage.Length];
        for (int i = 0; i < coverage.Length; i++)
            parents[i] = coverage[i].ParentIndex;
        LaneCoverageResult[] rebuilt = BlightLaneTopology.ComputeCoverage(positions, _ => (false, false, false), parents);

        rebuilt[3].Midpoint.Should().Be(new NumVector2(25f, 0f), "the rebuild recomputes the midpoint from the attached parent");
        rebuilt[3].ParentIndex.Should().Be(2);
    }

    private static LaneCoverageResult[] Coverage(params int[] parents)
        => parents.Select(p => new LaneCoverageResult(p, false, NumVector2.Zero)).ToArray();

    private static List<NumVector2> Positions(params (float X, float Y)[] pts)
        => [.. pts.Select(p => new NumVector2(p.X, p.Y))];

    [TestMethod]
    public void AttachUnassignedLanes_AttachesAlignedFragmentToBranchLaneEnd()
    {
        // Branch A = chain 0->1->2 (root 0 at the pump, lane end 2). Fragment = chain 3->4->5 whose pump-ward head 3 sits right past A's lane end — the game split one lane into two chains.
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -2, 3, 4);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (30, 0), (40, 0), (50, 0));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachUnassignedLanes(coverage, positions, branches);

        coverage[3].ParentIndex.Should().Be(2, "the fragment head is re-parented under the branch lane end");
        coverage[4].ParentIndex.Should().Be(3);
        coverage[5].ParentIndex.Should().Be(4);
        coverage[0].ParentIndex.Should().Be(-2, "the branch root is never treated as unassigned");
        coverage[1].ParentIndex.Should().Be(0);
        coverage[2].ParentIndex.Should().Be(1);
    }

    [TestMethod]
    public void AttachUnassignedLanes_CloseGap_JoinsOnLooseAngle()
    {
        // The real-world case from Infested Valley: the fragment head sits ~2 units past the lane end but at a visible angle (~54 deg, cos 0.45) — a close gap joins on the loose tier (cos >= 0.4) because a genuine continuation can leave the lane end at an angle.
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -2, 3);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (21, 2), (31, 2));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachUnassignedLanes(coverage, positions, branches);

        coverage[3].ParentIndex.Should().Be(2, "a fragment within the close-gap distance joins on the loose alignment tier");
        coverage[4].ParentIndex.Should().Be(3);
    }

    [TestMethod]
    public void AttachUnassignedLanes_HeadOutOfRange_AttachesDeeperSegment_AndRejoinsHeadSide()
    {
        // The real junction is one segment before the fragment head: the head (3) pokes out of range at 50u, but its child (4) sits 10u past the branch lane end — the walk attaches 4 and reconnects the head-side stub (3) onto it, because the whole chain is one physical lane and the stub would otherwise show as a gap in the branch.
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -2, 3, 4);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (70, 0), (30, 0), (40, 0));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachUnassignedLanes(coverage, positions, branches);

        coverage[3].ParentIndex.Should().Be(4, "the out-of-range head rejoins through its in-range child");
        coverage[4].ParentIndex.Should().Be(2, "the deeper in-range segment attaches to the lane end");
        coverage[5].ParentIndex.Should().Be(4);
    }

    [TestMethod]
    public void AttachUnassignedLanes_WalksTowardPortal_UntilASegmentIsInRange_AndRejoinsHeadSide()
    {
        // Head (3) and its next segment (4) are both out of range; the walk keeps going toward the portal end until segment 5 (10u from the lane end) attaches, then the whole head-side (3, 4) reconnects onto it.
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -2, 3, 4, 5);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (70, 0), (60, 0), (30, 0), (40, 0));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachUnassignedLanes(coverage, positions, branches);

        coverage[3].ParentIndex.Should().Be(4, "the out-of-range head rejoins onto its chain neighbor");
        coverage[4].ParentIndex.Should().Be(5, "the second head-side segment rejoins onto the attached segment");
        coverage[5].ParentIndex.Should().Be(2, "the first in-range segment attaches");
        coverage[6].ParentIndex.Should().Be(5);
    }

    [TestMethod]
    public void AttachUnassignedLanes_LeavesDistantFragmentUnassigned()
    {
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -2, 3, 4);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (200, 0), (210, 0), (220, 0));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachUnassignedLanes(coverage, positions, branches);

        coverage[3].ParentIndex.Should().Be(-2, "head is 180u from the branch end - beyond the attach distance");
    }

    [TestMethod]
    public void AttachUnassignedLanes_LeavesDistantMisalignedFragmentUnassigned()
    {
        // Fragment runs perpendicular to the branch lane: near enough for distance but beyond the close-gap tier (30u), so the strict alignment gate rejects it.
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -2, 3, 4);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (20, 30), (20, 40), (20, 50));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachUnassignedLanes(coverage, positions, branches);

        coverage[3].ParentIndex.Should().Be(-2, "30u away and perpendicular - the alignment gate rejects it");
    }

    [TestMethod]
    public void AttachUnassignedLanes_ChainsFragmentsIteratively()
    {
        // Fragment X (3->4) attaches to branch A, then fragment Y (5->6) attaches to X's end.
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -2, 3, -2, 5);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (30, 0), (40, 0), (50, 0), (60, 0));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachUnassignedLanes(coverage, positions, branches);

        coverage[3].ParentIndex.Should().Be(2);
        coverage[5].ParentIndex.Should().Be(4, "Y attaches to X's end once X is part of the branch");
    }

    [TestMethod]
    public void AttachUnassignedLanes_NoUnassignedChains_LeavesCoverageUnchanged()
    {
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, 2);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (30, 0));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachUnassignedLanes(coverage, positions, branches);

        coverage[0].ParentIndex.Should().Be(-2);
        coverage[1].ParentIndex.Should().Be(0);
        coverage[2].ParentIndex.Should().Be(1);
        coverage[3].ParentIndex.Should().Be(2);
    }

    [TestMethod]
    public void AttachUnassignedLanes_WithOnlyCachedAnchor_AttachesNothing()
    {
        // An anchor-only branch (CoverageSegment < 0) claims no segments, so there is no target to attach to and the coverage stays untouched.
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0));
        var branches = new List<PumpBranch> { new(-1, new NumVector2(5, 5)) };

        BlightBranches.AttachUnassignedLanes(coverage, positions, branches);

        coverage[0].ParentIndex.Should().Be(-2);
        coverage[1].ParentIndex.Should().Be(0);
        coverage[2].ParentIndex.Should().Be(1);
    }

    [TestMethod]
    public void AttachParallelLanes_AttachesParallelRowToLane()
    {
        // Branch A = lane 0->1->2 running east. A second row (3->4->5) travels parallel, offset only 3u north — the game laid the same lane twice. The lane-end pass cannot attach it (its head is beside the lane, not at a lane end), so the parallel pass re-parents the row onto the parallel lane segment.
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -2, 3, 4);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (10, 3), (20, 3), (30, 3));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachParallelLanes(coverage, positions, branches);

        coverage[3].ParentIndex.Should().Be(1, "the parallel row head attaches to the parallel lane segment");
        coverage[4].ParentIndex.Should().Be(3);
        coverage[5].ParentIndex.Should().Be(4);
    }

    [TestMethod]
    public void AttachParallelLanes_AttachesAntiParallelRowToLane()
    {
        // The same stacked row laid in the REVERSE direction (monsters can walk either way): the parallel gate uses |cos|, so an anti-parallel row still merges into the lane.
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -2, 3, 4);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (30, 3), (20, 3), (10, 3));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachParallelLanes(coverage, positions, branches);

        coverage[3].ParentIndex.Should().Be(4, "the anti-parallel row head rejoins through its in-range child");
        coverage[4].ParentIndex.Should().Be(2, "the anti-parallel row attaches to the parallel lane segment");
        coverage[5].ParentIndex.Should().Be(4);
    }

    [TestMethod]
    public void AttachParallelLanes_LeavesPerpendicularRowUnassigned()
    {
        // A nearby but PERPENDICULAR row is a crossing lane, not a stacked duplicate — the parallel gate rejects it and the chain stays unassigned.
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -2, 3, 4);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (10, 3), (10, 13), (10, 23));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachParallelLanes(coverage, positions, branches);

        coverage[3].ParentIndex.Should().Be(-2, "perpendicular row is not a stacked lane");
        coverage[4].ParentIndex.Should().Be(3);
        coverage[5].ParentIndex.Should().Be(4);
    }

    [TestMethod]
    public void AttachParallelLanes_LeavesDistantRowUnassigned()
    {
        // A parallel row beyond the parallel distance is a separate lane, not a stacked duplicate.
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, -2, 3, 4);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (10, 15), (20, 15), (30, 15));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachParallelLanes(coverage, positions, branches);

        coverage[3].ParentIndex.Should().Be(-2, "15u away exceeds the parallel window");
        coverage[4].ParentIndex.Should().Be(3);
        coverage[5].ParentIndex.Should().Be(4);
    }

    [TestMethod]
    public void AttachParallelLanes_LeavesFullyAssignedCoverageUnchanged()
    {
        LaneCoverageResult[] coverage = Coverage(-2, 0, 1, 2);
        List<NumVector2> positions = Positions((0, 0), (10, 0), (20, 0), (30, 0));
        var branches = new List<PumpBranch> { new(1, new NumVector2(0, 0)) };

        BlightBranches.AttachParallelLanes(coverage, positions, branches);

        coverage[0].ParentIndex.Should().Be(-2);
        coverage[1].ParentIndex.Should().Be(0);
        coverage[2].ParentIndex.Should().Be(1);
        coverage[3].ParentIndex.Should().Be(2);
    }
}
