# Making a Blight Strategy

A **strategy** tells ClickIt what Blight towers to build, where to place them,
and how to upgrade them.  It is a pure configuration object — the
planner (`BlightPlanner`) owns all the actual decision-making, so a strategy
can never corrupt safety logic.

A strategy is a class that implements `IBlightTowerStrategy`.  Strategies live
in `Features/Blight/Strategy/`.

---

## The shape of a strategy

```csharp
namespace ClickIt.Features.Blight.Strategy;

internal sealed class MyStrategy : IBlightTowerStrategy
{
    private static readonly TowerBuildRule[] s_rules = [ /* rules here */ ];

    public string Name => "My Strategy";                 // shown in the settings dropdown
    public string Description => "What it does.";         // shown in the settings panel
    public Color DefaultLaneColor => new(194, 200, 0, 57); // lane color before coverage

    public IReadOnlyList<TowerBuildRule> Rules => s_rules;

    public TowerBuildRule? GetRule(BlightTowerType type)
    {
        for (int i = 0; i < s_rules.Length; i++)
            if (s_rules[i].TowerType == type)
                return s_rules[i];
        return null;
    }

    public Color GetLaneColor(LaneCoverageResult segment)
    {
        // Color each lane segment in the debug overlay.
        if (segment.HasChilling && segment.HasSeismic)
            return new Color(0, 200, 0, 100);   // fully covered = green
        return new Color(200, 60, 60, 100);     // uncovered = red
    }

    // GetFoundationColour / GetFoundationOutline / GetTowerRangeColor are all
    // OPTIONAL — the interface provides sensible defaults (Chilling = blue,
    // Seismic = amber, Fireball = red, unbuilt foundation = grey; tower range
    // circles use the same per-type palette).  A plain strategy that is happy
    // with the defaults simply leaves them out.  Override only to re-theme:
    // unbuilt foundations use GetFoundationOutline (the planned type's colour),
    // built towers use GetFoundationColour(hasTower: true) (their current
    // type's colour), and range circles use GetTowerRangeColor.  To keep the
    // default colours for the other types, call the static
    // IBlightTowerStrategy.DefaultTowerColor helper — never cast back to the
    // interface and call the hook again (that recurses into the override and
    // overflows the stack).
    public Color GetTowerRangeColor(BlightTowerType towerType)
        => IBlightTowerStrategy.DefaultTowerColor(towerType);

    public Color GetFoundationColour(bool hasTower, BlightTowerType currentType)
    {
        if (hasTower && currentType == BlightTowerType.Fireball) return new Color(255, 40, 40, 100);
        return IBlightTowerStrategy.DefaultTowerColor(currentType); // default for the rest
    }

    public Color GetFoundationOutline(BlightTowerType plannedType)
        => IBlightTowerStrategy.DefaultTowerColor(plannedType); // planned type's colour
}
```

Every rule is built with `TowerStrategyBuilder.CreateRule()` and a chain of
self-descriptive calls ending in `.Build()`:

```csharp
TowerStrategyBuilder.CreateRule()
    .SetTower(BlightTowerType.Chilling)     // which tower
    .SetPriority(TowerBuildPriority.Critical)
    .SetMaxUpgradeLevel(3)
    .TreatAsCoverageTower()
    .PreferCloseFoundationToPump()
    .UpgradeOnlyWhenNeededForCoverage()
    .UpgradeBeforeMovingOntoLowerPriority()
    .Build(),
```

---

## The full flag reference

### What / when / how high

| Flag | Meaning |
| --- | --- |
| `SetTower(BlightTowerType type)` | **Required.** Which tower this rule controls. |
| `SetPriority(TowerBuildPriority p)` | **Required.** Priority tier — `Critical` (0), `High` (1), `Normal` (2), `Low` (3), `Fill` (4).  Lower number runs first. |
| `SetMaxUpgradeLevel(int level)` | **Required.** The highest level the planner will take this tower to. |
| `SetSpecialization(TowerSpecialization spec)` | Which upgrade path to pick at the 4th (specialization) level (e.g. `Meteor` for Fireball).  **Set this on every damage tower or the default path is used.**  The executor clicks the specialization by its in-game menu index, falling back to the tower ID (`Meteor` → `MeteorTower`), so the intended branch is always selected.  Rules WITHOUT `SetSpecialization` (e.g. Chilling/Seismic coverage towers) are treated as plain upgrades that never open the specialization selection. |

### Coverage tier

| Flag | Meaning |
| --- | --- |
| `TreatAsCoverageTower()` | This tower provides lane coverage.  The planner builds one per pump branch (up to `TowersPerBranch`), and the strategy's **fill tier does not start until every branch has full coverage of all coverage-tower types**.  When no explicit placement is set, coverage towers default to hugging the pump.  A strategy with **no** coverage towers is not gated at all — every rule is treated as fill. |

### Placement

Where the planner picks the foundation, among all foundations that can reach
the target lane segment within range.

| Flag | Meaning |
| --- | --- |
| `Placement(BlightPlacementPreference p)` | Set any preference explicitly. |
| `PreferCloseFoundationToPump()` | Pick the foundation nearest the pump.  On coverage towers (`TreatAsCoverageTower`) this is a tie-breaker AFTER the number of branches covered — it never picks a pump-closer foundation that would cover fewer branches. |
| `PlaceNearExistingTowers()` | Pick the foundation nearest an already-built tower — for support towers (e.g. Empowering) whose radius buffs neighbours. |
| `PlaceNearUncoveredLane()` | Pick the foundation nearest a lane segment with no coverage — for damage towers that should hit the least-covered lanes. |

`BlightPlacementPreference` values: `Default` (closest to the target lane
segment), `NearestPump`, `NearExistingTowers`,
`NearestUncoveredLane` (and the general `NearestPlayer` value, settable via
`Placement(...)`).

### Upgrade behaviour

| Flag | Meaning |
| --- | --- |
| `UpgradeOnlyWhenNeededForCoverage()` | Coverage towers are upgraded **only as far as their range needs to cover their branch base** — never blindly to max. |
| `UpgradeBeforeMovingOntoLowerPriority()` | This type is upgraded all the way to max level **before any lower-priority tier's steps are emitted**.  Used on coverage towers so Chilling/Seismic reach tier 3 before Fireball building starts. |
| `AlwaysUpgradeBeforeBuildingNew()` | Towers of this type are **always pushed to max, existing towers first**: every already-built tower is upgraded to max before any new tower is built, and each new tower is fully upgraded before the next one starts.  Intended for fill / damage towers (Fireball → Meteor). |

Without any upgrade flags, coverage towers follow the `UpgradeToMax` policy
(pushed to max level); fill towers are built first, then upgraded toward max.

### Count control

| Flag | Meaning |
| --- | --- |
| `TowersPerBranch(int count)` | How many coverage towers of this type each branch receives (default 1).  Extra slots are redundancy towers near the branch. |
| `MaxBuildCount(int count)` | Hard cap on how many towers of this type may exist (existing + newly built).  `0` = unlimited.  A capped rule stops contributing once the cap is reached. |

---

## Tower types, max levels, and specializations

Every tower has the same level curve: three marks (Mk I / Mk II / Mk III),
then its **specialization as the 4th and final level** — so the game max is 4
for every type (`BlightTowerData.MaxUpgradeLevel`).  `SetMaxUpgradeLevel(3)`
on a coverage tower is a *strategy* cap (coverage never needs the
specialization tier); `SetMaxUpgradeLevel(4)` is how a damage tower reaches
its specialization (e.g. Fireball → Meteor).

| Tower | Game max | Specializations (game menu order) |
| --- | --- | --- |
| `Chilling` | 4 | `Freezebolt` (0), `GlacialCage` (1) |
| `Seismic` | 4 | `Temporal` (0), `StoneGaze` (1) |
| `Fireball` | 4 | `Flamethrower` (0), `Meteor` (1) |
| `ShockNova` | 4 | `LightningStorm` (0), `ArcTower` (1) |
| `Empowering` | 4 | `BuffPlayers` (0), `Weaken` (1) |
| `Summoning` | 4 | `TankMinion` (0), `ScoutMinion` (1) |

The numbers are the **in-game menu child indexes** the executor clicks, not
the `TowerSpecialization` enum values — for most types they differ (Fireball's
enum is `Meteor=0, Flamethrower=1`, the game menu is the reverse).  The
executor resolves the button by the menu index first, falling back to the
game tower ID (`Meteor` → `MeteorTower`), so the intended branch is always
selected.

---

## How the planner uses your rules

1. Rules are processed in **priority order** (`Critical` first).
2. **Coverage tiers** (rules with `TreatAsCoverageTower`) are assigned first,
   in priority order — one tower per branch (up to `TowersPerBranch`),
   branches ordered nearest-first.
3. Once every branch has full coverage of all coverage types, **fill tiers**
   (everything else) assign the remaining foundations, in priority order,
   round-robin across the fill rules in each tier, honoring each rule's
   placement and `MaxBuildCount`.
4. A strategy with **no coverage towers** skips the gate — its rules are all
   fill and start immediately.
5. Non-coverage rules are never dropped: they become fill as soon as coverage
   completes, regardless of whether they sit above, below, or alongside a
   coverage tier.

---

## What a full strategy needs before it is complete

- [ ] A class implementing `IBlightTowerStrategy` in `Features/Blight/Strategy/`.
- [ ] `Name` — short, shown in the dropdown.
- [ ] `Description` — what it builds and recommended ring anoints.
- [ ] `DefaultLaneColor` — a `Color` for unanalysed lanes.
- [ ] `Rules` — at least **one** rule.  Every rule has `SetTower`, `SetPriority`,
      and `SetMaxUpgradeLevel`.
- [ ] `GetRule(type)` — return the rule for a type, else `null`.
- [ ] `GetLaneColor(segment)` — green when covered, red when not (or any scheme
      you prefer).
- [ ] Optional: `GetFoundationColour(hasTower, type)`,
      `GetFoundationOutline(plannedType)`, and `GetTowerRangeColor(type)` —
      colours for the tower dots and range circles in the overlay.  The
      interface defaults map Chilling=blue, ShockNova=purple, Empowering=green,
      Seismic=amber, Summoning=orange, Fireball=red (all matching the lane
      palette).  Every dot is a full circle: unbuilt foundations use
      `GetFoundationOutline` (the planned type's colour), built towers use
      `GetFoundationColour(hasTower: true)` (their current type's colour), and
      range circles use `GetTowerRangeColor`.  Override only to re-theme — a
      plain strategy can omit all three.
- [ ] Decide **coverage**: which towers are `TreatAsCoverageTower`.  At least
      one coverage type (usually Chilling and/or Seismic) unless the strategy
      is intentionally fill-only (e.g. Meteor Only).
- [ ] Decide **placement**: player-first, pump, near existing towers, or near
      uncovered lanes — explicitly, so the planner does what you expect.
- [ ] Decide **upgrade behaviour**: coverage-only-upgrades, upgrade-before-
      lower-priority, or always-upgrade-before-building-new.
- [ ] Set `SetSpecialization` on every damage tower (e.g. `Meteor`).
- [ ] Optional: `TowersPerBranch` for extra redundancy per branch, `MaxBuildCount`
      to cap how many of a type exist.

### Registering a new strategy

1. Add a value to the `BlightTowerStrategy` enum in
   `Features/Blight/Strategy/BlightTowerStrategy.cs`.
2. Add the instance and its cases in
   `Features/Blight/Strategy/BlightStrategyResolver.cs`
   (`Resolve`, `GetName`, `GetDescription`, `StrategyNames`).

---

## Worked examples

### Chilling + Seismic + Meteor (coverage first, then damage)

```csharp
TowerStrategyBuilder.CreateRule()
    .SetTower(BlightTowerType.Chilling)
    .SetPriority(TowerBuildPriority.Critical)
    .SetMaxUpgradeLevel(3)
    .TreatAsCoverageTower()
    .PreferCloseFoundationToPump()
    .UpgradeOnlyWhenNeededForCoverage()
    .UpgradeBeforeMovingOntoLowerPriority()
    .Build(),
TowerStrategyBuilder.CreateRule()
    .SetTower(BlightTowerType.Seismic)
    .SetPriority(TowerBuildPriority.Critical)
    .SetMaxUpgradeLevel(3)
    .TreatAsCoverageTower()
    .PreferCloseFoundationToPump()
    .UpgradeOnlyWhenNeededForCoverage()
    .UpgradeBeforeMovingOntoLowerPriority()
    .Build(),
TowerStrategyBuilder.CreateRule()
    .SetTower(BlightTowerType.Fireball)
    .SetPriority(TowerBuildPriority.High)
    .SetMaxUpgradeLevel(4)
    .SetSpecialization(TowerSpecialization.Meteor)
    .PreferCloseFoundationToPump()
    .AlwaysUpgradeBeforeBuildingNew()
    .Build()
```

### Meteor only (no coverage — fills every foundation)

```csharp
TowerStrategyBuilder.CreateRule()
    .SetTower(BlightTowerType.Fireball)
    .SetPriority(TowerBuildPriority.Critical)
    .SetMaxUpgradeLevel(4)
    .SetSpecialization(TowerSpecialization.Meteor)
    .PreferCloseFoundationToPump()
    .Build()
```

### Empowering support cluster

```csharp
TowerStrategyBuilder.CreateRule()
    .SetTower(BlightTowerType.Empowering)
    .SetPriority(TowerBuildPriority.High)
    .SetMaxUpgradeLevel(2)
    .PlaceNearExistingTowers()     // buffs the towers around it
    .MaxBuildCount(2)              // don't build more than two
    .Build()
```
