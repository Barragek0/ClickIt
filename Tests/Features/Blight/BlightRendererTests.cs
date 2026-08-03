namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightRendererTests
{
    [TestMethod]
    public void GetFoundationColour_UnbuiltFoundation_IsDefaultGrey()
    {
        Color dot = new TestStrategy().GetFoundationColour(hasTower: false, BlightTowerType.Chilling);
        dot.Should().Be(IBlightTowerStrategy.DefaultFoundationColour, "an unbuilt foundation dot top half is grey, regardless of planned type");
    }

    [TestMethod]
    public void GetFoundationColour_BuiltTower_UsesStrategyTowerColour()
    {
        Color dot = new TestStrategy().GetFoundationColour(hasTower: true, BlightTowerType.Seismic);
        dot.Should().Be(TestStrategy.SeismicColor, "a built tower dot uses the strategy colour for its current type");
    }

    [TestMethod]
    public void GetFoundationOutline_UsesPlannedTowerTypeColour()
    {
        Color planned = new TestStrategy().GetFoundationOutline(BlightTowerType.Fireball);
        planned.Should().Be(TestStrategy.FireballColor, "the bottom half represents the tower type the plan will build there");
    }

    [TestMethod]
    public void GetTowerRangeColor_DefaultsToPerTypePalette()
    {
        IBlightTowerStrategy strategy = new TestStrategy();
        strategy.GetTowerRangeColor(BlightTowerType.Chilling)
            .Should().Be(IBlightTowerStrategy.DefaultTowerColor(BlightTowerType.Chilling));
        strategy.GetTowerRangeColor(BlightTowerType.Seismic)
            .Should().Be(IBlightTowerStrategy.DefaultTowerColor(BlightTowerType.Seismic));
        strategy.GetTowerRangeColor(BlightTowerType.Fireball)
            .Should().Be(IBlightTowerStrategy.DefaultTowerColor(BlightTowerType.Fireball));
    }

    private static readonly NumVector2 ChillingPos = new(1193, 359);
    private static readonly NumVector2 SeismicPos = new(1268, 348);
    private static readonly NumVector2 FireballPos = new(1305, 342);

    private static BlightPlan BuildScenarioPlan()
        => new(
            new BlightPlanStep[]
            {
                new(BlightPlanAction.Build, ChillingPos, BlightTowerType.Chilling, 1),
                new(BlightPlanAction.Build, SeismicPos, BlightTowerType.Seismic, 1),
                new(BlightPlanAction.Upgrade, ChillingPos, BlightTowerType.Chilling, 2),
                new(BlightPlanAction.Upgrade, ChillingPos, BlightTowerType.Chilling, 3),
                new(BlightPlanAction.Upgrade, SeismicPos, BlightTowerType.Seismic, 2),
                new(BlightPlanAction.Upgrade, SeismicPos, BlightTowerType.Seismic, 3),
                new(BlightPlanAction.Build, FireballPos, BlightTowerType.Fireball, 1),
            },
            version: 5,
            debugSummary: "scenario",
            currentStepIndex: 0);

    [TestMethod]
    public void PendingPlanStepNumbers_ReturnsEmpty_ForNullPlan()
    {
        BlightRenderer.PendingPlanStepNumbers(plan: null, cursor: 0, ChillingPos).Should().BeEmpty();
    }

    [TestMethod]
    public void PendingPlanStepNumbers_ReturnsAllStepsForFoundation_AtCursorZero()
    {
        IReadOnlyList<int> chilling = BlightRenderer.PendingPlanStepNumbers(BuildScenarioPlan(), cursor: 0, ChillingPos);
        chilling.Should().Equal(new[] { 1, 3, 4 }, "build then two upgrades, in plan order");

        IReadOnlyList<int> seismic = BlightRenderer.PendingPlanStepNumbers(BuildScenarioPlan(), cursor: 0, SeismicPos);
        seismic.Should().Equal(new[] { 2, 5, 6 }, "build then two upgrades, interleaved with the chilling tower");

        IReadOnlyList<int> fireball = BlightRenderer.PendingPlanStepNumbers(BuildScenarioPlan(), cursor: 0, FireballPos);
        fireball.Should().Equal(new[] { 7 }, "only the build step is pending at cursor 0");
    }

    [TestMethod]
    public void PendingPlanStepNumbers_SkipsCompletedSteps_BeforeCursor()
    {
        IReadOnlyList<int> chilling = BlightRenderer.PendingPlanStepNumbers(BuildScenarioPlan(), cursor: 1, ChillingPos);
        chilling.Should().Equal(new[] { 3, 4 }, "the completed build step is dropped once the cursor passes it");

        IReadOnlyList<int> seismic = BlightRenderer.PendingPlanStepNumbers(BuildScenarioPlan(), cursor: 1, SeismicPos);
        seismic.Should().Equal(new[] { 2, 5, 6 }, "seismic steps are untouched by the chilling build completing");
    }

    [TestMethod]
    public void PendingPlanStepNumbers_ReturnsEmpty_WhenAllStepsDone()
    {
        IReadOnlyList<int> fireball = BlightRenderer.PendingPlanStepNumbers(BuildScenarioPlan(), cursor: 7, FireballPos);
        fireball.Should().BeEmpty("once every step has run, no numbers remain");
    }

    [TestMethod]
    public void PendingPlanStepNumbers_IgnoresPositionsNotInPlan()
    {
        IReadOnlyList<int> numbers = BlightRenderer.PendingPlanStepNumbers(
            BuildScenarioPlan(), cursor: 0, new NumVector2(1, 1));
        numbers.Should().BeEmpty();
    }

    [TestMethod]
    public void GetPendingPlanStepNumbers_MatchesStaticSemantics_AndReusesCachedList()
    {
        var renderer = new BlightRenderer(new BlightService(new ClickItSettings()), new ClickItSettings());
        BlightPlan plan = BuildScenarioPlan();

        IReadOnlyList<int> first = renderer.GetPendingPlanStepNumbers(plan, cursor: 1, SeismicPos);
        first.Should().Equal(new[] { 2, 5, 6 }, "same result as the static tolerance scan from cursor 1");

        // Re-querying the same (plan, cursor, position) returns the cached instance — no per-frame allocation.
        IReadOnlyList<int> second = renderer.GetPendingPlanStepNumbers(plan, cursor: 1, SeismicPos);
        ReferenceEquals(first, second).Should().BeTrue("pending numbers are cached per (plan, cursor)");

        // Advancing the cursor rebuilds the cache, so a different instance comes back.
        IReadOnlyList<int> afterAdvance = renderer.GetPendingPlanStepNumbers(plan, cursor: 3, SeismicPos);
        afterAdvance.Should().Equal(new[] { 5, 6 });
        ReferenceEquals(first, afterAdvance).Should().BeFalse("a new cursor produces a fresh list");
    }

    [TestMethod]
    public void GetPendingPlanStepNumbers_ToleranceFallback_IsCachedToo()
    {
        var renderer = new BlightRenderer(new BlightService(new ClickItSettings()), new ClickItSettings());
        BlightPlan plan = BuildScenarioPlan();
        NumVector2 fuzzyPos = new(SeismicPos.X + 0.5f, SeismicPos.Y);

        IReadOnlyList<int> fuzzy = renderer.GetPendingPlanStepNumbers(plan, cursor: 1, fuzzyPos);
        fuzzy.Should().Equal(new[] { 2, 5, 6 }, "positions within the <1 grid-unit tolerance still match");

        IReadOnlyList<int> fuzzyAgain = renderer.GetPendingPlanStepNumbers(plan, cursor: 1, fuzzyPos);
        ReferenceEquals(fuzzy, fuzzyAgain).Should().BeTrue("the tolerance fallback result is cached too");
    }

    [TestMethod]
    public void GetPendingPlanStepNumbers_ReturnsSharedEmpty_ForNullPlan()
    {
        var renderer = new BlightRenderer(new BlightService(new ClickItSettings()), new ClickItSettings());

        IReadOnlyList<int> numbers = renderer.GetPendingPlanStepNumbers(plan: null, cursor: 0, SeismicPos);
        numbers.Should().BeEmpty();
    }

    [TestMethod]
    public void IsCurrentStepAt_ReturnsFalse_ForNullPlanOrDonePlan()
    {
        BlightRenderer.IsCurrentStepAt(plan: null, cursor: 0, ChillingPos).Should().BeFalse();

        BlightRenderer.IsCurrentStepAt(BuildScenarioPlan(), cursor: 7, ChillingPos).Should().BeFalse();
    }

    [TestMethod]
    public void IsCurrentStepAt_TargetsOnlyTheFoundationsCurrentStep()
    {
        BlightRenderer.IsCurrentStepAt(BuildScenarioPlan(), cursor: 1, SeismicPos).Should().BeTrue(
            "the current step targets the seismic foundation");
        BlightRenderer.IsCurrentStepAt(BuildScenarioPlan(), cursor: 1, ChillingPos).Should().BeFalse(
            "the chilling foundation is not the current step");

        BlightRenderer.IsCurrentStepAt(BuildScenarioPlan(), cursor: 0, ChillingPos).Should().BeTrue();
        BlightRenderer.IsCurrentStepAt(BuildScenarioPlan(), cursor: 0, FireballPos).Should().BeFalse();
    }

    [TestMethod]
    public void IsCurrentStepAt_ReturnsFalse_ForPositionsNotInPlan()
    {
        BlightRenderer.IsCurrentStepAt(BuildScenarioPlan(), cursor: 0, new NumVector2(1, 1)).Should().BeFalse();
    }

    [TestMethod]
    public void DefaultTowerColor_MapsEveryTowerType()
    {
        foreach (BlightTowerType type in Enum.GetValues<BlightTowerType>())
        {
            Color c = IBlightTowerStrategy.DefaultTowerColor(type);
            c.Should().NotBe(default, $"tower type {type} must map to a non-default colour");
            c.A.Should().Be(100, $"tower type {type} colour uses the lane palette alpha so it matches the lanes");
        }
    }

    [TestMethod]
    public void DefaultTowerColor_IsPerTypeStable()
    {
        IBlightTowerStrategy.DefaultTowerColor(BlightTowerType.Chilling)
            .Should().Be(IBlightTowerStrategy.DefaultTowerColor(BlightTowerType.Chilling));
        IBlightTowerStrategy.DefaultTowerColor(BlightTowerType.Chilling)
            .Should().NotBe(IBlightTowerStrategy.DefaultTowerColor(BlightTowerType.Seismic),
                "different tower types use distinguishable colours");
    }

    [TestMethod]
    public void DefaultFoundationColour_IsGrey()
    {
        IBlightTowerStrategy.DefaultFoundationColour.ToColor3().Should().Be(
            new Color(128, 128, 128, 100).ToColor3());
    }

    [TestMethod]
    public void FireballColour_MatchesLaneUncoveredRed()
    {
        IBlightTowerStrategy.DefaultTowerColor(BlightTowerType.Fireball).Should().Be(
            new Color(200, 60, 60, 100));
    }

    [TestMethod]
    public void ShouldRenderTowerDot_UnplannedPosition_ShowsNoDot()
    {
        BlightRenderer.ShouldRenderTowerDot(isCurrentStep: false, pendingStepCount: 0)
            .Should().BeFalse("an unplanned foundation shows no dot");
        BlightRenderer.ShouldRenderTowerDot(isCurrentStep: false, pendingStepCount: 3)
            .Should().BeTrue("a foundation targeted by pending plan steps shows a dot");
        BlightRenderer.ShouldRenderTowerDot(isCurrentStep: true, pendingStepCount: 0)
            .Should().BeTrue("the current step target shows a dot");
    }

    [TestMethod]
    public void ShouldRenderTowerDot_BuiltTower_ShowsOnlyWhileInPlan()
    {
        BlightRenderer.ShouldRenderTowerDot(isCurrentStep: false, pendingStepCount: 2)
            .Should().BeTrue("still part of the build plan (pending upgrades)");

        BlightRenderer.ShouldRenderTowerDot(isCurrentStep: true, pendingStepCount: 0)
            .Should().BeTrue("the current step target keeps its dot");

        BlightRenderer.ShouldRenderTowerDot(isCurrentStep: false, pendingStepCount: 0)
            .Should().BeFalse("a finished tower is no longer part of the plan");
    }

    [TestMethod]
    public void LaneColorFor_PhantomBridge_AlwaysWhiteRegardlessOfCoverage()
    {
        LaneCoverageResult phantom = new(0, true, new NumVector2(5, 5),
            HasChilling: true, HasSeismic: true, IsPhantom: true);
        LaneCoverageResult real = new(0, true, new NumVector2(5, 5),
            HasChilling: true, HasSeismic: true);

        BlightRenderer.LaneColorFor(phantom, new TestStrategy())
            .Should().Be(BlightRenderer.PhantomLaneColor,
                "the bridge stays the white phantom colour, never the coverage colour");
        BlightRenderer.LaneColorFor(real, new TestStrategy())
            .Should().Be(Color.White, "real lane edges use the strategy's coverage colour");
    }

    private sealed class TestStrategy : IBlightTowerStrategy
    {
        internal static readonly Color SeismicColor = new(10, 20, 30, 255);
        internal static readonly Color FireballColor = new(40, 50, 60, 255);

        public string Name => "Test";
        public string Description => "";
        public Color DefaultLaneColor => Color.White;
        public IReadOnlyList<TowerBuildRule> Rules => [];

        public TowerBuildRule? GetRule(BlightTowerType type) => null;
        public Color GetLaneColor(LaneCoverageResult segment) => Color.White;

        public Color GetFoundationColour(bool hasTower, BlightTowerType currentType)
        {
            // Use the static defaults for the un-overridden cases — casting
            // back to the interface would recurse into this override.
            return hasTower && currentType == BlightTowerType.Seismic
                ? SeismicColor
                : hasTower
                    ? IBlightTowerStrategy.DefaultTowerColor(currentType)
                    : IBlightTowerStrategy.DefaultFoundationColour;
        }

        public Color GetFoundationOutline(BlightTowerType plannedType)
            => plannedType == BlightTowerType.Fireball
                ? FireballColor
                : IBlightTowerStrategy.DefaultTowerColor(plannedType);
    }
}
