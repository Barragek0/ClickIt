namespace ClickIt.Features.Blight;

/// <summary>How important this tower type is. Higher priority rules are built and upgraded before lower ones.</summary>
internal enum TowerBuildPriority
{
    Critical = 0,
    High = 1,
    Normal = 2,
    Low = 3,
    Fill = 4
}

/// <summary>How a tower type is upgraded over time.</summary>
internal enum TowerUpgradePolicy
{
    /// <summary>Push every tower of this type to its max level before building more.</summary>
    UpgradeToMax = 0,
    /// <summary>Only upgrade when the extra coverage is actually needed.</summary>
    BuildThenUpgradeForCoverage = 1,
}

/// <summary>Where new towers of this type should be placed on the lane network.</summary>
internal enum BlightPlacementPreference
{
    /// <summary>Let the planner decide (coverage towers default to nearest the pump).</summary>
    Default = 0,
    /// <summary>Build closest to the pump first.</summary>
    NearestPump = 1,
    /// <summary>Build next to towers that are already built.</summary>
    NearExistingTowers = 3,
    /// <summary>Build on the foundation closest to a lane that still needs coverage.</summary>
    NearestUncoveredLane = 4,
}

/// <summary>
/// A single tower rule: which tower to build, how many, where, and how far to upgrade it.
/// Build rules are created with <see cref="TowerStrategyBuilder.CreateRule"/> and its fluent setters.
/// </summary>
internal readonly record struct TowerBuildRule(
    /// <summary>The tower type this rule controls.</summary>
    BlightTowerType TowerType,
    /// <summary>Priority relative to other rules; higher priority rules run first.</summary>
    TowerBuildPriority Priority,
    /// <summary>Highest upgrade level the planner will push this tower to (1-4).</summary>
    int MaxUpgradeLevel,
    /// <summary>When true this tower is a coverage tower: built along lanes until the lanes it covers are protected.</summary>
    bool IsCoverageTower = false,
    /// <summary>The specialization to pick at level 4 (e.g. Scout for Summoning), or None for no specialization.</summary>
    int Specialization = (int)TowerSpecialization.None,
    /// <summary>Where to place new towers of this type.</summary>
    BlightPlacementPreference Placement = BlightPlacementPreference.Default,
    /// <summary>How this tower type is upgraded over time.</summary>
    TowerUpgradePolicy UpgradePolicy = TowerUpgradePolicy.UpgradeToMax,
    /// <summary>When true, all towers of this type reach max level before any lower-priority type is built.</summary>
    bool UpgradeBeforeMovingOntoLowerPriority = false,
    /// <summary>When true, every built tower of this type is fully upgraded before the next one is built.</summary>
    bool AlwaysUpgradeBeforeBuildingNew = false,
    /// <summary>Maximum number of this tower built per lane branch.</summary>
    int TowersPerBranch = 1,
    /// <summary>Maximum total count built across the whole encounter; 0 means unlimited.</summary>
    int MaxBuildCount = 0,
    /// <summary>For Empowering rules: the tower types that must be kept within range. Null/empty means not an empowering rule.</summary>
    IReadOnlyList<BlightTowerType>? TowersToEmpower = null)
{
    /// <summary>The tower types this Empowering rule must be within range of; empty when not an empowering rule.</summary>
    internal IReadOnlyList<BlightTowerType> EmpowerTargets => TowersToEmpower ?? [];
}

/// <summary>The level-4 specialization choices per base tower type (Scout for Summoning, Meteor for Fireball, etc.).</summary>
internal enum TowerSpecialization
{
    None = -1,

    Meteor = 0,
    Flamethrower = 1,

    GlacialCage = 0,
    Freezebolt = 1,

    ArcTower = 0,
    LightningStorm = 1,

    Weaken = 0,
    BuffPlayers = 1,

    StoneGaze = 0,
    Temporal = 1,

    ScoutMinion = 0,
    TankMinion = 1,
}

/// <summary>Entry point for building tower rules with a fluent builder.</summary>
internal static class TowerStrategyBuilder
{
    /// <summary>Starts a new tower rule. Chain the builder setters, then call <c>Build()</c> or use the implicit conversion to <see cref="TowerBuildRule"/>.</summary>
    internal static TowerBuildRuleBuilder CreateRule() => new();

    /// <summary>
    /// Fluent builder that describes one tower rule: what to build, where, and how far to upgrade it.
    /// Every setter returns the builder so calls can be chained: <c>CreateRule().SetTower(...).SetPriority(...).Build()</c>.
    /// </summary>
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
        private IReadOnlyList<BlightTowerType> _towersToEmpower = [];

        /// <summary>The tower type this rule builds (e.g. Fireball, Empowering).</summary>
        internal TowerBuildRuleBuilder SetTower(BlightTowerType type) { _towerType = type; return this; }
        /// <summary>How important this rule is relative to the other rules in the strategy.</summary>
        internal TowerBuildRuleBuilder SetPriority(TowerBuildPriority p) { _priority = p; return this; }
        /// <summary>The highest level (1-4) the planner will upgrade this tower to.</summary>
        internal TowerBuildRuleBuilder SetMaxUpgradeLevel(int level) { _maxUpgradeLevel = level; return this; }

        /// <summary>Marks this as a coverage tower: it is built along lanes until the lanes it protects are covered. Coverage towers default to building nearest the pump.</summary>
        internal TowerBuildRuleBuilder TreatAsCoverageTower(bool v = true)
        {
            _isCoverageTower = v;
            if (v && _placement == BlightPlacementPreference.Default)
                _placement = BlightPlacementPreference.NearestPump;
            return this;
        }

        /// <summary>Which specialization to pick when this tower reaches level 4 (e.g. Scout for Summoning).</summary>
        internal TowerBuildRuleBuilder SetSpecialization(TowerSpecialization spec) { _specialization = (int)spec; return this; }
        /// <summary>Directly sets where new towers of this type are placed.</summary>
        internal TowerBuildRuleBuilder Placement(BlightPlacementPreference p) { _placement = p; return this; }
        /// <summary>Build closest to the pump first.</summary>
        internal TowerBuildRuleBuilder PreferCloseFoundationToPump(bool v = true) { if (v) _placement = BlightPlacementPreference.NearestPump; return this; }
        /// <summary>Build next to towers that are already built.</summary>
        internal TowerBuildRuleBuilder PlaceNearExistingTowers(bool v = true) { if (v) _placement = BlightPlacementPreference.NearExistingTowers; return this; }
        /// <summary>Build on the foundation closest to a lane that still needs coverage.</summary>
        internal TowerBuildRuleBuilder PlaceNearUncoveredLane(bool v = true) { if (v) _placement = BlightPlacementPreference.NearestUncoveredLane; return this; }

        /// <summary>Maximum number of this tower built per lane branch (at least 1).</summary>
        internal TowerBuildRuleBuilder TowersPerBranch(int count) { _towersPerBranch = SystemMath.Max(1, count); return this; }

        /// <summary>Maximum total count of this tower built across the whole encounter; 0 means unlimited.</summary>
        internal TowerBuildRuleBuilder MaxBuildCount(int count) { _maxBuildCount = SystemMath.Max(0, count); return this; }

        /// <summary>Marks this as an Empowering rule: Empowering towers are placed in range of the given tower types and built until every such tower has an Empowering tower next to it.</summary>
        internal TowerBuildRuleBuilder BuildUntilTowersAreEmpowered(params BlightTowerType[] types)
        {
            _towersToEmpower = types ?? [];
            return this;
        }

        /// <summary>Only upgrade this tower when the extra coverage is actually needed instead of always pushing to max.</summary>
        internal TowerBuildRuleBuilder UpgradeOnlyWhenNeededForCoverage(bool v = true)
        {
            _upgradePolicy = v
                ? TowerUpgradePolicy.BuildThenUpgradeForCoverage
                : TowerUpgradePolicy.UpgradeToMax;
            return this;
        }
        /// <summary>Fully upgrade every tower of this type before building any lower-priority tower type.</summary>
        internal TowerBuildRuleBuilder UpgradeBeforeMovingOntoLowerPriority(bool v = true)
        {
            _upgradeBeforeMovingOntoLowerPriority = v;
            return this;
        }

        /// <summary>Fully upgrade each tower of this type as soon as it is built, before building the next one.</summary>
        internal TowerBuildRuleBuilder AlwaysUpgradeBeforeBuildingNew(bool v = true)
        {
            _alwaysUpgradeBeforeBuildingNew = v;
            return this;
        }

        /// <summary>Produces the finished <see cref="TowerBuildRule"/> from the values configured so far.</summary>
        internal TowerBuildRule Build() => new(
            _towerType, _priority, _maxUpgradeLevel,
            _isCoverageTower,
            _specialization, _placement, _upgradePolicy,
            _upgradeBeforeMovingOntoLowerPriority, _alwaysUpgradeBeforeBuildingNew,
            _towersPerBranch, _maxBuildCount, _towersToEmpower);

        public static implicit operator TowerBuildRule(TowerBuildRuleBuilder b)
        {
            return b.Build();
        }
    }
}

/// <summary>
/// A complete Blight build plan: which towers to build, where, and how to colour the lanes while it runs.
/// Implement this interface (or use the rule builder helpers) and register the strategy to drive Blight building.
/// </summary>
internal interface IBlightTowerStrategy
{
    /// <summary>Short strategy name shown in UI/debug output.</summary>
    string Name { get; }

    /// <summary>One-line description of what the strategy does.</summary>
    string Description { get; }

    /// <summary>Colour used for lanes that follow this strategy's coverage plan.</summary>
    Color DefaultLaneColor { get; }

    /// <summary>The ordered list of tower build rules that make up this strategy.</summary>
    IReadOnlyList<TowerBuildRule> Rules { get; }

    /// <summary>Whether the plan groups build steps by proximity so the pathfinder can walk between nearby towers first.</summary>
    bool GroupStepsByProximity => true;

    /// <summary>Returns the rule that controls the given tower type, or null if the strategy does not build it.</summary>
    TowerBuildRule? GetRule(BlightTowerType type);

    /// <summary>The colour for a lane segment given its current coverage state (e.g. protected, threatened, uncovered).</summary>
    Color GetLaneColor(LaneCoverageResult segment);

    /// <summary>Range-circle colour drawn around a built tower. Defaults to the shared tower palette.</summary>
    Color GetTowerRangeColor(BlightTowerType towerType)
        => DefaultTowerColor(towerType);

    /// <summary>Colour for a build foundation: the tower's colour when it has a tower, otherwise the neutral foundation colour.</summary>
    Color GetFoundationColour(bool hasTower, BlightTowerType currentType)
        => hasTower ? DefaultTowerColor(currentType) : DefaultFoundationColour;

    /// <summary>Outline colour drawn on a foundation to show which tower type is planned there.</summary>
    Color GetFoundationOutline(BlightTowerType plannedType)
        => DefaultTowerColor(plannedType);

    /// <summary>Neutral grey used for empty foundations.</summary>
    static readonly Color DefaultFoundationColour = new(128, 128, 128, 100);

    /// <summary>Shared tower palette at the lane alpha so range dots blend with lane colours; strategies can override per-tower colours via <see cref="GetTowerRangeColor"/> or <see cref="GetFoundationOutline"/>.</summary>
    static Color DefaultTowerColor(BlightTowerType type)
    {
        Color c = BlightTowerColors.AsColor(type);
        return new Color(c.R, c.G, c.B, (byte)100);
    }
}
