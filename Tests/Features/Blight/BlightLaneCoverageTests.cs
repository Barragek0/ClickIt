using ClickIt.Tests.Shared.TestUtils;

namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightLaneCoverageTests
{
    [TestMethod]
    public void SimpleBranch_OneChillingTower_CoversEntireBranch()
    {
        var positions = CreateBranchPositions(5, spacing: 10);

        LaneCoverageResult[] coverage = ComputeCoverage(positions,
            midpoint => midpoint.Y < 25 ? (true, false, false) : (false, false, false),
            ChainParents(positions.Count));

        for (int i = 1; i <= 4; i++)
            coverage[i].HasChilling.Should().BeTrue($"segment {i} should have Chilling");
    }

    [TestMethod]
    public void SimpleBranch_TwoTypesOnSameBranch_AllGreen()
    {
        var positions = CreateBranchPositions(5, spacing: 10);

        LaneCoverageResult[] coverage = ComputeCoverage(positions,
            _ => (true, true, false),
            ChainParents(positions.Count));

        for (int i = 1; i <= 4; i++)
        {
            coverage[i].HasChilling.Should().BeTrue($"segment {i} Chilling");
            coverage[i].HasSeismic.Should().BeTrue($"segment {i} Seismic");
        }
    }

    [TestMethod]
    public void Divergence_BothChildrenBothTypes_ParentGreen()
    {
        // Trunk 0(root) -> 1 -> 2 -> 3, then a fork at 3: left arm 4 -> 5, right arm 6 -> 7.
        var positions = CreateDivergingPositions(trunkLen: 3, leftLen: 2, rightLen: 2, spacing: 10, branchGap: 40);
        int[] parents = [-1, 0, 1, 2, 3, 4, 3, 6];

        LaneCoverageResult[] coverage = ComputeCoverage(positions,
            _ => (true, true, false),
            parents);

        for (int i = 1; i <= 7; i++)
        {
            coverage[i].HasChilling.Should().BeTrue($"seg {i} Chilling");
            coverage[i].HasSeismic.Should().BeTrue($"seg {i} Seismic");
        }
    }

    [TestMethod]
    public void DownwardPropagation_Chain_SingleTowerCoversAll()
    {
        var positions = new List<NumVector2>
        {
            new(0, 0), new(10, 10), new(20, 20), new(30, 30), new(40, 40), new(50, 50),
        };

        LaneCoverageResult[] coverage = ComputeCoverage(positions,
            midpoint => (midpoint.X == 25f && midpoint.Y == 25f) ? (true, false, false) : (false, false, false),
            ChainParents(positions.Count));

        for (int i = 1; i <= 4; i++)
            coverage[i].HasChilling.Should().BeTrue($"seg {i} HasChilling={coverage[i].HasChilling}");
    }

    [TestMethod]
    public void BuildLaneTree_LargestArmContinuesLane_SmallerArmIsNumberedSideLane()
    {
        // Tree: 0(root)->1->2(fork). Arm 3->4 has 2 segments, arm 5->6->7 has 3 — the larger arm
        // continues the lane "A", the smaller one becomes the numbered side lane "A-1".
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)),
            new(0, false, new NumVector2(5, 0)),
            new(1, false, new NumVector2(15, 0)),
            new(2, false, new NumVector2(25, 0)),
            new(3, false, new NumVector2(35, 0)),
            new(2, false, new NumVector2(25, 10)),
            new(5, false, new NumVector2(35, 10)),
            new(6, false, new NumVector2(45, 10)),
        ];

        List<List<int>> children = BlightLaneTopology.BuildCoverageChildren(coverage);
        BlightLaneNode lane = BlightLaneTopology.BuildLaneTree(coverage, children, 0, "A");

        lane.Name.Should().Be("A");
        lane.Segments.Should().Equal([0, 1, 2, 5, 6, 7], "the larger arm continues the same lane");
        lane.Children.Should().HaveCount(1, "only the smaller arm becomes a side lane");
        lane.Children[0].Name.Should().Be("A-1");
        lane.Children[0].Segments.Should().Equal(3, 4);
    }

    [TestMethod]
    public void BuildLaneTree_DeepChain_StaysOneLane_WithNumberedSideLane()
    {
        // A deep chain 0->1->2->3->4->5 then a fork at 5: child 6 (1 seg) vs child 7->8 (2 segs).
        // The larger arm (7->8) continues the lane, so the deep chain stays ONE lane "A".
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)),
            new(0, false, new NumVector2(5, 0)),
            new(1, false, new NumVector2(15, 0)),
            new(2, false, new NumVector2(25, 0)),
            new(3, false, new NumVector2(35, 0)),
            new(4, false, new NumVector2(45, 0)),
            new(5, false, new NumVector2(55, 0)),
            new(5, false, new NumVector2(55, 10)),
            new(7, false, new NumVector2(65, 10)),
        ];

        List<List<int>> children = BlightLaneTopology.BuildCoverageChildren(coverage);
        BlightLaneNode lane = BlightLaneTopology.BuildLaneTree(coverage, children, 0, "A");

        lane.Segments.Should().Equal([0, 1, 2, 3, 4, 5, 7, 8], "the deep chain stays one lane through the larger arm");
        lane.Children.Should().HaveCount(1);
        lane.Children[0].Name.Should().Be("A-1");
        lane.Children[0].Segments.Should().Equal(6);
    }

    [TestMethod]
    public void ComputeCoverage_StackedParallelRows_MergeCoverage_AndPropagate()
    {
        // The game lays the SAME lane down as two stacked parallel rows.  A tower on the main row
        // (3,4) must cover the stacked row (5,6) too, and coverage must then propagate up the fork
        // at 2 to the trunk (1,0) — without the merge the uncovered stacked row blocks AND-upward
        // propagation (the reported bug).
        var positions = new List<NumVector2>
        {
            new(0, 0),   // 0 root (pump)
            new(10, 0),  // 1
            new(20, 0),  // 2 fork
            new(30, 0),  // 3 main row
            new(40, 0),  // 4 main row (Chilling tower)
            new(30, 3),  // 5 stacked row
            new(40, 3),  // 6 stacked row
        };

        // Fork at 2 with stacked parallel rows 3-4 (main) and 5-6 (stacked duplicate).
        int[] parents = [-1, 0, 1, 2, 3, 2, 5];
        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions,
            midpoint => (chilling: midpoint.Y < 1f && midpoint.X >= 30f, seismic: false, fireball: false),
            parents);

        coverage[3].HasChilling.Should().BeTrue();
        coverage[4].HasChilling.Should().BeTrue();
        coverage[5].HasChilling.Should().BeTrue("the stacked row shares the tower's coverage with the main row");
        coverage[6].HasChilling.Should().BeTrue("the stacked row shares the tower's coverage with the main row");
        coverage[2].HasChilling.Should().BeTrue("coverage propagates up through the fork once the stacked row is merged");
        coverage[1].HasChilling.Should().BeTrue();
        coverage[0].HasChilling.Should().BeTrue();
    }

    [TestMethod]
    public void ComputeCoverage_StackedRows_SeismicSharesAcrossRows()
    {
        var positions = new List<NumVector2>
        {
            new(0, 0),   // 0 root (pump)
            new(10, 0),  // 1
            new(20, 0),  // 2 fork
            new(30, 0),  // 3 main row
            new(40, 0),  // 4 main row
            new(30, 3),  // 5 stacked row
            new(40, 3),  // 6 stacked row
        };

        // Fork at 2 with stacked parallel rows 3-4 (main) and 5-6 (stacked duplicate).
        int[] parents = [-1, 0, 1, 2, 3, 2, 5];
        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions,
            midpoint => (chilling: false, seismic: midpoint.Y < 1f && midpoint.X >= 30f, fireball: false),
            parents);

        coverage[5].HasSeismic.Should().BeTrue();
        coverage[6].HasSeismic.Should().BeTrue();
        coverage[0].HasSeismic.Should().BeTrue("Seismic propagates up through the merged stacked fork");
    }

    [TestMethod]
    public void BuildLaneTree_StackedDuplicateArm_MergesIntoMainLane()
    {
        // Fork at 2 with two equal arms: main 3->4 and a stacked duplicate 5->6 three units away.
        // The stacked duplicate is the same physical lane written twice, so it merges into the lane
        // instead of rendering as a numbered divergence.
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)),
            new(0, false, new NumVector2(10, 0)),
            new(1, false, new NumVector2(20, 0)),
            new(2, false, new NumVector2(30, 0)),
            new(3, false, new NumVector2(40, 0)),
            new(2, false, new NumVector2(30, 3)),
            new(5, false, new NumVector2(40, 3)),
        ];

        List<List<int>> children = BlightLaneTopology.BuildCoverageChildren(coverage);
        BlightLaneNode lane = BlightLaneTopology.BuildLaneTree(coverage, children, 0, "A");

        lane.Segments.Should().Equal([0, 1, 2, 3, 4], "the stacked duplicate arm merges into the lane");
        lane.Children.Should().HaveCount(0, "no divergence for a stacked duplicate row");
    }

    [TestMethod]
    public void BuildLaneTree_DistantParallelArm_StaysDivergence()
    {
        // A parallel arm 15 units away is a genuinely separate lane — the "quite close" merge
        // threshold must NOT absorb it.
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)),
            new(0, false, new NumVector2(10, 0)),
            new(1, false, new NumVector2(20, 0)),
            new(2, false, new NumVector2(30, 0)),
            new(3, false, new NumVector2(40, 0)),
            new(2, false, new NumVector2(30, 15)),
            new(5, false, new NumVector2(40, 15)),
        ];

        List<List<int>> children = BlightLaneTopology.BuildCoverageChildren(coverage);
        BlightLaneNode lane = BlightLaneTopology.BuildLaneTree(coverage, children, 0, "A");

        lane.Segments.Should().Equal([0, 1, 2, 3, 4]);
        lane.Children.Should().HaveCount(1, "a distant parallel arm is a real divergence");
        lane.Children[0].Name.Should().Be("A-1");
        lane.Children[0].Segments.Should().Equal(5, 6);
    }

    [TestMethod]
    public void IsStackedOnRenderedLane_StackedSegmentTrue_DistantFalse()
    {
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)),
            new(0, false, new NumVector2(10, 0)),
            new(1, false, new NumVector2(20, 0)),
            new(2, false, new NumVector2(30, 0)),
            new(3, false, new NumVector2(40, 0)),
            new(2, false, new NumVector2(30, 3)),
            new(5, false, new NumVector2(40, 3)),
            new(2, false, new NumVector2(30, 15)),
        ];
        IReadOnlySet<int> rendered = new HashSet<int> { 3, 4 };

        BlightLaneTopology.IsStackedOnRenderedLane(5, coverage, rendered).Should().BeTrue(
            "segment 5 is a stacked duplicate of rendered segment 3");
        BlightLaneTopology.IsStackedOnRenderedLane(6, coverage, rendered).Should().BeTrue(
            "segment 6 is a stacked duplicate of rendered segment 4");
        BlightLaneTopology.IsStackedOnRenderedLane(7, coverage, rendered).Should().BeFalse(
            "segment 7 is 15 units away — a real lane, not a stacked duplicate");
    }

    [TestMethod]
    public void AggregateLane_OrsSegmentFlagsAcrossTheLane()
    {
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)),
            new(0, false, new NumVector2(10, 0), HasChilling: true),
            new(1, false, new NumVector2(20, 0), HasSeismic: true, IsPhantom: true),
        ];
        var lane = new BlightLaneNode("A", [1, 2], []);

        LaneCoverageResult aggregate = BlightLaneTopology.AggregateLane(lane, coverage);

        aggregate.HasChilling.Should().BeTrue();
        aggregate.HasSeismic.Should().BeTrue();
        aggregate.HasFireball.Should().BeFalse();
        aggregate.IsPhantom.Should().BeTrue();
    }

    [TestMethod]
    public void BuildLaneTree_StopsOnSingleChildCycle_InsteadOfLooping()
    {
        // A corrupt children graph whose single-child chain cycles back on itself (1 -> 2 -> 1)
        // would walk forever without the guard; BuildLaneTree must terminate.
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)), // 0
            new(0, false, new NumVector2(10, 0)),                               // 1
            new(1, false, new NumVector2(20, 0)),                               // 2
        ];
        List<List<int>> children =
        [
            [1],
            [2],
            [1], // corrupt back-edge: 2 -> 1
        ];

        BlightLaneNode lane = BlightLaneTopology.BuildLaneTree(coverage, children, 0, "A");

        lane.Segments.Count.Should().BeLessThanOrEqualTo(coverage.Length + 1,
            "the chain walk must bail out after at most n+1 steps instead of looping forever");
        lane.Segments[0].Should().Be(0);
    }

    [TestMethod]
    public void BuildBranchLaneForest_RootWithMultipleArms_MainIsLargest()
    {
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)), // 0 = R (orphan root)
            new(0, false, new NumVector2(5, 0)),    // 1 small arm
            new(0, false, new NumVector2(5, 10)),   // 2 large arm
            new(2, false, new NumVector2(15, 10)),  // 3
            new(3, false, new NumVector2(25, 10)),  // 4
        ];

        List<List<int>> children = BlightLaneTopology.BuildCoverageChildren(coverage);
        List<BlightLaneNode> forest = BlightLaneTopology.BuildBranchLaneForest(coverage, children, [1, 2, 3, 4], mainStart: 1, "A");

        forest.Should().HaveCount(2, "both arms of the root render");
        forest[0].Name.Should().Be("A", "the largest arm is the main lane");
        forest[0].Segments.Should().Equal(2, 3, 4);
        forest[1].Name.Should().Be("A-1", "the smaller arm gets a clean numeric suffix");
        forest[1].Segments.Should().Equal(1);
    }

    [TestMethod]
    public void BuildBranchLaneForest_SeparateOrphanPiece_RendersAsExtraLane()
    {
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)), // 0 = R1 (orphan root)
            new(0, false, new NumVector2(5, 0)),    // 1 main chain
            new(1, false, new NumVector2(15, 0)),   // 2
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(5, 50)), // 3 = R2 (separate orphan)
            new(3, false, new NumVector2(15, 50)),  // 4
        ];

        List<List<int>> children = BlightLaneTopology.BuildCoverageChildren(coverage);
        List<BlightLaneNode> forest = BlightLaneTopology.BuildBranchLaneForest(
            coverage, children, [1, 2, 3, 4], mainStart: 1, "A");

        forest.Should().HaveCount(2, "the separate orphan piece is its own lane, not hidden");
        forest[0].Name.Should().Be("A");
        forest[0].Segments.Should().Equal(1, 2);
        forest[1].Name.Should().Be("A-1");
        forest[1].Segments.Should().Equal(3, 4);
    }

    [TestMethod]
    public void BuildCoverageChildren_SegmentZeroWithParent_IsIncludedAsChild()
    {

        LaneCoverageResult[] coverage =
        [
            new(2, false, new NumVector2(10, 10)), // 0 = child of segment 2
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)), // 1 = root
            new(1, false, new NumVector2(5, 5)),   // 2 = child of root
        ];

        List<List<int>> children = BlightLaneTopology.BuildCoverageChildren(coverage);
        children[2].Should().Contain(0, "segment 0 is a real child and must appear in its parent's children");
    }

    [TestMethod]
    public void PropagateType_PropagatesIntoSegmentZeroSubtree()
    {

        LaneCoverageResult[] coverage =
        [
            new(2, false, new NumVector2(10, 10)), // 0 = child of 2
            new(BlightLaneTopology.OrphanSentinel, false, new NumVector2(0, 0)), // 1 = root
            new(1, false, new NumVector2(5, 5)),   // 2 = child of 1
        ];

        bool[] propagated = BlightLaneTopology.PropagateType(coverage, [false, true, false]);

        propagated[2].Should().BeTrue("child 2 inherits from the covered root");
        propagated[0].Should().BeTrue("segment 0 inherits down the chain through its parent");
    }

    [TestMethod]
    public void ComputeCoverage_WithSupportCoverage_PopulatesAndPropagatesAllSixTypes()
    {
        var positions = CreateBranchPositions(4, spacing: 10);

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions,
            _ => (true, false, false),
            ChainParents(positions.Count),
            getSupportCoverage: _ => (true, true, true));

        for (int i = 1; i <= 4; i++)
        {
            coverage[i].HasChilling.Should().BeTrue($"seg {i} Chilling (plan type)");
            coverage[i].HasEmpowering.Should().BeTrue($"seg {i} Empowering");
            coverage[i].HasShockNova.Should().BeTrue($"seg {i} ShockNova");
            coverage[i].HasSummoning.Should().BeTrue($"seg {i} Summoning");
        }
    }

    [TestMethod]
    public void ComputeCoverage_WithoutSupportCoverage_SupportFlagsStayFalse()
    {
        var positions = CreateBranchPositions(2, spacing: 10);

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions, _ => (true, false, false), ChainParents(positions.Count));

        coverage[1].HasEmpowering.Should().BeFalse();
        coverage[1].HasShockNova.Should().BeFalse();
        coverage[1].HasSummoning.Should().BeFalse();
    }

    [TestMethod]
    public void FormatCoverageFlags_OnlyCoverageTowerColumns_ChillingSeismicShowsTwoPlusPhantom()
    {
        var chillingSeismic = new HashSet<BlightTowerType> { BlightTowerType.Chilling, BlightTowerType.Seismic };
        var allCovered = new LaneCoverageResult(
            ParentIndex: 0, IsFullyCovered: true, Midpoint: NumVector2.Zero,
            HasChilling: true, HasSeismic: true, HasFireball: true,
            HasEmpowering: true, HasShockNova: true, HasSummoning: true, IsPhantom: true);
        var none = new LaneCoverageResult(0, false, NumVector2.Zero);


        BlightCoverageFlags.Format(allCovered, chillingSeismic).Should().Be("C S P");
        BlightCoverageFlags.Format(none, chillingSeismic).Should().Be("- - -");
    }

    [TestMethod]
    public void FormatCoverageFlags_NoCoverageTowers_OnlyPhantomColumn()
    {
        var none = new LaneCoverageResult(0, false, NumVector2.Zero);
        var empty = new HashSet<BlightTowerType>();

        BlightCoverageFlags.Format(none, empty).Should().Be("-");
    }

    [TestMethod]
    public void CompactCoverageLetters_OnlyCoverageTowersPresent()
    {
        var chillingSeismic = new HashSet<BlightTowerType> { BlightTowerType.Chilling, BlightTowerType.Seismic };
        var seg = new LaneCoverageResult(
            ParentIndex: 0, IsFullyCovered: true, Midpoint: NumVector2.Zero,
            HasChilling: true, HasSeismic: false, HasFireball: true, IsPhantom: true);

        BlightCoverageFlags.Compact(seg, chillingSeismic).Should().Be("C P");

        var uncovered = new LaneCoverageResult(0, false, NumVector2.Zero);
        BlightCoverageFlags.Compact(uncovered, chillingSeismic).Should().BeEmpty();
    }

    [TestMethod]
    public void FindPumpBranches_SkipsStubChildren_DoesNotStartBranchOnConnector()
    {
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, NumVector2.Zero), // 0 orphan root
            new(0, false, new NumVector2(5, 5), IsPumpStub: true),          // 1 pump connector stub
        ];
        var positions = new List<NumVector2> { new(0, 0), new(5, 5) };

        List<PumpBranch> branches = BlightBranches.FindPumpBranches(
            coverage, new NumVector2(0, 0), positions, null);

        branches.Should().BeEmpty();
    }

    // ── Game-Id lane adjacency (the reference Blight plugin connects pathways whose entity Ids
    //    are consecutive, which is how the game encodes adjacent points on the same lane) ──

    [TestMethod]
    public void BuildCoverageChildren_ExcludesPumpStubSegments()
    {
        LaneCoverageResult[] coverage =
        [
            new(BlightLaneTopology.OrphanSentinel, false, NumVector2.Zero), // 0 root
            new(0, true, new NumVector2(10, 0)),                            // 1 real lane
            new(0, false, new NumVector2(5, 5), IsPumpStub: true),          // 2 pump stub
            new(2, false, new NumVector2(10, 10), IsPumpStub: true),        // 3 stub child
        ];

        List<List<int>> children = BlightLaneTopology.BuildCoverageChildren(coverage);

        children[0].Should().Equal(1);
        children[2].Should().BeEmpty();
    }

    [TestMethod]
    public void IsRealLaneSegment_ExcludesRootsAndPumpStubs()
    {
        var root = new LaneCoverageResult(BlightLaneTopology.OrphanSentinel, false, NumVector2.Zero);
        var lane = new LaneCoverageResult(0, true, new NumVector2(10, 0));
        var stub = new LaneCoverageResult(0, false, new NumVector2(5, 5), IsPumpStub: true);

        BlightLaneTopology.IsRealLaneSegment(root).Should().BeFalse();
        BlightLaneTopology.IsRealLaneSegment(lane).Should().BeTrue();
        BlightLaneTopology.IsRealLaneSegment(stub).Should().BeFalse();
    }

    [TestMethod]
    public void ComputeCoverage_PrecomputedParents_UsesBeamChainTree_NoStubsNoPhantoms()
    {
        // Icon-lane mode: the parent tree comes straight from the game's beam chains (pump-ward
        // neighbour). Segment 2 is the pump-ward root; 0 and 1 chain toward it. No id-run
        // re-splitting, no phantom bridging, no pump-stub removal.
        var positions = new List<NumVector2> { new(30, 0), new(20, 0), new(10, 0) };
        int[] parents = [1, 2, -1];

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions, _ => (true, false, false), parents);

        coverage[0].ParentIndex.Should().Be(1, "segment 0 chains one step toward the pump");
        coverage[1].ParentIndex.Should().Be(2, "segment 1 chains one step toward the pump");
        coverage[2].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel, "the pump-ward head is the branch root");
        coverage[0].IsPhantom.Should().BeFalse("icon mode never phantom-bridges");
        coverage[0].IsPumpStub.Should().BeFalse("icon mode never hides pump-ward segments as stubs");
    }

    [TestMethod]
    public void ComputeCoverage_PrecomputedParents_SharedStubPosition_TwoSeparateRoots()
    {
        // Two lanes (485 and 563 from the plaza dump) whose pump-ward heads sit at the SAME grid
        // position must stay two separate branch roots — the beam chains are per-lane.
        var positions = new List<NumVector2>
        {
            new(484, 140), // 0 lane A segment
            new(485, 120), // 1 lane A head
            new(562, 142), // 2 lane B segment
            new(563, 120), // 3 lane B head — same position as 485
        };
        int[] parents = [1, -1, 3, -1];

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions, _ => (true, false, false), parents);

        coverage[0].ParentIndex.Should().Be(1, "lane A chains to its own head");
        coverage[1].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel, "lane A head is a branch root");
        coverage[2].ParentIndex.Should().Be(3, "lane B chains to its own head");
        coverage[3].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel, "lane B head is its own branch root");
        int roots = coverage.Count(r => r.ParentIndex == BlightLaneTopology.OrphanSentinel);
        roots.Should().Be(2, "shared-position pump heads stay separate lanes");
    }

    [TestMethod]
    public void ComputeCoverage_ConvergenceJunction_ProbesEveryIncomingBeam()
    {
        // The reported bug: B.19 (0) and C.11 (1) both end at B.20's start (2) — a convergence
        // junction. The tree keeps only the primary parent (0), but BOTH incoming beams are real
        // walkable segments: a tower on the C.11 -> B.20 beam must count as covering the junction
        // even though C.11 is not the tree parent. allParents feeds the web into coverage.
        var positions = new List<NumVector2>
        {
            new(0, 0),    // 0 B.19 start
            new(0, 10),   // 1 C.11 start
            new(5, 5),    // 2 B.20 start (junction) — midpoints: (2.5,2.5) to 0, (2.5,7.5) to 1
        };
        int[] parents = [-1, -1, 0];
        int[][] allParents = [[], [], [0, 1]];

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions,
            midpoint => (midpoint.Y > 5f ? (true, false, false) : (false, false, false)),
            parents,
            null,
            allParents);

        coverage[2].HasChilling.Should().BeTrue("a tower on the C.11->B.20 beam covers the junction");
        coverage[2].ParentIndex.Should().Be(0, "the tree parent stays the primary for propagation");
    }

    private static LaneCoverageResult[] ComputeCoverage(
        List<NumVector2> positions,
        Func<NumVector2, (bool chilling, bool seismic, bool fireball)> getCoverage,
        IReadOnlyList<int> parents)
        => BlightLaneTopology.ComputeCoverage(positions, getCoverage, parents);

    private static int[] ChainParents(int count)
    {
        int[] parents = new int[count];
        for (int i = 1; i < count; i++)
            parents[i] = i - 1;
        parents[0] = -1;
        return parents;
    }

    private static List<NumVector2> CreateBranchPositions(int segmentCount, int spacing)
    {
        var positions = new List<NumVector2>(segmentCount + 1);
        positions.Add(new(0, 0)); // orphan root
        for (int i = 1; i <= segmentCount; i++)
            positions.Add(new(i * spacing, i * spacing));
        return positions;
    }

    private static List<NumVector2> CreateDivergingPositions(
        int trunkLen, int leftLen, int rightLen, int spacing, int branchGap)
    {
        var positions = new List<NumVector2>();
        // Orphan root
        positions.Add(new(0, 0));
        // Trunk
        for (int i = 1; i <= trunkLen; i++)
            positions.Add(new(i * spacing, i * spacing));
        // Left sub-branch (AA) — continues from trunk
        for (int i = 1; i <= leftLen; i++)
            positions.Add(new((trunkLen + i) * spacing, (trunkLen + i) * spacing));
        // Right sub-branch (AB) — offset to create a divergence
        float rightBaseY = trunkLen * spacing + branchGap;
        for (int i = 1; i <= rightLen; i++)
            positions.Add(new((trunkLen + i) * spacing, rightBaseY + i * spacing));
        return positions;
    }
}
