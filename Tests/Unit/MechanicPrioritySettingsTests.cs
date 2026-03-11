using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace ClickIt.Tests.Unit
{
    [TestClass]
    public class MechanicPrioritySettingsTests
    {
        [TestMethod]
        public void MechanicPriority_DefaultsContainAllKnownMechanics()
        {
            var settings = new ClickItSettings();

            var priorities = settings.GetMechanicPriorityOrder();

            priorities.Should().Contain("basic-chests");
            priorities.Should().Contain("league-chests");
            priorities.Should().Contain("doors");
            priorities.Should().Contain("levers");
            priorities.Should().Contain("betrayal");
            priorities.Should().Contain("sanctum");
            priorities.Should().Contain("essences");
            priorities.Should().Contain("altars");
            priorities.Should().Contain("ultimatum");
            priorities.Should().Contain("shrines");
            priorities.Should().Contain("strongboxes");
            priorities.Should().Contain("items");
            priorities.Count.Should().Be(priorities.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [TestMethod]
        public void MechanicPriority_SanitizesUnknownAndDuplicateEntries()
        {
            var settings = new ClickItSettings
            {
                MechanicPriorityOrder = new List<string>
                {
                    "essences",
                    "unknown-mechanic",
                    "essences"
                },
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "essences",
                    "unknown-mechanic"
                }
            };

            var priorities = settings.GetMechanicPriorityOrder();
            var ignoreDistance = settings.GetMechanicPriorityIgnoreDistanceIds();

            priorities.Should().Contain("essences");
            priorities.Should().NotContain("unknown-mechanic");
            priorities.Count.Should().Be(priorities.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            ignoreDistance.Should().Contain("essences");
            ignoreDistance.Should().NotContain("unknown-mechanic");
        }

        [TestMethod]
        public void MechanicPriority_PersistsOrderAndIgnoreDistanceAcrossJsonRoundTrip()
        {
            var settings = new ClickItSettings
            {
                MechanicPriorityOrder = new List<string>
                {
                    "essences",
                    "ultimatum",
                    "items"
                },
                MechanicPriorityIgnoreDistanceIds = new HashSet<string>(new[] { "essences", "ultimatum" }, StringComparer.OrdinalIgnoreCase)
            };

            string json = JsonConvert.SerializeObject(settings);
            var restored = JsonConvert.DeserializeObject<ClickItSettings>(json);

            restored.Should().NotBeNull();

            var priorities = restored!.GetMechanicPriorityOrder();
            var ignoreDistance = restored.GetMechanicPriorityIgnoreDistanceIds();

            priorities[0].Should().Be("essences");
            priorities[1].Should().Be("ultimatum");
            priorities[2].Should().Be("items");
            ignoreDistance.Should().Contain("essences");
            ignoreDistance.Should().Contain("ultimatum");
        }
    }
}