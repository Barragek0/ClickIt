namespace ClickIt.Tests.Features.Labels.Classification
{
    [TestClass]
    public class MetadataIdentifierRuleSetTests
    {
        [TestMethod]
        public void ContainsAnyMetadataIdentifier_ReturnsFalse_WhenIdentifiersAreEmpty()
        {
            bool result = MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(
                "Metadata/Items/Currency/CurrencyModValues",
                "Orb of Alteration",
                []);

            result.Should().BeFalse();
        }

        [TestMethod]
        public void ContainsAnyMetadataIdentifier_SkipsBlankEntries_AndMatchesNormalIdentifier()
        {
            bool result = MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(
                "Metadata/Items/Currency/CurrencyModValues",
                "Orb of Alteration",
                [string.Empty, "special:", "Items/Currency/"]);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ContainsAnyMetadataIdentifier_MatchesHeistQuestContractRule()
        {
            bool result = MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(
                "Metadata/Items/Heist/HeistContract",
                "Contract: Trial Run",
                ["special:heist-quest-contract"]);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ContainsAnyMetadataIdentifier_MatchesHeistNonQuestContractRule()
        {
            bool result = MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(
                "Metadata/Items/Heist/HeistContract",
                "Contract: Bunker",
                ["special:heist-non-quest-contract"]);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ContainsAnyMetadataIdentifier_MatchesInscribedUltimatumRule_FromMetadataPath()
        {
            bool result = MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(
                "Metadata/Items/Currency/ItemisedTrial/InscribedUltimatum",
                string.Empty,
                item: null,
                labelText: string.Empty,
                identifiers: ["special:inscribed-ultimatum"]);

            result.Should().BeTrue();
        }

        [DataTestMethod]
        [DataRow("Metadata/Items/Jewels/Jewel/SomeJewel", true)]
        [DataRow("Metadata/Items/Jewels/JewelAbyss/SomeAbyssJewel", false)]
        [DataRow("Metadata/Items/Jewels/JewelPassiveTreeExpansion/SomeClusterJewel", false)]
        public void ContainsAnyMetadataIdentifier_EvaluatesRegularJewelsRule(string metadataPath, bool expected)
        {
            bool result = MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(
                metadataPath,
                string.Empty,
                item: null,
                labelText: string.Empty,
                identifiers: ["special:jewels-regular"]);

            result.Should().Be(expected);
        }

        [TestMethod]
        public void ContainsAnyMetadataIdentifier_MatchesMysteriousWombgiftLabelRule_IgnoringTrimAndCase()
        {
            bool result = MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(
                metadataPath: string.Empty,
                itemName: string.Empty,
                item: null,
                labelText: "  mysterious wombgift  ",
                identifiers: ["special:mysterious-wombgift-label"]);

            result.Should().BeTrue();
        }

        [TestMethod]
        public void ContainsAnyMetadataIdentifier_ReturnsFalse_ForUnknownSpecialRule()
        {
            bool result = MetadataIdentifierRuleSet.ContainsAnyMetadataIdentifier(
                "Metadata/Items/Currency/CurrencyModValues",
                "Orb of Alteration",
                item: null,
                labelText: string.Empty,
                identifiers: ["special:unknown-rule"]);

            result.Should().BeFalse();
        }
    }
}