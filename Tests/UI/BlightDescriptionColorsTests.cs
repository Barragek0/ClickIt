namespace ClickIt.Tests.UI;

[TestClass]
public class BlightDescriptionColorsTests
{
    [TestMethod]
    public void Resolve_ReturnsNull_ForUnknownOrEmptyWords()
    {
        BlightDescriptionColors.Resolve(null).Should().BeNull();
        BlightDescriptionColors.Resolve("").Should().BeNull();
        BlightDescriptionColors.Resolve("towers").Should().BeNull();
        BlightDescriptionColors.Resolve("(+25%").Should().BeNull();
    }

    [TestMethod]
    public void Resolve_ColorsOilNames_AndTowerTypes()
    {
        BlightDescriptionColors.Resolve("Silver").Should().NotBeNull();
        BlightDescriptionColors.Resolve("Indigo").Should().NotBeNull();
        BlightDescriptionColors.Resolve("Violet").Should().NotBeNull();
        BlightDescriptionColors.Resolve("Teal").Should().NotBeNull();
        BlightDescriptionColors.Resolve("Clear").Should().NotBeNull();
        BlightDescriptionColors.Resolve("Amber").Should().NotBeNull();

        BlightDescriptionColors.Resolve("Chilling").Should().NotBeNull();
        BlightDescriptionColors.Resolve("Seismic").Should().NotBeNull();
        BlightDescriptionColors.Resolve("Meteor").Should().NotBeNull();
        BlightDescriptionColors.Resolve("Arc").Should().NotBeNull();
        BlightDescriptionColors.Resolve("Scout").Should().NotBeNull();
        BlightDescriptionColors.Resolve("Empowering").Should().NotBeNull();
    }

    [TestMethod]
    public void Resolve_AllTowerNames_HaveAColour()
    {
        string[] towers = ["Chilling", "Seismic", "Meteor", "Fireball", "Arc", "Shock", "ShockNova", "Scout", "Scouts", "Summoning", "Empowering"];
        foreach (string tower in towers)
            BlightDescriptionColors.Resolve(tower).Should().NotBeNull($"{tower} must have a colour in the description");
    }

    [TestMethod]
    public void Resolve_Shock_IsYellow_AndDistinctFromAmber()
    {
        BlightDescriptionColors.Resolve("Shock").Should().Be(BlightDescriptionColors.Resolve("ShockNova"));
        BlightDescriptionColors.Resolve("Shock").Should().NotBe(BlightDescriptionColors.Resolve("Amber"));
    }

    [TestMethod]
    public void Resolve_Specializations_MatchTheirBaseTowerColour()
    {
        BlightDescriptionColors.Resolve("Arc").Should().Be(BlightDescriptionColors.Resolve("ShockNova"), "Arc Tower is ShockNova's specialization and shares its yellow");
        BlightDescriptionColors.Resolve("Meteor").Should().Be(BlightDescriptionColors.Resolve("Fireball"), "Meteor is Fireball's specialization and shares its red");
        BlightDescriptionColors.Resolve("Scout").Should().Be(BlightDescriptionColors.Resolve("Summoning"), "Scout Minion is the Summoning tower's specialization and shares its purple");
        BlightDescriptionColors.Resolve("Scout").Should().Be(BlightDescriptionColors.Resolve("Scouts"));
        BlightDescriptionColors.Resolve("Scout").Should().Be(BlightDescriptionColors.Resolve("ScoutMinion"));
    }

    [TestMethod]
    public void Resolve_BaseTowerPalette_MatchesTheUserScheme()
    {
        BlightDescriptionColors.Resolve("Seismic").Should().Be(new Vector4(1.00f, 0.50f, 0.10f, 1f), "Seismic is orange");
        BlightDescriptionColors.Resolve("Chilling").Should().Be(new Vector4(0.30f, 0.62f, 1.00f, 1f), "Chilling is blue");
        BlightDescriptionColors.Resolve("Empowering").Should().Be(new Vector4(0.35f, 0.85f, 0.35f, 1f), "Empowering is green");
        BlightDescriptionColors.Resolve("Summoning").Should().Be(BlightDescriptionColors.Resolve("Scout"), "the minion family is purple");
        BlightDescriptionColors.Resolve("Fireball").Should().Be(BlightDescriptionColors.Resolve("Meteor"), "Fireball and its Meteor specialization share red");
        BlightDescriptionColors.Resolve("ShockNova").Should().Be(BlightDescriptionColors.Resolve("Arc"), "ShockNova and its Arc specialization share yellow");
    }

    [TestMethod]
    public void Resolve_Opalescent_ReturnsLightPurple()
    {
        BlightDescriptionColors.Resolve("Opalescent").Should().Be(new Vector4(0.80f, 0.65f, 0.95f, 1f));
    }

    [TestMethod]
    public void Resolve_IsCaseInsensitive_AndIgnoresSurroundingPunctuation()
    {
        BlightDescriptionColors.Resolve("silver").Should().NotBeNull();
        BlightDescriptionColors.Resolve("Seismic.").Should().Be(BlightDescriptionColors.Resolve("Seismic"));
        BlightDescriptionColors.Resolve("Scouts.").Should().Be(BlightDescriptionColors.Resolve("Scout"));
        BlightDescriptionColors.Resolve("(Chilling").Should().Be(BlightDescriptionColors.Resolve("Chilling"));
        BlightDescriptionColors.Resolve("Beams)").Should().BeNull();
        BlightDescriptionColors.Resolve("Amber,").Should().Be(BlightDescriptionColors.Resolve("Amber"));
        BlightDescriptionColors.Resolve("->Arc").Should().Be(BlightDescriptionColors.Resolve("Arc"));
    }

    [TestMethod]
    public void TryResolvePhrase_ColorsSingularTowerPhrases_IncludingParenthesizedSkillNames()
    {
        Vector4? blue = BlightDescriptionColors.Resolve("Chilling");
        Vector4? red = BlightDescriptionColors.Resolve("Meteor");

        string[] chillingTower = ["Chilling", "Tower"];
        BlightDescriptionColors.TryResolvePhrase(chillingTower, 0, out int consumed).Should().Be(blue);
        consumed.Should().Be(2);

        string[] chillingBeams = ["(Chilling", "Beams)"];
        BlightDescriptionColors.TryResolvePhrase(chillingBeams, 0, out consumed).Should().Be(blue);
        consumed.Should().Be(2);

        string[] meteorTower = ["Meteor", "Tower"];
        BlightDescriptionColors.TryResolvePhrase(meteorTower, 0, out consumed).Should().Be(red);
        consumed.Should().Be(2);

        string[] burningGround = ["(Burning", "Ground)"];
        BlightDescriptionColors.TryResolvePhrase(burningGround, 0, out consumed).Should().Be(red);
        consumed.Should().Be(2);
    }

    [TestMethod]
    public void TryResolvePhrase_PluralTowers_IsNotAPhrase_AndStaysBaseColoured()
    {
        string[] words = ["Chilling", "and", "Seismic", "Towers"];
        BlightDescriptionColors.TryResolvePhrase(words, 0, out int consumed).Should().Be(BlightDescriptionColors.Resolve("Chilling"));
        consumed.Should().Be(1); // "Chilling and..." is not a phrase — Chilling keeps its own colour
        BlightDescriptionColors.TryResolvePhrase(words, 1, out consumed).Should().BeNull();
        consumed.Should().Be(1);
        BlightDescriptionColors.TryResolvePhrase(words, 2, out consumed).Should().Be(BlightDescriptionColors.Resolve("Seismic"));
        consumed.Should().Be(1); // plural "Seismic Towers" is not a phrase — only "Seismic" is orange
        BlightDescriptionColors.TryResolvePhrase(words, 3, out consumed).Should().BeNull("plural 'Towers' does not inherit a colour");
        consumed.Should().Be(1);
    }

    [TestMethod]
    public void TryResolvePhrase_PunctuationOnlyOrUnknown_ReturnsNull()
    {
        string[] punctuated = ["Silver", "+", "Opalescent"];
        BlightDescriptionColors.TryResolvePhrase(punctuated, 0, out int consumed).Should().Be(BlightDescriptionColors.Resolve("Silver"));
        consumed.Should().Be(1);

        string[] unknown = ["Builds", "Meteor", "Towers", "for", "damage"];
        BlightDescriptionColors.TryResolvePhrase(unknown, 3, out consumed).Should().BeNull();
        consumed.Should().Be(1);
    }
}
