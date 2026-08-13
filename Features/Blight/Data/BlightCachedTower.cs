namespace ClickIt.Features.Blight.Data;

internal sealed class BlightCachedTower
{
    public NumVector2 WorldPosition { get; set; }
    public BlightTowerType TowerType { get; set; }
    public BlightTowerType PlannedTowerType { get; set; }
    public int UpgradeLevel { get; set; }
    public Element? LabelElement { get; set; }
    public Entity? FoundationEntity { get; set; }

    // WorldPos3 (PosNum) is used by in-world helpers and WorldToScreen; WorldPosition (grid) by map helpers. Null only for foundations restored from saved state after their entity streamed out.
    public System.Numerics.Vector3? WorldPos3 { get; set; }

    // ACTUAL radius at the current level from BlightTowerDat (0 = unknown); coverage falls back to the linear estimate only for streamed-out towers, never inflating coverage for a tower we can measure.
    public int Radius { get; set; }

    public BlightCachedTower(NumVector2 worldPosition, BlightTowerType towerType, int upgradeLevel = 0)
    {
        WorldPosition = worldPosition;
        TowerType = towerType;
        PlannedTowerType = towerType;
        UpgradeLevel = upgradeLevel;
    }
}
