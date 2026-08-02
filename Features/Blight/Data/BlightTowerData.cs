using System.Collections.Frozen;

namespace ClickIt.Features.Blight.Data;

internal enum BlightTowerId
{
    EmptyNode = 0,
    ChillingTower1,
    ChillingTower2,
    ChillingTower3,
    FreezingTower,
    IcePrisonTower,
    ShockingTower1,
    ShockingTower2,
    ShockingTower3,
    LightningStormTower,
    ArcingTower,
    BuffTower1,
    BuffTower2,
    BuffTower3,
    BuffPlayersTower,
    WeakenEnemiesTower,
    StunningTower1,
    StunningTower2,
    StunningTower3,
    TemporalTower,
    PetrificationTower,
    MinionTower1,
    MinionTower2,
    MinionTower3,
    FlyingMinionTower,
    TankyMinionTower,
    FlameTower1,
    FlameTower2,
    FlameTower3,
    FlamethrowerTower,
    MeteorTower,
}

internal readonly record struct BlightTowerInfo(
    BlightTowerId Id,
    string DatId,
    string Name,
    int Radius,
    BlightTowerType Type,
    TowerSpecialization Specialization,
    int MenuIndex);

internal static class BlightTowerData
{
    internal static readonly BlightTowerInfo[] Catalog =
    [
        new(BlightTowerId.EmptyNode, "EmptyNode", "Tower Foundation", 0, BlightTowerType.Chilling, TowerSpecialization.None, -1),
        new(BlightTowerId.ChillingTower1, "ChillingTower1", "Chilling Tower Mk I", 35, BlightTowerType.Chilling, TowerSpecialization.None, -1),
        new(BlightTowerId.ChillingTower2, "ChillingTower2", "Chilling Tower Mk II", 35, BlightTowerType.Chilling, TowerSpecialization.None, -1),
        new(BlightTowerId.ChillingTower3, "ChillingTower3", "Chilling Tower Mk III", 35, BlightTowerType.Chilling, TowerSpecialization.None, -1),
        new(BlightTowerId.FreezingTower, "FreezingTower", "Freezebolt Tower", 75, BlightTowerType.Chilling, TowerSpecialization.Freezebolt, 0),
        new(BlightTowerId.IcePrisonTower, "IcePrisonTower", "Glacial Cage Tower", 60, BlightTowerType.Chilling, TowerSpecialization.GlacialCage, 1),
        new(BlightTowerId.ShockingTower1, "ShockingTower1", "Shock Nova Tower Mk I", 20, BlightTowerType.ShockNova, TowerSpecialization.None, -1),
        new(BlightTowerId.ShockingTower2, "ShockingTower2", "Shock Nova Tower Mk II", 25, BlightTowerType.ShockNova, TowerSpecialization.None, -1),
        new(BlightTowerId.ShockingTower3, "ShockingTower3", "Shock Nova Tower Mk III", 30, BlightTowerType.ShockNova, TowerSpecialization.None, -1),
        new(BlightTowerId.LightningStormTower, "LightningStormTower", "Lightning Storm Tower", 45, BlightTowerType.ShockNova, TowerSpecialization.LightningStorm, 0),
        new(BlightTowerId.ArcingTower, "ArcingTower", "Arc Tower", 45, BlightTowerType.ShockNova, TowerSpecialization.ArcTower, 1),
        new(BlightTowerId.BuffTower1, "BuffTower1", "Empowering Tower Mk I", 35, BlightTowerType.Empowering, TowerSpecialization.None, -1),
        new(BlightTowerId.BuffTower2, "BuffTower2", "Empowering Tower Mk II", 45, BlightTowerType.Empowering, TowerSpecialization.None, -1),
        new(BlightTowerId.BuffTower3, "BuffTower3", "Empowering Tower Mk III", 55, BlightTowerType.Empowering, TowerSpecialization.None, -1),
        new(BlightTowerId.BuffPlayersTower, "BuffPlayersTower", "Imbuing Tower", 55, BlightTowerType.Empowering, TowerSpecialization.BuffPlayers, 0),
        new(BlightTowerId.WeakenEnemiesTower, "WeakenEnemiesTower", "Smothering Tower", 55, BlightTowerType.Empowering, TowerSpecialization.Weaken, 1),
        new(BlightTowerId.StunningTower1, "StunningTower1", "Seismic Tower Mk I", 45, BlightTowerType.Seismic, TowerSpecialization.None, -1),
        new(BlightTowerId.StunningTower2, "StunningTower2", "Seismic Tower Mk II", 45, BlightTowerType.Seismic, TowerSpecialization.None, -1),
        new(BlightTowerId.StunningTower3, "StunningTower3", "Seismic Tower Mk III", 45, BlightTowerType.Seismic, TowerSpecialization.None, -1),
        new(BlightTowerId.TemporalTower, "TemporalTower", "Temporal Tower", 45, BlightTowerType.Seismic, TowerSpecialization.Temporal, 0),
        new(BlightTowerId.PetrificationTower, "PetrificationTower", "Stone Gaze Tower", 45, BlightTowerType.Seismic, TowerSpecialization.StoneGaze, 1),
        new(BlightTowerId.MinionTower1, "MinionTower1", "Summoning Tower Mk I", 30, BlightTowerType.Summoning, TowerSpecialization.None, -1),
        new(BlightTowerId.MinionTower2, "MinionTower2", "Summoning Tower Mk II", 30, BlightTowerType.Summoning, TowerSpecialization.None, -1),
        new(BlightTowerId.MinionTower3, "MinionTower3", "Summoning Tower Mk III", 30, BlightTowerType.Summoning, TowerSpecialization.None, -1),
        new(BlightTowerId.FlyingMinionTower, "FlyingMinionTower", "Scout Tower", 75, BlightTowerType.Summoning, TowerSpecialization.ScoutMinion, 1),
        new(BlightTowerId.TankyMinionTower, "TankyMinionTower", "Sentinel Tower", 45, BlightTowerType.Summoning, TowerSpecialization.TankMinion, 0),
        new(BlightTowerId.FlameTower1, "FlameTower1", "Fireball Tower Mk I", 45, BlightTowerType.Fireball, TowerSpecialization.None, -1),
        new(BlightTowerId.FlameTower2, "FlameTower2", "Fireball Tower Mk II", 60, BlightTowerType.Fireball, TowerSpecialization.None, -1),
        new(BlightTowerId.FlameTower3, "FlameTower3", "Fireball Tower Mk III", 75, BlightTowerType.Fireball, TowerSpecialization.None, -1),
        new(BlightTowerId.FlamethrowerTower, "FlamethrowerTower", "Flamethrower Tower", 45, BlightTowerType.Fireball, TowerSpecialization.Flamethrower, 0),
        new(BlightTowerId.MeteorTower, "MeteorTower", "Meteor Tower", 100, BlightTowerType.Fireball, TowerSpecialization.Meteor, 1),
    ];

    private static readonly FrozenDictionary<string, BlightTowerInfo> ByDatId = BuildByDatId();
    private static readonly FrozenDictionary<string, BlightTowerType> TypeByDatId = BuildTypeByDatId();
    private static readonly FrozenDictionary<(BlightTowerType, TowerSpecialization), BlightTowerInfo> ByTypeSpec = BuildByTypeSpec();
    private static readonly FrozenDictionary<(BlightTowerType, int), int> RadiusByLevel = BuildRadiusByLevel();
    private static readonly (string Prefix, BlightTowerType Type)[] FoundationPrefixes = BuildFoundationPrefixes();

    private const int DefaultRadius = 35;

    internal static BlightTowerInfo Get(BlightTowerId id) => Catalog[(int)id];

    internal static BlightTowerInfo? FindByDatId(string datId)
        => ByDatId.TryGetValue(datId, out BlightTowerInfo info) ? info : null;

    internal static BlightTowerType? MapTowerIdToType(string datId)
        => TypeByDatId.TryGetValue(datId, out BlightTowerType type) ? type : null;

    internal static int FindRadius(string datId)
        => ByDatId.TryGetValue(datId, out BlightTowerInfo info) ? info.Radius : 0;

    internal static string GetDisplayName(BlightTowerType type) => type switch
    {
        BlightTowerType.Chilling => "Chilling Tower",
        BlightTowerType.ShockNova => "Shock Nova Tower",
        BlightTowerType.Empowering => "Empowering Tower",
        BlightTowerType.Seismic => "Seismic Tower",
        BlightTowerType.Summoning => "Summoning Tower",
        BlightTowerType.Fireball => "Fireball Tower",
        _ => "Unknown"
    };

    internal static int MaxUpgradeLevel => 4;

    internal static string GetSpecializationTowerId(BlightTowerType type, TowerSpecialization spec)
        => ByTypeSpec.TryGetValue((type, spec), out BlightTowerInfo info) ? info.DatId : string.Empty;

    internal static int GetSpecializationMenuChildIndex(BlightTowerType type, TowerSpecialization spec)
        => ByTypeSpec.TryGetValue((type, spec), out BlightTowerInfo info) ? info.MenuIndex : -1;

    internal static int RadiusForLevel(BlightTowerType type, int level)
    {
        if (RadiusByLevel.TryGetValue((type, level), out int radius))
            return radius;
        return RadiusByLevel.TryGetValue((type, MaxUpgradeLevel), out int maxRadius)
            ? maxRadius
            : DefaultRadius;
    }

    internal static BlightTowerType? MapFoundationSuffix(string suffix)
    {
        for (int i = 0; i < FoundationPrefixes.Length; i++)
        {
            if (suffix.StartsWith(FoundationPrefixes[i].Prefix, StringComparison.OrdinalIgnoreCase))
                return FoundationPrefixes[i].Type;
        }
        return null;
    }

    private static FrozenDictionary<string, BlightTowerInfo> BuildByDatId()
    {
        Dictionary<string, BlightTowerInfo> map = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Catalog.Length; i++)
            map[Catalog[i].DatId] = Catalog[i];
        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static FrozenDictionary<string, BlightTowerType> BuildTypeByDatId()
    {
        Dictionary<string, BlightTowerType> map = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Catalog.Length; i++)
        {
            if (Catalog[i].Id == BlightTowerId.EmptyNode)
                continue; // foundation — not a tower
            map[Catalog[i].DatId] = Catalog[i].Type;
        }
        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static FrozenDictionary<(BlightTowerType, TowerSpecialization), BlightTowerInfo> BuildByTypeSpec()
    {
        Dictionary<(BlightTowerType, TowerSpecialization), BlightTowerInfo> map = [];
        for (int i = 0; i < Catalog.Length; i++)
        {
            if (Catalog[i].Specialization == TowerSpecialization.None)
                continue;
            map[(Catalog[i].Type, Catalog[i].Specialization)] = Catalog[i];
        }
        return map.ToFrozenDictionary();
    }

    private static FrozenDictionary<(BlightTowerType, int), int> BuildRadiusByLevel()
    {
        Dictionary<(BlightTowerType, int), int> map = [];
        for (int i = 0; i < Catalog.Length; i++)
        {
            BlightTowerInfo e = Catalog[i];
            if (e.Specialization != TowerSpecialization.None || e.Id == BlightTowerId.EmptyNode)
                continue;
            char last = e.DatId[^1];
            if (char.IsAsciiDigit(last))
                map[(e.Type, last - '0')] = e.Radius;
        }
        // Fireball rank 4 is the Meteor specialization — the strategies that plan Fireball always pick Meteor.
        if (ByTypeSpec.TryGetValue((BlightTowerType.Fireball, TowerSpecialization.Meteor), out BlightTowerInfo meteor))
            map[(BlightTowerType.Fireball, 4)] = meteor.Radius;
        return map.ToFrozenDictionary();
    }

    private static (string Prefix, BlightTowerType Type)[] BuildFoundationPrefixes()
    {
        List<(string, BlightTowerType)> result = [];
        for (int i = 0; i < Catalog.Length; i++)
        {
            BlightTowerInfo e = Catalog[i];
            if (e.Specialization != TowerSpecialization.None || e.Id == BlightTowerId.EmptyNode)
                continue;
            if (!e.DatId.EndsWith("Tower1", StringComparison.Ordinal))
                continue;
            result.Add((e.DatId[..^"Tower1".Length], e.Type)); // "ChillingTower1" → "Chilling"
        }
        return [.. result];
    }
}
