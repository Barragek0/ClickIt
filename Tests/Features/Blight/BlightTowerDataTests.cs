namespace ClickIt.Tests.Features.Blight;

[TestClass]
public class BlightTowerDataTests
{
    [TestMethod]
    [DataRow((int)BlightTowerType.Fireball, (int)TowerSpecialization.Meteor, "MeteorTower")]
    [DataRow((int)BlightTowerType.Fireball, (int)TowerSpecialization.Flamethrower, "FlamethrowerTower")]
    [DataRow((int)BlightTowerType.Chilling, (int)TowerSpecialization.GlacialCage, "IcePrisonTower")]
    [DataRow((int)BlightTowerType.Chilling, (int)TowerSpecialization.Freezebolt, "FreezingTower")]
    [DataRow((int)BlightTowerType.ShockNova, (int)TowerSpecialization.ArcTower, "ArcingTower")]
    [DataRow((int)BlightTowerType.ShockNova, (int)TowerSpecialization.LightningStorm, "LightningStormTower")]
    [DataRow((int)BlightTowerType.Empowering, (int)TowerSpecialization.Weaken, "WeakenEnemiesTower")]
    [DataRow((int)BlightTowerType.Empowering, (int)TowerSpecialization.BuffPlayers, "BuffPlayersTower")]
    [DataRow((int)BlightTowerType.Seismic, (int)TowerSpecialization.StoneGaze, "PetrificationTower")]
    [DataRow((int)BlightTowerType.Seismic, (int)TowerSpecialization.Temporal, "TemporalTower")]
    [DataRow((int)BlightTowerType.Summoning, (int)TowerSpecialization.ScoutMinion, "FlyingMinionTower")]
    [DataRow((int)BlightTowerType.Summoning, (int)TowerSpecialization.TankMinion, "TankyMinionTower")]
    public void GetSpecializationTowerId_MapsSpecializationToExactGameTower(int type, int spec, string expected)
    {
        BlightTowerData.GetSpecializationTowerId((BlightTowerType)type, (TowerSpecialization)spec).Should().Be(expected);
    }

    [TestMethod]
    public void EverySpecializationTower_IsRepresentedAndCapturedInDat()
    {
        // Every specialization tower from the game's BlightTowerDat must be reachable through the
        // type/specialization system AND present in the captured dat catalog.  Guards against a
        // specialization being dropped from the enum or mappings (e.g. Lightning Storm, Imbuing).
        (BlightTowerType Type, TowerSpecialization Spec, string Id)[] all =
        [
            (BlightTowerType.Fireball, TowerSpecialization.Meteor, "MeteorTower"),
            (BlightTowerType.Fireball, TowerSpecialization.Flamethrower, "FlamethrowerTower"),
            (BlightTowerType.Chilling, TowerSpecialization.GlacialCage, "IcePrisonTower"),
            (BlightTowerType.Chilling, TowerSpecialization.Freezebolt, "FreezingTower"),
            (BlightTowerType.ShockNova, TowerSpecialization.ArcTower, "ArcingTower"),
            (BlightTowerType.ShockNova, TowerSpecialization.LightningStorm, "LightningStormTower"),
            (BlightTowerType.Empowering, TowerSpecialization.Weaken, "WeakenEnemiesTower"),
            (BlightTowerType.Empowering, TowerSpecialization.BuffPlayers, "BuffPlayersTower"),
            (BlightTowerType.Seismic, TowerSpecialization.StoneGaze, "PetrificationTower"),
            (BlightTowerType.Seismic, TowerSpecialization.Temporal, "TemporalTower"),
            (BlightTowerType.Summoning, TowerSpecialization.ScoutMinion, "FlyingMinionTower"),
            (BlightTowerType.Summoning, TowerSpecialization.TankMinion, "TankyMinionTower"),
        ];

        all.Length.Should().Be(12, "six tower types × two specializations each");
        foreach ((BlightTowerType type, TowerSpecialization spec, string id) in all)
        {
            BlightTowerData.GetSpecializationTowerId(type, spec).Should().Be(id, $"specialization {spec} of {type}");
            BlightTowerData.FindRadius(id).Should().BeGreaterThan(0, $"dat entry for {id} must be captured");
        }
    }

    [TestMethod]
    public void BlightTowerType_ContainsAllSixBaseTowerTypes()
    {
        Enum.GetValues<BlightTowerType>().Should().BeEquivalentTo(new[]
        {
            BlightTowerType.Chilling,
            BlightTowerType.ShockNova,
            BlightTowerType.Empowering,
            BlightTowerType.Seismic,
            BlightTowerType.Summoning,
            BlightTowerType.Fireball,
        });
    }

    [TestMethod]
    public void GetSpecializationTowerId_Meteor_MapsToMeteorTowerNotFlamethrower()
    {
        // Regression guard for the reported bug: clicking the "Meteor"
        // specialization produced a Flamethrower tower because the menu order
        // was assumed from the enum.  The ID-based resolution must target the
        // actual MeteorTower, never the Flamethrower button.
        BlightTowerData.GetSpecializationTowerId(BlightTowerType.Fireball, TowerSpecialization.Meteor)
            .Should().Be("MeteorTower");
        BlightTowerData.GetSpecializationTowerId(BlightTowerType.Fireball, TowerSpecialization.Meteor)
            .Should().NotBe("FlamethrowerTower");
    }

    [TestMethod]
    [DataRow("MeteorTower")]
    [DataRow("FlamethrowerTower")]
    [DataRow("TemporalTower")]
    [DataRow("PetrificationTower")]
    [DataRow("IcePrisonTower")]
    [DataRow("FreezingTower")]
    [DataRow("ArcingTower")]
    [DataRow("LightningStormTower")]
    [DataRow("BuffPlayersTower")]
    [DataRow("WeakenEnemiesTower")]
    [DataRow("FlyingMinionTower")]
    [DataRow("TankyMinionTower")]
    public void IsSpecializationTowerId_True_ForSpecializationTowers(string towerId)
    {
        BlightTowerData.IsSpecializationTowerId(towerId)
            .Should().BeTrue($"{towerId} is a level-4 specialization tower");
    }

    [TestMethod]
    [DataRow("FlameTower1")]
    [DataRow("FlameTower2")]
    [DataRow("FlameTower3")]
    [DataRow("ChillingTower3")]
    [DataRow("StunningTower3")]
    [DataRow("BuffTower2")]
    [DataRow("EmptyNode")]
    public void IsSpecializationTowerId_False_ForRankAndFoundationTowers(string towerId)
    {
        BlightTowerData.IsSpecializationTowerId(towerId)
            .Should().BeFalse($"{towerId} is a rank tower or the foundation, not a specialization");
    }

    [TestMethod]
    public void GetSpecializationMenuChildIndex_FireballMenuOrder_MatchesDatOrder()
    {
        // User-confirmed in-game (2026-08-11): the Fireball 3→4 upgrade panel's two specialization
        // buttons are DIRECT siblings of Child[0].Child[3] with Flamethrower at index 0 and Meteor at
        // index 1 — the same order as the BlightTowerDat file (FlamethrowerTower is declared before
        // MeteorTower), NOT the TowerSpecialization enum order. A plan step of 'SPECIAL Meteor' must
        // click index 1, never index 0 (index 0 is Flamethrower — the reported bug).
        BlightTowerData.GetSpecializationMenuChildIndex(BlightTowerType.Fireball, TowerSpecialization.Meteor)
            .Should().Be(1, "Meteor is the second button in the Fireball upgrade panel");
        BlightTowerData.GetSpecializationMenuChildIndex(BlightTowerType.Fireball, TowerSpecialization.Flamethrower)
            .Should().Be(0, "Flamethrower is the first button in the Fireball upgrade panel");
    }

    [TestMethod]
    // Menu indices follow the BlightTowerDat file order (user-confirmed for Fireball: Flamethrower=0,
    // Meteor=1); the same dat-order pattern applies to every base type. Summoning already matched.
    [DataRow((int)BlightTowerType.Fireball, (int)TowerSpecialization.Meteor, 1)]
    [DataRow((int)BlightTowerType.Fireball, (int)TowerSpecialization.Flamethrower, 0)]
    [DataRow((int)BlightTowerType.Chilling, (int)TowerSpecialization.GlacialCage, 1)]
    [DataRow((int)BlightTowerType.Chilling, (int)TowerSpecialization.Freezebolt, 0)]
    [DataRow((int)BlightTowerType.ShockNova, (int)TowerSpecialization.ArcTower, 1)]
    [DataRow((int)BlightTowerType.ShockNova, (int)TowerSpecialization.LightningStorm, 0)]
    [DataRow((int)BlightTowerType.Empowering, (int)TowerSpecialization.Weaken, 1)]
    [DataRow((int)BlightTowerType.Empowering, (int)TowerSpecialization.BuffPlayers, 0)]
    [DataRow((int)BlightTowerType.Seismic, (int)TowerSpecialization.StoneGaze, 1)]
    [DataRow((int)BlightTowerType.Seismic, (int)TowerSpecialization.Temporal, 0)]
    [DataRow((int)BlightTowerType.Summoning, (int)TowerSpecialization.ScoutMinion, 0)]
    [DataRow((int)BlightTowerType.Summoning, (int)TowerSpecialization.TankMinion, 1)]
    public void GetSpecializationMenuChildIndex_MapsEverySpecialization(int type, int spec, int expected)
    {
        BlightTowerData.GetSpecializationMenuChildIndex((BlightTowerType)type, (TowerSpecialization)spec)
            .Should().Be(expected);
    }

    [TestMethod]
    public void RuleWithoutSpecialization_DefaultsToNone_SoPlainUpgradesSkipSpecialization()
    {
        // Regression guard: a rule that never calls SetSpecialization (e.g.
        // Chilling/Seismic coverage towers) must report NO specialization, or
        // the executor enters the specialization-selection path for plain
        // upgrades (1→2, 2→3) and looks for a non-existent specialization
        // button (e.g. 'IcePrisonTower'), failing 3x and stalling the plan.
        TowerBuildRule rule = TowerStrategyBuilder.CreateRule()
            .SetTower(BlightTowerType.Seismic)
            .SetPriority(TowerBuildPriority.Critical)
            .SetMaxUpgradeLevel(3)
            .TreatAsCoverageTower()
            .Build();

        rule.Specialization.Should().Be((int)TowerSpecialization.None,
            "no SetSpecialization means the rule has no specialization branch");
    }

    [TestMethod]
    [DataRow((int)BlightTowerType.Chilling)]
    [DataRow((int)BlightTowerType.ShockNova)]
    [DataRow((int)BlightTowerType.Empowering)]
    [DataRow((int)BlightTowerType.Seismic)]
    [DataRow((int)BlightTowerType.Summoning)]
    [DataRow((int)BlightTowerType.Fireball)]
    public void MaxUpgradeLevel_IsGameMaxForEveryTowerType(int type)
    {
        BlightTowerData.MaxUpgradeLevel.Should().Be(4);
    }

    [TestMethod]
    [DataRow((int)BlightTowerType.Chilling, 1, 35)]
    [DataRow((int)BlightTowerType.Chilling, 2, 35)]
    [DataRow((int)BlightTowerType.Chilling, 3, 35)]
    [DataRow((int)BlightTowerType.Seismic, 1, 45)]
    [DataRow((int)BlightTowerType.Seismic, 2, 45)]
    [DataRow((int)BlightTowerType.Seismic, 3, 45)]
    [DataRow((int)BlightTowerType.Summoning, 1, 30)]
    [DataRow((int)BlightTowerType.Summoning, 2, 30)]
    [DataRow((int)BlightTowerType.Summoning, 3, 30)]
    [DataRow((int)BlightTowerType.Fireball, 1, 45)]
    [DataRow((int)BlightTowerType.Fireball, 2, 60)]
    [DataRow((int)BlightTowerType.Fireball, 3, 75)]
    [DataRow((int)BlightTowerType.Fireball, 4, 100)]
    [DataRow((int)BlightTowerType.ShockNova, 1, 20)]
    [DataRow((int)BlightTowerType.ShockNova, 2, 25)]
    [DataRow((int)BlightTowerType.ShockNova, 3, 30)]
    [DataRow((int)BlightTowerType.Empowering, 1, 35)]
    [DataRow((int)BlightTowerType.Empowering, 2, 45)]
    [DataRow((int)BlightTowerType.Empowering, 3, 55)]
    public void RadiusForLevel_MatchesCapturedDatPerTier(int type, int level, int expected)
    {
        BlightTowerData.RadiusForLevel((BlightTowerType)type, level).Should().Be(expected,
            $"the {level}-rank radius for {(BlightTowerType)type} was captured from BlightTowerDat");
    }

    [TestMethod]
    public void RadiusForLevel_FireballRank4_IsMeteorRadius()
    {
        // The plan upgrades Fireball to rank 4, which is the Meteor specialization in-game; the
        // strategies that plan Fireball always pick Meteor, so rank 4 must use Meteor's radius (100),
        // not the rank-3 FlameTower radius (75).
        BlightTowerData.RadiusForLevel(BlightTowerType.Fireball, 4).Should().Be(100);
        BlightTowerData.RadiusForLevel(BlightTowerType.Fireball, 4).Should().NotBe(75);
    }

    [TestMethod]
    public void FindRadius_MatchesCapturedDatIds()
    {
        // Exact-id lookups mirror the dat dump so the runtime cache can resolve known tower ids even
        // when the game's dat file is not loaded.
        BlightTowerData.FindRadius("ChillingTower1").Should().Be(35);
        BlightTowerData.FindRadius("ChillingTower3").Should().Be(35);
        BlightTowerData.FindRadius("StunningTower2").Should().Be(45);
        BlightTowerData.FindRadius("FlameTower2").Should().Be(60);
        BlightTowerData.FindRadius("MeteorTower").Should().Be(100);
        BlightTowerData.FindRadius("FreezingTower").Should().Be(75);
        BlightTowerData.FindRadius("FlyingMinionTower").Should().Be(75);
        BlightTowerData.FindRadius("TankyMinionTower").Should().Be(45);
        BlightTowerData.FindRadius("EmptyNode").Should().Be(0);
    }

    [TestMethod]
    public void FindRadius_UnknownId_ReturnsZero()
    {
        BlightTowerData.FindRadius("DoesNotExistTower").Should().Be(0,
            "unknown ids fall through to the generic default radius");
    }

    [TestMethod]
    public void Catalog_ContainsAllCapturedTowers()
    {
        // 31 entries exactly as dumped from BlightTowerDat (2026-08-02) — every tower at every level
        // plus the specialization entries.  Guarded so the table can never silently lose a row.
        BlightTowerData.Catalog.Should().HaveCount(31);
        BlightTowerData.Catalog.Should().Contain(e => e.DatId == "ChillingTower1" && e.Radius == 35);
        BlightTowerData.Catalog.Should().Contain(e => e.DatId == "FlameTower3" && e.Radius == 75);
        BlightTowerData.Catalog.Should().Contain(e => e.DatId == "MeteorTower" && e.Radius == 100);
    }

    [TestMethod]
    public void BlightTowerId_EnumAndCatalog_AreAligned()
    {
        // The Catalog array is indexed by the enum value, so every member must sit at its own index.
        BlightTowerId[] values = Enum.GetValues<BlightTowerId>();
        values.Length.Should().Be(BlightTowerData.Catalog.Length);
        foreach (BlightTowerId id in values)
            BlightTowerData.Catalog[(int)id].Id.Should().Be(id, "Catalog index must equal the BlightTowerId value");
    }

    [TestMethod]
    public void MapTowerIdToType_CoversEveryDatTowerId()
    {
        // Every real tower id (all but the EmptyNode foundation placeholder) maps to its own type.
        foreach (BlightTowerInfo e in BlightTowerData.Catalog)
        {
            if (e.Id == BlightTowerId.EmptyNode)
            {
                BlightTowerData.MapTowerIdToType(e.DatId).Should().BeNull("EmptyNode is not a tower type");
                continue;
            }
            BlightTowerData.MapTowerIdToType(e.DatId).Should().Be(e.Type, $"{e.DatId} must map to {e.Type}");
        }
    }

    [TestMethod]
    public void MapFoundationSuffix_ResolvesAllBaseTypes()
    {
        // Foundation paths carry the base-type word ("…/BlightFoundationChilling"), including the
        // @-suffixed runtime form.
        BlightTowerData.MapFoundationSuffix("Chilling").Should().Be(BlightTowerType.Chilling);
        BlightTowerData.MapFoundationSuffix("Stunning").Should().Be(BlightTowerType.Seismic);
        BlightTowerData.MapFoundationSuffix("Flame").Should().Be(BlightTowerType.Fireball);
        BlightTowerData.MapFoundationSuffix("Buff").Should().Be(BlightTowerType.Empowering);
        BlightTowerData.MapFoundationSuffix("Shocking").Should().Be(BlightTowerType.ShockNova);
        BlightTowerData.MapFoundationSuffix("Minion").Should().Be(BlightTowerType.Summoning);
        BlightTowerData.MapFoundationSuffix("Chilling@83").Should().Be(BlightTowerType.Chilling,
            "@-suffixed runtime paths still resolve");
        BlightTowerData.MapFoundationSuffix("Meteor").Should().BeNull("specialization words are not foundations");
    }

    // ── Coverage safety margin (spec §2.2: effective radius = real radius − 5) ──

    [TestMethod]
    public void GetCoverageRadius_AppliesFixedMargin()
    {
        BlightService.GetCoverageRadius(35).Should().Be(30, "Chilling effective radius");
        BlightService.GetCoverageRadius(45).Should().Be(40, "Seismic effective radius");
        BlightService.GetCoverageRadius(20).Should().Be(15, "ShockNova effective radius");
        BlightService.GetCoverageRadius(5).Should().Be(0, "a radius at or below the margin has no coverage reach");
        BlightService.GetCoverageRadius(0).Should().Be(0);
    }

    [TestMethod]
    public void GetCoverageRadiusForLevel_ReducesEveryLevelByTheMargin()
    {
        // Chilling radius is constant across ranks (35), so every level's effective radius is 30.
        BlightService.GetCoverageRadiusForLevel(BlightTowerType.Chilling, 1).Should().Be(30);
        BlightService.GetCoverageRadiusForLevel(BlightTowerType.Chilling, 3).Should().Be(30);
        // Fireball grows with rank: 45/60/75/100 → 40/55/70/95.
        BlightService.GetCoverageRadiusForLevel(BlightTowerType.Fireball, 1).Should().Be(40);
        BlightService.GetCoverageRadiusForLevel(BlightTowerType.Fireball, 4).Should().Be(95);
        // The margin must never produce a negative reach.
        BlightService.GetCoverageRadiusForLevel(BlightTowerType.Fireball, 1).Should().BeGreaterThan(0);
    }
}
