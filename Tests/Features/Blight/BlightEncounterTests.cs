namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightEncounterTests
{
    [TestMethod]
    public void FreeBuildCostIsRetainedOnlyForTheRecordedMapInstance()
    {
        var encounter = new BlightEncounter();
        var freeMap = new BlightAreaKey(1, 10);
        var otherInstance = new BlightAreaKey(1, 20);

        encounter.SwitchArea(freeMap);
        encounter.GetAreaAwaitingTowerCost().Should().Be(freeMap);
        encounter.TryRecordTowerBuildCost(freeMap, 0).Should().BeTrue();
        encounter.HasFreeTowerConstruction.Should().BeTrue();
        encounter.GetAreaAwaitingTowerCost().Should().BeNull();

        encounter.SwitchArea(otherInstance);
        encounter.HasFreeTowerConstruction.Should().BeFalse();
        encounter.TryRecordTowerBuildCost(freeMap, 0).Should().BeFalse();
        encounter.TryRecordTowerBuildCost(otherInstance, 100).Should().BeTrue();
        encounter.HasFreeTowerConstruction.Should().BeFalse();

        encounter.SwitchArea(freeMap);
        encounter.HasFreeTowerConstruction.Should().BeTrue();
        encounter.Reset();
        encounter.HasFreeTowerConstruction.Should().BeFalse();
    }

    [TestMethod]
    public void Update_ReenteredMapWithInvalidPumpAndRetainedActiveLanes_DoesNotStartEncounter()
    {
        var encounter = new BlightEncounter();
        var invalidPump = EntityProbeFactory.Create(isValid: false);

        encounter.Update(invalidPump, 41, 20, pumpCompleted: false).Should().BeFalse();
        encounter.IsActive.Should().BeFalse();
        encounter.Update(invalidPump, 41, 0, pumpCompleted: false).Should().BeFalse();
        encounter.IsActive.Should().BeFalse();
        encounter.Update(EntityProbeFactory.Create(), 41, 20, pumpCompleted: true).Should().BeFalse();
        encounter.IsActive.Should().BeFalse();
        encounter.Update(invalidPump, 41, 20, pumpCompleted: false).Should().BeFalse();
        encounter.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public void Update_DeathDuringEncounter_ResumesWhenPumpIsReadable()
    {
        var encounter = new BlightEncounter();
        var pump = EntityProbeFactory.Create();
        encounter.Update(pump, 12, 8, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        // Leaving the map resets the encounter. Re-entry initially exposes only
        // retained lanes, then the pump becomes readable again.
        encounter.Reset();
        encounter.Update(null, 12, 8, pumpCompleted: false);
        encounter.IsActive.Should().BeFalse();
        encounter.Update(pump, 12, 8, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();
        encounter.Update(null, 12, 8, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();
        encounter.Update(pump, 12, 8, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void SwitchArea_ReturningToSameMapInstance_ResumesKnownActiveEncounterWithoutEntities()
    {
        var encounter = new BlightEncounter();
        BlightAreaKey map = new(3387199367, -1533056491);
        BlightAreaKey hideout = new(12, 42);
        Entity pump = EntityProbeFactory.Create();

        encounter.SwitchArea(map);
        encounter.Update(pump, 12, 8, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();

        encounter.SwitchArea(hideout);
        encounter.IsActive.Should().BeFalse();
        encounter.SwitchArea(map);

        encounter.Update(null, pathwayCount: 0, activePathwayCount: 0, pumpCompleted: false).Should().BeFalse();
        encounter.IsActive.Should().BeTrue("the same map instance was previously confirmed active, even though its pump and lanes have not streamed in");
    }

    [TestMethod]
    public void SwitchArea_ReturningToCompletedMapInstance_StaysInactiveWithoutEntities()
    {
        var encounter = new BlightEncounter();
        BlightAreaKey map = new(3387199367, -1533056491);
        BlightAreaKey hideout = new(12, 42);
        Entity pump = EntityProbeFactory.Create();

        encounter.SwitchArea(map);
        encounter.Update(pump, 12, 8, pumpCompleted: false);
        encounter.Update(pump, 12, 0, pumpCompleted: true).Should().BeTrue();
        encounter.SwitchArea(hideout);
        encounter.SwitchArea(map);

        encounter.Update(null, pathwayCount: 0, activePathwayCount: 0, pumpCompleted: false).Should().BeFalse();
        encounter.IsActive.Should().BeFalse("the same map instance had already recorded the encounter as complete");
    }

    [TestMethod]
    public void SwitchArea_DifferentInstanceOfSameMap_DoesNotReuseEncounterState()
    {
        var encounter = new BlightEncounter();
        BlightAreaKey firstMap = new(3387199367, -1533056491);
        BlightAreaKey secondMap = new(3387199367, -1533056492);
        Entity pump = EntityProbeFactory.Create();

        encounter.SwitchArea(firstMap);
        encounter.Update(pump, 12, 8, pumpCompleted: false);
        encounter.SwitchArea(secondMap);
        encounter.Update(null, pathwayCount: 0, activePathwayCount: 0, pumpCompleted: false);

        encounter.IsActive.Should().BeFalse("area hash is shared by map instances, so the instance id must also match");
    }

    [TestMethod]
    public void Update_EnteringWithPumpStreamedOut_WaitsForPumpBeforeActivating()
    {
        var encounter = new BlightEncounter();
        encounter.Update(null, 12, 8, pumpCompleted: false);
        encounter.IsActive.Should().BeFalse();
        encounter.Update(EntityProbeFactory.Create(), 12, 8, pumpCompleted: false);
        encounter.IsActive.Should().BeTrue();
    }
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
