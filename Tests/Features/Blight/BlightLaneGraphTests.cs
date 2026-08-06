namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightLaneGraphTests
{
    private static LaneGraph Build(params NumVector2[] positions)
        => BlightLaneGraph.Build(positions);

    [TestMethod]
    public void Build_StraightChain_ConnectsConsecutivePairs_Only()
    {
        LaneGraph graph = Build(
            new NumVector2(0, 0), new NumVector2(10, 0), new NumVector2(20, 0), new NumVector2(30, 0));

        graph.Edges.Should().Contain((0, 1)).And.Contain((1, 2)).And.Contain((2, 3));
        graph.Edges.Should().HaveCount(3, "skip-ahead collinear pairs (0,2)/(1,3)/(0,3) must never connect");
    }

    [TestMethod]
    public void Build_TwoNodeLane_KeepsTheEdge()
    {
        LaneGraph graph = Build(new NumVector2(5, 5), new NumVector2(15, 5));

        graph.Edges.Should().ContainSingle().Which.Should().Be((0, 1), "a 2-point lane is one edge");
    }

    [TestMethod]
    public void Build_Fork_JunctionConnectsTrunkAndBothBranches()
    {
        // 0 trunk -> 1 junction -> branch east (2,3) and branch south-east (4,5). The two branch
        // ends are kept >45u apart so only the junction links appear.
        var graph = Build(
            new NumVector2(0, 0),    // 0 trunk
            new NumVector2(10, 0),   // 1 junction (fork)
            new NumVector2(20, 0),   // 2 branch east
            new NumVector2(30, 0),   // 3 branch east end
            new NumVector2(20, 15),  // 4 branch south-east
            new NumVector2(50, 45)); // 5 branch south-east end (dist to 3 > 45)

        graph.Edges.Should().Contain((0, 1), "trunk into the fork");
        graph.Edges.Should().Contain((1, 2), "fork -> east branch");
        graph.Edges.Should().Contain((1, 4), "fork -> south-east branch");
        graph.Edges.Should().Contain((2, 3));
        graph.Edges.Should().Contain((4, 5));
        graph.Edges.Should().NotContain((1, 3), "skip-ahead pairs never connect");
        graph.Edges.Should().NotContain((2, 4), "a perpendicular passing point is not a lane link");
    }

    [TestMethod]
    public void Build_ParallelLanes_NoCrossEdges()
    {
        // Two lanes 10 units apart, each a straight chain. Every point is within 45u of the other
        // lane, but no lane continues across the gap — the old distance-only rule cross-connected.
        var graph = Build(
            new NumVector2(0, 0), new NumVector2(10, 0), new NumVector2(20, 0), new NumVector2(30, 0),
            new NumVector2(0, 10), new NumVector2(10, 10), new NumVector2(20, 10), new NumVector2(30, 10));

        graph.Edges.Should().Contain((0, 1)).And.Contain((4, 5));
        graph.Edges.Should().NotContain((0, 4), "lanes 10 apart never connect across the gap");
        graph.Edges.Should().NotContain((1, 5));
        graph.Edges.Should().NotContain((3, 7));
        graph.Edges.Should().HaveCount(6, "two 4-point chains = 3 edges each, no cross edges");
    }

    [TestMethod]
    public void Build_Hairpin_StrandsStaySeparate_NoCrossStrandEdges()
    {
        // A lane runs east then doubles back west on a parallel strand 8 units below.  The two
        // strands are geometrically indistinguishable from two separate parallel lanes (their
        // lanes run the same way), so the pruner deliberately keeps them separate — a wrong
        // cross-strand link is far worse than the rare sharp-tip split.
        var graph = Build(
            new NumVector2(0, 0), new NumVector2(10, 0), new NumVector2(20, 0),
            new NumVector2(20, 8), new NumVector2(10, 8), new NumVector2(0, 8));

        graph.Edges.Should().Contain((0, 1)).And.Contain((1, 2)).And.Contain((3, 4)).And.Contain((4, 5));
        graph.Edges.Should().NotContain((2, 3), "the sharp tip is not distinguishable from a parallel cross");
        graph.Edges.Should().NotContain((1, 4), "parallel strands never connect along their length");
        graph.Edges.Should().HaveCount(4, "each strand is its own chain");
    }

    [TestMethod]
    public void Build_LaneContinuationOntoAnotherArm_IsKept()
    {
        // Leaf 0's lane points exactly at mid-chain node 2 on arm B (the validated Atoll shape:
        // AABAB1 -> ABA6, where the leaf's heading aligns with the target). The leaf's lane is on
        // the same 45-degree line as the target, so the straight continuation is real.
        //   0 (leaf) at (0,0), parent-side 1 at (-8,-8)  -> lane heads 45 degrees toward arm B
        //   arm B runs vertical through (10,10): 2 at (10,10), 3 at (10,20), 4 at (10,30)
        var graph = Build(
            new NumVector2(0, 0),   // 0 leaf
            new NumVector2(-8, -8), // 1 leaf's parent (heads 45° through 0)
            new NumVector2(10, 10), // 2 arm B (exactly on the leaf's heading)
            new NumVector2(10, 20), // 3 arm B
            new NumVector2(10, 30));// 4 arm B

        graph.Edges.Should().Contain((0, 2), "the leaf's lane continues onto arm B");
        graph.Edges.Should().Contain((0, 1));
        graph.Edges.Should().Contain((2, 3)).And.Contain((3, 4));
        graph.Edges.Should().NotContain((0, 4), "arm B's far end is not a lane link for the leaf");
    }

    [TestMethod]
    public void Build_SharpTurn_StaysConnected()
    {
        // A lane that turns 90 degrees at node 1.  The far arm is always within the 45u connect
        // distance (10,10 is 14u from the start), so node 0 sees it as a non-continuation impostor
        // and its leaf link (0,1) is deliberately dropped — the pruner prefers a safe split over a
        // wrong merge.  The turn itself (1,2),(2,3) stays connected.
        var graph = Build(
            new NumVector2(0, 0), new NumVector2(10, 0), new NumVector2(10, 10), new NumVector2(10, 20));

        graph.Edges.Should().Contain((1, 2)).And.Contain((2, 3));
        graph.Edges.Should().NotContain((0, 1), "the corner leaf sees the far arm and is left unlinked");
        graph.Edges.Should().HaveCount(2);
    }

    [TestMethod]
    public void Build_ThreeWayJunction_ConnectsAllStubs_NoStubCrossLinks()
    {
        // Center 0 with three 2-point stubs at 0°/120°/240° (a true Y — no through lane). The
        // center is a real junction; the stubs must not cross-connect to each other.
        var graph = Build(
            new NumVector2(0, 0),      // 0 center (junction)
            new NumVector2(12, 0),     // 1 stub east
            new NumVector2(40, 0),     // 2 stub east end
            new NumVector2(-6, 10.4f), // 3 stub north-west
            new NumVector2(-20, 34.6f),// 4 stub north-west end
            new NumVector2(-6, -10.4f),// 5 stub south-west
            new NumVector2(-20, -34.6f));// 6 stub south-west end

        graph.Edges.Should().Contain((0, 1), "center -> east stub");
        graph.Edges.Should().Contain((0, 3), "center -> north-west stub");
        graph.Edges.Should().Contain((0, 5), "center -> south-west stub");
        graph.Edges.Should().Contain((1, 2)).And.Contain((3, 4)).And.Contain((5, 6));
        graph.Edges.Should().NotContain((1, 3), "adjacent stubs are not one lane");
        graph.Edges.Should().NotContain((1, 5));
        graph.Edges.Should().NotContain((3, 5));
    }

    [TestMethod]
    public void BuildPumpRootedParents_RootsFromPump_AndFollowsFork()
    {
        // Pump at (-5,0): 0 nearest the pump -> 1 -> fork at 2 (east branch 3) and a clearly
        // diverging south branch (4,5) that leaves the junction exactly straight.
        LaneGraph graph = Build(
            new NumVector2(0, 0),    // 0 trunk (root)
            new NumVector2(10, 0),   // 1
            new NumVector2(25, 0),   // 2 fork
            new NumVector2(45, 0),   // 3 branch east end
            new NumVector2(10, 15),  // 4 branch south
            new NumVector2(10, 40)); // 5 branch south end

        int[] parent = BlightLaneGraph.BuildPumpRootedParents(graph, new NumVector2(-5, 0), out _);

        parent[0].Should().Be(-1, "the pump-nearest node is the tree root");
        parent[1].Should().Be(0);
        parent[2].Should().Be(1);
        parent[3].Should().Be(2, "fork node is the parent of every branch");
        parent[4].Should().Be(1, "the south branch attaches at the fork's parent");
        parent[5].Should().Be(4);
    }

    // ── Id-based graph (the game encodes lane membership in the pathway ids) ──

    private static LaneGraph BuildIdBased(NumVector2 pump, NumVector2[] positions, long[] ids)
        => BlightLaneGraph.BuildIdBased(positions, ids, pump);

    [TestMethod]
    public void BuildIdBased_NonAdjacentIds_NearbyButDifferentLanes_DoNotConnect()
    {
        // Two real lanes pass within the 45u connect distance of each other (the old geometry
        // graph would chain them into one fake lane) but are different id runs — id-adjacency is
        // the ground truth, so no cross-lane edge may exist.
        NumVector2[] positions =
        [
            new(10, 0), new(20, 0), new(30, 0), new(40, 0), // lane A ids 100..97
            new(15, 30), new(25, 30), new(35, 30),          // lane B ids 50..48 (30u away)
        ];
        long[] ids = [100, 99, 98, 97, 50, 49, 48];

        LaneGraph graph = BuildIdBased(new NumVector2(0, 0), positions, ids);

        graph.Edges.Should().Contain((0, 1)).And.Contain((1, 2)).And.Contain((2, 3));
        graph.Edges.Should().Contain((4, 5)).And.Contain((5, 6));
        graph.Edges.Should().HaveCount(5, "each lane is its own id run — no cross-lane edges");
        graph.Edges.Should().NotContain((0, 4), "nearby points from different runs are NOT one lane");
        graph.Edges.Should().NotContain((1, 5));
        graph.Edges.Should().NotContain((2, 6));
    }

    [TestMethod]
    public void BuildIdBased_SharedForkPoint_JoinsLanesAtTheHub()
    {
        // A fork places one entity per lane at the SAME position (Courthouse: all lanes share the
        // hub (625,502); Silo: ids 754/753 co-located). The shared point joins the runs.
        NumVector2[] positions =
        [
            new(10, 0), new(20, 0),  // lane A ids 100,99
            new(10, 0), new(15, 10), // lane B starts at the same fork point
        ];
        long[] ids = [100, 99, 50, 49];

        LaneGraph graph = BuildIdBased(new NumVector2(0, 0), positions, ids);

        graph.Edges.Should().Contain((0, 1), "lane A chains along its run");
        graph.Edges.Should().Contain((2, 3), "lane B chains along its run");
        graph.Edges.Should().Contain((0, 2), "the shared fork point joins both lanes");
    }

    [TestMethod]
    public void BuildIdBased_PumpHub_JoinsLanesConvergingAtThePump()
    {
        // Lanes converge at the pump: their pump-near points are 7u apart (outside the fork join
        // distance, inside the pump-root radius), so the pump hub joins them — the pump is where
        // every lane starts.
        NumVector2[] positions =
        [
            new(10, 0), new(20, 0),  // lane A ids 100,99
            new(15, 5), new(25, 5),  // lane B ids 50,49
        ];
        long[] ids = [100, 99, 50, 49];

        LaneGraph graph = BuildIdBased(new NumVector2(0, 0), positions, ids);

        graph.Edges.Should().Contain((0, 1)).And.Contain((2, 3), "each lane chains along its run");
        graph.Edges.Should().Contain((0, 2), "pump-near starts of both lanes join at the pump hub");
    }
}
