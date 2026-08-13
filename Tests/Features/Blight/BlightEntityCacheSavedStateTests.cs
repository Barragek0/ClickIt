namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightEntityCacheSavedStateTests
{
    private static Dictionary<NumVector2, (BlightTowerType Type, int Level, BlightTowerType Planned)> Saved(
        params (NumVector2 Pos, BlightTowerType Type, int Level, BlightTowerType Planned)[] entries)
    {
        Dictionary<NumVector2, (BlightTowerType Type, int Level, BlightTowerType Planned)> map = [];
        foreach ((NumVector2 pos, BlightTowerType type, int level, BlightTowerType planned) in entries)
            map[pos] = (type, level, planned);
        return map;
    }

    [TestMethod]
    public void RestoreSavedState_DriftedPosition_RestoresBuiltLevel_NoPhantomFoundation()
    {
        // A tower was built at grid cell ~(10,20); the next scan's foundation entity reports (10.4,20.3) — the SAME cell, but not an exact dictionary-key match.
        List<BlightCachedTower> scanned = [new(new NumVector2(10.4f, 20.3f), BlightTowerType.Chilling)];
        Dictionary<NumVector2, (BlightTowerType, int, BlightTowerType)> saved = Saved(
            (new NumVector2(10.1f, 20.1f), BlightTowerType.Chilling, 3, BlightTowerType.Chilling));

        int restored = BlightEntityCache.RestoreSavedState(scanned, saved);

        restored.Should().Be(0, "the scanned foundation consumes the saved built tower");
        scanned.Should().HaveCount(1, "no duplicate tower entry is created");
        scanned[0].TowerType.Should().Be(BlightTowerType.Chilling);
        scanned[0].UpgradeLevel.Should().Be(3, "the built level survives the sub-unit position drift");
        saved.Should().BeEmpty("the matched saved state is consumed");
    }

    [TestMethod]
    public void RestoreSavedState_ExactMatch_StillRestores()
    {
        List<BlightCachedTower> scanned = [new(new NumVector2(10, 20), BlightTowerType.Chilling)];
        Dictionary<NumVector2, (BlightTowerType, int, BlightTowerType)> saved = Saved(
            (new NumVector2(10, 20), BlightTowerType.Seismic, 2, BlightTowerType.Seismic));

        int restored = BlightEntityCache.RestoreSavedState(scanned, saved);

        restored.Should().Be(0);
        scanned[0].TowerType.Should().Be(BlightTowerType.Seismic);
        scanned[0].UpgradeLevel.Should().Be(2);
        scanned[0].PlannedTowerType.Should().Be(BlightTowerType.Seismic);
    }

    [TestMethod]
    public void RestoreSavedState_FarAwayState_IsRestoredAsSeparateEntry()
    {
        // A genuinely streamed-out tower (far from any scanned foundation) still survives the scan.
        List<BlightCachedTower> scanned = [new(new NumVector2(10, 20), BlightTowerType.Chilling)];
        Dictionary<NumVector2, (BlightTowerType, int, BlightTowerType)> saved = Saved(
            (new NumVector2(500, 500), BlightTowerType.Fireball, 2, BlightTowerType.Fireball));

        int restored = BlightEntityCache.RestoreSavedState(scanned, saved);

        restored.Should().Be(1, "the far-away streamed-out tower is restored as its own entry");
        scanned.Should().HaveCount(2);
        scanned[1].WorldPosition.Should().Be(new NumVector2(500, 500));
        scanned[1].TowerType.Should().Be(BlightTowerType.Fireball);
        scanned[1].UpgradeLevel.Should().Be(2);
    }

    [TestMethod]
    public void RestoreSavedState_TwoFoundationsNearOneSavedTower_FirstMatchConsumesIt()
    {
        // Two scanned foundations within the tolerance of one saved tower — the first consumes it, the second stays a fresh foundation (same ambiguity as every other tolerance lookup).
        List<BlightCachedTower> scanned =
        [
            new(new NumVector2(10.0f, 20.0f), BlightTowerType.Chilling),
            new(new NumVector2(10.4f, 20.3f), BlightTowerType.Chilling),
        ];
        Dictionary<NumVector2, (BlightTowerType, int, BlightTowerType)> saved = Saved(
            (new NumVector2(10.2f, 20.2f), BlightTowerType.Seismic, 1, BlightTowerType.Seismic));

        int restored = BlightEntityCache.RestoreSavedState(scanned, saved);

        restored.Should().Be(0);
        scanned[0].UpgradeLevel.Should().Be(1, "the first in-range foundation consumes the state");
        scanned[1].UpgradeLevel.Should().Be(0, "the second stays a fresh foundation");
    }
}
