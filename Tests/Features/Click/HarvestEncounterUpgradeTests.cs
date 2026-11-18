namespace ClickIt.Tests.Features.Click;

[TestClass]
public class HarvestEncounterUpgradeTests
{
    [TestMethod]
    public void VisibleWisdomActionTakesPrecedenceOverIdentifiedFlag()
    {
        bool? identified = HarvestEncounterUpgrade.ResolveIdentification(true, hasWisdomButton: true, hasScouringButton: true);
        HarvestEncounterUpgrade.ChooseStep(default, MonsterRarity.Magic, identified, false)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Identify);
        HarvestEncounterUpgrade.ChooseStep(HarvestEncounterUpgrade.UpgradeStep.Identify, MonsterRarity.Magic, identified, false)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Wait);
    }

    [TestMethod]
    public void MagicTree_WaitsForEachCurrencyResultBeforeOpening()
    {
        var identify = HarvestEncounterUpgrade.ChooseStep(default, MonsterRarity.Magic, false, false);
        identify.Should().Be(HarvestEncounterUpgrade.UpgradeStep.Identify);
        HarvestEncounterUpgrade.ChooseStep(identify, MonsterRarity.Magic, false, false)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Wait);

        var scour = HarvestEncounterUpgrade.ChooseStep(identify, MonsterRarity.Magic, true, false);
        scour.Should().Be(HarvestEncounterUpgrade.UpgradeStep.Scour);
        HarvestEncounterUpgrade.ChooseStep(scour, MonsterRarity.Magic, true, false)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Wait);

        var alchemy = HarvestEncounterUpgrade.ChooseStep(scour, MonsterRarity.White, null, false);
        alchemy.Should().Be(HarvestEncounterUpgrade.UpgradeStep.Alchemy);
        HarvestEncounterUpgrade.ChooseStep(alchemy, MonsterRarity.White, null, false)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Wait);
        HarvestEncounterUpgrade.ChooseStep(alchemy, MonsterRarity.Rare, null, false)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Open);
    }

    [TestMethod]
    public void NormalTree_UsesAlchemyOnceAndWaitsForRareBeforeOpening()
    {
        var alchemy = HarvestEncounterUpgrade.ChooseStep(default, MonsterRarity.White, null, false, alchNormal: true);
        alchemy.Should().Be(HarvestEncounterUpgrade.UpgradeStep.Alchemy);
        HarvestEncounterUpgrade.ChooseStep(alchemy, MonsterRarity.White, null, false, alchNormal: true)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Wait);
        HarvestEncounterUpgrade.ChooseStep(alchemy, MonsterRarity.Rare, null, false, alchNormal: true)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Open);
        HarvestEncounterUpgrade.ChooseStep(default, MonsterRarity.Rare, null, false, alchNormal: true)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Open);
    }

    [TestMethod]
    public void AlreadyIdentifiedMagicTree_ScoursWithoutAnotherIdentification()
    {
        HarvestEncounterUpgrade.ChooseStep(default, MonsterRarity.Magic, true, false)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Scour);
        HarvestEncounterUpgrade.ChooseStep(HarvestEncounterUpgrade.UpgradeStep.Identify, MonsterRarity.White, null, false)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Alchemy);
    }

    [TestMethod]
    public void MissingIdentificationFlag_WaitsForWisdomConsumption()
    {
        HarvestEncounterUpgrade.ChooseStep(HarvestEncounterUpgrade.UpgradeStep.Identify, MonsterRarity.Magic, null, false)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Wait);
        HarvestEncounterUpgrade.ChooseStep(HarvestEncounterUpgrade.UpgradeStep.Identify, MonsterRarity.Magic, null, true)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Scour);
    }

    [DataTestMethod]
    [DataRow(MonsterRarity.White)]
    [DataRow(MonsterRarity.Rare)]
    [DataRow(MonsterRarity.Unique)]
    public void NonMagicTree_UsesNormalOpenPath(MonsterRarity rarity)
    {
        HarvestEncounterUpgrade.ChooseStep(default, rarity, null, false)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Open);
    }
}
