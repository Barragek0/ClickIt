namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightEncounterTests
{
    [TestMethod]
    public void Update_NullPump_StaysInactive()
    {
        var encounter = new BlightEncounter();
        encounter.IsActive.Should().BeFalse();

        encounter.Update(null, 0).Should().BeFalse("no pump means no encounter and no end transition");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_ActiveOnlyWhenPumpAndPathwaysArePresent()
    {
        var encounter = new BlightEncounter();
        // A bare Entity has no loaded StateMachine, so IsPumpCompleted is safely false (guarded).
        var pump = new Entity();

        encounter.Update(pump, 0).Should().BeFalse("no pathway entities yet — not active");
        encounter.IsActive.Should().BeFalse();

        encounter.Update(pump, 3).Should().BeFalse("starting is not an end transition");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_PumpLost_EndsEncounter()
    {
        var encounter = new BlightEncounter();
        encounter.Update(new Entity(), 3);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 0).Should().BeTrue("pump lost — encounter ended");
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
        var pump = new Entity();

        encounter.Update(pump, 3);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 0); // died / left — encounter ends
        encounter.IsActive.Should().BeFalse();

        encounter.Update(pump, 3).Should().BeFalse("restart is not an end transition");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_StreamedOutPump_WithPersistedPosition_KeepsEncounterActive()
    {
        var encounter = new BlightEncounter();
        encounter.Update(new Entity(), 3);
        encounter.IsActive.Should().BeTrue();

        // Pump entity streamed out of scan range (position still cached) — not an end.
        encounter.Update(null, 0, hasPersistedPump: true).Should().BeFalse("stream-out is not an encounter end");
        encounter.IsActive.Should().BeTrue("the encounter stays active while the pump position is cached");

        // Fully gone (no persisted position) — ends, so cached data is cleared.
        encounter.Update(null, 0, hasPersistedPump: false).Should().BeTrue("no pump and no cached position ends the encounter");
        encounter.IsActive.Should().BeFalse();
    }
}
