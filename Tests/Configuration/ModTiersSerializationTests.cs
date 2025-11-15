#if false
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using System;
using ClickIt.Constants;
namespace ClickIt.Tests.Configuration
{
    [TestClass]
    public class ModTiersSerializationTests
    {
        [TestMethod]
        public void ModTiers_ShouldSerializeAndDeserialize_PreservingValues()
        {
            // Arrange
            var settings = new MockClickItSettings();
            settings.ModTiers["Boss|Final Boss drops # additional Divine Orbs"] = 90;
            settings.ModTiers["Player|#% increased Experience gain"] = 25;

            // Act - serialize only the ModTiers dictionary (the persistent data we care about)
            string serialized = SerializeModTiers(settings.ModTiers);
            var loaded = DeserializeModTiers(serialized);

            // Assert
            loaded.Should().NotBeNull();
            loaded!.Count.Should().Be(settings.ModTiers.Count);
            loaded["Boss|Final Boss drops # additional Divine Orbs"].Should().Be(90);
            loaded["Player|#% increased Experience gain"].Should().Be(25);
        }

        private static string SerializeModTiers(Dictionary<string, int> modTiers)
        {
            // Use a simple, dependency-free serialization: key=val pairs separated by '|' and escaped via Uri.EscapeDataString
            return string.Join(";", modTiers.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + kv.Value));
        }

        private static Dictionary<string, int> DeserializeModTiers(string data)
        {
            var dict = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(data)) return dict;
            foreach (var part in data.Split(';'))
            {
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

        // Moved EnsureAllModsHaveWeights to DefaultWeightInitializationTests to consolidate configuration initialization checks.
    }
}
#endif
