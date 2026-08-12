namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightEntityCachePerformanceTests
{
    [TestMethod]
    public void ComputeCoverageDataSignature_IsDeterministic_ForIdenticalData()
    {
        BlightPathwayIcon[] pathways = [CreateIcon(1, 1, 0), CreateIcon(2, 2, 1), CreateIcon(3, 3, 2)];
        (Entity Entity, string TowerId)[] towers = [CreateTowerEntity(10, 20)];
        BlightCachedTower[] known = [CreateKnownTower(5, 5, BlightTowerType.Chilling, 2, radius: 35)];

        int first = BlightEntityCache.ComputeCoverageDataSignature(pathways, towers, known);
        int second = BlightEntityCache.ComputeCoverageDataSignature(pathways, towers, known);

        first.Should().Be(second, "identical data must hash identically within the process");
    }

    [TestMethod]
    public void ComputeCoverageDataSignature_Changes_WhenBuiltTowerLevelChanges()
    {
        BlightPathwayIcon[] pathways = [CreateIcon(1, 1, 0), CreateIcon(2, 2, 1)];
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
        BlightPathwayIcon[] pathways = [CreateIcon(1, 1, 0), CreateIcon(2, 2, 1)];
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
        BlightPathwayIcon[] twoPathways = [CreateIcon(1, 1, 0), CreateIcon(2, 2, 1)];
        BlightPathwayIcon[] threePathways = [CreateIcon(1, 1, 0), CreateIcon(2, 2, 1), CreateIcon(3, 3, 2)];
        (Entity Entity, string TowerId)[] towers = [CreateTowerEntity(10, 20)];
        BlightCachedTower[] known = [CreateKnownTower(5, 5, BlightTowerType.Chilling, 1, radius: 35)];

        int before = BlightEntityCache.ComputeCoverageDataSignature(twoPathways, towers, known);
        int after = BlightEntityCache.ComputeCoverageDataSignature(threePathways, towers, known);

        before.Should().NotBe(after, "a new pathway alters the lane geometry");
    }

    private static BlightPathwayIcon CreateIcon(float x, float y, int id)
        => new(id, new NumVector2(x, y), 0, default, default, []);

    private static (Entity Entity, string TowerId) CreateTowerEntity(float gridX, float gridY)
        => (EntityProbeFactory.Create(gridX: gridX, gridY: gridY, type: EntityType.Chest), "ChillingTower1");

    private static BlightCachedTower CreateKnownTower(
        float gridX, float gridY, BlightTowerType type, int level, int radius)
        => new(new NumVector2(gridX, gridY), type, level) { Radius = radius };
}
