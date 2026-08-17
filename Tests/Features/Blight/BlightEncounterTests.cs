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
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 0, 0, pumpCompleted: false).Should().BeFalse("no pathway entities yet - not active");
        encounter.IsActive.Should().BeFalse();

        encounter.Update(pump, 3, 0, pumpCompleted: false).Should().BeFalse("starting is not an end transition");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_StreamOut_WithActiveLanes_KeepsEncounterActive()
    {
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
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 3, 2, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 3, 0, pumpCompleted: false).Should().BeTrue("ran then all lanes stopped while away - encounter ended");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_StreamOut_NoActiveLanes_BeforeRunning_KeepsActive()
    {
        var encounter = new BlightEncounter();
        encounter.Update(EntityProbeFactory.Create(), 3, 0, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 3, 0, pumpCompleted: false).Should().BeFalse("never ran - don't clear the build-phase encounter");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_InvalidPump_WithActiveLanes_KeepsEncounterActive()
    {
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

        encounter.Update(EntityProbeFactory.Create(), 3, 3, pumpCompleted: false);
        encounter.Update(invalidPump, 3, 2, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(invalidPump, 3, 0, pumpCompleted: false).Should().BeTrue("invalid pump and all lanes stopped - encounter ended");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_Ended_SameAreaRestartSuppressed_UntilReset()
    {
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 3, 2, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 0, 0, pumpCompleted: false); // ran, all lanes stopped while away - encounter ends
        encounter.IsActive.Should().BeFalse();

        encounter.Update(pump, 3, 3, pumpCompleted: false).Should().BeFalse("an ended encounter must not restart in the same area");
        encounter.IsActive.Should().BeFalse();

        encounter.Reset();
        encounter.Update(pump, 3, 3, pumpCompleted: false).Should().BeFalse("restart after reset is not an end transition");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_Ended_WalkedFar_LanesReRender_StaysEnded()
    {
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.Update(null, 3, 2, pumpCompleted: false).Should().BeFalse("pump away but lanes still flowing - still active");
        encounter.IsActive.Should().BeTrue();
        encounter.Update(null, 3, 0, pumpCompleted: false).Should().BeTrue("ran then all lanes stopped while away - encounter ended");
        encounter.IsActive.Should().BeFalse();

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
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.Update(null, 3, 2, pumpCompleted: false); // pump away, lanes flow: latch running
        encounter.Update(null, 0, 0, pumpCompleted: false); // ran, all lanes stopped while away - end
        encounter.IsActive.Should().BeFalse();

        encounter.Reset(); // area change

        encounter.Update(pump, 4, 4, pumpCompleted: false).Should().BeFalse("new encounter is not an end transition");
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void Update_Ended_NeverRunsAgain_UntilReset_EvenWithBuildPhaseFallback()
    {
        var encounter = new BlightEncounter();

        encounter.Update(EntityProbeFactory.Create(), 3, 3, pumpCompleted: false);
        encounter.Update(null, 3, 2, pumpCompleted: false); // pump away, lanes flow: latch running
        encounter.Update(null, 3, 0, pumpCompleted: false).Should().BeTrue("ran then all lanes stopped - encounter ended");
        encounter.IsActive.Should().BeFalse();

        encounter.Update(EntityProbeFactory.Create(isValid: false), 3, 0, pumpCompleted: false).Should().BeFalse();
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_CompletedPump_StreamsOut_EndsEncounter()
    {
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();

        encounter.Update(pump, 3, 3, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();
        encounter.Update(pump, 3, 0, pumpCompleted: true).Should().BeTrue("completed pump ends the encounter");
        encounter.IsActive.Should().BeFalse();

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

        var invalidPump = EntityProbeFactory.Create(isValid: false);
        encounter.Update(invalidPump, 3, 0, pumpCompleted: false).Should().BeFalse("already inactive - no new end transition");
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_CompletedPump_ResetThenRestart_ActivatesAgain()
    {
        var encounter = new BlightEncounter();
        encounter.Update(EntityProbeFactory.Create(), 3, 0, pumpCompleted: true);
        encounter.IsActive.Should().BeFalse();

        encounter.Reset();
        encounter.Update(EntityProbeFactory.Create(), 3, 0, pumpCompleted: false).Should().BeFalse("restart is not an end transition");
        encounter.IsActive.Should().BeTrue();
    }
}
