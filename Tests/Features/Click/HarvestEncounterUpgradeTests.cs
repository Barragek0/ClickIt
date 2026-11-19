namespace ClickIt.Tests.Features.Click;

[TestClass]
public class HarvestEncounterUpgradeTests
{
    [DataTestMethod]
    [TestCategory("Regression")]
    [DataRow("Other modifier\nTier 3 Sacred Fauna drop 2 additional Stacked Decks", true, 2, false, 2, true)]
    [DataRow("Tier 3 Sacred Fauna drop 1 additional Stacked Decks", true, 2, true, 2, false)]
    [DataRow("Other modifier\nTier 3 Sacred Fauna drop 3 additional Threads", false, 2, true, 2, true)]
    [DataRow("Tier 3 Sacred Fauna drop 3 additional Threads\nOther modifier", false, 2, true, 2, true)]
    [DataRow("Tier 3 Sacred Fauna drop 3 additional Stacked Decks\nTier 3 Sacred Fauna drop an additional Unique Item", true, 3, false, 1, true)]
    [DataRow("Tier 3 Sacred Fauna drop 1 additional Stacked Deck\nTier 3 Sacred Fauna drop 2 additional Threads", true, 3, true, 2, true)]
    [DataRow("Tier 3 Sacred Fauna drop 4 additional Threads", true, 1, false, 1, false)]
    [DataRow("Tier 3 Sacred Fauna drop 4 additional Stacked Decks", false, 2, false, 1, true)]
    [DataRow("Tier 3 Sacred Fauna drop 3 additional Stacked Decks", true, 100, false, 1, true)]
    [DataRow("Tier 3 Sacred Fauna drop 3 additional Threads", false, 1, true, 100, true)]
    [DataRow("Tier 3 Sacred Fauna drop an additional Stacked Deck", true, 1, false, 1, true)]
    [DataRow("Tier 3 Sacred Fauna drop an additional Stacked Deck", true, 2, false, 1, false)]
    [DataRow("Tier 3 Sacred Fauna drop an additional Thread", false, 1, true, 1, true)]
    [DataRow("Tier 3 Sacred Fauna drop an additional Thread", false, 1, true, 2, false)]
    public void RerollTarget_ChecksEveryModifierAndEnabledMinimum(string text, bool decks, int minDecks, bool threads, int minThreads, bool expected)
        => HarvestEncounterUpgrade.MatchesTarget(text, decks, minDecks, threads, minThreads).Should().Be(expected);

    [TestMethod]
    [TestCategory("Regression")]
    public void RerollTargets_CurrencyAndUniqueRespectSelectionsAndMinimums()
    {
        const string currency = "Tier 3 Sacred Fauna drop 3 additional Currency Items";
        const string unique = "Tier 3 Sacred Fauna drop an additional Unique Item";
        HarvestEncounterUpgrade.MatchesTarget(currency, false, 1, false, 2, true, 3, false).Should().BeTrue();
        HarvestEncounterUpgrade.MatchesTarget(currency, false, 1, false, 2, true, 4, false).Should().BeFalse();
        HarvestEncounterUpgrade.MatchesTarget(currency, false, 1, false, 2, false, 1, true).Should().BeFalse();
        HarvestEncounterUpgrade.MatchesTarget(unique, false, 1, false, 2, false, 1, true).Should().BeTrue();
        HarvestEncounterUpgrade.MatchesTarget("Tier 3 Sacred Fauna drop 1 additional Unique Items", false, 1, false, 2, false, 1, true).Should().BeTrue();
        HarvestEncounterUpgrade.MatchesTarget(unique, false, 1, false, 2, true, 1, false).Should().BeFalse();
        HarvestEncounterUpgrade.MatchesTarget("Tier 3 Sacred Fauna drop 3 additional Threads", false, 1, true, 3).Should().BeTrue();
    }

    [TestMethod]
    [TestCategory("Settings")]
    public void HarvestReroll_DefaultsMatchRequestedTargets()
    {
        var settings = new ClickItSettings();
        settings.HarvestRerollTargetMods.Value.Should().BeFalse();
        settings.HarvestTargetStackedDecks.Value.Should().BeTrue();
        settings.HarvestMinStackedDecks.Value.Should().Be(1);
        settings.HarvestTargetThreads.Value.Should().BeTrue();
        settings.HarvestMinThreads.Value.Should().Be(2);
        settings.HarvestTargetCurrencyItems.Value.Should().BeFalse();
        settings.HarvestMinCurrencyItems.Value.Should().Be(4);
        settings.HarvestTargetUniqueItems.Value.Should().BeFalse();
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void ChaosReroll_WaitsForChangedModifiersOrConfirmedCurrencyConsumption()
    {
        HarvestEncounterUpgrade.ChaosRerollConfirmed("a", "b", null, null, false).Should().BeTrue();
        HarvestEncounterUpgrade.ChaosRerollConfirmed("a", "a", 5, 4, true).Should().BeTrue();
        HarvestEncounterUpgrade.ChaosRerollConfirmed("a", "a", 5, 5, true).Should().BeFalse();
        HarvestEncounterUpgrade.ChaosRerollConfirmed("a", "a", 5, 0, false).Should().BeFalse();
        HarvestEncounterUpgrade.ChaosRerollConfirmed("a", "", 5, 4, true).Should().BeFalse();
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void Reroll_ChoosesActionFromIdentificationRarityAndTarget()
    {
        HarvestEncounterUpgrade.ChooseRerollStep(MonsterRarity.Magic, false, false, true).Should().Be(HarvestEncounterUpgrade.UpgradeStep.Identify);
        HarvestEncounterUpgrade.ChooseRerollStep(MonsterRarity.Rare, false, true, true).Should().Be(HarvestEncounterUpgrade.UpgradeStep.Identify);
        HarvestEncounterUpgrade.ChooseRerollStep(MonsterRarity.Magic, true, true, true).Should().Be(HarvestEncounterUpgrade.UpgradeStep.Open);
        HarvestEncounterUpgrade.ChooseRerollStep(MonsterRarity.Rare, true, true, true).Should().Be(HarvestEncounterUpgrade.UpgradeStep.Open);
        HarvestEncounterUpgrade.ChooseRerollStep(MonsterRarity.Magic, true, false, true).Should().Be(HarvestEncounterUpgrade.UpgradeStep.Scour);
        HarvestEncounterUpgrade.ChooseRerollStep(MonsterRarity.Rare, true, false, true).Should().Be(HarvestEncounterUpgrade.UpgradeStep.Chaos);
        HarvestEncounterUpgrade.ChooseRerollStep(MonsterRarity.Rare, true, false, false).Should().Be(HarvestEncounterUpgrade.UpgradeStep.Scour);
        HarvestEncounterUpgrade.ChooseRerollStep(MonsterRarity.White, null, false, true).Should().Be(HarvestEncounterUpgrade.UpgradeStep.Alchemy);
        HarvestEncounterUpgrade.ChooseRerollStep(MonsterRarity.Rare, null, false, true).Should().Be(HarvestEncounterUpgrade.UpgradeStep.Wait);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void Reroll_WarnsOnlyForTheCurrentStepCurrency()
    {
        HarvestEncounterUpgrade.GetMissingRerollCurrency(HarvestEncounterUpgrade.UpgradeStep.Identify, false, false, false, false).Should().Be("Scrolls of Wisdom");
        HarvestEncounterUpgrade.GetMissingRerollCurrency(HarvestEncounterUpgrade.UpgradeStep.Scour, false, false, false, false).Should().Be("Orbs of Scouring");
        HarvestEncounterUpgrade.GetMissingRerollCurrency(HarvestEncounterUpgrade.UpgradeStep.Alchemy, false, false, false, false).Should().Be("Orbs of Alchemy");
        HarvestEncounterUpgrade.GetMissingRerollCurrency(HarvestEncounterUpgrade.UpgradeStep.Chaos, false, false, false, false).Should().Be("Chaos Orbs");
        HarvestEncounterUpgrade.GetMissingRerollCurrency(HarvestEncounterUpgrade.UpgradeStep.Identify, true, false, false, false).Should().BeNull();
        HarvestEncounterUpgrade.GetMissingRerollCurrency(HarvestEncounterUpgrade.UpgradeStep.Scour, false, true, false, false).Should().BeNull();
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void RerollState_ResetsWhenTreeBecomesNormalOutsideExpectedScour()
    {
        HarvestEncounterUpgrade.ShouldResetRerollForRarity(HarvestEncounterUpgrade.UpgradeStep.Chaos, MonsterRarity.White).Should().BeTrue();
        HarvestEncounterUpgrade.ShouldResetRerollForRarity(HarvestEncounterUpgrade.UpgradeStep.Identify, MonsterRarity.White).Should().BeTrue();
        HarvestEncounterUpgrade.ShouldResetRerollForRarity(HarvestEncounterUpgrade.UpgradeStep.Scour, MonsterRarity.White).Should().BeFalse();
        HarvestEncounterUpgrade.ShouldResetRerollForRarity(HarvestEncounterUpgrade.UpgradeStep.Alchemy, MonsterRarity.White).Should().BeFalse();
        HarvestEncounterUpgrade.ShouldResetRerollForRarity(HarvestEncounterUpgrade.UpgradeStep.Chaos, MonsterRarity.Magic).Should().BeTrue();
        HarvestEncounterUpgrade.ShouldResetRerollForRarity(HarvestEncounterUpgrade.UpgradeStep.Chaos, MonsterRarity.Rare).Should().BeFalse();
    }

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
    [TestCategory("Regression")]
    public void CurrencyChecks_AllowIdentificationBeforeCheckingScourAndAlchemy()
    {
        HarvestEncounterUpgrade.GetMissingUpgradeCurrency(HarvestEncounterUpgrade.UpgradeStep.Identify, false, false, false)
            .Should().Be("Scrolls of Wisdom");
        HarvestEncounterUpgrade.GetMissingUpgradeCurrency(HarvestEncounterUpgrade.UpgradeStep.Identify, true, false, false)
            .Should().BeNull("the tree can be identified before later currency is available");
        HarvestEncounterUpgrade.GetMissingUpgradeCurrency(HarvestEncounterUpgrade.UpgradeStep.Scour, true, false, false)
            .Should().Be("Orbs of Scouring");
        HarvestEncounterUpgrade.GetMissingUpgradeCurrency(HarvestEncounterUpgrade.UpgradeStep.Alchemy, false, false, false)
            .Should().Be("Orbs of Alchemy");
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
    [TestCategory("Regression")]
    public void InitialRarity_ChoosesScourForMagicAndAlchemyForNormal()
    {
        HarvestEncounterUpgrade.ChooseStep(default, MonsterRarity.Magic, null, false).Should().Be(HarvestEncounterUpgrade.UpgradeStep.Scour);
        HarvestEncounterUpgrade.ChooseStep(default, MonsterRarity.Magic, false, false).Should().Be(HarvestEncounterUpgrade.UpgradeStep.Identify);
        HarvestEncounterUpgrade.ChooseStep(default, MonsterRarity.White, null, false, alchNormal: true).Should().Be(HarvestEncounterUpgrade.UpgradeStep.Alchemy);
    }

    [TestMethod]
    public void Identification_WaitsForLabelConfirmationEvenIfWisdomWasConsumed()
    {
        HarvestEncounterUpgrade.ChooseStep(HarvestEncounterUpgrade.UpgradeStep.Identify, MonsterRarity.Magic, null, false)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Wait);
        HarvestEncounterUpgrade.ChooseStep(HarvestEncounterUpgrade.UpgradeStep.Identify, MonsterRarity.Magic, null, true)
            .Should().Be(HarvestEncounterUpgrade.UpgradeStep.Wait);
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
