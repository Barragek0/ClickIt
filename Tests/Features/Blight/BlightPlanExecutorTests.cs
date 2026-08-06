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
        executor.CurrentPlan.Should().NotBeNull();
        executor.CurrentPlan!.Steps.Should().BeSameAs(first.Steps);

        // A second plan replaces the first — the debug UI (which reads
        // CurrentPlan) must always show the latest plan.
        BlightPlan second = Plan("second", Step(BlightPlanAction.Build, 10, 10, BlightTowerType.Chilling, 1));
        executor.SetPlan(second);
        executor.CurrentPlan!.Steps.Should().BeSameAs(second.Steps);
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

        // A regenerated plan replaces the current one; SetPlan always restarts at the first step.
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
        executor.CurrentCursor.Should().Be(0);
        executor.CurrentPlan.Should().NotBeNull();
        executor.CurrentPlan!.Steps.Should().BeSameAs(replacement.Steps);

        // Reset keeps the plan (walk-reapproach within the same encounter)
        // but rewinds to the first step.
        executor.Reset();
        executor.CurrentPlan!.Steps.Should().BeSameAs(replacement.Steps);
        executor.CurrentCursor.Should().Be(0);
    }

    [TestMethod]
    public void SetPlan_AlwaysRewindsToFirstStep_OnRegeneration()
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
        executor.CurrentCursor.Should().Be(0);
        executor.CurrentPlan!.CurrentStepIndex.Should().Be(0);

        // A regenerated plan (planner always starts at step 0) restarts the executor
        // from the first step — completed work is re-derived from live tower state.
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

        executor.CurrentCursor.Should().Be(0);
        executor.CurrentPlan.Should().NotBeNull();
        executor.CurrentPlan!.CurrentStepIndex.Should().Be(0);
        executor.CurrentPlan.CurrentStep.Should().Be(second.Steps[0]);
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
    public void ShouldSkipPlainUpgradeClick_True_WhenButtonIsASpecializationTower()
    {
        // When a plain (non-spec) upgrade step's first visible button resolves to a specialization
        // tower, the tower is already at its max plain level — clicking would over-upgrade it
        // (e.g. Seismic 3 -> 4 = Stone Gaze). The step must advance without clicking.
        BlightPlanExecutor.ShouldSkipPlainUpgradeClick("PetrificationTower")
            .Should().BeTrue("Seismic's Stone Gaze is a spec button — never click on a plain upgrade");
        BlightPlanExecutor.ShouldSkipPlainUpgradeClick("MeteorTower")
            .Should().BeTrue("Fireball's Meteor is a spec button — never click on a plain upgrade");
    }

    [TestMethod]
    public void ShouldSkipPlainUpgradeClick_False_ForPlainTierButtonsOrUnreadableId()
    {
        // A genuine next-tier plain button (StunningTower3, FlameTower2) is a legitimate click; an
        // unreadable/null button id must NOT trigger the skip (the executor proceeds with the click
        // as before when the id can't be read).
        BlightPlanExecutor.ShouldSkipPlainUpgradeClick("StunningTower3")
            .Should().BeFalse();
        BlightPlanExecutor.ShouldSkipPlainUpgradeClick("FlameTower2")
            .Should().BeFalse();
        BlightPlanExecutor.ShouldSkipPlainUpgradeClick(null)
            .Should().BeFalse("an unreadable button id must not block the click");
    }

    [TestMethod]
    public void ShouldSkipAfterVerifyFailures_OnlyBuildStepsSkip_UpgradesPause()
    {
        // A build step that can't be verified after 3 attempts is skipped (best-effort, spec §4.7).
        BlightPlanExecutor.ShouldSkipAfterVerifyFailures(BlightPlanAction.Build, 3)
            .Should().BeTrue();
        BlightPlanExecutor.ShouldSkipAfterVerifyFailures(BlightPlanAction.Build, 2)
            .Should().BeFalse("below the 3-failure threshold it retries");

        // An upgrade that can't be confirmed is an affordability/state pause — it must NEVER be
        // skipped, or the plan advances past an upgrade that never happened (the reported bug:
        // no currency for Fireball lvl3 / Meteor, and the plan moved on anyway).
        BlightPlanExecutor.ShouldSkipAfterVerifyFailures(BlightPlanAction.Upgrade, 3)
            .Should().BeFalse("an unaffordable upgrade must pause, not skip");
        BlightPlanExecutor.ShouldSkipAfterVerifyFailures(BlightPlanAction.Upgrade, 9)
            .Should().BeFalse("an upgrade never skips on verify failures");
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

    // ── Pathfinding stop condition (WantsWalkForAction) — must mirror EVERY executor walk trigger ──

    [TestMethod]
    public void WantsWalkForAction_Upgrade_IconOffWindow_WhileEntityOnScreen_ReturnsTrue()
    {
        // THE regression: the tower entity is fully on-screen (the old gate stopped walking) but the
        // upgrade icon sits off-window — the executor walks closer from OpenMenu, and pathfinding
        // must not refuse that walk (it used to return null and stall the executor forever).
        BlightPlanExecutor.WantsWalkForAction(
                action: BlightPlanAction.Upgrade,
                walkReadyGate: true,
                upgradeLabelFound: true,
                upgradeIconInWindow: false,
                upgradeEntityOnScreen: true)
            .Should().BeTrue("an off-window upgrade icon still needs the player to walk closer");
    }

    [TestMethod]
    public void WantsWalkForAction_Upgrade_EverythingReady_ReturnsFalse()
    {
        BlightPlanExecutor.WantsWalkForAction(
                action: BlightPlanAction.Upgrade,
                walkReadyGate: true,
                upgradeLabelFound: true,
                upgradeIconInWindow: true,
                upgradeEntityOnScreen: true)
            .Should().BeFalse("entity on-screen + icon in-window means the upgrade is clickable");
    }

    [TestMethod]
    public void WantsWalkForAction_Upgrade_EntityOffScreen_ReturnsTrue()
    {
        BlightPlanExecutor.WantsWalkForAction(
                action: BlightPlanAction.Upgrade,
                walkReadyGate: false,
                upgradeLabelFound: true,
                upgradeIconInWindow: false,
                upgradeEntityOnScreen: false)
            .Should().BeTrue("an off-screen tower still needs pathfinding");
    }

    [TestMethod]
    public void WantsWalkForAction_Upgrade_LabelMissing_EntityOnScreen_ReturnsFalse()
    {
        // The executor retries (does not walk) when the label is missing but the entity is on-screen.
        BlightPlanExecutor.WantsWalkForAction(
                action: BlightPlanAction.Upgrade,
                walkReadyGate: true,
                upgradeLabelFound: false,
                upgradeIconInWindow: false,
                upgradeEntityOnScreen: true)
            .Should().BeFalse("a missing label with the entity on-screen is a retry, not a walk");
    }

    [TestMethod]
    public void WantsWalkForAction_Build_RegionUsable_ReturnsFalse()
    {
        // Builds require the whole menu region usable — once that passes, no further walking.
        BlightPlanExecutor.WantsWalkForAction(
                action: BlightPlanAction.Build,
                walkReadyGate: true,
                upgradeLabelFound: false,
                upgradeIconInWindow: false,
                upgradeEntityOnScreen: false)
            .Should().BeFalse("a build with a usable menu region is clickable");
    }

    // ── Spec-result verification (wrong-spec guard) ──

    [TestMethod]
    public void PathShowsOtherSpecialization_FlamethrowerPath_WhenTargetingMeteor_ReturnsTrue()
    {
        // The reported bug: the spec click landed on Flamethrower while the strategy chose Meteor.
        // The path is the ground truth for which specialization the tower actually became.
        BlightPlanExecutor.PathShowsOtherSpecialization(
                "Metadata/Monsters/LeagueBlight/BlightTower/FlamethrowerTower@83",
                BlightTowerType.Fireball, TowerSpecialization.Meteor)
            .Should().BeTrue("a Flamethrower path must never pass a Meteor spec step");
    }

    [TestMethod]
    public void PathShowsOtherSpecialization_MeteorPath_WhenTargetingMeteor_ReturnsFalse()
    {
        BlightPlanExecutor.PathShowsOtherSpecialization(
                "Metadata/Monsters/LeagueBlight/BlightTower/MeteorTower@83",
                BlightTowerType.Fireball, TowerSpecialization.Meteor)
            .Should().BeFalse("the chosen Meteor path is the correct result");
    }

    [TestMethod]
    public void PathShowsOtherSpecialization_UnrelatedOrUnreadablePath_ReturnsFalse()
    {
        BlightPlanExecutor.PathShowsOtherSpecialization(
                "Metadata/Monsters/LeagueBlight/BlightTower/FlameTower3@83",
                BlightTowerType.Fireball, TowerSpecialization.Meteor)
            .Should().BeFalse("a plain rank path (pre-verify) must not trip the guard");

        BlightPlanExecutor.PathShowsOtherSpecialization(
                string.Empty, BlightTowerType.Fireball, TowerSpecialization.Meteor)
            .Should().BeFalse("an unreadable path is fail-open — never a false trip");
    }

    // ── Build sub-menu toggle-race guard ──

    [TestMethod]
    public void ShouldWaitForBuildSubMenu_True_WithinWaitWindowAfterClick()
    {
        // After the build-icon click the sub-menu needs time to appear; re-clicking the toggle would
        // close it.  Within the wait window the executor must wait instead of re-clicking.
        BlightPlanExecutor.ShouldWaitForBuildSubMenu(
                lastBuildMenuClickTimestampMs: 1000, nowMs: 1300, waitMs: 500)
            .Should().BeTrue("still inside the wait window — re-clicking would toggle the menu closed");
    }

    [TestMethod]
    public void ShouldWaitForBuildSubMenu_False_BeforeFirstClick()
    {
        BlightPlanExecutor.ShouldWaitForBuildSubMenu(
                lastBuildMenuClickTimestampMs: 0, nowMs: 1000, waitMs: 500)
            .Should().BeFalse("no build-icon click yet — the first click must go through immediately");
    }

    [TestMethod]
    public void ShouldWaitForBuildSubMenu_False_AfterWaitWindowElapsed()
    {
        // The click may have missed entirely (menu never opened) — after the timeout a retry click is
        // allowed so the executor cannot wait forever.
        BlightPlanExecutor.ShouldWaitForBuildSubMenu(
                lastBuildMenuClickTimestampMs: 1000, nowMs: 1600, waitMs: 500)
            .Should().BeFalse("past the retry timeout — a re-click is allowed");
    }
}
