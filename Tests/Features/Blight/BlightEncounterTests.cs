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
        // The reported case: the player walks out of pump range briefly while the encounter is still running. The pump is unreadable, but its lanes are still flowing (pending > 0), which is the encounter's own activity signal — the encounter must stay active, no end/clear.
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
        // The encounter ran and its lanes have all stopped while the pump is away: the encounter has ended, so it clears.
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        // Player walks away; the pump is unreadable but lanes are still flowing -> running continues and the running state is latched.
        encounter.Update(null, 3, 2, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        // The encounter ends while away: lanes stop flowing, pump still unreadable -> ended.
        encounter.Update(null, 3, 0, pumpCompleted: false).Should().BeTrue("ran then all lanes stopped while away - encounter ended");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_StreamOut_NoActiveLanes_BeforeRunning_KeepsActive()
    {
        // Build phase (the encounter has never run) and the pump streams out: don't clear a possibly still-building encounter; the pump re-read when it returns resolves the state.
        var encounter = new BlightEncounter();
        encounter.Update(EntityProbeFactory.Create(), 3, 0, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 3, 0, pumpCompleted: false).Should().BeFalse("never ran - don't clear the build-phase encounter");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_InvalidPump_WithActiveLanes_KeepsEncounterActive()
    {
        // Pump still in the retained set but invalid (IsValid false, e.g. streamed out): its StateMachine is unreadable, but flowing lanes prove the encounter is still running.
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

        // Running near pump, then the pump streams out (invalid) while lanes still flow — running is latched from the flowing lanes.
        encounter.Update(EntityProbeFactory.Create(), 3, 3, pumpCompleted: false);
        encounter.Update(invalidPump, 3, 2, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        // All lanes stop while the pump is still unreadable: the encounter has ended.
        encounter.Update(invalidPump, 3, 0, pumpCompleted: false).Should().BeTrue("invalid pump and all lanes stopped - encounter ended");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_Ended_SameAreaRestartSuppressed_UntilReset()
    {
        // Reported bug: after the encounter ends (lanes stopped while the pump was away), the retained lanes and a re-readable pump must NOT re-activate the encounter in the same area (the game re-renders the web when the player walks far from the pump, which previously resumed the stale build plan). Only Reset() - fired on area change / explicit clear - re-arms so a NEW encounter can start.
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        // Pump streams out while lanes still flow: running is latched.
        encounter.Update(null, 3, 2, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 0, 0, pumpCompleted: false); // ran, all lanes stopped while away - encounter ends
        encounter.IsActive.Should().BeFalse();

        // Same area: even a readable, not-completed pump with lanes must not restart the ended encounter.
        encounter.Update(pump, 3, 3, pumpCompleted: false).Should().BeFalse("an ended encounter must not restart in the same area");
        encounter.IsActive.Should().BeFalse();

        // A NEW area (or explicit clear) calls Reset(), which re-arms the encounter detection.
        encounter.Reset();
        encounter.Update(pump, 3, 3, pumpCompleted: false).Should().BeFalse("restart after reset is not an end transition");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_Ended_WalkedFar_LanesReRender_StaysEnded()
    {
        // The exact reported scenario: the encounter runs, ends (lanes stop while the pump is away), then the player walks FAR from the pump and the game re-renders the web (active lanes reappear, retained pathways remain, the pump is still unreadable). The encounter must stay ended - no re-activation, no plan rebuild, no build clicks.
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        // Encounter running near the pump.
        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        // The encounter finishes; the player walks away; the pump streams out while lanes still flow, then all lanes stop.
        encounter.Update(null, 3, 2, pumpCompleted: false).Should().BeFalse("pump away but lanes still flowing - still active");
        encounter.IsActive.Should().BeTrue();
        encounter.Update(null, 3, 0, pumpCompleted: false).Should().BeTrue("ran then all lanes stopped while away - encounter ended");
        encounter.IsActive.Should().BeFalse();

        // Far from the pump: lanes re-render (active lanes reappear) and retained pathways persist, but the pump stays unreadable. Must stay ended on every subsequent tick.
        Entity? invalidPump = EntityProbeFactory.Create(isValid: false);
        encounter.Update(invalidPump, 3, 2, pumpCompleted: false).Should().BeFalse("re-rendered lanes must not re-activate an ended encounter");
        encounter.IsActive.Should().BeFalse();
        encounter.Update(invalidPump, 3, 0, pumpCompleted: false).Should().BeFalse();
        encounter.IsActive.Should().BeFalse();
        encounter.Update(null, 5, 3, pumpCompleted: false).Should().BeFalse("even more re-rendered lanes must not re-activate");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_Ended_CompletedPump_WalkedFar_LanesReRender_StaysEnded()
    {
        // Same guarantee for the completed-pump end: lanes re-rendering far from the pump must not revive the encounter.
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.Update(pump, 3, 0, pumpCompleted: true).Should().BeTrue("completed pump ends the encounter");
        encounter.IsActive.Should().BeFalse();

        encounter.Update(null, 3, 2, pumpCompleted: false).Should().BeFalse("re-rendered lanes must not re-activate an ended encounter");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_Ended_AreaChangeReset_NewEncounterActivates()
    {
        // Map re-entry: the area changes, Clear()/Reset() fires, and a fresh encounter can activate.
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.Update(null, 3, 2, pumpCompleted: false); // pump away, lanes flow: latch running
        encounter.Update(null, 0, 0, pumpCompleted: false); // ran, all lanes stopped while away - end
        encounter.IsActive.Should().BeFalse();

        encounter.Reset(); // area change

        // New area, new encounter: valid pump with lanes activates.
        encounter.Update(pump, 4, 4, pumpCompleted: false).Should().BeFalse("new encounter is not an end transition");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_Ended_NeverRunsAgain_UntilReset_EvenWithBuildPhaseFallback()
    {
        // The specific fallback branch that previously re-activated the ended encounter: an unreadable pump with retained lanes but no active lanes (the "never ran, build phase" guard). After an end this must NOT apply.
        var encounter = new BlightEncounter();

        encounter.Update(EntityProbeFactory.Create(), 3, 3, pumpCompleted: false);
        encounter.Update(null, 3, 2, pumpCompleted: false); // pump away, lanes flow: latch running
        encounter.Update(null, 3, 0, pumpCompleted: false).Should().BeTrue("ran then all lanes stopped - encounter ended");
        encounter.IsActive.Should().BeFalse();

        // Unreadable pump + retained lanes + no active lanes: previously the pathwayCount>0 fallback re-activated it. Now the ended latch keeps it inactive.
        encounter.Update(EntityProbeFactory.Create(isValid: false), 3, 0, pumpCompleted: false).Should().BeFalse();
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_CompletedPump_StreamsOut_EndsEncounter()
    {
        // The reported case: the pump reported success/fail/pending==0 (completed) while in range, then the player walked away and the pump streamed out. The retained lanes must NOT keep the encounter alive — the completion must be latched so it still ends.
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        // Encounter running near the pump, then the pump reports completed -> ends and clears.
        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();
        encounter.Update(pump, 3, 0, pumpCompleted: true).Should().BeTrue("completed pump ends the encounter");
        encounter.IsActive.Should().BeFalse();

        // Player walks away; the pump streams out but the lanes are retained. Previously the pathwayCount fallback kept the encounter active (lanes rendered + pathfinding to the pump).
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
        // BlightService.Clear() calls Reset(); a re-detected encounter must restart (inactive -> active) even after a completed-pump clear, so the plan rebuild fires again.
        var encounter = new BlightEncounter();
        encounter.Update(EntityProbeFactory.Create(), 3, 0, pumpCompleted: true);
        encounter.IsActive.Should().BeFalse();

        encounter.Reset();
        encounter.Update(EntityProbeFactory.Create(), 3, 0, pumpCompleted: false).Should().BeFalse("restart is not an end transition");
        encounter.IsActive.Should().BeTrue();
    }
}
