namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightBranchesTests
{
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
