namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightHelpersTests
{
    [TestMethod]
    [DataRow("Metadata/Monsters/LeagueBlight/BlightFoundationChilling", (int)BlightTowerType.Chilling)]
    [DataRow("Metadata/Monsters/LeagueBlight/BlightFoundationStunning", (int)BlightTowerType.Seismic)]
    [DataRow("Metadata/Monsters/LeagueBlight/BlightFoundationFlame", (int)BlightTowerType.Fireball)]
    [DataRow("Metadata/Monsters/LeagueBlight/BlightFoundationBuff", (int)BlightTowerType.Empowering)]
    [DataRow("Metadata/Monsters/LeagueBlight/BlightFoundationShocking", (int)BlightTowerType.ShockNova)]
    [DataRow("Metadata/Monsters/LeagueBlight/BlightFoundationMinion", (int)BlightTowerType.Summoning)]
    public void DetectFoundationTypeFromPath_MapsEachFoundationPathToItsTowerType(string path, int expected)
    {
        // Regression guard: ScanFoundations previously hardcoded every labeled foundation as
        // Chilling and overwrote the type the entity scan had already resolved from the path.
        BlightHelpers.DetectFoundationTypeFromPath(path).Should().Be((BlightTowerType)expected);
    }

    [TestMethod]
    public void DetectFoundationTypeFromPath_UnknownOrMissingPath_FallsBackToChilling()
    {
        BlightHelpers.DetectFoundationTypeFromPath(null).Should().Be(BlightTowerType.Chilling);
        BlightHelpers.DetectFoundationTypeFromPath("").Should().Be(BlightTowerType.Chilling);
        BlightHelpers.DetectFoundationTypeFromPath("Metadata/Terrain/Leagues/Blight/Objects/BlightPathway")
            .Should().Be(BlightTowerType.Chilling, "paths without a BlightFoundation marker are not foundations");
    }

    [TestMethod]
    [DataRow("Metadata/Monsters/LeagueBlight/BlightTowerFlameRank3@83", (int)BlightTowerType.Fireball)]
    [DataRow("Metadata/Monsters/LeagueBlight/BlightTowerChillingRank2@83", (int)BlightTowerType.Chilling)]
    [DataRow("Metadata/Monsters/LeagueBlight/BlightTowerStunningRank4@83", (int)BlightTowerType.Seismic)]
    [DataRow("Metadata/Monsters/LeagueBlight/BlightTowerBuffRank1@83", (int)BlightTowerType.Empowering)]
    [DataRow("Metadata/Monsters/LeagueBlight/BlightTowerShockingRank3@83", (int)BlightTowerType.ShockNova)]
    [DataRow("Metadata/Monsters/LeagueBlight/BlightTowerMinionRank2@83", (int)BlightTowerType.Summoning)]
    public void DetectTowerTypeFromPath_MapsEachTowerPathToItsBaseType(string path, int expected)
    {
        // Regression guard: streamed-out/far-away towers have an unreadable BlightTower component, so
        // the retained path's type marker is the only signal for keeping them in the coverage set.
        BlightHelpers.DetectTowerTypeFromPath(path).Should().Be((BlightTowerType)expected);
    }

    [TestMethod]
    public void DetectTowerTypeFromPath_NonTowerPath_ReturnsNull()
    {
        BlightHelpers.DetectTowerTypeFromPath(null).Should().BeNull();
        BlightHelpers.DetectTowerTypeFromPath("").Should().BeNull();
        BlightHelpers.DetectTowerTypeFromPath("Metadata/Terrain/Leagues/Blight/Objects/BlightPathway")
            .Should().BeNull("paths without a BlightTower marker are not towers");
        BlightHelpers.DetectTowerTypeFromPath("Metadata/Monsters/LeagueBlight/BlightTower@83")
            .Should().BeNull("a tower path with no Rank segment has no type marker");
    }

    [TestMethod]
    public void WorldToGrid_ConvertsWorldCoordinatesBackToGrid()
    {
        System.Numerics.Vector3 world = new(100f, 200f, -281f);
        BlightHelpers.WorldToGrid(world).X.Should().BeApproximately(9.2f, 0.001f);
        BlightHelpers.WorldToGrid(world).Y.Should().BeApproximately(18.4f, 0.001f);
    }

    [TestMethod]
    public void WorldToGrid_RoundTripsThroughGridToWorld()
    {
        NumVector2 grid = new(615f, 1390f);
        NumVector2 back = BlightHelpers.WorldToGrid(BlightHelpers.GridToWorld(grid));
        back.X.Should().BeApproximately(grid.X, 0.01f);
        back.Y.Should().BeApproximately(grid.Y, 0.01f);
    }

    [TestMethod]
    public void BeamAnchorToGrid_WithBeamStart_UsesTheBeamAnchor()
    {
        System.Numerics.Vector3 beamStart = new(100f, 200f, -281f);
        NumVector2 anchor = BlightHelpers.BeamAnchorToGrid(new NumVector2(10f, 20f), beamStart);
        anchor.X.Should().BeApproximately(9.2f, 0.001f);
        anchor.Y.Should().BeApproximately(18.4f, 0.001f);
    }

    [TestMethod]
    public void BeamAnchorToGrid_WithZeroBeamStart_FallsBackToGridPos()
    {
        BlightHelpers.BeamAnchorToGrid(new NumVector2(10f, 20f), default)
            .Should().Be(new NumVector2(10f, 20f));
    }

    [TestMethod]
    public void BeamWorld_WithBeamStart_ReturnsTheBeamWorldPosition()
    {
        System.Numerics.Vector3 beamStart = new(100f, 200f, -281f);
        BlightHelpers.BeamWorld(new NumVector2(10f, 20f), beamStart).Should().Be(beamStart);
    }

    [TestMethod]
    public void BeamWorld_WithZeroBeamStart_FallsBackToFlatGridWorld()
    {
        System.Numerics.Vector3 world = BlightHelpers.BeamWorld(new NumVector2(10f, 20f), default);
        world.X.Should().BeApproximately(10f / 0.092f, 0.01f);
        world.Y.Should().BeApproximately(20f / 0.092f, 0.01f);
        world.Z.Should().Be(0f);
    }

    [TestMethod]
    public void TryReadStateValue_ReadsNumericValuesRobustly()
    {
        // StateMachine state values can be int/long/byte; the robust read converts any numeric form.
        // The probe must be PUBLIC: dynamic dispatch from the product assembly cannot bind to an
        // internal test type's members (RuntimeBinderException -> 0).
        BlightHelpers.TryReadStateValue(new StateProbe { Value = 5L }).Should().Be(5);
        BlightHelpers.TryReadStateValue(new StateProbe { Value = 1 }).Should().Be(1);
        BlightHelpers.TryReadStateValue(new StateProbe { Value = 0L }).Should().Be(0);
    }

    [TestMethod]
    public void TryReadStateValue_UnreadableValue_ReturnsZero()
    {
        // One state with an unreadable value must not fail the whole StateMachine read: the robust
        // per-state conversion returns 0 so the "pending"/"success"/"fail" checks still run on the
        // other states (this was failing open and kept the completed pump/encounter active).
        BlightHelpers.TryReadStateValue(new StateProbe { Value = "not-a-number" }).Should().Be(0);
    }

    public sealed class StateProbe
    {
        public object? Value { get; set; }
    }
}
