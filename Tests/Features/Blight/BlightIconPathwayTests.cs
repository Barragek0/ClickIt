namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightIconPathwayTests
{
    private static BlightPathwayIcon Icon(int id, float sx, float sy, float ex, float ey, int visual = 2)
        => new(
            id,
            new NumVector2(sx / 10f, sy / 10f),
            visual,
            new System.Numerics.Vector3(sx, sy, -281f),
            new System.Numerics.Vector3(ex, ey, -281f),
            []);

    [TestMethod]
    public void ComputePathwayLinks_ChainsConsecutiveSegments()
    {
        BlightPathwayIcon[] icons =
        [
            Icon(0, 0f, 0f, 10f, 0f),    // pump end (stub)
            Icon(1, 10f, 0f, 20f, 0f),
            Icon(2, 20f, 0f, 30f, 0f),   // far end
        ];

        int[] links = BlightEntityCache.ComputePathwayLinks(icons, ref linksScratch);

        links[0].Should().Be(-1, "the pump-end stub has no icon whose BeamEnd matches its start");
        links[1].Should().Be(0, "the middle segment chains to the stub ending at its start");
        links[2].Should().Be(1, "the far segment chains to the segment ending at its start");
    }

    [TestMethod]
    public void ComputePathwayLinks_SharedPumpStub_YieldsSeparateLanes()
    {
        BlightPathwayIcon[] icons =
        [
            Icon(484, 6212f, 14044f, 6125f, 14125f),
            Icon(485, 6299f, 13962f, 6212f, 14044f), // stub A — same start as 563
            Icon(562, 6347f, 14042f, 6398f, 14116f),
            Icon(563, 6299f, 13962f, 6347f, 14042f), // stub B — same start as 485
        ];

        int[] links = BlightEntityCache.ComputePathwayLinks(icons, ref linksScratch);

        links[0].Should().Be(1, "lane A's segment 484 chains to its own stub 485");
        links[1].Should().Be(-1, "stub A (485) terminates - no icon ends at its start");
        links[2].Should().Be(3, "lane B's segment 562 chains to its own stub 563");
        links[3].Should().Be(-1, "stub B (563) terminates - no icon ends at its start");
    }

    [TestMethod]
    public void ComputePathwayLinks_SingleIcon_NoLink()
    {
        BlightPathwayIcon[] icons = [Icon(7, 100f, 100f, 120f, 100f)];

        int[] links = BlightEntityCache.ComputePathwayLinks(icons, ref linksScratch);

        links[0].Should().Be(-1);
    }

    [TestMethod]
    public void ComputePathwayLinks_ForkWithSharedEnd_ResolvesToOneArmWithoutLoop()
    {
        BlightPathwayIcon[] icons =
        [
            Icon(0, 30f, 30f, 50f, 50f),
            Icon(1, 50f, 50f, 70f, 50f),
            Icon(2, 50f, 50f, 70f, 70f),
        ];

        int[] links = BlightEntityCache.ComputePathwayLinks(icons, ref linksScratch);

        links[0].Should().Be(-1, "the shared segment's start has no icon ending there");
        links[1].Should().Be(0, "arm 1 chains back to the shared segment");
        links[2].Should().Be(1, "the co-located arm twin chains to the fork node, not a loop");
    }

    [TestMethod]
    public void ComputePathwayLinks_GapCloseLinksNearlyMatchingBeamEnd()
    {
        BlightPathwayIcon[] icons =
        [
            Icon(0, 0f, 0f, 10f, 0f),
            Icon(1, 10f, 0f, 20f, 0f),
            Icon(2, 20.6f, 0.2f, 30f, 0f),   // start 20.06,0.02 - close to icon 1's end (20,0)
        ];

        int[] links = BlightEntityCache.ComputePathwayLinks(icons, ref linksScratch);

        links[0].Should().Be(-1);
        links[1].Should().Be(0, "the exact match still chains normally");
        links[2].Should().Be(1, "the nearly-matching beam end is gap-closed to the previous segment");
    }

    [TestMethod]
    public void DedupePathwayIcons_RemovesExactDuplicates_KeepsSharedPumpStubs()
    {
        List<BlightPathwayIcon> icons =
        [
            Icon(1, 0f, 0f, 10f, 0f),
            Icon(2, 0f, 0f, 10f, 0f),   // exact duplicate of 1
            Icon(3, 0f, 0f, 0f, 10f),   // same start, different beam (shared stub)
        ];

        List<BlightPathwayIcon> result = BlightEntityCache.DedupePathwayIcons(icons);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1);
        result[1].Id.Should().Be(3);
    }

    [TestMethod]
    public void DedupePathwayIcons_PrefersActiveIcon_WhenDeadDuplicateCollides()
    {
        List<BlightPathwayIcon> icons =
        [
            Icon(1, 0f, 0f, 10f, 0f, visual: 3),
            Icon(2, 0f, 0f, 10f, 0f, visual: 2),
        ];

        List<BlightPathwayIcon> result = BlightEntityCache.DedupePathwayIcons(icons);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(2);
        result[0].IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void ComputePathwayLinks_CoLocatedTwin_WithSameParent_MergesIntoNode()
    {
        BlightPathwayIcon[] icons =
        [
            Icon(0, 0f, 0f, 10f, 0f),
            Icon(1, 10f, 0f, 20f, 0f),
            Icon(2, 20f, 0f, 30f, 0f),   // fork node arm A
            Icon(3, 20f, 0f, 20f, 10f),  // fork node arm B - same position, same parent as 2
        ];

        int[] links = BlightEntityCache.ComputePathwayLinks(icons, ref linksScratch);

        links[0].Should().Be(-1);
        links[1].Should().Be(0);
        links[2].Should().Be(1, "the fork node keeps the incoming edge");
        links[3].Should().Be(2, "the co-located twin becomes a zero-length child of the node");
    }

    [TestMethod]
    public void ComputePathwayParents_ConvergenceJunction_KeepsEveryIncomingBeam()
    {
        BlightPathwayIcon[] icons =
        [
            Icon(795, 1475.1f, 999.4f, 1483.5f, 989.5f),  // B.19 -> ends at junction
            Icon(702, 1470.8f, 986.5f, 1483.5f, 989.5f),  // C.11 -> also ends at junction
            Icon(794, 1483.5f, 989.5f, 1496.9f, 997.9f),  // B.20 -> starts at junction
        ];

        int[][] parents = BlightEntityCache.ComputePathwayParents(icons);

        parents[2].Should().HaveCount(2, "both incoming beams become parents at the junction");
        parents[2][0].Should().Be(0, "the primary (tree) parent is the first beam in id order");
        parents[2][1].Should().Be(1, "the convergence beam is kept as an extra parent");
    }

    [TestMethod]
    public void PathwayIcon_IsActive_FollowsVisualState()
    {
        // visual 1 (spawned, not yet sending) and 2 (actively sending) show the lane; visual 3 (all enemies sent) and unreadable 0 hide it.
        Icon(1, 0f, 0f, 10f, 0f, visual: 1).IsActive.Should().BeTrue();
        Icon(2, 0f, 0f, 10f, 0f, visual: 2).IsActive.Should().BeTrue();
        Icon(3, 0f, 0f, 10f, 0f, visual: 3).IsActive.Should().BeFalse();
        Icon(4, 0f, 0f, 10f, 0f, visual: 0).IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void ComputePathwayLinks_ConvergenceJunction_PrimaryIsFirstIncomingBeam()
    {
        BlightPathwayIcon[] icons =
        [
            Icon(795, 1475.1f, 999.4f, 1483.5f, 989.5f),  // B.19
            Icon(702, 1470.8f, 986.5f, 1483.5f, 989.5f),  // C.11
            Icon(794, 1483.5f, 989.5f, 1496.9f, 997.9f),  // B.20
        ];

        int[] links = BlightEntityCache.ComputePathwayLinks(icons, ref linksScratch);

        links[2].Should().Be(0, "the tree parent is the first incoming beam (B.19)");
    }

    [TestMethod]
    public void ComputePathwayParents_BreaksCycle_ByConnectingToRootedSecondaryParent()
    {
        BlightPathwayIcon[] icons =
        [
            Icon(1, 10f, 0f, 30f, 0f),   // cycle: ends at 2's start
            Icon(2, 30f, 0f, 40f, 0f),   // cycle: ends at 3's start; R also ends at its start
            Icon(3, 40f, 0f, 10f, 0f),   // cycle: ends at 1's start (closes the loop)
            Icon(4, 0f, 0f, 30f, 0f),    // R: rooted tree, ends at 2's start
        ];

        int[][] parents = BlightEntityCache.ComputePathwayParents(icons);

        parents[1][0].Should().Be(3, "the loop connects to the rooted tree via the secondary parent");
        parents[1].Should().Contain(0, "the original cycle parent is kept as an extra beam edge");
        parents[0][0].Should().Be(2, "the rest of the cycle still chains toward the re-pointed node");
        parents[2][0].Should().Be(1);
        parents[3].Should().BeEmpty("R stays an orphan root");
    }

    [TestMethod]
    public void ComputePathwayParents_OrphansClosingNode_WhenCycleHasNoRootedEscape()
    {
        BlightPathwayIcon[] icons =
        [
            Icon(1, 10f, 0f, 30f, 0f),
            Icon(2, 30f, 0f, 40f, 0f),
            Icon(3, 40f, 0f, 10f, 0f),
        ];

        int[][] parents = BlightEntityCache.ComputePathwayParents(icons);

        parents.Count(p => p.Length == 0).Should().Be(1, "the loop is broken into one rooted tree");
        for (int i = 0; i < icons.Length; i++)
        {
            HashSet<int> seen = [];
            int cur = i;
            while (cur >= 0 && seen.Add(cur))
                cur = parents[cur].Length > 0 ? parents[cur][0] : -1;
            cur.Should().Be(-1, "every chain terminates at a root with no cycle");
        }
    }

    [TestMethod]
    public void ComputePathwayParents_AcyclicTree_IsLeftUnchanged()
    {
        BlightPathwayIcon[] icons =
        [
            Icon(1, 0f, 0f, 10f, 0f),   // root
            Icon(2, 10f, 0f, 20f, 0f),  // child
            Icon(3, 20f, 0f, 30f, 0f),  // grandchild
        ];

        int[][] parents = BlightEntityCache.ComputePathwayParents(icons);

        parents[0].Should().BeEmpty("the root has no pump-ward parent");
        parents[1].Should().Equal(0);
        parents[2].Should().Equal(1);
    }

    private static int[]? linksScratch;
}
