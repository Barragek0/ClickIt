namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightEncounterTests
{
    [TestMethod]
    public void Update_NullPump_StaysInactive()
    {
        var encounter = new BlightEncounter();
        encounter.IsActive.Should().BeFalse();

        encounter.Update(null, 0, 0, pumpCompleted: false).Should().BeFalse("no pump means no encounter and no end transition");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_ActiveOnlyWhenPumpAndPathwaysArePresent()
    {
        var encounter = new BlightEncounter();
        // A valid probe pump with no loaded StateMachine: IsPumpCompleted is safely false (guarded).
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 0, 0, pumpCompleted: false).Should().BeFalse("no pathway entities yet - not active");
        encounter.IsActive.Should().BeFalse();

        encounter.Update(pump, 3, 0, pumpCompleted: false).Should().BeFalse("starting is not an end transition");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_StreamOut_WithActiveLanes_KeepsEncounterActive()
    {
        // The reported case: the player walks out of pump range briefly while the encounter is still
        // running. The pump is unreadable, but its lanes are still flowing (pending > 0), which is
        // the encounter's own activity signal — the encounter must stay active, no end/clear.
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 0, 2, pumpCompleted: false).Should().BeFalse("lanes still flowing prove the encounter is running");
        encounter.IsActive.Should().BeTrue();

        encounter.Update(pump, 3, 2, pumpCompleted: false).Should().BeFalse("pump returns - no end, no clear");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_StreamOut_NoActiveLanes_AfterRunning_EndsEncounter()
    {
        // The encounter ran and its lanes have all stopped while the pump is away: the encounter has
        // ended, so it clears.
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        // Player walks away; the pump is unreadable but lanes are still flowing -> running continues
        // and the running state is latched.
        encounter.Update(null, 3, 2, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        // The encounter ends while away: lanes stop flowing, pump still unreadable -> ended.
        encounter.Update(null, 3, 0, pumpCompleted: false).Should().BeTrue("ran then all lanes stopped while away - encounter ended");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_StreamOut_NoActiveLanes_BeforeRunning_KeepsActive()
    {
        // Build phase (the encounter has never run) and the pump streams out: don't clear a possibly
        // still-building encounter; the pump re-read when it returns resolves the state.
        var encounter = new BlightEncounter();
        encounter.Update(EntityProbeFactory.Create(), 3, 0, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 3, 0, pumpCompleted: false).Should().BeFalse("never ran - don't clear the build-phase encounter");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_InvalidPump_WithActiveLanes_KeepsEncounterActive()
    {
        // Pump still in the retained set but invalid (IsValid false, e.g. streamed out): its
        // StateMachine is unreadable, but flowing lanes prove the encounter is still running.
        var encounter = new BlightEncounter();
        encounter.Update(EntityProbeFactory.Create(), 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        var invalidPump = EntityProbeFactory.Create(isValid: false);
        encounter.Update(invalidPump, 3, 1, pumpCompleted: false).Should().BeFalse("invalid pump but lanes flowing - still active");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_InvalidPump_NoActiveLanes_AfterRunning_EndsEncounter()
    {
        var encounter = new BlightEncounter();
        var invalidPump = EntityProbeFactory.Create(isValid: false);

        // Running near pump, then the pump streams out (invalid) while lanes still flow — running
        // is latched from the flowing lanes.
        encounter.Update(EntityProbeFactory.Create(), 3, 3, pumpCompleted: false);
        encounter.Update(invalidPump, 3, 2, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        // All lanes stop while the pump is still unreadable: the encounter has ended.
        encounter.Update(invalidPump, 3, 0, pumpCompleted: false).Should().BeTrue("invalid pump and all lanes stopped - encounter ended");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_ReDetectedAfterEnd_Restarts()
    {
        // Regression guard for the stuck-plan-after-respawn bug: the encounter is cleared when the
        // pump unloads, and the restart transition (inactive → active) is what must signal the plan
        // rebuild.  Previously the stale plan counters survived the clear, so the re-detected
        // encounter compared equal and the rebuild was skipped.
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 0, 0, pumpCompleted: false); // ran, all lanes stopped while away — encounter ends
        encounter.IsActive.Should().BeFalse();

        encounter.Update(pump, 3, 3, pumpCompleted: false).Should().BeFalse("restart is not an end transition");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_CompletedPump_StreamsOut_EndsEncounter()
    {
        // The reported case: the pump reported success/fail/pending==0 (completed) while in range,
        // then the player walked away and the pump streamed out. The retained lanes must NOT keep
        // the encounter alive — the completion must be latched so it still ends.
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        // Encounter running near the pump, then the pump reports completed -> ends and clears.
        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();
        encounter.Update(pump, 3, 0, pumpCompleted: true).Should().BeTrue("completed pump ends the encounter");
        encounter.IsActive.Should().BeFalse();

        // Player walks away; the pump streams out but the lanes are retained. Previously the
        // pathwayCount fallback kept the encounter active (lanes rendered + pathfinding to the pump).
        encounter.Update(null, 3, 0, pumpCompleted: false).Should().BeFalse("already inactive - no new end transition");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_CompletedPump_BecomesInvalid_StaysInactive()
    {
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 0, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(pump, 3, 0, pumpCompleted: true).Should().BeTrue("completed pump ends the encounter");
        encounter.IsActive.Should().BeFalse();

        // Pump retained but invalid (streamed out), lanes retained: must stay inactive.
        var invalidPump = EntityProbeFactory.Create(isValid: false);
        encounter.Update(invalidPump, 3, 0, pumpCompleted: false).Should().BeFalse("already inactive - no new end transition");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_CompletedPump_ResetThenRestart_ActivatesAgain()
    {
        // BlightService.Clear() calls Reset(); a re-detected encounter must restart (inactive ->
        // active) even after a completed-pump clear, so the plan rebuild fires again.
        var encounter = new BlightEncounter();
        encounter.Update(EntityProbeFactory.Create(), 3, 0, pumpCompleted: true);
        encounter.IsActive.Should().BeFalse();

        encounter.Reset();
        encounter.Update(EntityProbeFactory.Create(), 3, 0, pumpCompleted: false).Should().BeFalse("restart is not an end transition");
        encounter.IsActive.Should().BeTrue();
    }
}
