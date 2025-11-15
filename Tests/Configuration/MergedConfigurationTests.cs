using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ClickIt.Tests.Configuration
{
    [TestClass]
    public class MergedConfigurationTests
    {
        [TestMethod]
        public void ModTiers_SerializeDeserialize_PreservesValues()
        {
            var settings = new MockClickItSettings();
            settings.ModTiers["Boss|Final Boss drops # additional Divine Orbs"] = 90;
            settings.ModTiers["Player|#% increased Experience gain"] = 25;

            string serialized = SerializeModTiers(settings.ModTiers);
            var loaded = DeserializeModTiers(serialized);

            loaded.Should().NotBeNull();
            loaded!.Count.Should().Be(settings.ModTiers.Count);
            loaded["Boss|Final Boss drops # additional Divine Orbs"].Should().Be(90);
            loaded["Player|#% increased Experience gain"].Should().Be(25);
        }

        [TestMethod]
        public void Import_MalformedEntriesAndDuplicates_HandleCorrectly()
        {
            // malformed + valid + duplicate last-wins
            string validKey = System.Uri.EscapeDataString("Boss|Final Boss drops # additional Divine Orbs");
            string malformed = "BadEntryWithoutEquals";
            string payload = validKey + "=90;" + malformed + ";Key=NotANumber;";

            var result = DeserializeModTiers(payload);

            result.Should().ContainKey("Boss|Final Boss drops # additional Divine Orbs");
            result["Boss|Final Boss drops # additional Divine Orbs"].Should().Be(90);
            result.Should().NotContainKey("BadEntryWithoutEquals");
            result.Should().NotContainKey("Key");

            // duplicates - last wins
            string k = System.Uri.EscapeDataString("Player|#% increased Experience gain");
            string dupPayload = k + "=10;" + k + "=77;";
            var dupRes = DeserializeModTiers(dupPayload);
            dupRes.Should().ContainKey("Player|#% increased Experience gain");
            dupRes["Player|#% increased Experience gain"].Should().Be(77);
        }

        [TestMethod]
        public void Import_EmptyOrWhitespaceAndEmptySegments_ReturnsEmptyOrIgnores()
        {
            var empty = DeserializeModTiers("");
            empty.Should().BeEmpty();

            empty = DeserializeModTiers("   ");
            empty.Should().BeEmpty();

            string k1 = System.Uri.EscapeDataString("A|One");
            string k2 = System.Uri.EscapeDataString("B|Two");
            string payload = k1 + "=1;;" + k2 + "=2;";
            var result = DeserializeModTiers(payload);
            result.Should().HaveCount(2);
            result[System.Uri.UnescapeDataString(k1)].Should().Be(1);
            result[System.Uri.UnescapeDataString(k2)].Should().Be(2);
        }

        [TestMethod]
        public void InitializeDefaultWeights_PopulatesCompositeKeys_ForAllUpsideAndDownside()
        {
            var settings = new ClickIt.ClickItSettings();
            settings.ModTiers.Clear();
            settings.InitializeDefaultWeights();

            foreach (var tuple in ClickIt.Constants.AltarModsConstants.UpsideMods)
            {
                var (Id, _, Type, DefaultValue) = tuple;
                var composite = $"{Type}|{Id}";
                settings.ModTiers.Should().ContainKey(composite);
                settings.ModTiers[composite].Should().Be(DefaultValue);
            }

            foreach (var tuple in ClickIt.Constants.AltarModsConstants.DownsideMods)
            {
                var (Id, _, Type, DefaultValue) = tuple;
                var composite = $"{Type}|{Id}";
                settings.ModTiers.Should().ContainKey(composite);
                settings.ModTiers[composite].Should().Be(DefaultValue);
            }
        }

        [TestMethod]
        public void EnsureAllModsHaveWeights_AfterInitialization_PopulatesCompositeKeys()
        {
            var settings = new ClickIt.ClickItSettings();
            settings.ModTiers.Clear();
            settings.InitializeDefaultWeights();

            settings.ModTiers.Should().NotBeEmpty();
            settings.ModTiers.ContainsKey("Boss|Final Boss drops # additional Divine Orbs").Should().BeTrue();
        }

        [TestMethod]
        public void InitializeDefaultWeights_PopulatesAtLeastConstantsCount()
        {
            var s = new ClickIt.ClickItSettings();
            s.ModTiers.Clear();
            s.InitializeDefaultWeights();

            int expected = ClickIt.Constants.AltarModsConstants.UpsideMods.Count + ClickIt.Constants.AltarModsConstants.DownsideMods.Count;
            s.ModTiers.Count.Should().BeGreaterOrEqualTo(expected);
        }

        // Helpers (shared deserialize/serialize used across tests)
        private static string SerializeModTiers(Dictionary<string, int> modTiers)
        {
            return string.Join(";", modTiers.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + kv.Value));
        }

        private static Dictionary<string, int> DeserializeModTiers(string data)
        {
            var dict = new Dictionary<string, int>();
            if (string.IsNullOrWhiteSpace(data)) return dict;
            foreach (var part in data.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                var idx = part.IndexOf('=');
                if (idx <= 0) continue;
                var key = Uri.UnescapeDataString(part.Substring(0, idx));
                if (int.TryParse(part.Substring(idx + 1), out int val))
                {
                    dict[key] = val;
                }
            }
            return dict;
        }
    }
}
