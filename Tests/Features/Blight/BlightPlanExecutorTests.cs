namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightPlanExecutorTests
{
    private static BlightPlanStep Step(BlightPlanAction action, float x, float y, BlightTowerType type, int targetLevel)
        => new(action, new NumVector2(x, y), type, targetLevel);

    private static BlightPlan Plan(string name, params BlightPlanStep[] steps)
        => new(steps, version: 1, debugSummary: name);

    [TestMethod]
    public void SetPlan_ReplacesActivePlan()
    {
        var executor = new BlightPlanExecutor();
        executor.CurrentPlan.Should().BeNull();

        BlightPlan first = Plan("first", Step(BlightPlanAction.Build, 5, 5, BlightTowerType.Seismic, 1));
        executor.SetPlan(first);
        executor.CurrentPlan.Should().BeSameAs(first);

        // A second plan replaces the first — the debug UI (which reads
        // CurrentPlan) must always show the latest plan.
        BlightPlan second = Plan("second", Step(BlightPlanAction.Build, 10, 10, BlightTowerType.Chilling, 1));
        executor.SetPlan(second);
        executor.CurrentPlan.Should().BeSameAs(second);
    }

    [TestMethod]
    public void ClearPlan_DropsActivePlan()
    {
        var executor = new BlightPlanExecutor();
        executor.SetPlan(Plan("plan", Step(BlightPlanAction.Build, 5, 5, BlightTowerType.Seismic, 1)));

        executor.ClearPlan();

        // A stale plan from a previous area must never survive a full clear.
        executor.CurrentPlan.Should().BeNull();
        executor.CurrentCursor.Should().Be(0);
    }

    [TestMethod]
    public void Reset_PreservesActivePlanButRewindsCursor()
    {
        var executor = new BlightPlanExecutor();
        BlightPlan first = Plan(
            "first",
            Step(BlightPlanAction.Build, 5, 5, BlightTowerType.Seismic, 1),
            Step(BlightPlanAction.Build, 10, 10, BlightTowerType.Chilling, 1));
        executor.SetPlan(first);

        // A regenerated plan whose steps don't match the current one hits
        // the SetPlan fallback and takes the new plan's cursor.
        BlightPlan replacement = new(
            new[]
            {
                Step(BlightPlanAction.Build, 20, 20, BlightTowerType.Fireball, 1),
                Step(BlightPlanAction.Upgrade, 20, 20, BlightTowerType.Fireball, 2),
            },
            version: 2,
            debugSummary: "replacement",
            currentStepIndex: 1);
        executor.SetPlan(replacement);
        executor.CurrentCursor.Should().Be(1);
        executor.CurrentPlan.Should().BeSameAs(replacement);

        // Reset keeps the plan (walk-reapproach within the same encounter)
        // but rewinds to the first step.
        executor.Reset();
        executor.CurrentPlan.Should().BeSameAs(replacement);
        executor.CurrentCursor.Should().Be(0);
    }

    [TestMethod]
    public void SetPlan_PreservesCursor_AndSyncsPlanCurrentStepIndex()
    {
        var executor = new BlightPlanExecutor();
        BlightPlan first = new(
            new[]
            {
                Step(BlightPlanAction.Build, 5, 5, BlightTowerType.Seismic, 1),
                Step(BlightPlanAction.Build, 10, 10, BlightTowerType.Chilling, 1),
                Step(BlightPlanAction.Build, 20, 20, BlightTowerType.Fireball, 1),
            },
            version: 1,
            debugSummary: "first",
            currentStepIndex: 1);
        executor.SetPlan(first);
        executor.CurrentCursor.Should().Be(1);

        // Regenerated plan (planner always starts at step 0) that still contains the
        // current step (Chilling at 10,10) — but moved to index 2.
        BlightPlan second = new(
            new[]
            {
                Step(BlightPlanAction.Build, 30, 30, BlightTowerType.Fireball, 1),
                Step(BlightPlanAction.Build, 40, 40, BlightTowerType.Seismic, 1),
                Step(BlightPlanAction.Build, 10, 10, BlightTowerType.Chilling, 1),
            },
            version: 2,
            debugSummary: "second");
        executor.SetPlan(second);

        // The cursor is preserved at the relocated step...
        executor.CurrentCursor.Should().Be(2);
        // ...and the plan's current-step index must follow so the debug marker
        // (CurrentStepIndex) and the on-screen pending numbers (cursor) agree.
        executor.CurrentPlan.Should().NotBeNull();
        executor.CurrentPlan!.CurrentStepIndex.Should().Be(2);
        executor.CurrentPlan.CurrentStep.Should().Be(second.Steps[2]);
    }

    // ── Specialization gating (only 3→4 with a chosen specialization) ──

    [TestMethod]
    public void IsSpecializationStep_OnlyForSpecTierUpgrade_WithChosenSpecialization()
    {
        const int meteor = 0; // TowerSpecialization.Meteor

        // Fireball plain upgrades are NOT specialization steps — this is the
        // bug: the executor used to search for 'MeteorTower' on these menus
        // where no specialization exists, failing and skipping the step.
        BlightPlanExecutor.IsSpecializationStep(meteor, targetLevel: 2, currentTowerLevel: 1)
            .Should().BeFalse("Fireball 1→2 is a plain upgrade");
        BlightPlanExecutor.IsSpecializationStep(meteor, targetLevel: 3, currentTowerLevel: 2)
            .Should().BeFalse("Fireball 2→3 is a plain upgrade");

        // Only the 3→4 specialization tier with a chosen spec is a spec step.
        BlightPlanExecutor.IsSpecializationStep(meteor, targetLevel: 4, currentTowerLevel: 3)
            .Should().BeTrue("Fireball 3→4 is the specialization step");
    }

    [TestMethod]
    public void IsSpecializationStep_RequiresTowerAtLevel3()
    {
        const int meteor = 0;

        // A step targeting level 4 is still NOT a specialization step when the
        // tower isn't actually at level 3 yet.
        BlightPlanExecutor.IsSpecializationStep(meteor, targetLevel: 4, currentTowerLevel: 2)
            .Should().BeFalse("the tower must be at level 3 before specializing");
        BlightPlanExecutor.IsSpecializationStep(meteor, targetLevel: 4, currentTowerLevel: 1)
            .Should().BeFalse();
    }

    [TestMethod]
    public void IsSpecializationStep_RequiresChosenSpecialization()
    {
        const int noSpecialization = -1; // TowerSpecialization.None

        // A rule without a specialization (e.g. Chilling/Seismic, or a Fireball
        // rule that never called SetSpecialization) never takes the spec path.
        BlightPlanExecutor.IsSpecializationStep(noSpecialization, targetLevel: 4, currentTowerLevel: 3)
            .Should().BeFalse("no chosen specialization means a plain upgrade");
    }

    [TestMethod]
    public void IsMenuRegionUsable_ReturnsTrue_WhenFullyOnScreenAndClickable()
    {
        BlightPlanExecutor.IsMenuRegionUsable(new RectangleF(500f, 300f, 200f, 100f), 1920f, 1080f, static _ => true)
            .Should().BeTrue();
    }

    [TestMethod]
    public void IsMenuRegionUsable_ReturnsFalse_WhenPartlyOffScreen()
    {
        BlightPlanExecutor.IsMenuRegionUsable(new RectangleF(1850f, 300f, 200f, 100f), 1920f, 1080f, static _ => true)
            .Should().BeFalse("a menu region partly off-screen must keep the player walking");
    }

    [TestMethod]
    public void IsMenuRegionUsable_ReturnsFalse_WhenAnyCornerNotClickable()
    {
        // Blocks any point below y=350, so the bottom corners of the region are not clickable
        // (e.g. the region overlaps the buff bar area) — the whole region must be clickable.
        BlightPlanExecutor.IsMenuRegionUsable(new RectangleF(500f, 300f, 200f, 100f), 1920f, 1080f,
                static point => point.Y <= 350f)
            .Should().BeFalse("a menu region overlapping a blocked UI region must keep the player walking");
    }

    [TestMethod]
    public void IsMenuRegionUsable_ReturnsFalse_ForDegenerateRect()
    {
        BlightPlanExecutor.IsMenuRegionUsable(RectangleF.Empty, 1920f, 1080f, static _ => true)
            .Should().BeFalse();
    }

    [TestMethod]
    public void IsMenuRegionUsable_ReturnsFalse_ForDegenerateWindow()
    {
        BlightPlanExecutor.IsMenuRegionUsable(new RectangleF(0f, 0f, 10f, 10f), 0f, 0f, static _ => true)
            .Should().BeFalse();
    }

    // ── Walk target resolution (stuck-after-meteor-upgrade regression) ──

    [TestMethod]
    public void ResolveWalkActionKind_UsesEntityWalk_WhenEntityIsCached()
    {
        // A cached foundation entity is the normal case: walk toward the entity.
        BlightPlanExecutor.ResolveWalkActionKind(hasWalkEntity: true, positionOffScreen: true)
            .Should().Be(BlightBuildActionKind.WalkToTarget);
        BlightPlanExecutor.ResolveWalkActionKind(hasWalkEntity: true, positionOffScreen: false)
            .Should().Be(BlightBuildActionKind.WalkToTarget);
    }

    [TestMethod]
    public void ResolveWalkActionKind_NeverRequestsEntityWalk_WithoutAnEntity()
    {
        // The deadlock: a foundation known only by its persisted position has no entity to walk
        // toward, so the old code kept requesting WalkToTarget and the pipeline (which resolves the
        // entity) never walked — the executor spun forever. With no entity, the executor must never
        // emit WalkToTarget.
        BlightPlanExecutor.ResolveWalkActionKind(hasWalkEntity: false, positionOffScreen: true)
            .Should().Be(BlightBuildActionKind.WalkToPosition);
        BlightPlanExecutor.ResolveWalkActionKind(hasWalkEntity: false, positionOffScreen: false)
            .Should().Be(BlightBuildActionKind.None);
    }

    [TestMethod]
    public void ResolveWalkActionKind_WalksTowardPosition_WhenOffScreen_ButWaits_WhenOnScreen()
    {
        // Off-screen foundation with no cached entity: walk toward the known position so the player
        // gets within scan range. On-screen but still unscannable: wait for the scan rather than walk.
        BlightPlanExecutor.ResolveWalkActionKind(hasWalkEntity: false, positionOffScreen: true)
            .Should().Be(BlightBuildActionKind.WalkToPosition);
        BlightPlanExecutor.ResolveWalkActionKind(hasWalkEntity: false, positionOffScreen: false)
            .Should().Be(BlightBuildActionKind.None);
    }

    // ── Walk-ready gate (build needs the menu region, upgrade just needs the tower on-screen) ──

    [TestMethod]
    public void IsStepWalkReadyForAction_Upgrade_StopsOnceTowerIsOnScreen()
    {
        // Regression: the executor used to keep pathfinding toward an already-on-screen tower for
        // UPGRADE steps because the full enlarged menu region was required. Upgrading is a single
        // click on the upgrade icon — no sub-menu opens — so the tower being fully on-screen is
        // enough to stop walking.
        BlightPlanExecutor.IsStepWalkReadyForAction(BlightPlanAction.Upgrade,
                menuRegionReady: false, hasWalkEntity: true, entityFullyOnScreen: true)
            .Should().BeTrue("an upgrade with the tower on-screen must stop walking");
    }

    [TestMethod]
    public void IsStepWalkReadyForAction_Upgrade_WalksWhileTowerIsOffScreen()
    {
        BlightPlanExecutor.IsStepWalkReadyForAction(BlightPlanAction.Upgrade,
                menuRegionReady: false, hasWalkEntity: true, entityFullyOnScreen: false)
            .Should().BeFalse("an off-screen tower still needs pathfinding");
    }

    [TestMethod]
    public void IsStepWalkReadyForAction_Upgrade_NotReady_WithoutEntity()
    {
        BlightPlanExecutor.IsStepWalkReadyForAction(BlightPlanAction.Upgrade,
                menuRegionReady: false, hasWalkEntity: false, entityFullyOnScreen: false)
            .Should().BeFalse("with no cached entity the upgrade can't proceed yet");
    }

    [TestMethod]
    public void IsStepWalkReadyForAction_Build_RequiresMenuRegion()
    {
        // A build step clicks the build icon which opens the tower sub-menu, so the WHOLE enlarged
        // menu region must be on-screen and clickable before stopping — even when the tower itself
        // is already on-screen.
        BlightPlanExecutor.IsStepWalkReadyForAction(BlightPlanAction.Build,
                menuRegionReady: true, hasWalkEntity: true, entityFullyOnScreen: true)
            .Should().BeTrue("a build step with a usable menu region can stop walking");

        BlightPlanExecutor.IsStepWalkReadyForAction(BlightPlanAction.Build,
                menuRegionReady: false, hasWalkEntity: true, entityFullyOnScreen: true)
            .Should().BeFalse("a build step must keep walking until the menu region is usable");
    }
}
