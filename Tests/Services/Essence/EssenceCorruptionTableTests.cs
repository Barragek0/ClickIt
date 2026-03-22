using System;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class EssenceCorruptionTableTests
    {
        [TestMethod]
        public void Defaults_ContainOnlyScreamingShriekingAndDeafeningEssences()
        {
            var settings = new ClickItSettings();

            settings.EssenceCorruptNames.Should().OnlyContain(x => x.StartsWith("Screaming Essence of ", StringComparison.OrdinalIgnoreCase)
                || x.StartsWith("Shrieking Essence of ", StringComparison.OrdinalIgnoreCase)
                || x.StartsWith("Deafening Essence of ", StringComparison.OrdinalIgnoreCase));
            settings.EssenceDontCorruptNames.Should().OnlyContain(x => x.StartsWith("Screaming Essence of ", StringComparison.OrdinalIgnoreCase)
                || x.StartsWith("Shrieking Essence of ", StringComparison.OrdinalIgnoreCase)
                || x.StartsWith("Deafening Essence of ", StringComparison.OrdinalIgnoreCase));
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
            corrupt.Should().Contain("Deafening Essence of Misery");
            corrupt.Should().Contain("Deafening Essence of Envy");
            corrupt.Should().Contain("Deafening Essence of Dread");
            corrupt.Should().Contain("Deafening Essence of Scorn");

            corrupt.Should().HaveCount(12);
            settings.EssenceDontCorruptNames.Should().HaveCount(48);
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

            allConfigured.Should().HaveCount(60);
        }

        [TestMethod]
        public void EssenceCorruption_RoundTrip_PreservesMovedEntries()
        {
            var settings = new ClickItSettings();

            const string movedToDontCorrupt = "Screaming Essence of Misery";
            const string movedToCorrupt = "Screaming Essence of Greed";

            settings.EssenceCorruptNames.Remove(movedToDontCorrupt);
            settings.EssenceDontCorruptNames.Add(movedToDontCorrupt);

            settings.EssenceDontCorruptNames.Remove(movedToCorrupt);
            settings.EssenceCorruptNames.Add(movedToCorrupt);

            string json = JsonConvert.SerializeObject(settings);
            var restored = JsonConvert.DeserializeObject<ClickItSettings>(json);

            restored.Should().NotBeNull();
            restored!.EssenceCorruptNames.Should().Contain(movedToCorrupt);
            restored.EssenceCorruptNames.Should().NotContain(movedToDontCorrupt);
            restored.EssenceDontCorruptNames.Should().Contain(movedToDontCorrupt);
            restored.EssenceDontCorruptNames.Should().NotContain(movedToCorrupt);
        }
    }
}
