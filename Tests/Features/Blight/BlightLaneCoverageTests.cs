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
            midpoint => midpoint.Y < 25 ? (true, false, false) : (false, false, false));

        for (int i = 1; i <= 4; i++)
            coverage[i].HasChilling.Should().BeTrue($"segment {i} should have Chilling");
    }

    [TestMethod]
    public void SimpleBranch_TwoTypesOnSameBranch_AllGreen()
    {
        var positions = CreateBranchPositions(5, spacing: 10);

        LaneCoverageResult[] coverage = ComputeCoverage(positions,
            _ => (true, true, false));

        for (int i = 1; i <= 4; i++)
        {
            coverage[i].HasChilling.Should().BeTrue($"segment {i} Chilling");
            coverage[i].HasSeismic.Should().BeTrue($"segment {i} Seismic");
        }
    }

    [TestMethod]
    public void Divergence_BothChildrenBothTypes_ParentGreen()
    {
        var positions = CreateDivergingPositions(trunkLen: 3, leftLen: 2, rightLen: 2, spacing: 10, branchGap: 40);

        LaneCoverageResult[] coverage = ComputeCoverage(positions,
            _ => (true, true, false));

        for (int i = 1; i <= 7; i++)
        {
            coverage[i].HasChilling.Should().BeTrue($"seg {i} Chilling");
            coverage[i].HasSeismic.Should().BeTrue($"seg {i} Seismic");
        }
    }

    [TestMethod]
    public void DistanceConnection_PreSpawnedEntities_Distance35Connects()
    {
        var positions = new List<NumVector2>
        {
            new(100, 100),
            new(100, 130),
            new(100, 165),
        };

        LaneCoverageResult[] coverage = ComputeCoverage(positions,
            _ => (true, false, false), segmentConnectDistance: 35f);

        coverage[1].ParentIndex.Should().Be(0, "dist 30 within 35");
        coverage[2].ParentIndex.Should().Be(1, "dist 35 on boundary");
    }

    [TestMethod]
    public void DistanceConnection_Distance36_Disconnected()
    {
        var positions = new List<NumVector2>
        {
            new(100, 100),
            new(100, 136),
        };

        LaneCoverageResult[] coverage = ComputeCoverage(positions,
            _ => (true, false, false), segmentConnectDistance: 35f, phantomConnectDistance: 35f);

        coverage[1].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel,
            "dist 36 exceeds both the connect and phantom distances");
    }

    [TestMethod]
    public void CoalescingEpsilon_SamePosition_Connected()
    {
        var positions = new List<NumVector2>
        {
            new(50, 50),
            new(50, 50),
        };

        LaneCoverageResult[] coverage = ComputeCoverage(positions,
            _ => (true, false, false));

        coverage[1].ParentIndex.Should().Be(0, "same pos should connect");
    }

    [TestMethod]
    public void DownwardPropagation_Chain_SingleTowerCoversAll()
    {
        var positions = new List<NumVector2>
        {
            new(0, 0), new(10, 10), new(20, 20), new(30, 30), new(40, 40), new(50, 50),
        };

        LaneCoverageResult[] coverage = ComputeCoverage(positions,
            midpoint => (midpoint.X == 25f && midpoint.Y == 25f) ? (true, false, false) : (false, false, false));

        for (int i = 1; i <= 4; i++)
            coverage[i].HasChilling.Should().BeTrue($"seg {i} HasChilling={coverage[i].HasChilling}");
    }

    [TestMethod]
    public void Fork_SeismicOnOneBranchOnly_TrunkAndNewBranchUncoveredForSeismic()
    {
        var positions = new List<NumVector2>
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
            positions,
            midpoint => (
                chilling: midpoint.Y < 30f,                       // Chilling tower on the trunk
                seismic: midpoint.Y == 30f && midpoint.X >= 35f,  // Seismic tower on the main branch only
                fireball: false),
            pumpGridPosition: new NumVector2(0, 0));


        for (int i = 1; i <= 3; i++)
        {
            coverage[i].HasChilling.Should().BeTrue($"trunk seg {i} Chilling");
            coverage[i].HasSeismic.Should().BeFalse($"trunk seg {i} must not inherit Seismic from a single fork branch");
        }
        coverage[4].HasChilling.Should().BeTrue();
        coverage[4].HasSeismic.Should().BeTrue();
        coverage[5].HasChilling.Should().BeTrue();
        coverage[5].HasSeismic.Should().BeTrue();
        coverage[6].HasChilling.Should().BeTrue();
        coverage[6].HasSeismic.Should().BeFalse();
        coverage[7].HasChilling.Should().BeTrue();
        coverage[7].HasSeismic.Should().BeFalse();
    }

    [TestMethod]
    public void WindingLane_HairpinLocalMinimum_StaysOneChain()
    {

        var positions = new List<NumVector2>
        {
            new(10, 10), // 0 root
            new(20, 10), // 1
            new(30, 10), // 2
            new(30, 20), // 3 — local minimum of pump distance
            new(30, 30), // 4
            new(20, 30), // 5
            new(10, 30), // 6
        };

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions, _ => (false, false, false), pumpGridPosition: new NumVector2(0, 0));

        for (int i = 1; i <= 6; i++)
            coverage[i].ParentIndex.Should().BeGreaterThanOrEqualTo(0,
                $"point {i} must stay attached to the single lane chain");
        coverage[3].ParentIndex.Should().Be(2,
            "the local-minimum point attaches to the point before it on the lane");


        LaneCoverageResult[] covered = BlightLaneTopology.ComputeCoverage(
            positions,
            midpoint => (midpoint.X == 30f && midpoint.Y == 15f) ? (true, false, false) : (false, false, false),
            pumpGridPosition: new NumVector2(0, 0));
        for (int i = 1; i <= 6; i++)
            covered[i].HasChilling.Should().BeTrue($"segment {i} inherits coverage through the single chain");
    }

    [TestMethod]
    public void PhantomLane_GapWithinPhantomDistance_BridgesDisconnectedChain()
    {
        var positions = new List<NumVector2>
        {
            new(0, 0),     // 0 main root (pump)
            new(10, 10),   // 1
            new(20, 20),   // 2
            new(30, 30),   // 3 main end
            new(80, 80),   // 4 orphan chain start — 70.7 from (30,30): beyond 35, within 100
            new(90, 90),   // 5
            new(100, 100), // 6
        };

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions,
            midpoint => midpoint.X < 50f ? (true, false, false) : (false, false, false),
            segmentConnectDistance: 35f,
            pumpGridPosition: new NumVector2(0, 0));

        coverage[5].ParentIndex.Should().Be(4, "chain points link internally");
        coverage[6].ParentIndex.Should().Be(5, "chain points link internally");
        coverage[5].IsPhantom.Should().BeFalse("internal chain edges are real lanes");
        coverage[6].IsPhantom.Should().BeFalse("internal chain edges are real lanes");

        coverage[4].ParentIndex.Should().Be(3, "the chain is bridged to the nearest connected point");
        coverage[4].IsPhantom.Should().BeTrue("the bridge across the gap is a phantom edge");


        for (int i = 1; i <= 6; i++)
            coverage[i].HasChilling.Should().BeTrue($"segment {i} covered through the phantom lane");
    }

    [TestMethod]
    public void PhantomLane_GapBeyondPhantomDistance_ChainStaysOrphaned()
    {
        var positions = new List<NumVector2>
        {
            new(0, 0),     // 0 main root
            new(10, 10),   // 1
            new(200, 200), // 2 — 268 from (10,10): beyond the phantom distance
            new(210, 210), // 3
        };

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions,
            _ => (true, false, false),
            segmentConnectDistance: 35f,
            pumpGridPosition: new NumVector2(0, 0));

        coverage[2].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel,
            "beyond the phantom distance — not bridged");
        coverage[2].IsPhantom.Should().BeFalse();
        coverage[3].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel,
            "the whole chain stays orphaned when it cannot be bridged");
    }

    [TestMethod]
    public void PhantomLane_ForkedSubLane_InheritsCoverageThroughBridge()
    {
        var positions = new List<NumVector2>
        {
            new(0, 0),      // 0 root (pump)
            new(10, 0),     // 1  branch A
            new(20, 0),     // 2  branch A
            new(30, 0),     // 3  branch A end (bridgeK)
            new(90, 0),     // 4  phantom bridge (bridgeJ) — gap 60: within 100
            new(100, 0),    // 5  sub-lane
            new(110, 0),    // 6  sub-lane fork
            new(120, 0),    // 7  sub-branch X
            new(120, 10),   // 8  sub-branch Y
        };

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions,
            midpoint => (
                chilling: midpoint.X < 40f,  // towers cover the main chain only
                seismic: midpoint.X < 40f,
                fireball: false),
            pumpGridPosition: new NumVector2(0, 0));

        coverage[4].ParentIndex.Should().Be(3, "the bridge attaches to the main chain's end");
        coverage[4].IsPhantom.Should().BeTrue();
        for (int i = 5; i <= 8; i++)
            coverage[i].IsPhantom.Should().BeFalse("internal sub-lane edges are real lanes");

        for (int i = 1; i <= 8; i++)
        {
            coverage[i].HasChilling.Should().BeTrue($"segment {i} inherits Chilling through the phantom lane");
            coverage[i].HasSeismic.Should().BeTrue($"segment {i} inherits Seismic through the phantom lane");
        }
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
            pumpGridPosition: new NumVector2(0, 0),
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
            positions, _ => (true, false, false), pumpGridPosition: new NumVector2(0, 0));

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
    public void ComputeCoverage_PumpStubSpur_IsMarkedNotALane()
    {
        var positions = new List<NumVector2>
        {
            new(0, 0),     // 0 root (pump)
            new(30, 0),    // 1 real lane start (at the radius edge, but its subtree extends far)
            new(70, 0),    // 2 real lane continues well beyond the stub radius
            new(-8, 8),    // 3 spur start
            new(-16, 16),  // 4 spur leaf
        };

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions, _ => (true, false, false), pumpGridPosition: new NumVector2(0, 0));

        coverage[3].IsPumpStub.Should().BeTrue("the pump-object spur is not a monster lane");
        coverage[4].IsPumpStub.Should().BeTrue();
        coverage[1].IsPumpStub.Should().BeFalse("the real lane's near-pump segment is still a lane");
        coverage[2].IsPumpStub.Should().BeFalse();
    }

    [TestMethod]
    public void ComputeCoverage_NoPump_NoSegmentsMarkedStub()
    {
        var positions = CreateBranchPositions(3, spacing: 10);

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions, _ => (true, false, false));

        for (int i = 0; i < coverage.Length; i++)
            coverage[i].IsPumpStub.Should().BeFalse();
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
    public void ComputeCoverage_PumpNearLanes_NonAdjacentIds_SplitIntoSeparateBranchRoots()
    {
        // Two lanes radiate from the pump within the connect distance (the greedy tree would merge
        // them into one branch root). The game's lane ids are per-lane runs — adjacent ids = same
        // lane — so non-adjacent pump-near ids must split into separate orphan roots (branch starts).
        var positions = new List<NumVector2>
        {
            new(5, 0),    // 0 lane A start (pump-near)
            new(15, 0),   // 1 lane A
            new(25, 0),   // 2 lane A
            new(5, 20),   // 3 lane B start (pump-near)
            new(15, 20),  // 4 lane B
            new(25, 20),  // 5 lane B
        };
        long[] ids = [100, 99, 98, 50, 49, 48];

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions, _ => (true, false, false), pumpGridPosition: new NumVector2(0, 0), ids: ids);

        coverage[0].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel, "lane A's pump-near segment is its own branch root");
        coverage[3].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel, "lane B's pump-near segment is its own branch root (non-adjacent id)");
        coverage[1].ParentIndex.Should().Be(0, "lane A continues along its run");
        coverage[4].ParentIndex.Should().Be(3, "lane B continues along its run");
    }

    [TestMethod]
    public void ComputeCoverage_SinglePumpLane_AdjacentIds_StaysOneBranchRoot()
    {
        // A single lane whose pump-near segments carry adjacent ids must NOT be split into spurious
        // branches (the greedy chain is the one lane).
        var positions = new List<NumVector2>
        {
            new(5, 0),    // 0 start
            new(15, 0),   // 1
            new(25, 0),   // 2
        };
        long[] ids = [100, 99, 98];

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions, _ => (true, false, false), pumpGridPosition: new NumVector2(0, 0), ids: ids);

        coverage[0].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel, "the single lane's start is the one branch root");
        coverage[1].ParentIndex.Should().Be(0, "adjacent id — same lane, no split");
        coverage[2].ParentIndex.Should().Be(1);
    }

    [TestMethod]
    public void ComputeCoverage_PumpNearLanes_WithoutIds_KeepsLanesSeparate()
    {
        // ids == null: the connectivity graph keeps two parallel pump-near lanes SEPARATE (each is
        // its own branch) instead of the old greedy tree falsely merging them — a tower never
        // covers the other lane just because the lanes run near each other.
        var positions = new List<NumVector2>
        {
            new(5, 0), new(15, 0), new(25, 0), new(5, 20), new(15, 20), new(25, 20),
        };

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions, _ => (true, false, false), pumpGridPosition: new NumVector2(0, 0));

        coverage[0].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel);
        coverage[3].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel,
            "two parallel lanes stay separate branches; no false cross-lane merge");
    }

    [TestMethod]
    public void ComputeCoverage_FourLaneHub_RealCourthouseGeometry_SplitsIntoFourBranchRoots()
    {
        // Replicates the Courthouse 83 capture: four id-runs all starting at the shared hub (625,502)
        // and fanning around the pump (607,522). The old greedy tree merged all four into one branch;
        // the id-lane structure must root each lane at its own pump-closest segment.
        var positions = new List<NumVector2>
        {
            new(625, 502), // 0 lane A hub (id 1141)
            new(617, 510), // 1
            new(609, 517), // 2 lane A root — 5.4 from pump
            new(596, 509), // 3
            new(588, 503), // 4
            new(575, 494), // 5
            new(625, 502), // 6 lane B hub (id 1044)
            new(627, 517), // 7 lane B root — 20.6 from pump
            new(629, 527), // 8
            new(632, 540), // 9
            new(627, 553), // 10
            new(627, 562), // 11
            new(625, 502), // 12 lane C hub (id 935)
            new(621, 517), // 13 lane C root — 14.9 from pump
            new(627, 530), // 14
            new(627, 539), // 15
            new(621, 552), // 16
            new(625, 502), // 17 lane D hub (id 802)
            new(617, 498), // 18 lane D root — 26.0 from pump
            new(609, 494), // 19
            new(615, 481), // 20
        };
        long[] ids =
        [
            1141, 1140, 1139, 1138, 1137, 1136,
            1044, 1043, 1042, 1041, 1040, 1039,
            935, 934, 933, 932, 931,
            802, 801, 800, 799,
        ];

        LaneCoverageResult[] coverage = BlightLaneTopology.ComputeCoverage(
            positions, _ => (true, false, false), pumpGridPosition: new NumVector2(607, 522), ids: ids);

        coverage[2].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel, "lane A roots at its pump-closest segment (609,517)");
        coverage[13].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel, "merged lane BC roots at (621,517)");
        coverage[18].ParentIndex.Should().Be(BlightLaneTopology.OrphanSentinel, "lane D roots at (617,498)");

        // Lanes B and C run alongside each other (parallel rows of one game lane) — they merge into
        // one branch, so B's pump-closest segment (627,517) is NOT a separate branch root.
        coverage[7].ParentIndex.Should().NotBe(BlightLaneTopology.OrphanSentinel, "lane B merges into lane C");

        int orphans = coverage.Count(r => r.ParentIndex == BlightLaneTopology.OrphanSentinel);
        orphans.Should().Be(3, "three real lanes at the pump → three branch roots (A, merged BC, D)");

        // Each lane chains along its own id run and never cross-merges into a neighbour lane.
        coverage[1].ParentIndex.Should().Be(2, "lane A continues toward its root");
        coverage[3].ParentIndex.Should().Be(2, "lane A's other side chains back to the root");
        coverage[8].ParentIndex.Should().Be(7, "lane B chains to its own pump-closest segment");
        coverage[14].ParentIndex.Should().Be(13, "lane C chains to its own root, not lane B's");
        coverage[19].ParentIndex.Should().Be(18, "lane D chains to its own root");
    }

    // ── Bridge coverage propagation (a bridged dead-end is one lane with its target arm) ──

    // Fork at node 1 (pump-rooted): arm A = 1→2 (dead-end 2), arm B = 1→3→4 (dead-end 4, leaving
    // the junction exactly straight so the connectivity graph keeps the fork); dead-end 2 bridges
    // to dead-end 4.
    private static LaneCoverageResult[] ComputeCoverage(
        List<NumVector2> positions,
        Func<NumVector2, (bool chilling, bool seismic, bool fireball)> getCoverage,
        float segmentConnectDistance = 35f,
        float phantomConnectDistance = BlightLaneTopology.PhantomConnectDistance)
    {
        return BlightLaneTopology.ComputeCoverage(positions, getCoverage, segmentConnectDistance,
            phantomConnectDistance: phantomConnectDistance);
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
