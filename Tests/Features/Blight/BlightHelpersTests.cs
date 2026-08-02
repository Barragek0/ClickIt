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
}
