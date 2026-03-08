using System;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class EssenceCorruptionTableTests
    {
        [TestMethod]
        public void Defaults_ContainOnlyScreamingAndShriekingEssences()
        {
            var settings = new ClickItSettings();

            settings.EssenceCorruptNames.Should().OnlyContain(x => x.StartsWith("Screaming Essence of ", StringComparison.OrdinalIgnoreCase)
                || x.StartsWith("Shrieking Essence of ", StringComparison.OrdinalIgnoreCase));
            settings.EssenceDontCorruptNames.Should().OnlyContain(x => x.StartsWith("Screaming Essence of ", StringComparison.OrdinalIgnoreCase)
                || x.StartsWith("Shrieking Essence of ", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void Defaults_CorruptMeds_AndDontCorruptEverythingElse()
        {
            var settings = new ClickItSettings();

            var corrupt = settings.GetCorruptEssenceNames();
            corrupt.Should().Contain("Screaming Essence of Misery");
            corrupt.Should().Contain("Screaming Essence of Envy");
            corrupt.Should().Contain("Screaming Essence of Dread");
            corrupt.Should().Contain("Screaming Essence of Scorn");
            corrupt.Should().Contain("Shrieking Essence of Misery");
            corrupt.Should().Contain("Shrieking Essence of Envy");
            corrupt.Should().Contain("Shrieking Essence of Dread");
            corrupt.Should().Contain("Shrieking Essence of Scorn");

            corrupt.Should().HaveCount(8);
            settings.EssenceDontCorruptNames.Should().HaveCount(32);
            settings.EssenceDontCorruptNames.Should().NotIntersectWith(corrupt);
        }

        [TestMethod]
        public void CorruptAndDontCorruptLists_TogetherCoverAllConfiguredEssences()
        {
            var settings = new ClickItSettings();

            var allConfigured = settings.EssenceCorruptNames
                .Concat(settings.EssenceDontCorruptNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            allConfigured.Should().HaveCount(40);
        }
    }
}
