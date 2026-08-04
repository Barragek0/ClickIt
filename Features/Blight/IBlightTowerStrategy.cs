namespace ClickIt.Features.Blight;

internal enum TowerBuildPriority
{
    Critical = 0,
    High = 1,
    Normal = 2,
    Low = 3,
    Fill = 4
}

internal enum TowerUpgradePolicy
{
    UpgradeToMax = 0,

    BuildThenUpgradeForCoverage = 1,
}

internal enum BlightPlacementPreference
{
    Default = 0,

    NearestPump = 1,

    NearestPlayer = 2,

    NearExistingTowers = 3,

    NearestUncoveredLane = 4,
}

internal readonly record struct TowerBuildRule(
    BlightTowerType TowerType,
    TowerBuildPriority Priority,
    int MaxUpgradeLevel,
    bool IsCoverageTower = false,
    int Specialization = (int)TowerSpecialization.None,
    BlightPlacementPreference Placement = BlightPlacementPreference.Default,
    TowerUpgradePolicy UpgradePolicy = TowerUpgradePolicy.UpgradeToMax,
    bool UpgradeBeforeMovingOntoLowerPriority = false,
    bool AlwaysUpgradeBeforeBuildingNew = false,
    int TowersPerBranch = 1,
    int MaxBuildCount = 0);

internal enum TowerSpecialization
{
    None = -1,

    // Fireball
    Meteor = 0,
    Flamethrower = 1,

    // Chilling
    GlacialCage = 0,
    Freezebolt = 1,

    // ShockNova
    ArcTower = 0,
    LightningStorm = 1,

    // Empowering
    Weaken = 0,
    BuffPlayers = 1,

    // Seismic
    StoneGaze = 0,
    Temporal = 1,

    // Summoning
    ScoutMinion = 0,
    TankMinion = 1,
}

internal static class TowerStrategyBuilder
{
    internal static TowerBuildRuleBuilder CreateRule() => new();

    internal sealed class TowerBuildRuleBuilder
    {
        private BlightTowerType _towerType;
        private TowerBuildPriority _priority;
        private int _maxUpgradeLevel;
        private bool _isCoverageTower;
        private int _specialization = (int)TowerSpecialization.None;
        private BlightPlacementPreference _placement = BlightPlacementPreference.Default;
        private TowerUpgradePolicy _upgradePolicy = TowerUpgradePolicy.UpgradeToMax;
        private bool _upgradeBeforeMovingOntoLowerPriority;
        private bool _alwaysUpgradeBeforeBuildingNew;
        private int _towersPerBranch = 1;
        private int _maxBuildCount;

        internal TowerBuildRuleBuilder SetTower(BlightTowerType type) { _towerType = type; return this; }
        internal TowerBuildRuleBuilder SetPriority(TowerBuildPriority p) { _priority = p; return this; }
        internal TowerBuildRuleBuilder SetMaxUpgradeLevel(int level) { _maxUpgradeLevel = level; return this; }

        internal TowerBuildRuleBuilder TreatAsCoverageTower(bool v = true)
        {
            _isCoverageTower = v;
            if (v && _placement == BlightPlacementPreference.Default)
                _placement = BlightPlacementPreference.NearestPump;
            return this;
        }

        internal TowerBuildRuleBuilder SetSpecialization(TowerSpecialization spec) { _specialization = (int)spec; return this; }
        internal TowerBuildRuleBuilder Placement(BlightPlacementPreference p) { _placement = p; return this; }
        internal TowerBuildRuleBuilder PreferCloseFoundationToPump(bool v = true) { if (v) _placement = BlightPlacementPreference.NearestPump; return this; }
        internal TowerBuildRuleBuilder PlaceNearExistingTowers(bool v = true) { if (v) _placement = BlightPlacementPreference.NearExistingTowers; return this; }
        internal TowerBuildRuleBuilder PlaceNearUncoveredLane(bool v = true) { if (v) _placement = BlightPlacementPreference.NearestUncoveredLane; return this; }

        internal TowerBuildRuleBuilder TowersPerBranch(int count) { _towersPerBranch = SystemMath.Max(1, count); return this; }

        internal TowerBuildRuleBuilder MaxBuildCount(int count) { _maxBuildCount = SystemMath.Max(0, count); return this; }

        internal TowerBuildRuleBuilder UpgradeOnlyWhenNeededForCoverage(bool v = true)
        {
            _upgradePolicy = v
                ? TowerUpgradePolicy.BuildThenUpgradeForCoverage
                : TowerUpgradePolicy.UpgradeToMax;
            return this;
        }
        internal TowerBuildRuleBuilder UpgradeBeforeMovingOntoLowerPriority(bool v = true)
        {
            _upgradeBeforeMovingOntoLowerPriority = v;
            return this;
        }

        internal TowerBuildRuleBuilder AlwaysUpgradeBeforeBuildingNew(bool v = true)
        {
            _alwaysUpgradeBeforeBuildingNew = v;
            return this;
        }

        internal TowerBuildRule Build() => new(
            _towerType, _priority, _maxUpgradeLevel,
            _isCoverageTower,
            _specialization, _placement, _upgradePolicy,
            _upgradeBeforeMovingOntoLowerPriority, _alwaysUpgradeBeforeBuildingNew,
            _towersPerBranch, _maxBuildCount);

        public static implicit operator TowerBuildRule(TowerBuildRuleBuilder b)
        {
            return b.Build();
        }
    }
}

internal interface IBlightTowerStrategy
{
    string Name { get; }

    string Description { get; }

    Color DefaultLaneColor { get; }

    IReadOnlyList<TowerBuildRule> Rules { get; }

    bool GroupStepsByProximity => true;

    TowerBuildRule? GetRule(BlightTowerType type);

    Color GetLaneColor(LaneCoverageResult segment);

    Color GetTowerRangeColor(BlightTowerType towerType)
        => DefaultTowerColor(towerType);

    Color GetFoundationColour(bool hasTower, BlightTowerType currentType)
        => hasTower ? DefaultTowerColor(currentType) : DefaultFoundationColour;

    Color GetFoundationOutline(BlightTowerType plannedType)
        => DefaultTowerColor(plannedType);

    static readonly Color DefaultFoundationColour = new(128, 128, 128, 100);

    static Color DefaultTowerColor(BlightTowerType type)
    {
        return type switch
        {
            BlightTowerType.Chilling => new Color(50, 130, 255, 100),   // blue — matches lane "only chilling"
            BlightTowerType.ShockNova => new Color(180, 60, 255, 100),  // purple
            BlightTowerType.Empowering => new Color(0, 200, 0, 100),    // green — matches lane "both covered"
            BlightTowerType.Seismic => new Color(255, 200, 0, 100),     // amber — matches lane "only seismic"
            BlightTowerType.Summoning => new Color(255, 160, 50, 100),  // orange
            BlightTowerType.Fireball => new Color(200, 60, 60, 100),    // red — matches lane "uncovered" red
            _ => Color.Gray
        };
    }
}
