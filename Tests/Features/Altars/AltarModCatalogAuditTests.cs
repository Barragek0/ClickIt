namespace ClickIt.Tests.Features.Altars;

[TestClass]
public class AltarModCatalogAuditTests
{
    [TestMethod]
    public void AllUpsideAndDownsideMods_HaveNonZeroDefaultValue()
    {
        foreach (var entry in AltarModsConstants.UpsideMods)
            entry.DefaultValue.Should().BeGreaterThan(0, $"upside {entry.Id} must not have a zero default weight");

        foreach (var entry in AltarModsConstants.DownsideMods)
            entry.DefaultValue.Should().BeGreaterThan(0, $"downside {entry.Id} must not have a zero default weight");
    }

    [TestMethod]
    public void ModsRemovedFromGame_NoLongerMatch()
    {
        // These mods are weight-0 (or absent) on poedb.tw/us/Eldritch_Altar and were removed.
        string[] removed = [
            "1.6% chance to drop an additional Eldritch Chaos Orb",
            "1.6% chance to drop an additional Eldritch Exalted Orb",
            "1.6% chance to drop an additional Eldritch Orb of Annulment",
            "1.6% chance to drop an additional Cartographer's Chisel",
            "1.6% chance to drop an additional Orb of Horizons",
            "1.6% chance to drop an additional Harbinger Scarab",
            "1.6% chance to drop an additional Reliquary Scarab",
            "1.6% chance to drop an additional Divination Card which rewards Currency",
            "1.6% chance to drop an additional Divination Card which rewards Exotic Currency",
            "1.6% chance to drop an additional Divination Card which rewards a Unique Item",
            "Final Boss drops 2 additional Eldritch Exalted Orbs",
            "Final Boss drops 2 additional Eldritch Orbs of Annulment",
            "Final Boss drops 2 additional Cartographer's Chisels",
            "Final Boss drops 2 additional Orbs of Horizons",
            "Final Boss drops 2 additional Harbinger Scarabs",
            "Final Boss drops 2 additional Reliquary Scarabs",
            "Final Boss drops 2 additional Divination Cards which reward Currency",
            "Final Boss drops 2 additional Divination Cards which reward Exotic Currency",
            "Final Boss drops 2 additional Divination Cards which reward a Unique Item",
            "Gain 1 Grasping Vines per second while Stationary",
        ];

        foreach (string mod in removed)
        {
            bool matched = AltarModMatcher.TryMatchMod(mod, "Eldritch Minions gain:", out _, out _)
                || AltarModMatcher.TryMatchMod(mod, "Map boss gains:", out _, out _)
                || AltarModMatcher.TryMatchMod(mod, "Player gains:", out _, out _);

            matched.Should().BeFalse($"removed mod should not match: {mod}");
        }
    }

    [TestMethod]
    public void RepresentativeStillExistingMods_StillMatch()
    {
        (string Mod, string Target)[] existing = [
            ("1.6% chance to drop an additional Divine Orb", "Eldritch Minions gain:"),
            ("1.6% chance to drop an additional Chaos Orb", "Eldritch Minions gain:"),
            ("1.6% chance to drop an additional Breach Thread", "Eldritch Minions gain:"),
            ("1.6% chance to drop an additional Legion Thread", "Eldritch Minions gain:"),
            ("1.6% chance to drop an additional Blight Thread", "Eldritch Minions gain:"),
            ("1.6% chance to drop an additional Ritual Thread", "Eldritch Minions gain:"),
            ("1.6% chance to drop an additional Incursion Thread", "Eldritch Minions gain:"),
            ("1.6% chance to drop an additional Abyss Thread", "Eldritch Minions gain:"),
            ("1.6% chance to drop an additional Expedition Thread", "Eldritch Minions gain:"),
            ("1.6% chance to drop an additional Betrayal Thread", "Eldritch Minions gain:"),
            ("1.6% chance to drop an additional Bestiary Thread", "Eldritch Minions gain:"),
            ("1.6% chance to drop an additional Ultimatum Thread", "Eldritch Minions gain:"),
            ("1.6% chance to drop an additional Divination Card which rewards Basic Currency", "Eldritch Minions gain:"),
            ("Final Boss drops 2 additional Divine Orbs", "Map boss gains:"),
            ("Final Boss drops 2 additional Breach Threads", "Map boss gains:"),
            ("Final Boss drops 2 additional Legion Threads", "Map boss gains:"),
            ("Final Boss drops 2 additional Blight Threads", "Map boss gains:"),
            ("Final Boss drops 2 additional Ritual Threads", "Map boss gains:"),
            ("Final Boss drops 2 additional Incursion Threads", "Map boss gains:"),
            ("Final Boss drops 2 additional Abyss Threads", "Map boss gains:"),
            ("Final Boss drops 2 additional Expedition Threads", "Map boss gains:"),
            ("Final Boss drops 2 additional Betrayal Threads", "Map boss gains:"),
            ("Final Boss drops 2 additional Bestiary Threads", "Map boss gains:"),
            ("Final Boss drops 2 additional Ultimatum Threads", "Map boss gains:"),
            ("Final Boss drops 2 additional Divination Cards which reward a Unique Weapon", "Map boss gains:"),
            ("Basic Currency Items dropped by slain Enemies have 15% chance to be Duplicated", "Player gains:"),
            ("Threads dropped by slain Enemies have 20% chance to be Duplicated", "Player gains:"),
            ("Gems dropped by slain Enemies have 10% chance to be Duplicated", "Player gains:"),
            ("Enemies within 4 metres Gain 100% of their Physical Damage as Extra Cold Damage", "Player gains:"),
            ("Enemies within 4 metres Gain 100% of their Physical Damage as Extra Lightning Damage", "Player gains:"),
            ("Enemies within 4 metres Gain 100% of their Physical Damage as Extra Fire Damage", "Player gains:"),
            ("Enemies within 4 metres Gain 100% of their Physical Damage as Extra Chaos Damage", "Player gains:"),
            ("Projectiles are fired in random directions", "Player gains:"),
            // Combined poedb mods are split per-line in the codebase catalog; each line matches its own entry.
            ("Hits always Ignite", "Map boss gains:"),
            ("All Damage can Ignite", "Map boss gains:"),
            ("Gain (70-130)% of Physical Damage as Extra Chaos Damage", "Map boss gains:"),
            ("Poison on Hit", "Map boss gains:"),
            ("All Damage from Hits can Poison", "Map boss gains:"),
            ("Inflict 1 Grasping Vines on Hit", "Eldritch Minions gain:"),
            ("Enemies within 3.5 metres are Hindered, with 40% reduced Movement Speed", "Map boss gains:"),
        ];

        foreach ((string mod, string target) in existing)
        {
            bool matched = AltarModMatcher.TryMatchMod(mod, target, out _, out _);
            matched.Should().BeTrue($"existing mod should match: {mod}");
        }
    }
}
