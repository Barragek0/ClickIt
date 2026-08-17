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

        BlightPlanExecutor.IsSpecializationStep(meteor, targetLevel: 2)
            .Should().BeFalse("Fireball 1→2 is a plain upgrade");
        BlightPlanExecutor.IsSpecializationStep(meteor, targetLevel: 3)
            .Should().BeFalse("Fireball 2→3 is a plain upgrade");

        BlightPlanExecutor.IsSpecializationStep(meteor, targetLevel: 4)
            .Should().BeTrue("Fireball 3→4 is the specialization step");
    }

    [TestMethod]
    public void IsSpecializationStep_TargetsSpecTier_RegardlessOfCachedLevel()
    {
        const int meteor = 0;

        BlightPlanExecutor.IsSpecializationStep(meteor, targetLevel: 4)
            .Should().BeTrue("Fireball 3->4 is the specialization step");
    }

    [TestMethod]
    public void IsSpecializationStep_RequiresChosenSpecialization()
    {
        const int noSpecialization = -1; // TowerSpecialization.None

        BlightPlanExecutor.IsSpecializationStep(noSpecialization, targetLevel: 4)
            .Should().BeFalse("no chosen specialization means a plain upgrade");
    }

    [TestMethod]
    public void ShouldSkipPlainUpgradeClick_True_WhenButtonIsASpecializationTower()
    {
        BlightPlanExecutor.ShouldSkipPlainUpgradeClick("PetrificationTower")
            .Should().BeTrue("Seismic's Stone Gaze is a spec button — never click on a plain upgrade");
        BlightPlanExecutor.ShouldSkipPlainUpgradeClick("MeteorTower")
            .Should().BeTrue("Fireball's Meteor is a spec button — never click on a plain upgrade");
    }

    [TestMethod]
    public void ShouldSkipPlainUpgradeClick_False_ForPlainTierButtonsOrUnreadableId()
    {
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
        BlightPlanExecutor.ShouldSkipAfterVerifyFailures(BlightPlanAction.Build, 3)
            .Should().BeTrue();
        BlightPlanExecutor.ShouldSkipAfterVerifyFailures(BlightPlanAction.Build, 2)
            .Should().BeFalse("below the 3-failure threshold it retries");

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
            .Should().BeFalse("a region partly off-screen must keep the player walking");
    }

    [TestMethod]
    public void IsMenuRegionUsable_ReturnsFalse_WhenAnyCornerNotClickable()
    {
        BlightPlanExecutor.IsMenuRegionUsable(new RectangleF(500f, 300f, 200f, 100f), 1920f, 1080f,
                static point => point.Y <= 350f)
            .Should().BeFalse("a region overlapping a blocked UI region must keep the player walking");
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
        BlightPlanExecutor.ResolveWalkActionKind(hasWalkEntity: true, positionOffScreen: true)
            .Should().Be(BlightBuildActionKind.WalkToTarget);
        BlightPlanExecutor.ResolveWalkActionKind(hasWalkEntity: true, positionOffScreen: false)
            .Should().Be(BlightBuildActionKind.WalkToTarget);
    }

    [TestMethod]
    public void ResolveWalkActionKind_NeverRequestsEntityWalk_WithoutAnEntity()
    {
        BlightPlanExecutor.ResolveWalkActionKind(hasWalkEntity: false, positionOffScreen: true)
            .Should().Be(BlightBuildActionKind.WalkToPosition);
        BlightPlanExecutor.ResolveWalkActionKind(hasWalkEntity: false, positionOffScreen: false)
            .Should().Be(BlightBuildActionKind.None);
    }

    [TestMethod]
    public void ResolveWalkActionKind_WalksTowardPosition_WhenOffScreen_ButWaits_WhenOnScreen()
    {
        BlightPlanExecutor.ResolveWalkActionKind(hasWalkEntity: false, positionOffScreen: true)
            .Should().Be(BlightBuildActionKind.WalkToPosition);
        BlightPlanExecutor.ResolveWalkActionKind(hasWalkEntity: false, positionOffScreen: false)
            .Should().Be(BlightBuildActionKind.None);
    }

    // ── Walk-ready gate (build needs the menu region, upgrade just needs the tower on-screen) ──

    [TestMethod]
    public void IsStepWalkReadyForAction_Upgrade_StopsOnceTowerIsOnScreen()
    {
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
        BlightPlanExecutor.IsStepWalkReadyForAction(BlightPlanAction.Build,
                menuRegionReady: true, hasWalkEntity: true, entityFullyOnScreen: true)
            .Should().BeTrue("a build step with a fully usable menu region can stop walking");

        BlightPlanExecutor.IsStepWalkReadyForAction(BlightPlanAction.Build,
                menuRegionReady: false, hasWalkEntity: true, entityFullyOnScreen: true)
            .Should().BeFalse("a build step must keep walking until the menu region is fully usable");
    }

    // ── Pathfinding stop condition (WantsWalkForAction) — must mirror EVERY executor walk trigger ──

    [TestMethod]
    public void WantsWalkForAction_Upgrade_IconOffWindow_WhileEntityOnScreen_ReturnsTrue()
    {
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
        BlightPlanExecutor.WantsWalkForAction(
                action: BlightPlanAction.Build,
                walkReadyGate: true,
                upgradeLabelFound: false,
                upgradeIconInWindow: false,
                upgradeEntityOnScreen: false)
            .Should().BeFalse("a build with a usable menu region is clickable");
    }

    // ── Executor-phase gate (pipeline must stand down while the executor is stopping/clicking) ──

    [TestMethod]
    public void WantsWalkForCurrentPhase_OnlyWalksInWalkingPhase()
    {
        BlightPlanExecutor.WantsWalkForCurrentPhase(BlightPlanExecutor.Phase.Walking).Should().BeTrue();
        BlightPlanExecutor.WantsWalkForCurrentPhase(BlightPlanExecutor.Phase.StopPlayer).Should().BeFalse();
        BlightPlanExecutor.WantsWalkForCurrentPhase(BlightPlanExecutor.Phase.OpenMenu).Should().BeFalse();
        BlightPlanExecutor.WantsWalkForCurrentPhase(BlightPlanExecutor.Phase.SelectTower).Should().BeFalse();
        BlightPlanExecutor.WantsWalkForCurrentPhase(BlightPlanExecutor.Phase.SelectSpecialization).Should().BeFalse();
        BlightPlanExecutor.WantsWalkForCurrentPhase(BlightPlanExecutor.Phase.WaitVerify).Should().BeFalse();
        BlightPlanExecutor.WantsWalkForCurrentPhase(BlightPlanExecutor.Phase.Done).Should().BeFalse();
    }

    // ── Spec-result verification (wrong-spec guard) ──

    [TestMethod]
    public void PathShowsOtherSpecialization_FlamethrowerPath_WhenTargetingMeteor_ReturnsTrue()
    {
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

    [TestMethod]
    public void DatIdShowsOtherSpecialization_Flamethrower_WhenTargetingMeteor_ReturnsTrue()
    {
        BlightPlanExecutor.DatIdShowsOtherSpecialization("FlamethrowerTower", BlightTowerType.Fireball, TowerSpecialization.Meteor)
            .Should().BeTrue("a Flamethrower dat id must never pass a Meteor spec step");
        BlightPlanExecutor.DatIdShowsOtherSpecialization("TemporalTower", BlightTowerType.Seismic, TowerSpecialization.StoneGaze)
            .Should().BeTrue("Temporal is the other Seismic spec — never pass a Stone Gaze step");
    }

    [TestMethod]
    public void DatIdShowsOtherSpecialization_ChosenSpec_ReturnsFalse()
    {
        BlightPlanExecutor.DatIdShowsOtherSpecialization("MeteorTower", BlightTowerType.Fireball, TowerSpecialization.Meteor)
            .Should().BeFalse("the chosen Meteor dat id is the correct result");
        BlightPlanExecutor.DatIdShowsOtherSpecialization("PetrificationTower", BlightTowerType.Seismic, TowerSpecialization.StoneGaze)
            .Should().BeFalse("the chosen Stone Gaze dat id is the correct result");
    }

    [TestMethod]
    public void DatIdShowsOtherSpecialization_UnrelatedOrEmpty_ReturnsFalse()
    {
        BlightPlanExecutor.DatIdShowsOtherSpecialization("FlameTower3", BlightTowerType.Fireball, TowerSpecialization.Meteor)
            .Should().BeFalse("a plain Mk dat id (pre-verify) must not trip the guard");
        BlightPlanExecutor.DatIdShowsOtherSpecialization(string.Empty, BlightTowerType.Fireball, TowerSpecialization.Meteor)
            .Should().BeFalse("an unreadable dat id is fail-open — never a false trip");
        BlightPlanExecutor.DatIdShowsOtherSpecialization("MeteorTower", BlightTowerType.Seismic, TowerSpecialization.StoneGaze)
            .Should().BeFalse("a Fireball dat id is unrelated to a Seismic step");
    }

    // ── Build sub-menu toggle-race guard ──

    [TestMethod]
    public void ShouldWaitForBuildSubMenu_True_WithinWaitWindowAfterClick()
    {
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
        BlightPlanExecutor.ShouldWaitForBuildSubMenu(
                lastBuildMenuClickTimestampMs: 1000, nowMs: 1600, waitMs: 500)
            .Should().BeFalse("past the retry timeout — a re-click is allowed");
    }
}
