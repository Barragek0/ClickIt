namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightEntityCachePerformanceTests
{
    [TestMethod]
    public void ComputeCoverageDataSignature_IsDeterministic_ForIdenticalData()
    {
        NumVector2[] pathways = [new(1, 1), new(2, 2), new(3, 3)];
        (Entity Entity, string TowerId)[] towers = [CreateTowerEntity(10, 20)];
        BlightCachedTower[] known = [CreateKnownTower(5, 5, BlightTowerType.Chilling, 2, radius: 35)];

        int first = BlightEntityCache.ComputeCoverageDataSignature(pathways, towers, known);
        int second = BlightEntityCache.ComputeCoverageDataSignature(pathways, towers, known);

        first.Should().Be(second, "identical data must hash identically within the process");
    }

    [TestMethod]
    public void ComputeCoverageDataSignature_Changes_WhenBuiltTowerLevelChanges()
    {
        NumVector2[] pathways = [new(1, 1), new(2, 2)];
        (Entity Entity, string TowerId)[] towers = [];
        BlightCachedTower[] levelOne = [CreateKnownTower(5, 5, BlightTowerType.Chilling, 1, radius: 35)];
        BlightCachedTower[] levelTwo = [CreateKnownTower(5, 5, BlightTowerType.Chilling, 2, radius: 35)];

        int before = BlightEntityCache.ComputeCoverageDataSignature(pathways, towers, levelOne);
        int after = BlightEntityCache.ComputeCoverageDataSignature(pathways, towers, levelTwo);

        before.Should().NotBe(after, "a built tower level change alters coverage");
    }

    [TestMethod]
    public void ComputeCoverageDataSignature_Changes_WhenTowerCountChanges()
    {
        NumVector2[] pathways = [new(1, 1), new(2, 2)];
        (Entity Entity, string TowerId)[] towers = [CreateTowerEntity(10, 20)];
        BlightCachedTower[] known = [CreateKnownTower(5, 5, BlightTowerType.Chilling, 1, radius: 35)];

        int before = BlightEntityCache.ComputeCoverageDataSignature(pathways, towers, known);
        (Entity Entity, string TowerId)[] moreTowers = [CreateTowerEntity(10, 20), CreateTowerEntity(30, 40)];
        int after = BlightEntityCache.ComputeCoverageDataSignature(pathways, moreTowers, known);

        before.Should().NotBe(after, "a new tower entity alters coverage");
    }

    [TestMethod]
    public void ComputeCoverageDataSignature_Changes_WhenPathwayCountChanges()
    {
        NumVector2[] twoPathways = [new(1, 1), new(2, 2)];
        NumVector2[] threePathways = [new(1, 1), new(2, 2), new(3, 3)];
        (Entity Entity, string TowerId)[] towers = [CreateTowerEntity(10, 20)];
        BlightCachedTower[] known = [CreateKnownTower(5, 5, BlightTowerType.Chilling, 1, radius: 35)];

        int before = BlightEntityCache.ComputeCoverageDataSignature(twoPathways, towers, known);
        int after = BlightEntityCache.ComputeCoverageDataSignature(threePathways, towers, known);

        before.Should().NotBe(after, "a new pathway alters the lane geometry");
    }

    private static (Entity Entity, string TowerId) CreateTowerEntity(float gridX, float gridY)
        => (EntityProbeFactory.Create(gridX: gridX, gridY: gridY, type: EntityType.Chest), "ChillingTower1");

    private static BlightCachedTower CreateKnownTower(
        float gridX, float gridY, BlightTowerType type, int level, int radius)
        => new(new NumVector2(gridX, gridY), type, level) { Radius = radius };
}
