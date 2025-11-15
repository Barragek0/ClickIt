#if false
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;

namespace ClickIt.Tests.Configuration
{
    [TestClass]
    public class ExportImportEdgeCasesTests
    {
        private static Dictionary<string, int> DeserializeModTiers(string data)
        {
            var dict = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(data)) return dict;
            foreach (var part in data.Split(';'))
            {
                var idx = part.IndexOf('=');
                if (idx <= 0) continue;
                var key = System.Uri.UnescapeDataString(part.Substring(0, idx));
                if (int.TryParse(part.Substring(idx + 1), out int val))
                {
                    dict[key] = val;
                }
            }
            return dict;
        }

        [TestMethod]
        public void Import_MalformedEntries_ShouldSkipInvalidPairs()
        {
            // Arrange: malformed entries include missing '=', non-numeric values
            string payload = System.Uri.EscapeDataString("Boss|Final Boss drops # additional Divine Orbs") + "=90;BadEntryWithoutEquals;Key=NotANumber;";

            // Act
            var result = DeserializeModTiers(payload);

            // Assert: only the valid pair parsed
            result.Should().ContainKey("Boss|Final Boss drops # additional Divine Orbs");
            result["Boss|Final Boss drops # additional Divine Orbs"].Should().Be(90);
            result.Should().NotContainKey("BadEntryWithoutEquals");
            result.Should().NotContainKey("Key");
        }

        [TestMethod]
        public void Import_DuplicateKeys_LastOneWins()
        {
            // Arrange: duplicate composite keys
            string k = System.Uri.EscapeDataString("Player|#% increased Experience gain");
            string payload = k + "=10;" + k + "=77;";

            // Act
            var result = DeserializeModTiers(payload);

            // Assert: last value should be kept
            result.Should().ContainKey("Player|#% increased Experience gain");
            result["Player|#% increased Experience gain"].Should().Be(77);
        }

        [TestMethod]
        public void Import_EmptyOrWhitespace_ReturnsEmpty()
        {
            // Arrange
            string payload = "";

            // Act
            var result = DeserializeModTiers(payload);

            // Assert
            result.Should().BeEmpty();

            // Also whitespace-only
            result = DeserializeModTiers("   ");
            result.Should().BeEmpty();
        }

        [TestMethod]
        public void Import_Ignores_EmptySegments()
        {
            // Arrange: extra semicolons and empty segments should be ignored
            string k1 = System.Uri.EscapeDataString("A|One");
            string k2 = System.Uri.EscapeDataString("B|Two");
            string payload = k1 + "=1;;" + k2 + "=2;";

            // Act
            var result = DeserializeModTiers(payload);

            // Assert
            result.Should().HaveCount(2);
            result[System.Uri.UnescapeDataString(k1)].Should().Be(1);
            result[System.Uri.UnescapeDataString(k2)].Should().Be(2);
        }
    }
}
#endif
